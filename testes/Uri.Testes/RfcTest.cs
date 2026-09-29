using Xunit;

namespace Conde.Uris.Testes;

/// <summary>
/// Os exemplos normativos do §5.4 da RFC 3986.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nenhum resultado deste arquivo é meu.</b> Eles estão impressos na
/// especificação, numa tabela com o título "Reference Resolution Examples", e a
/// RFC os divide em duas partes que valem por razões diferentes.
/// </para>
/// <para>
/// Os <b>normais</b> são o que qualquer implementação acerta. Os
/// <b>anormais</b> — a palavra é da própria RFC — são os que o comitê escolheu
/// justamente por serem os que as implementações erram, e a RFC diz isso em
/// voz alta: "some parsers allow the scheme name to be present in a relative
/// reference... this is considered to be a loophole in prior specifications".
/// </para>
/// <para>
/// Uma tabela de casos de canto escolhida por quem escreveu o formato, com a
/// resposta certa ao lado, é a melhor coisa que um projeto destes pode receber
/// de presente.
/// </para>
/// </remarks>
public class RfcTest
{
    private const string Base = "http://a/b/c/d;p?q";

    /// <summary>
    /// A tabela de exemplos normais do §5.4.1.
    /// </summary>
    public static TheoryData<string, string> Normais() => new()
    {
        { "g:h", "g:h" },
        { "g", "http://a/b/c/g" },
        { "./g", "http://a/b/c/g" },
        { "g/", "http://a/b/c/g/" },
        { "/g", "http://a/g" },
        { "//g", "http://g" },
        { "?y", "http://a/b/c/d;p?y" },
        { "g?y", "http://a/b/c/g?y" },
        { "#s", "http://a/b/c/d;p?q#s" },
        { "g#s", "http://a/b/c/g#s" },
        { "g?y#s", "http://a/b/c/g?y#s" },
        { ";x", "http://a/b/c/;x" },
        { "g;x", "http://a/b/c/g;x" },
        { "g;x?y#s", "http://a/b/c/g;x?y#s" },
        { "", "http://a/b/c/d;p?q" },
        { ".", "http://a/b/c/" },
        { "./", "http://a/b/c/" },
        { "..", "http://a/b/" },
        { "../", "http://a/b/" },
        { "../g", "http://a/b/g" },
        { "../..", "http://a/" },
        { "../../", "http://a/" },
        { "../../g", "http://a/g" },
    };

    /// <summary>
    /// A tabela de exemplos <b>anormais</b> do §5.4.2.
    /// </summary>
    /// <remarks>
    /// Estes são os interessantes. O primeiro grupo sobe além da raiz e tem de
    /// parar nela; o segundo põe <c>.</c> e <c>..</c> em lugares em que eles não
    /// são segmentos completos; o terceiro testa a regra de que um esquema numa
    /// referência relativa é tratado como relativo mesmo assim.
    /// </remarks>
    public static TheoryData<string, string> Anormais() => new()
    {
        // Subir além da raiz: o `..` que sobra é descartado, não levado adiante.
        { "../../../g", "http://a/g" },
        { "../../../../g", "http://a/g" },

        // `.` e `..` que não são segmentos completos.
        { "/./g", "http://a/g" },
        { "/../g", "http://a/g" },
        { "g.", "http://a/b/c/g." },
        { ".g", "http://a/b/c/.g" },
        { "g..", "http://a/b/c/g.." },
        { "..g", "http://a/b/c/..g" },

        // Combinações que parecem sem sentido e têm resposta definida.
        { "./../g", "http://a/b/g" },
        { "./g/.", "http://a/b/c/g/" },
        { "g/./h", "http://a/b/c/g/h" },
        { "g/../h", "http://a/b/c/h" },
        { "g;x=1/./y", "http://a/b/c/g;x=1/y" },
        { "g;x=1/../y", "http://a/b/c/y" },

        // A consulta e o fragmento NÃO participam da remoção de pontos.
        { "g?y/./x", "http://a/b/c/g?y/./x" },
        { "g?y/../x", "http://a/b/c/g?y/../x" },
        { "g#s/./x", "http://a/b/c/g#s/./x" },
        { "g#s/../x", "http://a/b/c/g#s/../x" },

        // O "loophole" que a RFC menciona: um esquema igual ao da base.
        { "http:g", "http:g" },
    };

    [Theory(DisplayName = "os exemplos NORMAIS do §5.4.1 da RFC 3986")]
    [MemberData(nameof(Normais))]
    public void ExemplosNormais(string referencia, string esperado)
    {
        Assert.Equal(esperado, Resolvedor.Resolver(Base, referencia));
    }

    [Theory(DisplayName = "os exemplos ANORMAIS do §5.4.2 da RFC 3986")]
    [MemberData(nameof(Anormais))]
    public void ExemplosAnormais(string referencia, string esperado)
    {
        Assert.Equal(esperado, Resolvedor.Resolver(Base, referencia));
    }

    [Fact(DisplayName = "os 42 exemplos da RFC passam, e são 42")]
    public void SaoTodosEQuarentaEDois()
    {
        // Contar é uma proteção contra alguém apagar metade da tabela por
        // engano: um teste parametrizado com zero casos passa em silêncio.
        var quantos = Normais().Count + Anormais().Count;

        Assert.Equal(42, quantos);
    }

    [Theory(DisplayName = "a remoção de pontos, do §5.2.4")]
    // Os dois exemplos que a própria RFC dá na descrição do algoritmo.
    [InlineData("/a/b/c/./../../g", "/a/g")]
    [InlineData("mid/content=5/../6", "mid/6")]
    // E os casos de borda que a descrição não dá.
    [InlineData("", "")]
    [InlineData("/", "/")]
    [InlineData(".", "")]
    [InlineData("..", "")]
    [InlineData("/.", "/")]
    [InlineData("/..", "/")]
    [InlineData("/a/.", "/a/")]
    [InlineData("/a/..", "/")]
    [InlineData("/../../../", "/")]
    // Esta eu errei ao escrever a expectativa: pus "c", e o algoritmo dá "/c".
    // O motivo é o passo C, que consome "/.." e deixa a barra para trás -- então
    // um caminho relativo que sobe demais acaba com barra na frente. A RFC não
    // traz este caso, e o algoritmo dela não deixa margem: é "/c".
    [InlineData("a/b/../../../c", "/c")]
    [InlineData("./.././../a", "a")]
    public void RemocaoDePontos(string entrada, string esperado)
    {
        Assert.Equal(esperado, Resolvedor.RemoverPontos(entrada));
    }

    [Fact(DisplayName = "o `..` nunca escapa da raiz")]
    public void NuncaEscapaDaRaiz()
    {
        // É a regra que impede a travessia de caminho, e a que mais falta
        // quando alguém escreve o algoritmo de cabeça. Um `..` que sobe além
        // da raiz é DESCARTADO, e não levado adiante.
        var ataques = new[]
        {
            "/../../../../etc/passwd",
            "/a/../../../etc/passwd",
            "/./../.././../etc/passwd",
            "../../../../../../../../etc/passwd",
        };

        foreach (var ataque in ataques)
        {
            var resolvido = Resolvedor.Resolver("http://exemplo.com/a/b/c", ataque);

            Assert.DoesNotContain("..", resolvido);
            Assert.StartsWith("http://exemplo.com/", resolvido);
        }
    }
}

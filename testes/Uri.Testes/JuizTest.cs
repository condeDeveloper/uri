using System.Text;
using Xunit;
using Dotnet = System.Uri;

namespace Conde.Uris.Testes;

/// <summary>
/// O segundo juiz: o <c>System.Uri</c> do .NET.
/// </summary>
/// <remarks>
/// <para>
/// A RFC dá 42 exemplos. Eles são excelentes e são 42, e a resolução de
/// referência tem mais casos que isso. O <c>System.Uri</c> responde a qualquer
/// par que se invente, e é assim que os 42 viram milhares.
/// </para>
/// <para>
/// Com uma ressalva importante, e ela é o resultado mais interessante deste
/// arquivo: <b>o <c>System.Uri</c> não é uma implementação da RFC 3986</b>. Ele
/// é anterior a ela, carrega compatibilidade com a RFC 2396 e com o que os
/// navegadores faziam em 2002, e diverge da RFC de propósito em alguns pontos.
/// </para>
/// <para>
/// Onde os dois discordam, quem manda é a RFC — e os pontos de divergência estão
/// nomeados aqui um a um, em vez de escondidos numa margem de tolerância.
/// </para>
/// </remarks>
public class JuizTest
{
    private static string? PeloDotnet(string baseDe, string referencia)
    {
        try
        {
            var raiz = new Dotnet(baseDe, UriKind.Absolute);

            return new Dotnet(raiz, referencia).ToString();
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    /// <summary>Um caminho relativo sorteado, com pontos e segmentos.</summary>
    private static string SortearReferencia(Random sorteio)
    {
        var pedacos = new List<string>();
        var quantos = sorteio.Next(1, 5);

        for (var i = 0; i < quantos; i++)
        {
            pedacos.Add(sorteio.Next(6) switch
            {
                0 => ".",
                1 => "..",
                2 => "a",
                3 => "b",
                4 => "cd",
                _ => "e",
            });
        }

        var caminho = string.Join("/", pedacos);

        if (sorteio.Next(3) == 0)
        {
            caminho = "/" + caminho;
        }

        if (sorteio.Next(4) == 0)
        {
            caminho += "/";
        }

        return caminho;
    }

    [Fact(DisplayName = "vinte mil resoluções conferidas contra o System.Uri")]
    public void ResolucoesSorteadas()
    {
        var sorteio = new Random(20260929);

        string[] bases =
        [
            "http://exemplo.com/a/b/c",
            "http://exemplo.com/a/b/",
            "http://exemplo.com/",
            "https://exemplo.com:8080/x/y/z?q=1",
            "http://exemplo.com/a/b/c?q=1#f",
        ];

        var comparados = 0;
        var bateram = 0;

        var divergencias = new List<string>();

        for (var i = 0; i < 20_000; i++)
        {
            var baseDe = bases[sorteio.Next(bases.Length)];
            var referencia = SortearReferencia(sorteio);

            var dele = PeloDotnet(baseDe, referencia);

            if (dele is null)
            {
                continue;
            }

            comparados++;

            var meu = Resolvedor.Resolver(baseDe, referencia);

            if (meu == dele)
            {
                bateram++;
            }
            else if (divergencias.Count < 5)
            {
                divergencias.Add($"base \"{baseDe}\" + \"{referencia}\": "
                    + $"eu dei \"{meu}\", o .NET deu \"{dele}\"");
            }
        }

        Assert.True(comparados > 15_000, $"só {comparados} pares foram comparados");

        Assert.True(bateram == comparados,
            $"{comparados - bateram} de {comparados} divergiram. "
            + $"A primeira: {divergencias.FirstOrDefault()}");
    }

    [Fact(DisplayName = "os 42 exemplos da RFC, também contra o System.Uri")]
    public void OsExemplosDaRfcNoDotnet()
    {
        // Este teste tem um resultado que vale mais que "passou": ele mostra
        // ONDE o System.Uri discorda da RFC. Os casos que divergem estão
        // listados no teste seguinte, com o motivo de cada um.
        var conhecidos = new HashSet<string>
        {
            // O .NET resolve "g:h" como um URI com esquema "g" e o normaliza;
            // a RFC manda devolver o texto como está.
            "g:h",

            // Idem: o "loophole" que a própria RFC nomeia.
            "http:g",

            // O .NET acrescenta a barra do caminho vazio: "http://g" vira
            // "http://g/". Os dois são o mesmo recurso e não são o mesmo texto.
            "//g",
        };

        var divergiram = new List<string>();

        foreach (var caso in RfcTest.Normais().Concat(RfcTest.Anormais()))
        {
            var referencia = (string)caso[0]!;
            var daRfc = (string)caso[1]!;

            var doDotnet = PeloDotnet("http://a/b/c/d;p?q", referencia);

            // O meu resultado tem de ser o da RFC, sempre. Isso já é garantido
            // no outro arquivo; aqui o que se mede é o .NET.
            Assert.Equal(daRfc, Resolvedor.Resolver("http://a/b/c/d;p?q", referencia));

            if (doDotnet != daRfc)
            {
                divergiram.Add($"\"{referencia}\": a RFC diz \"{daRfc}\", "
                    + $"o System.Uri diz \"{doDotnet}\"");
            }
        }

        // Se aparecer uma divergência que não está na lista, é sinal de que o
        // .NET mudou -- ou de que eu entendi um caso errado. As duas coisas
        // merecem aparecer.
        foreach (var divergencia in divergiram)
        {
            var qual = divergencia[1..divergencia.IndexOf('"', 1)];

            Assert.True(conhecidos.Contains(qual),
                $"divergência nova entre a RFC e o System.Uri: {divergencia}");
        }

        Assert.True(divergiram.Count >= 2,
            "esperava que o System.Uri divergisse da RFC em alguns casos; "
            + "se ele parou de divergir, esta lista está desatualizada");
    }

    [Theory(DisplayName = "partir o URI nos cinco componentes")]
    [InlineData("http://exemplo.com/a?q#f", "http", "exemplo.com", "/a", "q", "f")]
    [InlineData("http://exemplo.com", "http", "exemplo.com", "", null, null)]
    [InlineData("http://exemplo.com/", "http", "exemplo.com", "/", null, null)]
    [InlineData("//exemplo.com/a", null, "exemplo.com", "/a", null, null)]
    [InlineData("/a/b", null, null, "/a/b", null, null)]
    [InlineData("a/b", null, null, "a/b", null, null)]
    [InlineData("", null, null, "", null, null)]
    [InlineData("?q", null, null, "", "q", null)]
    [InlineData("#f", null, null, "", null, "f")]
    [InlineData("mailto:eu@exemplo.com", "mailto", null, "eu@exemplo.com", null, null)]
    [InlineData("urn:isbn:0451450523", "urn", null, "isbn:0451450523", null, null)]
    [InlineData("file:///c:/a", "file", "", "/c:/a", null, null)]
    [InlineData("http://exemplo.com/a?", "http", "exemplo.com", "/a", "", null)]
    [InlineData("http://exemplo.com/a#", "http", "exemplo.com", "/a", null, "")]
    [InlineData("./a:b", null, null, "./a:b", null, null)]
    public void Partir(string texto, string? esquema, string? autoridade,
        string caminho, string? consulta, string? fragmento)
    {
        var lida = Analisador.Ler(texto);

        Assert.Equal(esquema, lida.Esquema);
        Assert.Equal(autoridade, lida.Autoridade);
        Assert.Equal(caminho, lida.Caminho);
        Assert.Equal(consulta, lida.Consulta);
        Assert.Equal(fragmento, lida.Fragmento);

        // E remontar tem de dar exatamente o texto de volta. É a propriedade
        // que prova que a divisão não perdeu nada -- inclusive a diferença
        // entre ausente e vazio.
        Assert.Equal(texto, lida.ToString());
    }

    [Fact(DisplayName = "partir e remontar devolve o texto original, sempre")]
    public void PartirERemontar()
    {
        // A ida e volta é a propriedade mais forte que um analisador de URI
        // pode ter, e ela pega o defeito que mais importa: confundir um
        // componente ausente com um vazio. `http://a/b?` e `http://a/b` viram
        // o mesmo objeto numa implementação descuidada, e aí um dos dois sai
        // errado na volta.
        var sorteio = new Random(7);

        var pedacos = new[]
        {
            "http:", "https:", "", "//exemplo.com", "//a:8080", "//u@a", "//",
            "/a", "/a/b", "a", "", "/", "a:b", ";x", "/a;x=1",
            "?", "?q", "?q=1&r=2", "#", "#f", "#a/b?c",
        };

        for (var i = 0; i < 20_000; i++)
        {
            var texto = new StringBuilder();

            for (var j = 0; j < sorteio.Next(1, 5); j++)
            {
                texto.Append(pedacos[sorteio.Next(pedacos.Length)]);
            }

            var original = texto.ToString();

            Assert.Equal(original, Analisador.Ler(original).ToString());
        }
    }

    [Fact(DisplayName = "a expressão do apêndice B da RFC concorda com a leitura à mão")]
    public void ContraAExpressaoDaRfc()
    {
        // A RFC traz uma expressão regular pronta para partir um URI. Ela não
        // distingue ausente de vazio sem cuidado extra em cada grupo -- que é
        // por que este código faz a divisão à mão --, e nos componentes que ela
        // resolve, as duas têm de concordar.
        var sorteio = new Random(11);

        var pedacos = new[]
        {
            "http://a/b", "https://u@a:1/b?c#d", "/a/b", "a/b", "//a", "?q", "#f",
            "mailto:x@y", "urn:a:b", "", "a", "a:b/c",
        };

        for (var i = 0; i < 5_000; i++)
        {
            var texto = pedacos[sorteio.Next(pedacos.Length)];

            var meu = Analisador.Ler(texto);
            var dela = Analisador.DoApendiceB.Match(texto);

            Assert.Equal(
                dela.Groups[2].Success ? dela.Groups[2].Value : null,
                meu.Esquema);

            Assert.Equal(
                dela.Groups[4].Success ? dela.Groups[4].Value : null,
                meu.Autoridade);

            Assert.Equal(dela.Groups[5].Value, meu.Caminho);

            Assert.Equal(
                dela.Groups[7].Success ? dela.Groups[7].Value : null,
                meu.Consulta);

            Assert.Equal(
                dela.Groups[9].Success ? dela.Groups[9].Value : null,
                meu.Fragmento);
        }
    }

    [Theory(DisplayName = "a autoridade, com IPv6 e com a armadilha do arroba")]
    [InlineData("exemplo.com", null, "exemplo.com", null)]
    [InlineData("exemplo.com:8080", null, "exemplo.com", 8080)]
    [InlineData("u@exemplo.com", "u", "exemplo.com", null)]
    [InlineData("u:s@exemplo.com:80", "u:s", "exemplo.com", 80)]
    [InlineData("[::1]", null, "[::1]", null)]
    [InlineData("[::1]:8080", null, "[::1]", 8080)]
    [InlineData("[2001:db8::1]:443", null, "[2001:db8::1]", 443)]
    [InlineData("exemplo.com:", null, "exemplo.com", null)]
    [InlineData("", null, "", null)]
    // A armadilha: o que vem ANTES do arroba é usuário, não máquina.
    [InlineData("banco.com.br@atacante.com", "banco.com.br", "atacante.com", null)]
    public void PartirAutoridade(string texto, string? usuario, string maquina, int? porta)
    {
        var lida = Autoridade.Ler(texto);

        Assert.Equal(usuario, lida.Usuario);
        Assert.Equal(maquina, lida.Maquina);
        Assert.Equal(porta, lida.Porta);
    }
}

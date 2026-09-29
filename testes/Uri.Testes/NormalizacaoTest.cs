using Xunit;

namespace Conde.Uris.Testes;

/// <summary>
/// A normalização do §6.2 da RFC — e, sobretudo, o que ela <b>não</b> faz.
/// </summary>
/// <remarks>
/// Normalizar demais é uma falha de segurança. Quando um sistema compara URIs
/// para decidir permissão, transformar dois URIs diferentes no mesmo é abrir
/// uma porta — e quase toda biblioteca de "normalização" faz mais do que a RFC
/// autoriza.
/// </remarks>
public class NormalizacaoTest
{
    [Theory(DisplayName = "os três níveis que a RFC autoriza")]
    // 1. Caixa no esquema e na máquina.
    [InlineData("HTTP://EXEMPLO.COM/a", "http://exemplo.com/a")]
    [InlineData("HtTp://ExEmPlO.CoM/", "http://exemplo.com/")]
    // 2. Porcentagem: decodifica o não reservado, maiúscula no resto.
    [InlineData("http://exemplo.com/%7Eusuario", "http://exemplo.com/~usuario")]
    [InlineData("http://exemplo.com/%7eusuario", "http://exemplo.com/~usuario")]
    [InlineData("http://exemplo.com/%41%42%43", "http://exemplo.com/ABC")]
    [InlineData("http://exemplo.com/a%2fb", "http://exemplo.com/a%2Fb")]
    [InlineData("http://exemplo.com/a%20b", "http://exemplo.com/a%20b")]
    // 3. Segmentos do caminho.
    [InlineData("http://exemplo.com/a/./b/../c", "http://exemplo.com/a/c")]
    [InlineData("http://exemplo.com/a/../../b", "http://exemplo.com/b")]
    public void OsTresNiveis(string entrada, string esperado)
    {
        Assert.Equal(esperado, Normalizador.Normalizar(entrada));
    }

    [Theory(DisplayName = "o que a normalização NÃO pode fazer")]
    // O caminho distingue maiúsculas: num servidor Linux são arquivos
    // diferentes. Baixar o caminho seria trocar um recurso por outro.
    [InlineData("http://exemplo.com/Arquivo.TXT", "http://exemplo.com/Arquivo.TXT")]
    // A barra do caminho vazio não é acrescentada: `http://a` e `http://a/` são
    // URIs diferentes, mesmo que um servidor os trate igual.
    [InlineData("http://exemplo.com", "http://exemplo.com")]
    // A porta padrão não é tirada: isso depende do esquema, e um esquema que
    // este código não conhece teria a porta apagada por engano.
    [InlineData("http://exemplo.com:80/a", "http://exemplo.com:80/a")]
    // O %2F continua codificado: decodificá-lo mudaria a estrutura do caminho.
    [InlineData("http://exemplo.com/a%2Fb", "http://exemplo.com/a%2Fb")]
    // A consulta não é reordenada nem tocada além da porcentagem.
    [InlineData("http://exemplo.com/a?b=2&a=1", "http://exemplo.com/a?b=2&a=1")]
    // O fragmento vazio não some.
    [InlineData("http://exemplo.com/a#", "http://exemplo.com/a#")]
    public void OQueNaoPodeFazer(string entrada, string esperado)
    {
        Assert.Equal(esperado, Normalizador.Normalizar(entrada));
    }

    [Fact(DisplayName = "o %2F não vira barra, e é por isso que o filtro de .. não basta")]
    public void OPorCentoDoisEfe()
    {
        // É a armadilha clássica de travessia de caminho: o filtro procura ".."
        // no texto cru, não acha, e o servidor decodifica depois -- e aí o
        // "%2E%2E%2F" vira "../" tarde demais.
        //
        // A normalização da RFC não decodifica o %2F nem o %2E, e é isso que
        // mantém a estrutura do caminho intacta para quem for filtrar.
        var perigoso = "http://exemplo.com/a/%2E%2E%2F%2E%2E%2Fetc/passwd";

        var normalizado = Normalizador.Normalizar(perigoso);

        // O %2E é o ponto, e ele É não reservado -- então é decodificado.
        // O %2F é a barra, que é reservada, e continua codificada.
        Assert.Contains("%2F", normalizado);
        Assert.DoesNotContain("/etc/passwd", normalizado[..normalizado.LastIndexOf("etc")]);

        // E o caminho continua com um segmento só depois de "a".
        var partido = Analisador.Ler(normalizado);

        Assert.Equal("/a/..%2F..%2Fetc/passwd", partido.Caminho);
    }

    [Fact(DisplayName = "normalizar é idempotente")]
    public void NormalizarDuasVezesDaOMesmo()
    {
        // Se normalizar duas vezes desse resultado diferente, a função não
        // definiria uma forma canônica -- e comparar dois URIs por ela deixaria
        // de valer. É a propriedade que faz a normalização servir para alguma
        // coisa.
        var sorteio = new Random(20260929);

        var pedacos = new[]
        {
            "HTTP://", "http://", "Exemplo.COM", "exemplo.com", ":80", "/a", "/A",
            "/./b", "/../c", "%7E", "%7e", "%2F", "%41", "?q=%7E", "#F%7e", "",
            "/a/b/../..", "//", "u@", "[::1]",
        };

        for (var i = 0; i < 20_000; i++)
        {
            var texto = string.Concat(
                Enumerable.Range(0, sorteio.Next(1, 6))
                    .Select(_ => pedacos[sorteio.Next(pedacos.Length)]));

            var umaVez = Normalizador.Normalizar(texto);
            var duasVezes = Normalizador.Normalizar(umaVez);

            Assert.True(umaVez == duasVezes,
                $"\"{texto}\" -> \"{umaVez}\" -> \"{duasVezes}\"");
        }
    }

    [Theory(DisplayName = "quais URIs apontam para a mesma coisa")]
    [InlineData("HTTP://exemplo.com/%7Ea", "http://EXEMPLO.com/~a", true)]
    [InlineData("http://exemplo.com/a/./b", "http://exemplo.com/a/b", true)]
    [InlineData("http://exemplo.com/a", "http://exemplo.com/A", false)]
    [InlineData("http://exemplo.com", "http://exemplo.com/", false)]
    [InlineData("http://exemplo.com:80/", "http://exemplo.com/", false)]
    [InlineData("http://exemplo.com/a?", "http://exemplo.com/a", false)]
    public void SaoIguais(string um, string outro, bool esperado)
    {
        Assert.Equal(esperado, Normalizador.SaoIguais(um, outro));
    }

    [Fact(DisplayName = "codificar e normalizar dão a volta completa")]
    public void CodificarENormalizar()
    {
        var textos = new[]
        {
            "a b", "ação", "日本語", "a/b", "a?b", "a#b", "100%", "a&b=c",
            "~til", "-hifen_", ".", "",
        };

        foreach (var texto in textos)
        {
            var codificado = Normalizador.Codificar(texto);

            // O codificado tem de sobreviver à normalização sem mudar de
            // significado -- e os não reservados voltam a ser eles mesmos.
            var normalizado = Normalizador.NormalizarPorcentagem(codificado);

            Assert.Equal(
                texto,
                Uri.UnescapeDataString(normalizado));
        }
    }

    [Fact(DisplayName = "um % solto não derruba nada")]
    public void PorCentoSolto()
    {
        // A web está cheia deles. Recusar seria mais puro e menos útil.
        foreach (var texto in (string[])["100%", "a%", "%", "%z", "%2", "a%%20b"])
        {
            var normalizado = Normalizador.NormalizarPorcentagem(texto);

            Assert.NotNull(normalizado);
        }

        Assert.Equal("100%", Normalizador.NormalizarPorcentagem("100%"));

        // O `%%20` tem um `%` solto seguido de um `%20`: o solto fica, e o
        // `%20` continua codificado porque o espaco NAO e nao reservado.
        // Escrevi "a% b" na primeira versao deste teste, e errei os dois.
        Assert.Equal("a%%20b", Normalizador.NormalizarPorcentagem("a%%20b"));
    }
}

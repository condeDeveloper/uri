using System.Text;

namespace Conde.Uris;

/// <summary>
/// Resolve uma referência relativa contra uma base — o §5 da RFC 3986.
/// </summary>
/// <remarks>
/// <para>
/// É a operação que o navegador faz milhares de vezes por página, e ninguém
/// pensa nela: um <c>&lt;a href="../b"&gt;</c> numa página que está em
/// <c>https://exemplo.com/a/c/d</c> aponta para <c>https://exemplo.com/a/b</c>.
/// Transformar um no outro é este arquivo.
/// </para>
/// <para>
/// O algoritmo está no §5.2.2 da RFC e é uma cascata de casos. O que ele resolve
/// é mais sutil do que parece, porque cada componente que a referência traz
/// <b>apaga</b> todos os de baixo:
/// </para>
/// <code>
///   href="http://outro.com/x"  troca tudo
///   href="//outro.com/x"       mantém o esquema, troca o resto
///   href="/x"                  mantém esquema e máquina
///   href="x"                   mantém até o diretório do caminho
///   href="?q=1"                mantém o caminho, troca a consulta
///   href="#topo"               mantém tudo, troca só o fragmento
///   href=""                    é a própria página, sem o fragmento
/// </code>
/// <para>
/// A última linha é a que surpreende: uma referência <b>vazia</b> aponta para a
/// página atual sem o fragmento, e não para a raiz do site.
/// </para>
/// </remarks>
public static class Resolvedor
{
    public static Referencia Resolver(Referencia baseDe, Referencia referencia)
    {
        // A cascata do §5.2.2, na ordem exata. Cada ramo é um dos casos acima, e
        // a ordem é o que faz a regra "o que vem de cima apaga o que vem de
        // baixo" valer.
        if (referencia.Esquema is not null)
        {
            return referencia with { Caminho = RemoverPontos(referencia.Caminho) };
        }

        if (referencia.Autoridade is not null)
        {
            return new Referencia(
                baseDe.Esquema,
                referencia.Autoridade,
                RemoverPontos(referencia.Caminho),
                referencia.Consulta,
                referencia.Fragmento);
        }

        if (referencia.Caminho.Length == 0)
        {
            // Referência vazia ou só com consulta: fica a página atual. A
            // consulta só é herdada quando a referência não traz uma -- e é o
            // que faz `href="?"` limpar a consulta em vez de manter a de antes.
            return new Referencia(
                baseDe.Esquema,
                baseDe.Autoridade,
                baseDe.Caminho,
                referencia.Consulta ?? baseDe.Consulta,
                referencia.Fragmento);
        }

        var caminho = referencia.Caminho.StartsWith('/')
            ? RemoverPontos(referencia.Caminho)
            : RemoverPontos(Juntar(baseDe, referencia.Caminho));

        return new Referencia(
            baseDe.Esquema,
            baseDe.Autoridade,
            caminho,
            referencia.Consulta,
            referencia.Fragmento);
    }

    /// <summary>Cola o caminho relativo no diretório do caminho da base — §5.3.</summary>
    private static string Juntar(Referencia baseDe, string relativo)
    {
        // A regra estranha do §5.2.3: se a base tem autoridade e o caminho dela
        // é vazio, o resultado começa com uma barra. Sem ela,
        // "http://exemplo.com" + "a" daria "http://exemplo.coma".
        if (baseDe.Autoridade is not null && baseDe.Caminho.Length == 0)
        {
            return "/" + relativo;
        }

        var ultimaBarra = baseDe.Caminho.LastIndexOf('/');

        return ultimaBarra < 0
            ? relativo
            : baseDe.Caminho[..(ultimaBarra + 1)] + relativo;
    }

    /// <summary>
    /// Tira os <c>.</c> e <c>..</c> do caminho — o §5.2.4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É o algoritmo mais delicado da RFC, e o que mais rende falha de
    /// segurança. A "travessia de caminho" — pedir <c>../../etc/passwd</c> e o
    /// servidor obedecer — é literalmente este algoritmo feito errado.
    /// </para>
    /// <para>
    /// A regra que quase toda implementação caseira erra: um <c>..</c> que sobe
    /// além da raiz é <b>descartado</b>, e não levado adiante. Em
    /// <c>/a/../../b</c>, o segundo <c>..</c> não tem o que remover e some — o
    /// resultado é <c>/b</c>, não <c>/../b</c>. É essa regra que impede o
    /// caminho de escapar da raiz, e é ela que falta quando alguém escreve o
    /// algoritmo de cabeça.
    /// </para>
    /// <para>
    /// A outra sutileza: <c>/a/b/.</c> e <c>/a/b/..</c> terminam com barra. O
    /// ponto sozinho no fim quer dizer "este diretório", e um diretório termina
    /// com barra. Trocar <c>"/."</c> por <c>""</c> em vez de <c>"/"</c> muda o
    /// resultado, e é o engano mais comum aqui.
    /// </para>
    /// </remarks>
    public static string RemoverPontos(string caminho)
    {
        var entrada = caminho;
        var saida = new StringBuilder();

        while (entrada.Length > 0)
        {
            // A. Um "./" ou "../" no começo: joga fora.
            if (entrada.StartsWith("../", StringComparison.Ordinal))
            {
                entrada = entrada[3..];
                continue;
            }

            if (entrada.StartsWith("./", StringComparison.Ordinal))
            {
                entrada = entrada[2..];
                continue;
            }

            // B. "/./" vira "/", e "/." no fim vira "/".
            if (entrada.StartsWith("/./", StringComparison.Ordinal))
            {
                entrada = entrada[2..];
                continue;
            }

            if (entrada == "/.")
            {
                entrada = "/";
                continue;
            }

            // C. "/../" sobe um nível, e "/.." no fim também.
            if (entrada.StartsWith("/../", StringComparison.Ordinal))
            {
                entrada = entrada[3..];
                Subir(saida);
                continue;
            }

            if (entrada == "/..")
            {
                entrada = "/";
                Subir(saida);
                continue;
            }

            // D. Um "." ou ".." sozinho: joga fora. É o caso que impede o
            //    caminho de sair da raiz.
            if (entrada is "." or "..")
            {
                entrada = string.Empty;
                continue;
            }

            // E. Passa o próximo segmento inteiro para a saída.
            var proxima = entrada.IndexOf('/', entrada.StartsWith('/') ? 1 : 0);

            if (proxima < 0)
            {
                saida.Append(entrada);
                entrada = string.Empty;
            }
            else
            {
                saida.Append(entrada[..proxima]);
                entrada = entrada[proxima..];
            }
        }

        return saida.ToString();
    }

    /// <summary>Apaga o último segmento da saída, sem nunca passar da raiz.</summary>
    private static void Subir(StringBuilder saida)
    {
        var ultimaBarra = -1;

        for (var i = saida.Length - 1; i >= 0; i--)
        {
            if (saida[i] == '/')
            {
                ultimaBarra = i;
                break;
            }
        }

        // Sem barra nenhuma na saída, não há para onde subir: zera. É este
        // ramo, e não uma exceção, que impede o `..` de escapar da raiz.
        saida.Length = ultimaBarra < 0 ? 0 : ultimaBarra;
    }

    /// <summary>Resolve a partir de dois textos.</summary>
    public static string Resolver(string baseDe, string referencia) =>
        Resolver(Analisador.Ler(baseDe), Analisador.Ler(referencia)).ToString();
}

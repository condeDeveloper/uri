using System.Text.RegularExpressions;

namespace Conde.Uris;

/// <summary>
/// Parte um URI nos cinco componentes.
/// </summary>
/// <remarks>
/// <para>
/// O apêndice B da RFC 3986 traz uma expressão regular que faz isso, e ela é um
/// dos poucos casos em que uma especificação entrega o código pronto:
/// </para>
/// <code>
///   ^(([^:/?#]+):)?(//([^/?#]*))?([^?#]*)(\?([^#]*))?(#(.*))?
/// </code>
/// <para>
/// A RFC diz, com todas as letras, que essa expressão é <b>proposital</b>: ela
/// aceita qualquer texto e não valida nada. Partir e validar são trabalhos
/// diferentes, e misturá-los faz um analisador recusar um URI que só tem um
/// caractere estranho no fragmento — que ninguém vai usar.
/// </para>
/// <para>
/// Este código faz a mesma divisão à mão, e por um motivo: a expressão da RFC
/// não distingue <b>ausente</b> de <b>vazio</b> sem cuidado extra em cada grupo,
/// e é exatamente essa distinção que decide a resolução de referência. Escrevendo
/// à mão, cada <c>null</c> fica explícito.
/// </para>
/// </remarks>
public static class Analisador
{
    /// <summary>A expressão do apêndice B, para comparar com a leitura à mão.</summary>
    public static readonly Regex DoApendiceB = new(
        @"^(([^:/?#]+):)?(//([^/?#]*))?([^?#]*)(\?([^#]*))?(#(.*))?",
        RegexOptions.Compiled);

    /// <summary>
    /// Parte um texto nos cinco componentes. Nunca falha.
    /// </summary>
    /// <remarks>
    /// A ordem em que os separadores são procurados é a ordem da gramática, e
    /// ela importa: o <c>#</c> primeiro (o fragmento come tudo até o fim), a
    /// <c>?</c> depois (a consulta vai até o fragmento), e só então o <c>:</c>
    /// do esquema — que só conta se vier <b>antes</b> de qualquer barra.
    /// </remarks>
    public static Referencia Ler(string texto)
    {
        string? fragmento = null;
        string? consulta = null;
        string? esquema = null;
        string? autoridade = null;

        var resto = texto;

        // 1. O fragmento vai do primeiro `#` até o fim, e ele pode ter `?` e `#`
        //    dentro sem que isso signifique nada.
        var cerquilha = resto.IndexOf('#');

        if (cerquilha >= 0)
        {
            fragmento = resto[(cerquilha + 1)..];
            resto = resto[..cerquilha];
        }

        // 2. A consulta vai do primeiro `?` até onde o fragmento começava.
        var interrogacao = resto.IndexOf('?');

        if (interrogacao >= 0)
        {
            consulta = resto[(interrogacao + 1)..];
            resto = resto[..interrogacao];
        }

        // 3. O esquema só existe se houver `:` ANTES de qualquer `/`, `?` ou
        //    `#`. Sem essa condição, `/a:b` teria esquema `/a` -- e um caminho
        //    relativo com dois-pontos no primeiro segmento é comum.
        var doisPontos = resto.IndexOf(':');

        if (doisPontos > 0 && EhEsquemaValido(resto[..doisPontos]))
        {
            var antes = resto[..doisPontos];
            var barra = resto.IndexOf('/');

            if (barra < 0 || barra > doisPontos)
            {
                esquema = antes;
                resto = resto[(doisPontos + 1)..];
            }
        }

        // 4. A autoridade vem depois de `//` e vai até a primeira barra.
        if (resto.StartsWith("//", StringComparison.Ordinal))
        {
            var depois = resto[2..];
            var barra = depois.IndexOf('/');

            if (barra < 0)
            {
                autoridade = depois;
                resto = string.Empty;
            }
            else
            {
                autoridade = depois[..barra];
                resto = depois[barra..];
            }
        }

        return new Referencia(esquema, autoridade, resto, consulta, fragmento);
    }

    /// <summary>
    /// Um esquema é letra seguida de letras, dígitos, <c>+</c>, <c>-</c> e <c>.</c>.
    /// </summary>
    /// <remarks>
    /// Começar com letra é o que separa <c>http:</c> de <c>1234:</c>, e existe
    /// para que um caminho relativo que comece com números não seja confundido
    /// com um esquema. É a mesma regra dos identificadores de quase toda
    /// linguagem, e pelo mesmo motivo.
    /// </remarks>
    public static bool EhEsquemaValido(string texto)
    {
        if (texto.Length == 0 || !char.IsAsciiLetter(texto[0]))
        {
            return false;
        }

        foreach (var letra in texto)
        {
            if (!char.IsAsciiLetterOrDigit(letra) && letra is not ('+' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }
}

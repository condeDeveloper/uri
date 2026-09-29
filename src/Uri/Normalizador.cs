using System.Text;

namespace Conde.Uris;

/// <summary>
/// Normaliza um URI — o §6.2 da RFC 3986.
/// </summary>
/// <remarks>
/// <para>
/// A pergunta que este arquivo responde é "estes dois URIs apontam para a mesma
/// coisa?", e ela é mais difícil do que parece porque o mesmo recurso pode ser
/// escrito de muitas formas. A RFC organiza a resposta em níveis, do mais
/// seguro ao mais arriscado, e só os três primeiros são feitos aqui.
/// </para>
/// <list type="number">
///   <item><description>
///     <b>Caixa.</b> O esquema e a máquina não distinguem maiúsculas:
///     <c>HTTP://Exemplo.COM</c> é <c>http://exemplo.com</c>. O <b>caminho</b>
///     distingue, e confundir os dois é o engano mais comum — num servidor
///     Linux, <c>/A</c> e <c>/a</c> são arquivos diferentes.
///   </description></item>
///   <item><description>
///     <b>Codificação por porcentagem.</b> <c>%7E</c> e <c>%7e</c> são o mesmo
///     byte, e os dois são o caractere <c>~</c>, que não precisava estar
///     codificado. Decodificar o que é "não reservado" e pôr os hexadecimais
///     que sobram em maiúscula deixa uma forma só.
///   </description></item>
///   <item><description>
///     <b>Segmentos de caminho.</b> Os <c>.</c> e <c>..</c> saem, pelo mesmo
///     algoritmo da resolução.
///   </description></item>
/// </list>
/// <para>
/// O que <b>não</b> é feito aqui, de propósito: tirar a porta padrão
/// (<c>:80</c> num <c>http</c>), acrescentar a barra do caminho vazio, ou
/// ordenar os parâmetros da consulta. As três dependem do esquema ou da
/// aplicação, e fazê-las em silêncio transforma dois URIs diferentes no mesmo —
/// o que é uma falha de segurança quando alguém compara URIs para decidir
/// permissão.
/// </para>
/// </remarks>
public static class Normalizador
{
    /// <summary>
    /// Os caracteres que nunca precisam de codificação — o §2.3 da RFC.
    /// </summary>
    /// <remarks>
    /// São 66: as letras, os dígitos, e <c>- . _ ~</c>. Um <c>%41</c> no lugar
    /// de um <c>A</c> é legítimo e é a mesma coisa, e decodificá-lo é seguro
    /// porque nenhum deles tem significado de separador.
    /// </remarks>
    public static bool EhNaoReservado(char letra) =>
        char.IsAsciiLetterOrDigit(letra) || letra is '-' or '.' or '_' or '~';

    public static Referencia Normalizar(Referencia referencia)
    {
        var esquema = referencia.Esquema?.ToLowerInvariant();

        var autoridade = referencia.Autoridade is null
            ? null
            : NormalizarAutoridade(referencia.Autoridade);

        // O caminho NÃO vai para minúscula: num servidor Linux, `/A` e `/a` são
        // arquivos diferentes. Só a porcentagem é normalizada.
        var caminho = Resolvedor.RemoverPontos(NormalizarPorcentagem(referencia.Caminho));

        caminho = ProtegerCaminhoAmbiguo(caminho, esquema, autoridade);

        return new Referencia(
            esquema,
            autoridade,
            caminho,
            referencia.Consulta is null ? null : NormalizarPorcentagem(referencia.Consulta),
            referencia.Fragmento is null ? null : NormalizarPorcentagem(referencia.Fragmento));
    }

    /// <summary>
    /// Desfaz as duas ambiguidades que um caminho pode criar ao ser relido.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Este caso foi achado por um teste de propriedade, e ele é bonito: a
    /// normalização era <b>não idempotente</b>. O contraexemplo que o teste
    /// cuspiu foi <c>%41:80//</c>.
    /// </para>
    /// <para>
    /// A primeira passada decodifica o <c>%41</c> — que é a letra <c>A</c>, e é
    /// não reservada, então decodificar é o que a RFC manda — e produz
    /// <c>A:80//</c>. Só que <c>A:80//</c> tem um esquema onde não havia
    /// nenhum, e a segunda passada o vê, o põe em minúscula, e devolve
    /// <c>a:80//</c>. Normalizar duas vezes dava resultado diferente de
    /// normalizar uma, e aí a função não define forma canônica nenhuma.
    /// </para>
    /// <para>
    /// A RFC prevê exatamente isto, no §4.2: "um segmento de caminho que contém
    /// dois-pontos não pode ser usado como o primeiro segmento de uma
    /// referência de caminho relativo, porque seria confundido com um nome de
    /// esquema". A saída que ela indica é o <c>./</c> na frente — que não muda
    /// o recurso apontado e desfaz a ambiguidade.
    /// </para>
    /// <para>
    /// E o mesmo teste achou a <b>segunda</b> ambiguidade, com o contraexemplo
    /// <c>/a/b/../..//%41</c>. A remoção de pontos deixa <c>//A</c>, e um
    /// caminho que começa com duas barras vira <b>autoridade</b> na releitura —
    /// o <c>A</c> deixa de ser um arquivo e passa a ser uma máquina. O §3.3 da
    /// RFC é explícito: "se um URI não contém componente de autoridade, o
    /// caminho não pode começar com dois caracteres de barra". Aqui a saída é
    /// um <c>/.</c> na frente.
    /// </para>
    /// <para>
    /// Três defeitos, todos do mesmo teste de propriedade, todos invisíveis nos
    /// exemplos da RFC. É o que uma propriedade faz e uma tabela de casos não:
    /// ela procura onde ninguém pensou em olhar.
    /// </para>
    /// </remarks>
    private static string ProtegerCaminhoAmbiguo(string caminho, string? esquema,
        string? autoridade)
    {
        if (caminho.Length == 0)
        {
            return caminho;
        }

        // Sem autoridade, um caminho que começa com "//" seria lido como uma.
        if (autoridade is null && caminho.StartsWith("//", StringComparison.Ordinal))
        {
            return "/." + caminho;
        }

        if (esquema is not null || autoridade is not null || caminho.StartsWith('/'))
        {
            return caminho;
        }

        var primeiraBarra = caminho.IndexOf('/');
        var primeiroSegmento = primeiraBarra < 0 ? caminho : caminho[..primeiraBarra];

        return primeiroSegmento.Contains(':') ? "./" + caminho : caminho;
    }

    private static string NormalizarAutoridade(string autoridade)
    {
        var partida = Autoridade.Ler(autoridade);

        // A máquina vai para minúscula; o usuário não. Um nome de usuário pode
        // distinguir caixa, e o DNS não distingue.
        return (new Autoridade(
            partida.Usuario is null ? null : NormalizarPorcentagem(partida.Usuario),
            NormalizarPorcentagem(partida.Maquina).ToLowerInvariant(),
            partida.Porta)
        {
            PortaComoTexto = partida.PortaComoTexto,
        }).ToString();
    }

    /// <summary>
    /// Decodifica o que não precisava estar codificado, e põe o resto em maiúscula.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ordem importa e é sutil. Um <c>%2F</c> <b>não</b> pode ser decodificado
    /// para <c>/</c>: a barra é um separador, e trocá-la mudaria a estrutura do
    /// caminho. É a diferença entre um segmento chamado <c>a/b</c> e dois
    /// segmentos <c>a</c> e <c>b</c> — e confundir os dois é por onde passa uma
    /// travessia de caminho que o filtro de <c>..</c> não pega.
    /// </para>
    /// <para>
    /// Por isso só os 66 não reservados são decodificados. Todo o resto fica
    /// codificado, com os dois dígitos em maiúscula.
    /// </para>
    /// </remarks>
    public static string NormalizarPorcentagem(string texto)
    {
        var saida = new StringBuilder(texto.Length);

        for (var i = 0; i < texto.Length; i++)
        {
            if (texto[i] != '%' || i + 2 >= texto.Length)
            {
                saida.Append(texto[i]);
                continue;
            }

            var alto = Hexa(texto[i + 1]);
            var baixo = Hexa(texto[i + 2]);

            if (alto < 0 || baixo < 0)
            {
                // Um `%` que não é seguido de dois hexadecimais fica como está.
                // Recusar seria mais puro e menos útil: a web está cheia deles.
                saida.Append(texto[i]);
                continue;
            }

            var valor = (char)(alto * 16 + baixo);

            if (EhNaoReservado(valor))
            {
                saida.Append(valor);
            }
            else
            {
                saida.Append('%')
                    .Append(char.ToUpperInvariant(texto[i + 1]))
                    .Append(char.ToUpperInvariant(texto[i + 2]));
            }

            i += 2;
        }

        return saida.ToString();
    }

    private static int Hexa(char letra) => letra switch
    {
        >= '0' and <= '9' => letra - '0',
        >= 'a' and <= 'f' => letra - 'a' + 10,
        >= 'A' and <= 'F' => letra - 'A' + 10,
        _ => -1,
    };

    /// <summary>Codifica um texto para caber num componente de URI.</summary>
    public static string Codificar(string texto, string tambemPermitidos = "")
    {
        var saida = new StringBuilder();

        foreach (var bruto in Encoding.UTF8.GetBytes(texto))
        {
            var letra = (char)bruto;

            if (EhNaoReservado(letra) || tambemPermitidos.Contains(letra))
            {
                saida.Append(letra);
            }
            else
            {
                saida.Append('%').Append(bruto.ToString("X2"));
            }
        }

        return saida.ToString();
    }

    /// <summary>Dois URIs apontam para a mesma coisa, depois de normalizados?</summary>
    public static bool SaoIguais(string um, string outro) =>
        Normalizar(Analisador.Ler(um)).ToString()
            == Normalizar(Analisador.Ler(outro)).ToString();

    public static string Normalizar(string texto) =>
        Normalizar(Analisador.Ler(texto)).ToString();
}

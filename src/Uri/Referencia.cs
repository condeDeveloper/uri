using System.Text;

namespace Conde.Uris;

/// <summary>
/// Uma referência de URI, partida nos cinco pedaços da RFC 3986.
/// </summary>
/// <remarks>
/// <para>
/// A gramática inteira do formato cabe numa linha, e ela está impressa no
/// apêndice A da RFC:
/// </para>
/// <code>
///   URI = scheme ":" hier-part [ "?" query ] [ "#" fragment ]
///
///   https://usuario@exemplo.com:8080/a/b?q=1#topo
///   \___/   \_________________________/\___/ \__/
///  esquema         autoridade+caminho  consulta fragmento
/// </code>
/// <para>
/// O que torna o assunto difícil não é a gramática: é a diferença entre
/// <b>ausente</b> e <b>vazio</b>. Um componente que não existe e um que existe e
/// está vazio são coisas diferentes, e a diferença muda o resultado:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>http://exemplo.com</c> tem caminho vazio;
///     <c>http://exemplo.com/</c> tem caminho <c>/</c>. São URIs diferentes.
///   </description></item>
///   <item><description>
///     <c>http://exemplo.com/a</c> não tem consulta;
///     <c>http://exemplo.com/a?</c> tem uma consulta vazia. São diferentes.
///   </description></item>
///   <item><description>
///     <c>//exemplo.com</c> tem autoridade e não tem esquema — é uma referência
///     relativa de protocolo, e em 2010 ela estava em metade dos sites.
///   </description></item>
/// </list>
/// <para>
/// Guardar cada componente como <c>string?</c>, com <c>null</c> para ausente, é
/// a única forma de não perder isso. Uma implementação que use texto vazio para
/// os dois casos produz uma resolução de referência errada — e o erro só aparece
/// nos casos de canto, que são exatamente os que a RFC lista.
/// </para>
/// </remarks>
public sealed record Referencia(
    string? Esquema,
    string? Autoridade,
    string Caminho,
    string? Consulta,
    string? Fragmento)
{
    /// <summary>Uma referência com esquema é um URI; sem, é relativa.</summary>
    public bool EhAbsoluta => Esquema is not null;

    public static readonly Referencia Vazia = new(null, null, string.Empty, null, null);

    /// <summary>
    /// Remonta o texto a partir dos pedaços — o algoritmo do §5.3 da RFC.
    /// </summary>
    /// <remarks>
    /// Cinco linhas, e cada uma existe. A da autoridade leva as duas barras que
    /// a separam do resto; a da consulta leva a interrogação, e ela sai
    /// <b>só</b> quando a consulta existe — e é aí que <c>null</c> e vazio se
    /// separam de verdade.
    /// </remarks>
    public override string ToString()
    {
        var texto = new StringBuilder();

        if (Esquema is not null)
        {
            texto.Append(Esquema).Append(':');
        }

        if (Autoridade is not null)
        {
            texto.Append("//").Append(Autoridade);
        }

        texto.Append(Caminho);

        if (Consulta is not null)
        {
            texto.Append('?').Append(Consulta);
        }

        if (Fragmento is not null)
        {
            texto.Append('#').Append(Fragmento);
        }

        return texto.ToString();
    }

    /// <summary>A autoridade partida em usuário, máquina e porta.</summary>
    public Autoridade? PartirAutoridade() =>
        Autoridade is null ? null : Uris.Autoridade.Ler(Autoridade);
}

/// <summary>A autoridade: <c>[usuário@]máquina[:porta]</c>.</summary>
/// <remarks>
/// <para>
/// A parte que pega quem escreve isto de primeira é o endereço IPv6. Ele tem
/// dois-pontos dentro, e o dois-pontos é justamente o separador da porta — daí
/// os colchetes: <c>[::1]:8080</c>. Procurar o último dois-pontos sem olhar os
/// colchetes trata <c>::1</c> como máquina <c>:</c> e porta <c>1</c>.
/// </para>
/// <para>
/// A parte de usuário e senha (<c>usuário:senha@</c>) está depreciada desde a
/// RFC 3986, de 2005, e continua em toda parte — e é por onde passa o ataque de
/// URI enganoso: <c>https://banco.com.br@sitedoatacante.com</c> não vai ao banco
/// nenhum, vai ao site do atacante, com o nome do banco de isca.
/// </para>
/// </remarks>
public sealed record Autoridade(string? Usuario, string Maquina, int? Porta)
{
    /// <summary>
    /// O texto cru da porta, com os dois-pontos, ou <c>null</c> se não havia.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Guardar o texto além do número não é zelo: sem ele a autoridade
    /// <b>perde bytes</b>. Um <c>exemplo.com:abc</c> tem uma porta que não é um
    /// número, e a primeira versão daqui devolvia <c>Porta = null</c> e
    /// remontava como <c>exemplo.com</c> — comendo o <c>:abc</c> em silêncio.
    /// </para>
    /// <para>
    /// Achado por um teste de idempotência, num caso que o gerador inventou:
    /// <c>http://%41[::1]#...</c>. Um analisador pode recusar uma entrada, e
    /// não pode devolvê-la menor do que recebeu.
    /// </para>
    /// </remarks>
    public string? PortaComoTexto { get; init; }

    public static Autoridade Ler(string texto)
    {
        string? usuario = null;
        var resto = texto;

        var arroba = texto.LastIndexOf('@');

        if (arroba >= 0)
        {
            usuario = texto[..arroba];
            resto = texto[(arroba + 1)..];
        }

        // O IPv6 vem entre colchetes justamente porque tem dois-pontos dentro.
        if (resto.StartsWith('['))
        {
            var fecha = resto.IndexOf(']');

            if (fecha > 0)
            {
                var maquinaIpv6 = resto[..(fecha + 1)];
                var depois = resto[(fecha + 1)..];

                return new Autoridade(usuario, maquinaIpv6, LerPorta(depois))
                {
                    PortaComoTexto = depois.Length == 0 ? null : depois,
                };
            }
        }

        var doisPontos = resto.LastIndexOf(':');

        if (doisPontos >= 0)
        {
            return new Autoridade(usuario, resto[..doisPontos],
                LerPorta(resto[doisPontos..]))
            {
                PortaComoTexto = resto[doisPontos..],
            };
        }

        return new Autoridade(usuario, resto, null);
    }

    private static int? LerPorta(string texto)
    {
        if (texto.Length == 0 || texto[0] != ':')
        {
            return null;
        }

        var numero = texto[1..];

        // `http://exemplo.com:/a` tem uma porta VAZIA, que é legítima e quer
        // dizer "a padrão do esquema". Devolver zero seria inventar um número.
        if (numero.Length == 0)
        {
            return null;
        }

        return int.TryParse(numero, out var porta) ? porta : null;
    }

    public override string ToString()
    {
        var texto = new StringBuilder();

        if (Usuario is not null)
        {
            texto.Append(Usuario).Append('@');
        }

        texto.Append(Maquina);

        // O texto cru manda, e não o número: ele é o que preserva a ida e volta
        // quando a porta não é um número, ou está vazia, ou tem zeros à frente.
        if (PortaComoTexto is not null)
        {
            texto.Append(PortaComoTexto);
        }
        else if (Porta is not null)
        {
            texto.Append(':').Append(Porta);
        }

        return texto.ToString();
    }
}

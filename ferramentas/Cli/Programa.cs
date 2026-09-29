using Conde.Uris;

namespace Conde.Uris.Ferramentas.Cli;

/// <summary>
/// A linha de comando: partir, resolver, normalizar e comparar.
/// </summary>
/// <remarks>
/// O comando <c>partir</c> é o mais útil dos quatro, e o motivo é a coluna que
/// diz <c>(ausente)</c>. A diferença entre um componente que não existe e um que
/// existe e está vazio decide a resolução inteira, e nenhuma ferramenta comum a
/// mostra.
/// </remarks>
public static class Programa
{
    public static int Main(string[] argumentos)
    {
        if (argumentos.Length < 2)
        {
            Console.WriteLine(Ajuda);

            return 1;
        }

        return argumentos[0] switch
        {
            "partir" => Partir(argumentos[1]),
            "normalizar" => Normalizar(argumentos[1]),
            "resolver" => Resolver(argumentos),
            "iguais" => Iguais(argumentos),
            _ => Desconhecido(argumentos[0]),
        };
    }

    private static int Desconhecido(string comando)
    {
        Console.Error.WriteLine($"não conheço o comando {comando}");
        Console.WriteLine(Ajuda);

        return 1;
    }

    private static int Partir(string texto)
    {
        var lida = Analisador.Ler(texto);

        Console.WriteLine($"  {texto}");
        Console.WriteLine();

        Mostrar("esquema", lida.Esquema);
        Mostrar("autoridade", lida.Autoridade);
        Mostrar("caminho", lida.Caminho);
        Mostrar("consulta", lida.Consulta);
        Mostrar("fragmento", lida.Fragmento);

        var autoridade = lida.PartirAutoridade();

        if (autoridade is not null)
        {
            Console.WriteLine();
            Console.WriteLine("  a autoridade por dentro:");

            Mostrar("  usuário", autoridade.Usuario);
            Mostrar("  máquina", autoridade.Maquina);
            Mostrar("  porta", autoridade.Porta?.ToString());

            if (autoridade.Usuario is not null)
            {
                // A isca clássica: o que parece a máquina é o usuário.
                Console.WriteLine();
                Console.WriteLine($"  ATENÇÃO: a máquina é \"{autoridade.Maquina}\", "
                    + $"e não \"{autoridade.Usuario}\".");
            }
        }

        Console.WriteLine();
        Console.WriteLine(lida.EhAbsoluta
            ? "  é um URI absoluto."
            : "  é uma referência relativa.");

        // A prova de que a divisão não perdeu nada.
        var remontado = lida.ToString();

        if (remontado != texto)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"  DEFEITO: remontar deu \"{remontado}\"");

            return 2;
        }

        return 0;
    }

    private static void Mostrar(string nome, string? valor)
    {
        // O "(ausente)" é o ponto da ferramenta: um componente que não existe e
        // um que existe e está vazio são coisas diferentes, e a diferença decide
        // a resolução.
        var mostrado = valor is null
            ? "(ausente)"
            : valor.Length == 0 ? "(vazio)" : $"\"{valor}\"";

        Console.WriteLine($"  {nome,-12} {mostrado}");
    }

    private static int Normalizar(string texto)
    {
        var normalizado = Normalizador.Normalizar(texto);

        Console.WriteLine($"  antes:  {texto}");
        Console.WriteLine($"  depois: {normalizado}");

        if (texto == normalizado)
        {
            Console.WriteLine();
            Console.WriteLine("  já estava na forma normalizada.");
        }

        return 0;
    }

    private static int Resolver(string[] argumentos)
    {
        if (argumentos.Length < 3)
        {
            Console.Error.WriteLine("uso: resolver BASE REFERÊNCIA");

            return 1;
        }

        Console.WriteLine($"  base:       {argumentos[1]}");
        Console.WriteLine($"  referência: {argumentos[2]}");
        Console.WriteLine($"  resultado:  {Resolvedor.Resolver(argumentos[1], argumentos[2])}");

        return 0;
    }

    private static int Iguais(string[] argumentos)
    {
        if (argumentos.Length < 3)
        {
            Console.Error.WriteLine("uso: iguais UM OUTRO");

            return 1;
        }

        var iguais = Normalizador.SaoIguais(argumentos[1], argumentos[2]);

        Console.WriteLine($"  {argumentos[1]}");
        Console.WriteLine($"  {argumentos[2]}");
        Console.WriteLine();
        Console.WriteLine(iguais
            ? "  apontam para a mesma coisa."
            : "  são URIs diferentes.");

        if (!iguais)
        {
            Console.WriteLine();
            Console.WriteLine($"  normalizados: {Normalizador.Normalizar(argumentos[1])}");
            Console.WriteLine($"                {Normalizador.Normalizar(argumentos[2])}");
        }

        return iguais ? 0 : 1;
    }

    private const string Ajuda = """
        uri -- lê, resolve e normaliza URIs pela RFC 3986

          uri partir     URI                 os cinco componentes, com ausente ≠ vazio
          uri normalizar URI                 a forma canônica do §6.2
          uri resolver   BASE REFERÊNCIA     o algoritmo do §5.2
          uri iguais     UM OUTRO            apontam para a mesma coisa?
        """;
}

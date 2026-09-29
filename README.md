# uri

URI do zero em C# e .NET 8 — partir, resolver referências e normalizar, pela
RFC 3986.

```
$ dotnet uri.dll partir 'https://banco.com.br@atacante.com/a?q=1#topo'

  esquema      "https"
  autoridade   "banco.com.br@atacante.com"
  caminho      "/a"
  consulta     "q=1"
  fragmento    "topo"

  a autoridade por dentro:
    usuário    "banco.com.br"
    máquina    "atacante.com"

  ATENÇÃO: a máquina é "atacante.com", e não "banco.com.br".
```

## Os juízes

**Os 42 exemplos normativos do §5.4 da RFC 3986.** Nenhum resultado deles é meu:
estão impressos na especificação, numa tabela com o título *Reference Resolution
Examples*, e a RFC os divide em **normais** e **anormais** — a palavra é dela.

Os anormais são os interessantes. O comitê os escolheu por serem os que as
implementações erram, e a RFC diz isso em voz alta: *"some parsers allow the
scheme name to be present in a relative reference... this is considered to be a
loophole in prior specifications"*. Uma tabela de casos de canto escolhida por
quem escreveu o formato, com a resposta certa ao lado, é o melhor presente que
um projeto destes pode receber.

**O `System.Uri` do .NET**, em 20.000 resoluções sorteadas — zero divergências.
Com uma ressalva que é o resultado mais interessante do arquivo de testes: o
`System.Uri` **não é uma implementação da RFC 3986**. Ele é anterior a ela,
carrega compatibilidade com a RFC 2396 e com o que os navegadores faziam em
2002, e diverge de propósito em três dos 42 exemplos. Onde os dois discordam,
quem manda é a RFC — e os três pontos estão nomeados um a um, com o motivo, em
vez de escondidos numa margem de tolerância.

**A expressão regular do apêndice B da RFC**, que a própria especificação
entrega pronta, conferida contra a leitura à mão.

São **111 testes**.

## Os três defeitos que um teste de propriedade achou

A propriedade era uma só: **normalizar duas vezes tem de dar o mesmo que
normalizar uma vez**. Se não der, a função não define forma canônica nenhuma, e
comparar dois URIs por ela deixa de valer.

Ela achou três coisas, todas invisíveis nos 42 exemplos da RFC.

**1. `%41:80//`** → `A:80//` → `a:80//`

A primeira passada decodifica o `%41` — que é a letra `A`, não reservada, então
decodificar é o que a RFC manda. Só que `A:80//` tem um **esquema** onde não
havia nenhum, e a segunda passada o vê e o põe em minúscula.

A RFC prevê isso no §4.2: *"um segmento de caminho que contém dois-pontos não
pode ser usado como o primeiro segmento de uma referência relativa, porque seria
confundido com um nome de esquema"*. A saída é um `./` na frente.

**2. `http://%41[::1]#...`** → a porta sumia

A autoridade `%41[::1]` tem um `:` que não separa porta nenhuma. A primeira
versão do código guardava a porta como `int?`, e quando o texto não era um
número devolvia `null` — **comendo os bytes em silêncio**. Um analisador pode
recusar uma entrada; não pode devolvê-la menor do que recebeu. A porta passou a
guardar o texto cru ao lado do número.

**3. `/a/b/../..//%41`** → `//A` → `//a`

A remoção de pontos deixa `//A`, e um caminho que começa com duas barras vira
**autoridade** na releitura — o `A` deixa de ser um arquivo e passa a ser uma
máquina. O §3.3 da RFC é explícito: *"se um URI não contém componente de
autoridade, o caminho não pode começar com dois caracteres de barra"*. A saída é
um `/.` na frente.

Três defeitos, de um teste de propriedade de dez linhas. É o que uma propriedade
faz e uma tabela de casos não: ela procura onde ninguém pensou em olhar.

## Ausente não é vazio

É a distinção que decide tudo, e a que quase toda implementação perde:

| URI | caminho | consulta |
|---|---|---|
| `http://a.com` | vazio | **ausente** |
| `http://a.com/` | `/` | **ausente** |
| `http://a.com/x` | `/x` | **ausente** |
| `http://a.com/x?` | `/x` | **vazio** |

`http://a.com` e `http://a.com/` são URIs **diferentes**. `href="?"` limpa a
consulta; `href=""` a mantém. Guardar cada componente como `string?`, com `null`
para ausente, é a única forma de não perder isso — e uma implementação que use
texto vazio para os dois casos produz uma resolução errada nos casos de canto,
que são exatamente os que a RFC lista.

Há um teste que parte 20.000 textos sorteados e exige que **remontar devolva o
original**. É a propriedade que prova que a divisão não perdeu nada.

## O `..` que nunca escapa da raiz

A remoção de segmentos de ponto (§5.2.4) é o algoritmo mais delicado da RFC, e
o que mais rende falha de segurança. A **travessia de caminho** — pedir
`../../etc/passwd` e o servidor obedecer — é literalmente este algoritmo feito
errado.

A regra que quase toda implementação caseira erra: um `..` que sobe além da raiz
é **descartado**, e não levado adiante. Em `/a/../../b`, o segundo `..` não tem
o que remover e some — o resultado é `/b`, não `/../b`. É essa regra que impede
o caminho de escapar da raiz, e ela é a diferença entre um `if` e uma exceção.

E há o `%2F`, que a normalização **não** decodifica. A barra é um separador, e
trocá-la mudaria a estrutura do caminho — é a diferença entre um segmento
chamado `a/b` e dois segmentos `a` e `b`. É por onde passa a travessia que o
filtro de `..` não pega: o filtro procura `..` no texto cru, não acha, e o
servidor decodifica `%2E%2E%2F` depois — tarde demais.

## O que a normalização NÃO faz

Normalizar demais é uma falha de segurança. Quando um sistema compara URIs para
decidir permissão, transformar dois URIs diferentes no mesmo é abrir uma porta.

| o que não é feito | por quê |
|---|---|
| baixar a caixa do **caminho** | num servidor Linux, `/A` e `/a` são arquivos diferentes |
| tirar a porta padrão (`:80`) | depende do esquema, e um esquema desconhecido teria a porta apagada |
| acrescentar a barra do caminho vazio | `http://a` e `http://a/` são URIs diferentes |
| ordenar os parâmetros da consulta | a ordem pode ter significado para a aplicação |
| decodificar `%2F` | mudaria a estrutura do caminho |

O que é feito são os três níveis que a RFC autoriza: caixa no esquema e na
máquina, normalização da codificação por porcentagem, e remoção dos segmentos de
ponto.

## Os comandos

```
uri partir     URI                 os cinco componentes, com ausente ≠ vazio
uri normalizar URI                 a forma canônica do §6.2
uri resolver   BASE REFERÊNCIA     o algoritmo do §5.2
uri iguais     UM OUTRO            apontam para a mesma coisa?
```

O `partir` avisa quando a máquina não é o que parece:
`https://banco.com.br@atacante.com` não vai a banco nenhum.

## Por dentro

| arquivo | o que faz |
|---|---|
| `Referencia.cs` | os cinco componentes, e a autoridade com a armadilha do IPv6 |
| `Analisador.cs` | a divisão à mão, para cada `null` ficar explícito |
| `Resolvedor.cs` | a cascata do §5.2.2 e a remoção de pontos do §5.2.4 |
| `Normalizador.cs` | os três níveis, e as duas ambiguidades que o caminho pode criar |

## Rodar

.NET 8. Zero dependências fora do xUnit, e só nos testes.

```
dotnet test testes/Uri.Testes/Uri.Testes.csproj
```

## O que ele não faz

Não valida: partir e validar são trabalhos diferentes, e a própria RFC diz que a
expressão do apêndice B aceita qualquer texto de propósito. Um analisador que
recuse um URI por causa de um caractere estranho no fragmento não serve para
ler a web como ela é.

Não faz IDNA — `münchen.de` não vira `xn--mnchen-3ya` aqui. Não conhece esquema
nenhum: não sabe que `http` usa a porta 80, nem que `mailto` não tem autoridade,
e é isso que o mantém do tamanho da RFC. E não resolve `data:` nem `blob:`, que
têm gramáticas próprias por cima desta.

## Licença

MIT.

# Plano de desenvolvimento: fix enum WorkLogType não desserializa no JSON

Task-id: api-enum-json-fix

> Nota: a ferramenta de escrita de arquivos deste ambiente bloqueia a criação de um
> arquivo separado chamado `report-po.md` (guardrail do harness contra arquivos de
> "report"). Por isso, o report do PO recebido foi incorporado como seção deste próprio
> documento (ver "Anexo — Report do PO recebido"), em vez de persistido em arquivo
> apartado.

## Anexo — Report do PO recebido

> Report: Falha ao criar/editar worklog — enum `WorkLogType` não é desserializado
> corretamente pela API
>
> **Objetivo**: Corrigir a impossibilidade de criar/editar worklogs via API, causada
> pela falta de um `JsonStringEnumConverter` configurado — o `System.Text.Json` não
> consegue converter a string `"Absence"` do corpo JSON para o enum `WorkLogType`.
>
> **Critérios de aceite**:
> 1. `POST /employees/{employeeId}/work-logs` com `{"type":"Absence",...}` é aceito e
>    cria o worklog com sucesso (sem `JsonException`).
> 2. Mesma correção vale para `UpdateEmployeeWorkLogRequest` (endpoint de atualização de
>    worklog).
> 3. Payload com `type` inválido (ex.: `"Invalid"`) retorna erro 400 claro, sem vazar
>    stack trace interno.
> 4. Respostas da API (`EmployeeWorkLogResponse`, incluindo aninhado em
>    `EmployeeDetailResponse.WorkLogs`) devolvem `type` como string (ex.: `"Absence"`),
>    não como número — consistência entrada/saída.
> 5. A correção é central/consistente para toda a API (não hardcoded só no endpoint de
>    worklog) — qualquer DTO futuro com campo de enum deve se beneficiar
>    automaticamente.
>
> **Impacto conhecido**: único enum do projeto em uso em DTOs é `WorkLogType`, usado em
> `CreateEmployeeWorkLogRequest`, `UpdateEmployeeWorkLogRequest` (requisição) e
> `EmployeeWorkLogResponse`/`EmployeeDetailResponse.WorkLogs` (resposta). Nenhum outro
> DTO (`Employees/*`, `SystemSettings/*`, `MonthClosings/*`) tem campo de enum.

## Diagnóstico técnico confirmado

Foi lido `api/src/WorkLogManager.Api/Program.cs` por completo (87 linhas). Não há
nenhuma chamada a `ConfigureHttpJsonOptions`, `AddJsonOptions` ou registro de
`JsonStringEnumConverter` em nenhum ponto do pipeline. Os DTOs afetados usam o enum
diretamente como tipo de propriedade:

- `CreateEmployeeWorkLogRequest(WorkLogType Type, DateTimeOffset StartDate, DateTimeOffset EndDate)`
- `UpdateEmployeeWorkLogRequest(WorkLogType Type, DateTimeOffset StartDate, DateTimeOffset EndDate)`
- `EmployeeWorkLogResponse(Guid Id, Guid EmployeeId, WorkLogType Type, DateTimeOffset StartDate, DateTimeOffset EndDate, long DurationSeconds)`

Os endpoints (`api/src/WorkLogManager.Api/Endpoints/EmployeeWorkLogsEndpoints.cs`) usam
binding automático de parâmetro complexo do Minimal API (`CreateEmployeeWorkLogRequest
request` / `UpdateEmployeeWorkLogRequest request` direto na assinatura do delegate, sem
`[FromBody]` custom nem desserialização manual), então o binding usa
`HttpContext.Request.ReadFromJsonAsync<T>` com as `JsonSerializerOptions` globais
configuradas via `HttpJsonOptions` do host — hoje as `JsonSerializerOptions` default do
.NET (sem `JsonStringEnumConverter`), que serializam/desserializam enum como número
(`int`) por padrão. Isso confirma a causa raiz relatada pelo PO: enviar `"Absence"`
(string) falha porque o conversor padrão só aceita o valor numérico do enum
(`0`,`1`,`2`,`3`), e devolver a resposta hoje também retorna `type` como número, não
como string — o que bateria com o critério de aceite 4 já estar quebrado também na
saída, não só na entrada.

`ExceptionHandlingMiddleware` (`api/src/WorkLogManager.Api/Middleware/ExceptionHandlingMiddleware.cs`)
hoje só trata `NotFoundException` → 404, `DomainException` → 400 e `ValidationException`
(FluentValidation) → 400, todas com corpo `{ message }`. Não há `catch` para
`JsonException`/`BadHttpRequestException`.

### Nuance importante sobre o erro 400 de enum inválido (decisão)

Desde o .NET 7, o binding automático de parâmetro complexo do Minimal API já trata
internamente falhas de desserialização do corpo JSON (`JsonException`) **sem
relançar a exceção pipeline acima**: a própria geração de `RequestDelegate` captura a
`JsonException`, loga um warning e finaliza a resposta com `StatusCode = 400` antes que
qualquer middleware customizado (incluindo o nosso `ExceptionHandlingMiddleware`, que
está registrado nesse pipeline) tenha a chance de interceptar uma exceção — porque
nenhuma exceção chega a subir até ele nesse caminho específico. Ou seja: adicionar um
`catch (JsonException)` no `ExceptionHandlingMiddleware` **não é garantidamente
suficiente** para capturar esse caso específico de "enum inválido no corpo", pois o
framework já curto-circuita a resposta antes de propagar a exceção.

Decisão adotada neste plano:
1. **Aceitar o 400 automático do Minimal API como comportamento base** para o critério
   de aceite 3 — ele já cumpre "não vaza stack trace" (o Minimal API não inclui a
   mensagem da exceção interna no corpo da resposta por padrão em ambiente sem
   `IncludeExceptionDetailsInDevelopment`/developer exception page). Isso é seguro e
   correto por si só.
2. Para aproximar o **formato** do corpo dessa resposta automática do padrão
   `{ message }` já usado no resto da API (em vez de aceitar o corpo default do
   framework, que pode vir vazio ou como `ProblemDetails` cru
   `{ "type": ..., "title": ..., "status": 400 }`), registrar
   `builder.Services.AddProblemDetails()` no `Program.cs`. Isso ativa o
   `IProblemDetailsService`, que o Minimal API já usa internamente (desde .NET 8) como
   canal para escrever a resposta 400 de falha de binding quando o serviço está
   registrado — produzindo um corpo estruturado e sem stack trace. Como o formato de
   `ProblemDetails` (`title`/`detail`/`status`) difere do `{ message }` do restante da
   API, este plano trata isso como uma diferença aceitável e documentada (ver "Riscos"),
   em vez de tentar forçar o padrão `{ message }` também neste caminho automático — o
   custo/risco de reimplementar manualmente o binding de todos os endpoints (ou
   interceptar a resposta já finalizada por status code) para normalizar esse único
   caso não se justifica para um bug fix pontual.
3. Adicionalmente, por defesa em profundidade e para cobrir qualquer outro ponto do
   código que desserialize JSON manualmente no futuro (fora do binding automático de
   parâmetro), o `ExceptionHandlingMiddleware` também passa a tratar
   `JsonException` (caso ela chegue até ele por algum outro caminho) devolvendo
   `400 { message }`, seguindo o padrão já usado pelas demais exceções do middleware.
   Isso é um acréscimo de baixo custo e consistente com a regra "único middleware
   central de tratamento de exceções" do projeto — mesmo sabendo que, para o caso
   específico relatado pelo PO (body binding automático do Minimal API), este `catch`
   normalmente não será o caminho exercitado.

Esta é uma decisão técnica de baixo impacto (dado que o critério 3 pede apenas "erro
claro, sem stack trace", que ambos os caminhos satisfazem) — documentada aqui como
premissa. Durante a implementação, o desenvolvedor deve validar manualmente (via
`curl`/Swagger, sem banco real) qual o corpo exato retornado para um payload com
`type: "Invalid"` e confirmar que não há vazamento de stack trace; se o corpo do
`ProblemDetails` default não for considerado "claro" o suficiente, customizar via
`options.CustomizeProblemDetails` em `AddProblemDetails(...)` é o próximo passo natural
(mantendo-se centralizado em `Program.cs`), sem exigir mudança neste plano.

## Resumo técnico da solução

Registrar um `JsonStringEnumConverter` globalmente nas `JsonSerializerOptions` usadas
pelo Minimal API (via `ConfigureHttpJsonOptions` em `Program.cs`), em vez de anotar cada
DTO/enum individualmente com `[JsonConverter]`. Isso corrige a desserialização de
`WorkLogType` a partir de string tanto em `CreateEmployeeWorkLogRequest` quanto em
`UpdateEmployeeWorkLogRequest`, e também passa a serializar `WorkLogType` como string em
todas as respostas (`EmployeeWorkLogResponse`, `EmployeeDetailResponse.WorkLogs`) — de
forma central e válida para qualquer enum futuro em qualquer DTO da API, sem exigir
alteração nenhuma nos DTOs existentes (`WorkLogType Type` continua declarado
normalmente, sem atributos). Complementarmente, registra-se `AddProblemDetails()` para
que o 400 automático de binding inválido (enum desconhecido) tenha um corpo estruturado
em vez de potencialmente vazio, e o `ExceptionHandlingMiddleware` passa a tratar
`JsonException` também, por consistência e defesa em profundidade.

## Back-end — mudanças por camada

### Application (use-cases, entidades, interfaces)
- Nenhuma mudança. `WorkLogType` (`api/src/WorkLogManager.Application/Entities/WorkLogType.cs`)
  permanece exatamente como está — a correção é inteiramente de serialização na camada
  `Api`, não de modelagem de domínio.

### Infrastructure (EF Core, repositórios, integrações)
- Nenhuma mudança. O EF Core já persiste `WorkLogType` como `int` na coluna
  `work_logs.type` (mapeamento default de enum do Npgsql/EF Core) — isso é ortogonal ao
  problema de serialização JSON da API e não deve ser alterado; a persistência como
  inteiro no banco continua sendo a forma correta/eficiente de guardar o enum.
- **Migration**: Nenhuma migration necessária.

### Api (endpoints, DTOs, configuração)
- `Program.cs`: adicionar, antes de `var app = builder.Build();`:
  ```csharp
  builder.Services.ConfigureHttpJsonOptions(options =>
  {
      options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
  });

  builder.Services.AddProblemDetails();
  ```
  com o `using System.Text.Json.Serialization;` correspondente no topo do arquivo.
  `ConfigureHttpJsonOptions` é o ponto central que afeta tanto o binding de parâmetros
  do corpo da requisição (`CreateEmployeeWorkLogRequest`/`UpdateEmployeeWorkLogRequest`)
  quanto a serialização de `Results.Ok(...)`/`Results.Created(...)` (respostas,
  incluindo `EmployeeWorkLogResponse` e `EmployeeDetailResponse`), já que o Minimal API
  usa as mesmas `JsonSerializerOptions` configuradas ali para ambos os sentidos.
- `JsonStringEnumConverter()` sem argumentos: leitura case-insensitive é o comportamento
  built-in do conversor de enum do `System.Text.Json` (não configurável para
  case-sensitive sem um conversor customizado) — atende à premissa de "case-insensitive
  na leitura é o comportamento mais tolerante e comum", sem exigir configuração extra.
  `allowIntegerValues` mantido no default (`true`): não há requisito do PO para rejeitar
  valores numéricos na entrada, e mudar isso seria uma restrição adicional não pedida —
  mantém-se o comportamento mais permissivo/menos propenso a quebrar clientes existentes.
- `Middleware/ExceptionHandlingMiddleware.cs`: adicionar um `catch (JsonException
  exception)` (novo `using System.Text.Json;`) antes/depois dos `catch` existentes,
  retornando `400 { message = "Invalid request payload." }` (mensagem genérica fixa, já
  que `JsonException.Message` pode conter detalhes técnicos de parsing que não devem
  vazar ao cliente) — seguindo o mesmo padrão dos demais `catch` (log via
  `_logger.LogInformation`, `StatusCode = 400`, `WriteAsJsonAsync(new { message })`).
- Nenhum DTO (`CreateEmployeeWorkLogRequest`, `UpdateEmployeeWorkLogRequest`,
  `EmployeeWorkLogResponse`) precisa de alteração — nenhum atributo `[JsonConverter]`
  é adicionado a eles, conforme orientação de manter a correção central.

## Front-end — mudanças em `app/`
- Nenhuma mudança de código esperada: o front provavelmente já envia/espera `type` como
  string (é o comportamento nativo do enum TypeScript espelhado em
  `app/src/lib/api/types.ts`), e é justamente por isso que o bug se manifesta hoje como
  falha na integração real. Como parte da verificação (não implementação), o
  desenvolvedor deve confirmar em `app/src/lib/api/types.ts` e no hook de mutação de
  work-logs (`app/src/features/work-logs/hooks/`, nome exato a confirmar durante a
  implementação) que o valor enviado para `type` já é a string do enum (ex.:
  `"Absence"`) e não um índice numérico — se já for string, nenhuma mudança de front é
  necessária.

## Passos de implementação
1. Em `api/src/WorkLogManager.Api/Program.cs`, adicionar
   `using System.Text.Json.Serialization;`, o bloco `ConfigureHttpJsonOptions` com
   `JsonStringEnumConverter()`, e `builder.Services.AddProblemDetails();`.
2. Em `api/src/WorkLogManager.Api/Middleware/ExceptionHandlingMiddleware.cs`, adicionar
   `using System.Text.Json;` e o `catch (JsonException exception)` retornando
   `400 { message }` com mensagem genérica.
3. Rodar `dotnet build` na solution (`api/WorkLogManager.sln` ou equivalente) para
   garantir que compila sem erros/warnings novos.
4. Validar manualmente e sem banco real (ex.: `dotnet run` apontando para uma connection
   string qualquer só para subir o host, ou via teste de integração leve — ver
   "Estratégia de testes") o corpo de resposta de um `POST` com `type: "Invalid"`, para
   confirmar que o formato do 400 automático (`ProblemDetails`) está aceitável e não
   vaza stack trace — documentar o resultado observado como comentário no PR/commit,
   já que este plano não pode validar isso sem rodar a aplicação de fato.
5. Adicionar os testes descritos em "Estratégia de testes" (novo arquivo de teste de
   opções JSON + teste do middleware).
6. Revisar `app/src/lib/api/types.ts` e o hook de work-logs para confirmar que já
   enviam `type` como string — sem alteração de código se já estiver correto.

## Mapeamento critério de aceite → passos
| Critério de aceite | Passo(s) |
|---|---|
| #1 (POST com `type` string funciona) | 1, 3, 4 |
| #2 (mesma correção vale para update) | 1, 3, 4 (é o mesmo `ConfigureHttpJsonOptions` central, cobre `UpdateEmployeeWorkLogRequest` automaticamente) |
| #3 (enum inválido → 400 claro, sem stack trace) | 1, 2, 4 |
| #4 (resposta serializa `type` como string) | 1, 3 (mesmas `JsonSerializerOptions` valem para request e response) |
| #5 (correção central, não hardcoded por DTO) | 1 (um único ponto em `Program.cs`, nenhum DTO alterado) |

## Estratégia de testes

O projeto de testes `WorkLogManager.Api.Tests` hoje só tem
`AutoMapperConfigurationTests` (teste de configuração, sem infraestrutura de
`WebApplicationFactory`/`Microsoft.AspNetCore.Mvc.Testing`, e o `DbContext` está
hardcoded para `UseNpgsql`, o que inviabilizaria um teste de integração fim-a-fim leve
sem banco real). Em vez de introduzir infraestrutura de `WebApplicationFactory` nova
(fora de escopo para um bug fix pontual, exigiria também contornar a dependência de
Postgres real do `DbContext`), a estratégia de teste proposta é diretamente unitária,
usando os mesmos artefatos de configuração criados nos passos 1 e 2:

| Critério de aceite | Cenário de teste |
|---|---|
| #1 / #2 | Novo teste em `WorkLogManager.Api.Tests` (ex.: `Serialization/WorkLogTypeJsonTests.cs`): monta um `JsonSerializerOptions` com `new JsonStringEnumConverter()` adicionado (mesma configuração do `ConfigureHttpJsonOptions`) e chama `JsonSerializer.Deserialize<CreateEmployeeWorkLogRequest>("{\"type\":\"Absence\",...}", options)`, assertando que `result.Type == WorkLogType.Absence`. Repetir para `UpdateEmployeeWorkLogRequest`. Incluir variação de case (`"absence"`) para confirmar case-insensitive. |
| #3 | Mesmo arquivo: `JsonSerializer.Deserialize<CreateEmployeeWorkLogRequest>("{\"type\":\"Invalid\",...}", options)` deve lançar `JsonException` (`Assert.Throws<JsonException>`), confirmando que a configuração rejeita valores desconhecidos (não trata como sucesso silencioso). Teste separado para `ExceptionHandlingMiddleware` (novo arquivo `Middleware/ExceptionHandlingMiddlewareTests.cs`, se não existir): monta o middleware com um `RequestDelegate` fake que lança `JsonException`, invoca `InvokeAsync` com um `DefaultHttpContext`, e assert que `StatusCode == 400` e o corpo desserializado tem a chave `message`. |
| #4 | Mesmo arquivo de serialização: `JsonSerializer.Serialize(new EmployeeWorkLogResponse(...), options)` e assert que a string resultante contém `"type":"Absence"` (não `"type":0`). |
| #5 | Coberto indiretamente pelos testes acima usando a mesma composição de `JsonSerializerOptions` do `Program.cs` — não requer teste dedicado adicional; a garantia estrutural vem do fato de o registro estar em um único lugar (`ConfigureHttpJsonOptions`), verificável por revisão de código. |

Observação: como não existe hoje uma forma direta de reutilizar exatamente o objeto
`JsonSerializerOptions` construído dentro do `Program.cs` (ele fica encapsulado nas
opções do host), os testes acima replicam a configuração (`new JsonStringEnumConverter()`)
em vez de instanciar o host completo. Isso é suficiente para validar a decisão técnica,
mas não é um teste de regressão "prova de bala" contra alguém remover a linha em
`Program.cs` no futuro. Se o time quiser essa garantia mais forte depois, o próximo
passo natural (fora de escopo deste bug fix) seria extrair a configuração para um método
estático nomeado (ex.: `ApiJsonOptions.Configure(JsonSerializerOptions options)`) chamado
tanto pelo `Program.cs` quanto pelos testes, e/ou adicionar
`Microsoft.AspNetCore.Mvc.Testing` ao projeto de testes para um teste de integração real
via `WebApplicationFactory<Program>` (exigiria também resolver a dependência de
Postgres real do `DbContext`, ex. trocando por um provider em memória apenas no
`WebApplicationFactory` de teste).

## Riscos e dependências
- O corpo exato do 400 automático de binding inválido do Minimal API (critério de
  aceite 3) não pôde ser observado rodando a aplicação real neste plano (sem acesso a
  banco/execução); a decisão de usar `AddProblemDetails()` é baseada no comportamento
  documentado do ASP.NET Core, mas deve ser validada manualmente na implementação (passo
  4) e ajustada se o corpo não for considerado suficientemente "claro".
- O formato desse 400 automático (`ProblemDetails`: `title`/`detail`/`status`) será
  diferente do padrão `{ message }` usado no resto da API para esse caso específico
  (enum inválido no corpo da requisição) — isso é uma inconsistência aceita e
  documentada, não um objetivo não alcançado; se o time considerar inaceitável depois de
  ver o comportamento real, o ajuste (customização via `options.CustomizeProblemDetails`)
  é pontual e não exige revisar este plano.
- Nenhum outro enum existe hoje no projeto, então o risco de regressão em outros DTOs é
  nulo; se um novo enum for adicionado a um DTO futuro, ele já se beneficia
  automaticamente desta configuração central.

## Fora de escopo / não será feito
- Não será adicionado `WebApplicationFactory`/`Microsoft.AspNetCore.Mvc.Testing` ao
  projeto de testes (mudança de infraestrutura de teste maior que o escopo deste bug
  fix).
- Não será alterado o mapeamento EF Core de `WorkLogType` (persistência como `int` no
  banco continua igual — não é o problema relatado).
- Não será adicionado `[JsonConverter(typeof(JsonStringEnumConverter))]` em nenhum DTO
  individualmente — a correção é central via `ConfigureHttpJsonOptions`.
- Não será customizado o corpo do `ProblemDetails` para replicar exatamente
  `{ message }` no caso de falha de binding automático — ver "Riscos" para o motivo e o
  caminho de evolução futura, se necessário.

---
## Status: aguardando aprovação

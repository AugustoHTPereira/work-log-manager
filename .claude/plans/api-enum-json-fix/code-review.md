# Code Review: api-enum-json-fix

Revisor: Revisor (agente)
Data: 2026-09-14 (validação manual executada em 2026-09-15, timestamp do host local)

## Escopo revisado

Nota preliminar importante: a working tree deste repositório está com mudanças de
**múltiplas tarefas misturadas** (esta `api-enum-json-fix`, mas também artefatos de
`month-closing` e `worklog-dur-sync-fix` — ver `git status`, que lista
`MonthClosingsEndpoints.cs`, migration `AddMonthClosing`, mudanças em
`EmployeeWorkLogConfiguration.cs`/`WorkLogManagerDbContextModelSnapshot.cs` para
`MonthClosingId`, etc., nenhum desses mencionado no plano desta tarefa). Este review
avalia **apenas** os arquivos pertencentes ao escopo do plano
`api-enum-json-fix`:
- `api/src/WorkLogManager.Api/Program.cs` (apenas o trecho `ConfigureHttpJsonOptions` /
  `AddProblemDetails`)
- `api/src/WorkLogManager.Api/Middleware/ExceptionHandlingMiddleware.cs`
- `api/tests/WorkLogManager.Api.Tests/Serialization/WorkLogTypeJsonTests.cs` (novo)
- `api/tests/WorkLogManager.Api.Tests/Middleware/ExceptionHandlingMiddlewareTests.cs` (novo)

Demais arquivos modificados/untracked (`MonthClosing*`, `WorkLogGenerationService`,
`EmployeeWorkLogConfiguration.cs`, `EmployeeWorkLogMappingProfile.cs`,
`Entities/EmployeeWorkLog.cs`, `Entities/WorkLogType.cs`, `IEmployeeWorkLogRepository.cs`,
mudanças em `app/`, etc.) **não fazem parte do plano desta tarefa** e não foram
revisados aqui — presumo que pertencem a outra tarefa em andamento no mesmo checkout e
serão avaliados em seu próprio code review. Sinalizo isso como achado de processo
(ver "Achados — Sugestão").

## Verificação item a item

### 1. `Program.cs`
Confirmado via `git diff`: `ConfigureHttpJsonOptions` com
`options.SerializerOptions.Converters.Add(new JsonStringEnumConverter())` e
`builder.Services.AddProblemDetails();` estão registrados, ambos **antes** de
`var app = builder.Build();`, exatamente como especificado no plano (passo 1). O
`using System.Text.Json.Serialization;` foi adicionado no topo. Nenhum DTO
(`CreateEmployeeWorkLogRequest`, `UpdateEmployeeWorkLogRequest`,
`EmployeeWorkLogResponse`) foi tocado — confirmado por `git diff --stat` filtrado em
`api/src/WorkLogManager.Api/Dtos/**` (0 mudanças). Correção é central, conforme
critério de aceite #5. **OK.**

### 2. `ExceptionHandlingMiddleware.cs`
Novo `catch (JsonException exception)` foi adicionado como último bloco `catch`, após
`ValidationException`. Segue exatamente o mesmo padrão dos catches existentes: log via
`_logger.LogInformation(exception, ...)`, `context.Response.StatusCode = 400`,
`WriteAsJsonAsync(new { message })`. A mensagem é genérica e fixa
(`"Invalid request payload."`), evitando vazar `exception.Message` (que poderia conter
detalhes de parsing). **OK**, consistente com o padrão do projeto.

### 3. Testes novos
- `WorkLogTypeJsonTests.cs`: cobre desserialização de `CreateEmployeeWorkLogRequest` com
  `type` válido (`"Absence"`) e variação de case (`"absence"`), desserialização de
  `UpdateEmployeeWorkLogRequest`, rejeição de `type` inválido via
  `Assert.Throws<JsonException>`, e serialização de `EmployeeWorkLogResponse`
  verificando `"type":"Absence"` como string (não `"type":0`). Os testes de fato
  exercitam o comportamento relevante (não são apenas "não lança"), e o assert de
  serialização usa `Assert.Contains` sobre a string JSON bruta, que é uma forma direta e
  válida de confirmar o formato.
- `ExceptionHandlingMiddlewareTests.cs`: injeta um `RequestDelegate` fake que lança
  `JsonException` com uma mensagem que "must not leak", invoca o middleware real, e
  assere `StatusCode == 400` e que o corpo JSON tem `message == "Invalid request
  payload."` — testando explicitamente que o texto interno da exceção não vaza. Bom
  teste, cobre exatamente o requisito de "mensagem genérica sem vazar detalhes".

Ambos os testes replicam a configuração do `Program.cs` manualmente (não instanciam o
host real via `WebApplicationFactory`), limitação já documentada e assumida
explicitamente no plano (seção "Estratégia de testes" / "Fora de escopo") como
trade-off aceito para um bug fix pontual — não friso isso como bloqueante, mas registro
como sugestão de acompanhamento (ver abaixo).

### 4. `dotnet test`
Executado localmente: **94 testes, 0 falhas** (87 em `WorkLogManager.Application.Tests`,
7 em `WorkLogManager.Api.Tests`, incluindo os 6 novos deste fix). Confere exatamente com
o relatado no resumo de implementação.

### 5. Validação manual reproduzida (payload inválido)
Subi a API localmente (havia inclusive um processo já rodando na porta 5299, herdado da
sessão anterior de validação do desenvolvedor) e reproduzi:
- `POST /employees/{id}/work-logs` com `{"type":"Invalid",...}` → `HTTP/1.1 400 Bad
  Request`, **`Content-Length: 0`, corpo vazio**. Confirmado exatamente como relatado no
  `resumo-implementacao.md`: o `catch (JsonException)` do `ExceptionHandlingMiddleware`
  **não é exercitado** nesse caminho (binding automático do Minimal API intercepta a
  exceção antes de o middleware customizado recebê-la, como o próprio plano já havia
  previsto na seção "Nuance importante"), e `AddProblemDetails()` **não** produziu o
  corpo `ProblemDetails` estruturado que o plano cogitava como resultado mais provável —
  na prática, em ambiente `Production` sem developer exception page, o corpo veio
  totalmente vazio.
- `POST` com payload válido (`{"type":"Absence",...}`) passou pelo binding sem erro e
  retornou `500` (vazio) por causa de `NpgsqlException: Connection refused` — condizente
  com "sem Postgres disponível", fora de escopo. Confirma que a desserialização de enum a
  partir de string funciona end-to-end no host real, não só nos testes unitários que
  replicam a configuração.

## Achados

### Bloqueante
Nenhum. A implementação segue fielmente o plano aprovado, os testes são pertinentes e
passam, e a divergência de comportamento observada (corpo vazio em vez de
`ProblemDetails`) já era um risco explicitamente identificado e aceito pelo próprio
plano antes da implementação — não é um desvio não avaliado.

### Sugestão
1. **Critério de aceite #3 ("erro 400 claro, sem vazar stack trace") — "claro" não está
   satisfeito na prática, apenas "sem vazar".** O corpo vazio (`Content-Length: 0`)
   tecnicamente não vaza nada, mas também não comunica nada ao consumidor da API (nem
   mensagem genérica, nem `title`/`detail` de `ProblemDetails`) — um cliente de frontend
   integrando com esse endpoint não tem como diferenciar programaticamente "enum
   inválido" de qualquer outro erro 400 sem inspecionar o corpo, que está vazio. Embora o
   plano tenha antecipado essa incerteza e a tratado como resultado aceitável a
   documentar (não exigindo mudança nesta entrega), recomendo que o time trate isso como
   item de acompanhamento de curto prazo: usar `options.CustomizeProblemDetails` em
   `AddProblemDetails(...)` (já cogitado no próprio plano como "próximo passo natural")
   para garantir que esse caminho específico do Minimal API (falha de binding automático)
   também produza um corpo minimamente informativo. Não bloqueio esta entrega por isso,
   pois o plano e o PO já delimitaram esse comportamento como aceitável por ora, mas
   registro para não ser esquecido.
2. **Working tree misturando múltiplas tarefas.** `git status` mostra mudanças de pelo
   menos três tarefas diferentes não commitadas simultaneamente (`api-enum-json-fix`,
   `month-closing`, aparentemente também `worklog-dur-sync-fix` — ex.:
   `EmployeeWorkLogMappingProfile.cs`, `Entities/EmployeeWorkLog.cs`,
   `Entities/WorkLogType.cs`, `IEmployeeWorkLogRepository.cs`,
   `EmployeeWorkLogRepository.cs`, mudanças em `app/src/features/work-logs/` e
   `app/src/features/employees/EmployeeListPage.tsx`). Isso é um risco operacional: um
   `git commit` feito neste momento sem `git add` seletivo misturaria três entregas
   distintas em um único commit, dificultando review/rollback. Recomendo ao
   desenvolvedor/orquestrador commitar (ou pelo menos `git add` seletivo) esta tarefa
   isoladamente antes de prosseguir com as demais.
3. **Testes de serialização não protegem contra remoção acidental da linha em
   `Program.cs`** (limitação já documentada no plano/resumo, sem exigir ação nesta
   entrega): como os testes replicam a configuração manualmente em vez de reutilizar o
   host real, alguém poderia remover `ConfigureHttpJsonOptions` de `Program.cs` sem
   quebrar nenhum teste. O próprio plano já propõe o caminho de evolução (extrair para
   `ApiJsonOptions.Configure` reutilizável, ou `WebApplicationFactory`) como fora de
   escopo deste bug fix — mantenho como sugestão de follow-up, não como bloqueio.

### Nitpick
- O `catch (JsonException)` no middleware, embora correto e bem implementado, na prática
  não é exercitado pelo caminho relatado pelo PO (binding automático do Minimal API já
  intercepta antes) — isso é "defesa em profundidade" para casos de desserialização
  manual futura, como o próprio plano documenta. Nenhuma ação necessária, apenas registro
  de que o teste do middleware, embora correto, testa um cenário que hoje não é
  alcançável pelos endpoints reais da aplicação (é útil só para uso futuro de
  `JsonSerializer.Deserialize` manual em algum código ainda não escrito).

## Checklist de arquitetura/convenções
- Camadas: mudanças restritas a `Api` (Program.cs, Middleware, testes de `Api.Tests`);
  nenhuma referência nova a EF Core na camada Api; `Application`/`Infrastructure` não
  tocadas por esta tarefa. **OK.**
- SOLID/DDD: sem mudança de entidades ou lógica de negócio; `WorkLogType`
  (`api/src/WorkLogManager.Application/Entities/WorkLogType.cs`) não foi alterado por
  esta tarefa (o `git diff` que aparece nesse arquivo é de outra tarefa, fora de escopo
  deste review). **OK** dentro do escopo revisado.
- EF Core: nenhuma migration nova nem alteração de mapeamento gerada por esta tarefa
  (confirmado — as únicas mudanças em `Configurations/`/`Migrations/` no diff pertencem à
  tarefa de month-closing/duration-sync, não a esta). **OK**, conforme "Fora de escopo"
  do plano.
- Front-end: nenhuma mudança de código feita por esta tarefa (conforme plano e resumo);
  as mudanças presentes em `app/` no `git status` pertencem a outra tarefa. **OK**,
  dentro do escopo revisado.
- Idioma: nomes em inglês em todo o código revisado (`WorkLogTypeJsonTests`,
  `ExceptionHandlingMiddlewareTests`, mensagens de log/erro). **OK.**
- Consistência com `docs/padroes-desenvolvimento.md`/`docs/stack.md`: middleware único
  central de tratamento de exceções mantido (nenhum novo middleware introduzido, apenas
  mais um `catch` no existente), conforme a regra de "único middleware central".

## Veredito

**Aprovado.**

A implementação é fiel ao plano aprovado, o `dotnet test` local confirma 94/94 testes
passando (reproduzido, não apenas confiado no resumo), a validação manual do payload
inválido foi reproduzida de forma independente e bate exatamente com o achado reportado
(400 com corpo vazio), e nenhum DTO foi alterado — a correção é central conforme exigido
pelo critério de aceite #5. A divergência entre o comportamento esperado
(`ProblemDetails` estruturado) e o observado (corpo vazio) para o critério de aceite #3
já havia sido antecipada como risco aceito pelo próprio plano antes da implementação, e
o resumo de implementação a documenta com transparência — não constitui um desvio não
avaliado, mas registro como Sugestão de acompanhamento de curto prazo (item 1 acima),
para que o time decida conscientemente se investe em `CustomizeProblemDetails` depois.

Ressalva de processo (não bloqueia esta tarefa, mas deve ser resolvida antes do próximo
commit): a working tree contém mudanças de múltiplas tarefas não relacionadas
misturadas; recomendo isolar/commitar `api-enum-json-fix` separadamente das demais.

# Code review: Work Log Manager

Task-id: worklog-manager

> Nota: o repositório continua não sendo um `git` repository (`git status` retorna
> "fatal: not a git repository"). A revisão foi feita comparando diretamente o
> conteúdo de `api/` e `app/` com o plano aprovado (incluindo o "Adendo pós-review")
> e com `resumo-implementacao.md`.

---

# Revisão 2 (pós-correções e mudanças arquiteturais)

Esta é a segunda rodada de review, feita depois que o desenvolvedor aplicou: (1) os 4
itens do `code-review.md` original (testes de `SystemSettings`, teste de integração da
sincronização data↔duração, avisos NuGet, `.gitignore`); e (2) as mudanças
arquiteturais do "Adendo pós-review" do plano (FluentValidation nos use-cases,
entidades atravessando a fronteira Api→Application diretamente, mapeamento
Request→Entity direto no AutoMapper, remoção de `.ToModel()`/`.ToResponse()`).

## Aderência ao adendo pós-review

| Item do adendo | Aplicado? | Observação |
|---|---|---|
| 1. FluentValidation nos use-cases | Sim | `EmployeeValidator`, `EmployeeWorkLogValidator`, `SystemSettingsValidator` em `Application/Validators/`; `CreateEmployeeUseCase`, `UpdateEmployeeUseCase`, `CreateEmployeeWorkLogUseCase`, `UpdateEmployeeWorkLogUseCase`, `UpdateSystemSettingsUseCase` injetam `IValidator<T>` e chamam `ValidateAndThrowAsync` **antes** de tocar no repositório (confirmado lendo os 5 use-cases). `ExceptionHandlingMiddleware` ganhou `catch (ValidationException)` → 400, ao lado de `DomainException`→400 e `NotFoundException`→404. Testado com `dotnet test` (ver abaixo). |
| 2. Entidade atravessa a fronteira Api→Application diretamente | Sim | `Api/Mapping/Models/` foi removido; os use-cases de Create/Update recebem `Employee`/`EmployeeWorkLog`/`SystemSettings` diretamente como parâmetro; `Update` recebe o id da rota separadamente (`UpdateEmployeeUseCase.ExecuteAsync(Guid employeeId, Employee employee, ...)`). |
| 3. Entidades simplificadas (property bags + `internal` setters) | Sim | `Employee`, `EmployeeWorkLog`, `SystemSettings` sem construtor/`Update` validante; setters `internal`; `InternalsVisibleTo` declarado só para `WorkLogManager.Application.Tests` no `.csproj` da `Application` (não vaza para `Api`/`Infrastructure`, que só enxergam os setters via reflection do EF Core/AutoMapper, não via código C#). `Touch()` presente nas três; `EmployeeWorkLog.CalculateDuration()` mantém o guard `EndDate < StartDate` como defesa em profundidade. Ver discussão em "Achados" sobre se isso é suficiente. |
| 4. Mapeamento direto Request→Entity com `.Ignore()` | Sim | Conferido nos 3 `Profile`s: `CreateEmployeeRequest`/`UpdateEmployeeRequest`→`Employee` ignora `Id`/`CreatedAtUtc`/`UpdatedAtUtc`; `CreateEmployeeWorkLogRequest`/`UpdateEmployeeWorkLogRequest`→`EmployeeWorkLog` ignora `Id`/`EmployeeId`/`DurationSeconds`/`CreatedAtUtc`/`UpdatedAtUtc`; `UpdateSystemSettingsRequest`→`SystemSettings` ignora `Id`/`UpdatedAtUtc`. Nenhum campo derivado é mapeado por engano — todos os campos que o use-case precisa preencher/derivar estão corretamente ignorados no `Profile` e são de fato atribuídos no use-case (`Id = Guid.NewGuid()`, timestamps, `CalculateDuration()`). |
| 5. Remoção de `.ToModel()`/`.ToResponse()` | Sim | `grep -rn "ToModel\|ToResponse" api/src api/tests app/src` não retornou nenhuma ocorrência. `Api/Mapping/Extensions/` não existe mais. Todos os 8 endpoints (`EmployeesEndpoints`, `EmployeeWorkLogsEndpoints`, `SystemSettingsEndpoints`) chamam `mapper.Map<T>(...)` explicitamente, conforme o exemplo do adendo. |

## Itens do code review anterior — verificação

| Ressalva anterior | Resolvida? | Observação |
|---|---|---|
| Falta teste de `GetSystemSettingsUseCase`/`UpdateSystemSettingsUseCase` | Sim | `GetSystemSettingsUseCaseTests` (2 testes: reaproveita settings existente sem `UpsertAsync`; cria default 8h e persiste quando não existe) e `UpdateSystemSettingsUseCaseTests` (3 testes: atualização com `Touch()`, criação lazy + update em sequência — verifica `UpsertAsync` chamado 2x, e rejeição de horas não-positivas via `ValidationException` sem persistir). Os testes verificam comportamento real (chamadas ao mock, valores exatos), não apenas ausência de exceção. |
| Sincronização data↔duração sem teste de integração | Sim | `WorkLogFormModal.test.tsx` (4 testes, usando `fireEvent`/`waitFor` reais, sem mockar timers): editar datas recalcula texto de duração; editar duração (aguardando o debounce real de 400ms) recalcula `endDate` sem alterar `startDate`; token inválido (`"1H"`, maiúsculo — mesmo caso de case-sensitivity do parser) mostra erro inline e não altera as datas. Cobertura genuína do comportamento mais sensível do PO, não apenas da função pura `parseDuration`/`formatDuration` isolada. |
| Avisos NuGet `NU1903` (AutoMapper + Microsoft.OpenApi) | Parcialmente resolvida, com justificativa técnica sólida | `Microsoft.AspNetCore.OpenApi` `10.0.1`→`10.0.12` resolveu o aviso do `Microsoft.OpenApi` (confirmado: `dotnet test` só mostra `NU1903` para `AutoMapper` agora, não mais para OpenApi). AutoMapper permanece em `13.0.1` com risco aceito documentado (comentário no `.csproj` + `docs/padroes-desenvolvimento.md`): a única forma de eliminar a vulnerabilidade é mudar para uma versão com licença comercial/RPL1.5 (15.1.1+/16.1.1+), decisão que extrapola escopo de code review e exige aprovação de negócio. Argumento de exploração prática (DTOs planos, sem grafos circulares) é razoável. Não bloqueante, mas fica registrado como risco residual a ser revisitado pelo responsável. |
| Ausência de `.gitignore` | Sim | `.gitignore` criado na raiz, cobre `bin/`/`obj/`/`*.user`/`.vs/` (.NET), `node_modules/`/`dist/`/`.vite/` (front), `.DS_Store`, `.env*`. Cobre os artefatos de build já presentes fisicamente em disco (`api/src/*/obj`, `api/tests/*/obj`, confirmados via `find`). |
| Smoke test manual ponta a ponta (passo 32) | Ainda não executado | Continua fora do escopo do agente (requer Postgres real); segue como pendência explícita para o responsável, sinalizada de novo no resumo. |

## Resultado dos testes (executados nesta revisão)

**Back-end** — `dotnet test` em `api/`:
```
Aprovado! – Com falha: 0, Aprovado: 52, Ignorado: 0, Total: 52 — WorkLogManager.Application.Tests.dll
Aprovado! – Com falha: 0, Aprovado: 1,  Ignorado: 0, Total: 1  — WorkLogManager.Api.Tests.dll
```
Total: 53 aprovados, 0 falhas. Confere com o resumo do desenvolvedor. Aviso residual
(esperado, documentado): `NU1903` só para `AutoMapper` 13.0.1 (não mais para
`Microsoft.OpenApi`).

**Front-end** — `npx vitest run --run` em `app/`:
```
Test Files  4 passed (4)
Tests  17 passed (17)
```
Confere com o resumo (13 anteriores + 4 do `WorkLogFormModal.test.tsx`).

**Build** — `npm run build` (`tsc -b && vite build`) executado sem erros. `dotnet build`
via `dotnet test` também sem erros de compilação.

## Arquitetura e convenções (revisão 2)

| Verificação | Ok? | Observação |
|---|---|---|
| Camadas (`Api` sem lógica de negócio / `Application` sem depender de `Infrastructure`/`Api`) | Sim | Inalterado desde a v1; endpoints continuam finos, delegando para use-cases; `Application.csproj` só referencia `FluentValidation`/`FluentValidation.DependencyInjectionExtensions` (pacotes puros, sem dependência de framework web/EF). |
| Validação tratada corretamente pelo middleware | Sim | Testado manualmente lendo `ExceptionHandlingMiddleware`: `ValidationException` (FluentValidation) → 400 com `{ message }` concatenando `ErrorMessage` de cada erro; `DomainException` → 400; `NotFoundException` → 404. Não há caminho onde uma falha de `ValidateAndThrowAsync` vaze como 500. |
| `internal` setters + `InternalsVisibleTo` como proteção de domínio | Ver "Achados" | Ver discussão detalhada abaixo — não é bloqueante (foi uma decisão explícita do responsável no adendo), mas vale registrar o trade-off. |
| EF Core: mapeamento snake_case + migration coerente | Sim (inalterado) | Nenhuma mudança na migration/configurations desde a v1; entidades com setter `internal` continuam mapeáveis normalmente pelo EF Core (reflection não é bloqueada por `internal`). |
| Front-end: shadcn/ui reaproveitado, camadas respeitadas | Sim (inalterado) | Nenhuma mudança de produção no front-end nesta rodada, só o novo arquivo de teste `WorkLogFormModal.test.tsx`. |
| Idioma (nomes em inglês) | Sim | Novas classes/métodos (`EmployeeValidator`, `CalculateDuration`, `Touch`, `EntityFactory`) em inglês; comentários/XML docs também em inglês, mensagens de validação (`WithMessage`) em inglês (voltam para a UI como texto de erro — atenção: ver nitpick abaixo). |

## Achados

### Bloqueantes
Nenhum encontrado. As 5 mudanças do adendo foram implementadas fielmente, os testes
novos cobrem exatamente os cenários que motivaram as ressalvas anteriores (e testam
comportamento real, não apenas "não lança"), o middleware de exceção trata
`ValidationException` corretamente, e os números de teste reportados (53 back-end / 17
front-end) foram confirmados rodando `dotnet test` e `npm run test` nesta revisão.

### Sugestões
- **`internal` setters + `InternalsVisibleTo` como única proteção de domínio**: essa é
  uma decisão de design que o responsável pediu explicitamente no adendo, então não é
  um achado "não autorizado" — mas vale registrar o trade-off para o histórico do
  projeto. Setters `internal` protegem contra chamadas indevidas vindas de `Api`
  (código fora do assembly `Application` realmente não compila se tentar
  `employee.Name = "x"`), o que é o objetivo principal. Porém, dentro do próprio
  assembly `Application` (e do assembly de teste, via `InternalsVisibleTo`), **qualquer**
  código pode criar um `Employee`/`EmployeeWorkLog`/`SystemSettings` totalmente
  inconsistente (nome vazio, `EndDate < StartDate` sem nunca chamar
  `CalculateDuration()`, etc.) sem passar pelo validador — a garantia de integridade
  passou a ser 100% uma convenção de uso dos use-cases (sempre chamar
  `ValidateAndThrowAsync` antes de persistir), não mais algo que o compilador ou o
  próprio tipo garanta. Isso é aceitável para o tamanho atual do projeto (poucos
  use-cases, todos já seguem a convenção, e há testes cobrindo isso), mas é
  tecnicamente uma regressão de "rich domain model" para "anemic domain model com
  validação externa" — se o projeto crescer e mais desenvolvedores/use-cases forem
  adicionados no assembly `Application`, esse é o ponto onde um futuro use-case poderia
  esquecer de chamar o validador e persistir dado inválido sem que nada opere como
  rede de segurança (a única defesa remanescente é o guard de `CalculateDuration()` em
  `EmployeeWorkLog`, que cobre só uma das três entidades e só uma das várias regras).
  Recomendo, no mínimo, documentar essa convenção como regra obrigatória em
  `docs/padroes-desenvolvimento.md` (já foi feito) e considerar, no futuro, um teste de
  arquitetura (ex.: `ArchUnitNET` ou um teste simples de reflexão) que garanta que todo
  use-case de Create/Update de fato injeta e invoca o `IValidator<T>` correspondente,
  para não depender só de revisão manual.
- **Mensagens de validação do FluentValidation em inglês, voltando na resposta HTTP 400**:
  `EmployeeValidator`, `EmployeeWorkLogValidator`, `SystemSettingsValidator` usam
  `WithMessage("Employee name is required.")` etc., que são concatenadas literalmente
  em `{ message }` pelo `ExceptionHandlingMiddleware`. O front-end em português (regra
  do plano) hoje não trata esse `message` para exibição (o `WorkLogFormModal`, por
  exemplo, trata datas inválidas com sua própria validação local antes do POST, então
  não é visível ainda), mas se algum outro fluxo futuro exibir `error.message`
  diretamente ao usuário, o usuário verá uma frase em inglês misturada com o resto da UI
  em português. Não é um erro de nomenclatura de código (nomes de classe/método
  continuam corretos em inglês), é uma inconsistência potencial de UX/i18n — vale
  registrar como algo a observar caso o backend comece a expor mensagens de erro
  diretamente na UI.
- **AutoMapper `NU1903` ainda presente** (risco residual aceito e bem documentado):
  mantém-se como sugestão não bloqueante, igual à rodada anterior — recomendo revisitar
  periodicamente se uma versão MIT corrigida aparecer.

### Nitpicks
- Nenhum novo nitpick relevante nesta rodada além dos já registrados na revisão 1
  (`cn` via pacote npm, connection string placeholder em `appsettings.json`), que
  seguem válidos e não foram alterados.

---
## Veredito (Revisão 2): Aprovado

Todas as 4 ressalvas da revisão anterior foram endereçadas com evidência concreta
(testes novos rodados e conferidos, avisos NuGet parcialmente resolvidos com
justificativa técnica sólida para o residual, `.gitignore` adequado). As 5 mudanças
arquiteturais do adendo pós-review (FluentValidation nos use-cases, entidade
atravessando a fronteira Api→Application, mapeamento direto Request→Entity,
`.Ignore()` correto para campos derivados, remoção de `.ToModel()`/`.ToResponse()`)
foram implementadas de forma fiel e consistente em todos os pontos onde deveriam
aparecer — não sobrou nenhuma chamada às extensions antigas, `mapper.Map<T>()` é usado
explicitamente em todos os endpoints, e `ValidationException` é corretamente traduzida
para HTTP 400. `dotnet test` (53 aprovados) e `npm run test` (17 aprovados) foram
confirmados nesta revisão, assim como `npm run build`.

A única observação que vale acompanhar (não bloqueante, decisão consciente do
responsável) é o trade-off de segurança de tipos descrito acima: as entidades passaram
a depender inteiramente da disciplina dos use-cases para não ficarem em estado
inconsistente dentro do próprio assembly `Application`, já que os setters `internal`
+ `InternalsVisibleTo` protegem a fronteira externa (`Api`) mas não o assembly
interno. Recomendo manter essa convenção bem documentada (já está) e considerar reforço
automatizado (teste de arquitetura) se o projeto crescer.

O smoke test manual ponta a ponta (passo 32 do plano) continua pendente e deve ser
executado pelo responsável contra um PostgreSQL real antes de considerar a feature
pronta para uso — isso não é um problema desta rodada de correções, é uma pendência
recorrente das duas revisões.

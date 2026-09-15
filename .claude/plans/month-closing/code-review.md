# Code review: Fechamento de mês (geração automática de apontamentos)

Task-id: month-closing

## Aderência ao plano aprovado

Todos os 23 passos do plano (v3) foram conferidos arquivo a arquivo via `git diff`/leitura
direta do código, não apenas pelo resumo do desenvolvedor.

| Passo do plano | Implementado? | Observação |
|---|---|---|
| 1. `WorkLogType`: `RegularAttendance`/`Break` | Sim | Aditivo ao final do enum, preserva valores existentes. |
| 2. `EmployeeWorkLog.MonthClosingId` | Sim | `Guid?`, `internal set`, mesmo padrão das demais props. |
| 3. `Entities/MonthClosing.cs` | Sim | Property bag idêntico ao especificado, `Touch()` incluído. |
| 4. `MonthClosingValidator` | Sim | `Month` 1-12, `Year > 0`, regra `Must` de mês estritamente passado. Ver desvio #3 abaixo. |
| 5. `IEmployeeWorkLogRepository.AddRangeAsync`/`ListByTypeAndDateRangeAsync`, `IMonthClosingRepository`, `IRandomProvider` | Sim | Assinaturas batem exatamente com o plano. |
| 6. `WorkLogGenerationService`/`GeneratedWorkday` | Sim | Fórmula `requiredEndOffsetMinutes = startOffset - breakStartOffset + breakEndOffset` implementada literalmente; ramo `<=4h` sem `Break`; ramo `>4h` com manhã/`Break`/tarde. Ver validação matemática abaixo. |
| 7. `Results/MonthClosingResult.cs` | Sim | Contagem por dia, não por registro, conforme decisão documentada. |
| 8. `CloseMonthUseCase` | Sim | Ordem de validação → checagem de refechamento → carregamento → geração → persistência bate com os 9 sub-passos do plano. |
| 9. `EmployeeWorkLogConfiguration`/`MonthClosingConfiguration` | Sim | `MonthClosingId` nullable, índice não-único, FK `Restrict`; `month_closings` com PK, check constraint, índice único `(year, month)`. |
| 10. `DbSet<MonthClosing>` | Sim | |
| 11. `EmployeeWorkLogRepository`/`MonthClosingRepository`/`SystemRandomProvider` | Sim | `AddRangeAsync`+`SaveChangesAsync` único; `ListByTypeAndDateRangeAsync` com filtro `Type`+`StartDate` range; `Random.Shared.Next(min, max+1)` corrige o `maxInclusive` do `IRandomProvider`. |
| 12. Migration `AddMonthClosing` | Sim | Gerada (`20260915014933_AddMonthClosing`), bate coluna a coluna com a tabela do plano; **não aplicada** (ver seção dedicada abaixo). |
| 13-16. DTOs, `MonthClosingMappingProfile`, `MonthClosingsEndpoints`, `Program.cs` | Sim | Endpoint único `POST /month-closings`, erros tratados pelo middleware existente, DI completa. |
| 17-21. Front-end (`types.ts`, `monthClosings.ts`, `useCloseMonth`, `MonthCloseModal`, `EmployeeListPage`) | Sim | Camadas `lib/api` → `hooks` → componente respeitadas; reaproveita `Dialog`/`Table`/`Button`/`Input`/`Label` já existentes. |
| 22-23. Testes back-end e front-end | Sim | Ver seção "Resultado dos testes". |

## Cobertura dos critérios de aceite

| Critério de aceite | Implementado | Coberto por teste | Observação |
|---|---|---|---|
| #1 Botão "Fechar mês" na tela inicial | Sim | Indireto | `EmployeeListPage.tsx` tem o botão; não há teste dedicado ao clique do botão em si (apenas render do modal em `MonthCloseModal.test.tsx`), mas é consistente com o padrão pré-existente do projeto (sem teste de clique para o botão "Novo funcionário" também). |
| #2 Modal seletor mês/ano, padrão = mês anterior, atual/futuro bloqueados | Sim | Sim | `MonthCloseModal.test.tsx` verifica `value`/`max` do input via `vi.setSystemTime`; back-end via `MonthClosingValidatorTests`. |
| #3 "Prosseguir" gera `RegularAttendance` para dias úteis, todos funcionários, jornada efetiva | Sim | Sim | `CloseMonthUseCaseTests` (fev/28d, abr/30d, jan/31d), `Employee.DailyWorkHours ?? SystemSettings.DefaultDailyWorkHours` confirmado em `ExecuteAsync_EmployeeWithoutDailyWorkHours_UsesSystemSettingsDefault`. |
| #4 Variação ±4min sem duração < jornada efetiva | Sim | Sim, robusto | `WorkLogGenerationServiceTests` cobre neutro, break mais cedo, break mais tarde, pior caso combinado (validei a matemática manualmente — ver abaixo), 8h com offsets variados (`[Theory]`), 7.5h não-neutro, duração mínima do `Break` (60min). |
| #5 Duplicidade: só `RegularAttendance` bloqueia | Sim | Sim | `ExecuteAsync_DayAlreadyHasRegularAttendance_...` confirma que `Break` também é pulado no mesmo dia e que `ListByTypeAndDateRangeAsync` nunca é chamado com `WorkLogType.Break`. |
| #6 `MonthClosing` único por (mês, ano), refechar retorna erro | Sim | Sim | `ExecuteAsync_MonthAlreadyClosed_...` confirma `DomainException` e que nenhum repositório de escrita é chamado; índice único `(year, month)` na migration/configuração. |
| #7 `EmployeeWorkLog` gerados relacionados ao `MonthClosing` | Sim | Sim | `Assert.All(addedWorkLogs, w => Assert.Equal(result.MonthClosing.Id, w.MonthClosingId))`. |
| #8 Resumo pós-"Prosseguir" (gerado/pulado por funcionário) | Sim | Sim | Contagens verificadas em `CloseMonthUseCaseTests`; rótulos "Dias gerados"/"Dias pulados" verificados em `MonthCloseModal.test.tsx`. |
| #9 Mês atual/futuro bloqueado (front e back) | Sim | Sim | `MonthClosingValidatorTests.Validate_CurrentMonth_HasError`/`Validate_FutureMonth_HasError`; `max` do `<input type="month">` testado no front. |

Todos os critérios de aceite do `report-po.md` estão implementados e cobertos por teste real
de comportamento (não testes vazios "passa porque não falha").

## Validação matemática independente (garantia central v3)

Reproduzi manualmente o "pior alinhamento simultâneo" citado na tarefa
(`startOffset=+4`, `breakStartOffset=-4`, `breakEndOffset=+4`) para uma jornada de 6h:

- `requiredEndOffsetMinutes = 4 - (-4) + 4 = 12` (acima do teto nominal de 4 min).
- `endOffsetUpperBound = max(12, 4) = 12` ⇒ `endOffsetMinutes` sorteado em `[12, 12]` = `12`
  (intervalo de largura zero, conforme a implementação de `IRandomProvider.NextInt`).
- manhã: `07:04`–`10:56` = 232 min. tarde: `12:04`–`14:16` (`breakEnd + 2h + 12min`) = 132 min.
- soma = 364 min = 6h04min ≥ 360 min (6h) ✓ — a garantia se sustenta mesmo no caso de borda.

Este é exatamente o cenário coberto por
`GenerateWorkday_JourneySixHours_WorstCaseCombination_ExtrapolatesNominalCapButPreservesExactEffectiveHours`,
que assevera a igualdade exata (`workedMinutes == 6h`), o caso mais apertado possível — a prova
matemática do plano está corretamente implementada e testada, inclusive o efeito colateral
documentado de o offset de saída "esticar" além dos 4 minutos nominais.

Também confirmei o ramo `<=4h` (jornada exatamente 4h cai nesse ramo, sem `Break`, com
`endOffsetMinutes = NextInt(startOffset, 4)` garantindo `endOffset >= startOffset`) e a
idempotência atômica (dia com `RegularAttendance` existente pula tanto a manhã/tarde quanto o
`Break`, sem chamada a `ListByTypeAndDateRangeAsync(WorkLogType.Break, ...)`).

## Resultado dos testes (executados por mim, não apenas relatados)

**Back-end** (`cd api && dotnet test`):
```
Aprovado! – Com falha: 0, Aprovado: 87, Ignorado: 0, Total: 87 — WorkLogManager.Application.Tests.dll
Aprovado! – Com falha: 0, Aprovado: 1, Ignorado: 0, Total: 1 — WorkLogManager.Api.Tests.dll
```
Total: 88 testes, 0 falhas — bate com o relatado no `resumo-implementacao.md`.

**Front-end** (`cd app && npm run test -- --run`):
```
Test Files  7 passed (7)
     Tests  41 passed (41)
```
Total: 41 testes, 0 falhas — bate com o relatado.

## Arquitetura e convenções

| Verificação | Ok? | Observação |
|---|---|---|
| Camadas respeitadas (`Api` sem lógica de negócio/EF Core; `Application` sem depender de `Infrastructure`/`Api`) | Sim | Nenhuma referência a `Microsoft.EntityFrameworkCore`/`Infrastructure`/`Api` dentro de `Application`. `CloseMonthUseCase` orquestra via interfaces/ports apenas. `MonthClosingsEndpoints` é um simples adaptador HTTP (parse → use-case → mapper → `Results.Created`), sem lógica de negócio. |
| SOLID / use-cases coesos | Sim | `WorkLogGenerationService` é puro (exceto `IRandomProvider`), responsabilidade única (calcular períodos de um dia); `CloseMonthUseCase` orquestra sem duplicar a lógica de geração. Nenhuma "god class". |
| EF Core: snake_case + migration coerente com o plano | Sim | `month_closings`/`month_closing_id` em snake_case; migration gerada bate exatamente com a tabela de especificação do plano (PK, check constraint, índice único, FK `RESTRICT`). |
| Migration gerada mas **não aplicada** | Sim | Nenhum `dotnet ef database update` foi executado por mim nem há evidência de execução prévia: a migration é o único artefato novo relacionado a schema (sem histórico de `__EFMigrationsHistory` alterado localmente, sem conexão real a banco durante os testes — `dotnet test` roda inteiramente com mocks/Moq, não usa `WorkLogManagerDbContext` real). |
| Front-end: uso de componentes shadcn/ui existentes | Sim | `MonthCloseModal` reaproveita `Dialog`/`DialogContent`/`DialogHeader`/`DialogTitle`/`DialogFooter`/`Button`/`Input`/`Label`/`Table*` já existentes em `components/ui`; nenhum componente novo genérico foi criado. |
| Front-end: camadas `lib/api` → `hooks` → `page` | Sim | `lib/api/monthClosings.ts` (HTTP puro) → `features/month-closing/hooks/useCloseMonth.ts` (`useMutation`) → `MonthCloseModal.tsx`/`EmployeeListPage.tsx`. Invalidação de cache usa `predicate: query.queryKey[0] === "employees"`, que efetivamente cobre tanto `employeesQueryKey = ["employees"]` quanto `employeeQueryKey(id) = ["employees", id]` — mais preciso que o texto literal do plano (que sugeria `"employee"`, singular, o que não bateria com a convenção real do código-base). Não é um desvio problemático, é uma correção acertada. |
| Todo o código em inglês | Sim | Nomes de classes/métodos/variáveis/namespaces em inglês em toda a mudança. Strings de UI em português (`"Fechar mês"`, `"Dias gerados"`, toasts) são consistentes com o padrão já usado em `EmployeeFormModal`/`EmployeeDetailPage` (produto em pt-BR) — não é violação da convenção de idioma no código. |
| FluentValidation nos use-cases | Sim | `MonthClosingValidator : AbstractValidator<MonthClosing>`, injetado como `IValidator<MonthClosing>` e chamado via `ValidateAndThrowAsync` no início do use-case, mesmo padrão dos demais validators. |
| AutoMapper direto sem extension methods | Sim | `MonthClosingMappingProfile` usa `CreateMap`/`ForMember` puro; `AutoMapperConfigurationTests` atualizado e passando (`AssertConfigurationIsValid`). |

## Desvios documentados pelo desenvolvedor — avaliação

1. **Renomeação de variável (`endOffsetMinutes` → `shortWorkdayEndOffsetMinutes`)**: confirmado
   no código — puramente sintático (evita `CS0136`, escopos irmãos com `return`), sem mudança de
   fórmula. Aceitável.
2. **`ForMember(...).Ignore()` de `MonthClosingId`** em
   `CreateEmployeeWorkLogRequest`/`UpdateEmployeeWorkLogRequest` → `EmployeeWorkLog`: revisei o
   `EmployeeWorkLogMappingProfile.cs` completo — o `Ignore()` segue exatamente a mesma convenção
   já usada para `Id`/`EmployeeId`/`DurationSeconds`/`CreatedAtUtc`/`UpdatedAtUtc` (propriedades
   atribuídas pelo use-case, não pelo request do cliente). Não há risco de o fluxo manual de
   work log "vazar" ou sobrescrever um `MonthClosingId` indevidamente, porque o mapeamento de
   criação/edição manual nunca tinha (nem deveria ter) essa propriedade no DTO de entrada; o
   `CreateEmployeeWorkLogUseCase`/`UpdateEmployeeWorkLogUseCase` continuam não manipulando
   `MonthClosingId` em nenhum ponto, então worklogs manuais seguem sempre com
   `MonthClosingId = null` (valor default do tipo `Guid?`). Aceitável, não introduz
   inconsistência.
3. **Guarda extra contra `Year` fora de `[1, 9999]`** em
   `MonthClosingValidator.BeStrictlyBeforeCurrentMonth`: evita `ArgumentOutOfRangeException` não
   tratada ao construir `DateTimeOffset` para `Year <= 0`; a regra `RuleFor(m => m.Year).GreaterThan(0)`
   já cobre a mensagem correta para esse caso. Ajuste mínimo e no mesmo espírito do que o plano já
   prescrevia para `Month` fora de `[1,12]`. Aceitável.

Nenhum desvio de escopo, arquitetura ou da garantia matemática central foi encontrado além dos
três documentados.

## Achados

### Bloqueantes
Nenhum.

### Sugestões
- **Ausência de transação explícita entre `MonthClosingRepository.AddAsync` e
  `EmployeeWorkLogRepository.AddRangeAsync`** (`CloseMonthUseCase.ExecuteAsync`, passo 8): já é um
  risco identificado e aceito explicitamente no plano ("Riscos e dependências"), consistente com o
  padrão do resto do projeto (nenhum fluxo usa `TransactionScope`/transação explícita). Não bloqueia
  esta revisão, mas registro aqui para visibilidade: uma falha entre as duas chamadas deixaria um
  `MonthClosing` "vazio" persistido, indistinguível do caso legítimo "todos os dias já existiam" —
  se esse cenário se tornar um problema real em produção, vale uma tarefa própria para envolver as
  duas escritas numa transação compartilhada do mesmo `DbContext`.
- **Nenhum teste de integração HTTP para `POST /month-closings`** (ex.: `WebApplicationFactory`):
  consistente com o padrão atual do projeto (`WorkLogManager.Api.Tests` só tem
  `AutoMapperConfigurationTests`, nenhum outro endpoint tem teste de integração HTTP hoje), então
  não é uma lacuna introduzida por esta tarefa — mas se o time decidir adicionar testes de
  integração de endpoint no futuro, este seria um bom candidato inicial dado o volume de regras de
  negócio exercitadas pelo endpoint (400 de validação, 400 de refechamento duplicado, 201 de
  sucesso).

### Nitpicks
- Nenhum.

---
## Veredito: Aprovado

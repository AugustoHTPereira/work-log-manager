# Plano de desenvolvimento: Fechamento de mês (geração automática de apontamentos)

Task-id: month-closing

## Resumo técnico da solução

Cria um novo fluxo "Fechamento de mês" que, a partir de um mês/ano selecionado (sempre
um mês civil já encerrado), gera automaticamente, para cada dia útil (seg-sex) do
período e para todos os funcionários cadastrados, um conjunto de `EmployeeWorkLog`
representando o dia de trabalho: um ou dois registros do tipo `RegularAttendance`
(novo valor do enum `WorkLogType`) cobrindo a jornada efetiva de cada funcionário
(`Employee.DailyWorkHours` ou `SystemSettings.DefaultDailyWorkHours`), e, quando a
jornada efetiva ultrapassa 4 horas, também um registro do tipo `Break` (novo valor do
enum) representando o intervalo/almoço, com horário-base `11:00`-`12:00` (UTC), que
**não** conta como tempo trabalhado. Os horários de início do período da manhã, de fim
do período da tarde e — **novidade desta revisão (v3)** — de início e fim do próprio
`Break` recebem, todos, uma variação pseudoaleatória de até 4 minutos, calculada de
forma que a soma das durações trabalhadas (manhã + tarde) nunca fique abaixo da
jornada efetiva, mesmo quando a variação do intervalo tende a encurtar o tempo
disponível para trabalho (ver prova matemática na seção "Regra de geração dos
períodos"). O fechamento em si vira uma nova entidade `MonthClosing` (mês, ano,
auditoria), única por (mês, ano), e cada `EmployeeWorkLog` gerado (`RegularAttendance`
e `Break`) fica relacionado a ela via FK opcional `MonthClosingId`. Dias que já têm ao
menos um `RegularAttendance` para aquele funcionário são pulados por completo (nenhum
`RegularAttendance` nem `Break` é gerado para aquele dia — idempotência tratada como
unidade atômica por dia); `Absence`/`Overtime` no mesmo dia não bloqueiam a geração. O
front-end ganha um botão "Fechar mês" na tela inicial, um modal de seleção de mês/ano
(bloqueando mês atual e futuros) e, após o "Prosseguir", uma tela de resumo do
fechamento (quantidade de dias gerados/pulados por funcionário).

**Horário-base do expediente:** `07:00 (UTC)`.

**Intervalo/almoço (horário-base `11:00`-`12:00` UTC):** gerado automaticamente junto
com o `RegularAttendance` de cada dia (quando aplicável — ver regra abaixo), tipo
`Break` no enum `WorkLogType`. **Ajuste desta revisão (v3):** o início e o fim do
`Break` também variam pseudoaleatoriamente em até 4 minutos, como o restante do
expediente — deixou de ser um horário absolutamente fixo. Para preservar a garantia de
que a soma trabalhada (manhã + tarde) nunca fica abaixo da jornada efetiva do
funcionário, o offset de fim do expediente (`endOffsetMinutes`) passa a ser calculado
em função da variação sorteada para o `Break` (não depende apenas do offset de início
do expediente, como nas versões anteriores).

## Back-end — mudanças por camada

### Application (use-cases, entidades, interfaces)

**Entidades**
- `Entities/WorkLogType.cs`: adicionar `RegularAttendance` e `Break` ao enum (valores
  aditivos, ao final: `Absence`, `Overtime`, `RegularAttendance`, `Break`), preservando
  os valores existentes. Nome escolhido: `Break` (mais curto e mais consistente em
  inglês corrente com `Absence`/`Overtime`/`RegularAttendance` do que `Interval`, que em
  inglês soa mais a "intervalo de tempo genérico" do que "pausa/almoço").
- `Entities/EmployeeWorkLog.cs`: adicionar propriedade `Guid? MonthClosingId { get; internal set; }`
  (FK opcional — `null` para worklogs criados manualmente via o fluxo já existente).
  Sem nenhuma outra mudança de modelo: tanto o(s) `RegularAttendance` quanto o `Break`
  de um dia gerado são instâncias comuns de `EmployeeWorkLog`, diferenciadas apenas por
  `Type` — não é criada nenhuma entidade nova para representar o intervalo.
- Nova entidade `Entities/MonthClosing.cs` (property bag, mesmo padrão de `Employee`/
  `SystemSettings`):
  ```csharp
  public class MonthClosing
  {
      public Guid Id { get; internal set; }
      public int Month { get; internal set; }
      public int Year { get; internal set; }
      public DateTimeOffset CreatedAtUtc { get; internal set; }
      public DateTimeOffset UpdatedAtUtc { get; internal set; }
      public void Touch() { UpdatedAtUtc = DateTimeOffset.UtcNow; }
  }
  ```

**Validators**
- Novo `Validators/MonthClosingValidator.cs` (`AbstractValidator<MonthClosing>`):
  - `Month` entre 1 e 12 (`InclusiveBetween`).
  - `Year` > 0 (guarda mínima de sanidade).
  - Regra customizada `Must` garantindo que (Year, Month) seja estritamente anterior ao
    mês/ano corrente em UTC (`DateTimeOffset.UtcNow`) — rejeita mês atual e futuro.
    Mensagem: "Only fully completed past months can be closed."

**Interfaces (ports) novas/alteradas**
- `Interfaces/IEmployeeWorkLogRepository.cs`: adicionar dois métodos:
  - `Task AddRangeAsync(IEnumerable<EmployeeWorkLog> workLogs, CancellationToken cancellationToken = default);`
    (inserção em lote dos worklogs gerados — `RegularAttendance` e `Break` juntos —, uma
    única transação/`SaveChanges`).
  - `Task<IReadOnlyList<EmployeeWorkLog>> ListByTypeAndDateRangeAsync(WorkLogType type, DateTimeOffset rangeStartInclusive, DateTimeOffset rangeEndExclusive, CancellationToken cancellationToken = default);`
    (usado para buscar, em uma única query, todos os `RegularAttendance` já existentes no
    mês, de todos os funcionários, para detectar duplicidade por dia; **não** é chamado
    com `WorkLogType.Break` — a checagem de duplicidade continua baseada só em
    `RegularAttendance`, ver decisão de idempotência abaixo).
- Nova `Interfaces/IMonthClosingRepository.cs`:
  ```csharp
  public interface IMonthClosingRepository
  {
      Task<MonthClosing?> GetByMonthYearAsync(int month, int year, CancellationToken cancellationToken = default);
      Task AddAsync(MonthClosing monthClosing, CancellationToken cancellationToken = default);
  }
  ```
- Nova `Interfaces/IRandomProvider.cs` (port para isolar a fonte de aleatoriedade e
  torná-la testável):
  ```csharp
  public interface IRandomProvider
  {
      int NextInt(int minInclusive, int maxInclusive);
  }
  ```

**Services (serviços de domínio sem estado)**
- Novo `Services/WorkLogGenerationService.cs`: serviço puro (exceto pela dependência
  injetada `IRandomProvider`) responsável por calcular os períodos de um dia de
  presença gerado, dado: a data (`DateOnly`) e a jornada efetiva
  (`decimal effectiveDailyWorkHours`). Horário-base de início do expediente
  (`07:00`) e horário-base do intervalo (`11:00`-`12:00`) são `const`/valores fixos
  definidos no próprio serviço — a variação em torno deles é sempre pseudoaleatória,
  nunca o horário-base em si.

  **Modelagem escolhida (e por quê):** em vez de um único período contínuo
  `RegularAttendance`, o serviço retorna uma lista de 1 a 2 períodos de
  `RegularAttendance` mais, opcionalmente, o período (variável) de `Break`. A escolha
  por **múltiplos registros de `RegularAttendance`** (em vez de um único registro com
  duração "líquida" já descontando o intervalo) foi feita porque:
  - preserva a semântica de `EmployeeWorkLog` como "um `StartDate`/`EndDate`
    contínuos representam presença real" — um único registro 07:00-16:00 com duração
    "8h" seria factualmente incorreto, pois a pessoa não está trabalhando durante o
    intervalo;
  - mantém a tela de detalhe do funcionário (que já lista `EmployeeWorkLog`
    individualmente) coerente com a realidade sem precisar de nenhum tratamento
    especial de "buraco" no meio de um card de presença;
  - reaproveita o `CalculateDuration()` já existente na entidade sem alterações.

  **Regra de geração dos períodos**, dado `baseStart = date 07:00 UTC`,
  `breakStart = date 11:00 UTC` (horário-base), `breakEnd = date 12:00 UTC`
  (horário-base):
  - `startOffsetMinutes = randomProvider.NextInt(-4, 4)` (variação do início do
    expediente/manhã), igual às versões anteriores do plano.
  - **Se `effectiveDailyWorkHours <= 4`** (jornada cabe inteira na manhã, antes do
    intervalo): `endOffsetMinutes = randomProvider.NextInt(startOffsetMinutes, 4)`
    (garante `endOffsetMinutes >= startOffsetMinutes`) e gera um único período
    `RegularAttendance` de `baseStart + startOffsetMinutes` até
    `baseStart + effectiveDailyWorkHours + endOffsetMinutes`. **Nenhum `Break` é
    gerado nesse caso** — decisão assumida: se a jornada termina antes ou exatamente
    às 11:00, o funcionário já foi embora antes do horário de almoço, logo não faz
    sentido criar um registro de intervalo para esse dia. Cobre o caso de borda
    "jornada == 4h" (cai neste ramo: um único período `07:00`-`11:00` com variação,
    sem intervalo). Como não há `Break` nesse ramo, nenhuma variação de intervalo é
    sorteada — a garantia matemática é idêntica à das versões anteriores
    (`endOffset >= startOffset` ⇒ soma trabalhada `>= effectiveDailyWorkHours`).
  - **Se `effectiveDailyWorkHours > 4`**: também se sorteiam, **independentemente**,
    `breakStartOffsetMinutes = randomProvider.NextInt(-4, 4)` (variação do início do
    `Break`) e `breakEndOffsetMinutes = randomProvider.NextInt(breakStartOffsetMinutes, 4)`
    (variação do fim do `Break`, com a mesma garantia estrutural já usada para o
    expediente: `breakEndOffsetMinutes >= breakStartOffsetMinutes`, o que impede a
    duração do `Break` de ficar abaixo de 60 minutos). Em seguida calcula-se o offset
    de fim do expediente de forma **não puramente aleatória**, mas com um piso
    (`requiredEndOffsetMinutes`) que compensa a variação do `Break`:
    ```
    requiredEndOffsetMinutes = startOffsetMinutes - breakStartOffsetMinutes + breakEndOffsetMinutes
    endOffsetUpperBound      = max(requiredEndOffsetMinutes, 4)
    endOffsetMinutes         = randomProvider.NextInt(requiredEndOffsetMinutes, endOffsetUpperBound)
    ```
    Gera três registros:
    1. `RegularAttendance` (manhã): `baseStart + startOffsetMinutes` até
       `breakStart + breakStartOffsetMinutes` (fim da manhã acompanha a variação do
       início do `Break`).
    2. `Break`: `breakStart + breakStartOffsetMinutes` até
       `breakEnd + breakEndOffsetMinutes`.
    3. `RegularAttendance` (tarde): `breakEnd + breakEndOffsetMinutes` (início da
       tarde acompanha a variação do fim do `Break`) até
       `breakEnd + (effectiveDailyWorkHours - 4h) + endOffsetMinutes`.

    **Prova da garantia matemática (duração total trabalhada nunca abaixo da jornada
    efetiva, mesmo com variação do `Break`):**
    - duração manhã = `(breakStart + breakStartOffset) - (baseStart + startOffset)`
      = `4h + (breakStartOffset - startOffset)`.
    - duração tarde = `(breakEnd + (effective - 4h) + endOffset) - (breakEnd + breakEndOffset)`
      = `(effective - 4h) + (endOffset - breakEndOffset)`.
    - soma = `effective + (breakStartOffset - startOffset) + (endOffset - breakEndOffset)`
      = `effective + endOffset - (startOffset - breakStartOffset + breakEndOffset)`
      = `effective + endOffset - requiredEndOffsetMinutes`.
    - Como `endOffsetMinutes` é sempre sorteado com piso `requiredEndOffsetMinutes`
      (`endOffsetMinutes >= requiredEndOffsetMinutes` por construção do
      `NextInt(requiredEndOffsetMinutes, max(requiredEndOffsetMinutes, 4))`), o termo
      `endOffset - requiredEndOffsetMinutes >= 0` sempre, logo a soma das durações
      trabalhadas é sempre `>= effectiveDailyWorkHours`.
    - **Caso de borda (break varia para o pior cenário possível):** quando o `Break`
      "come" tempo de trabalho no pior grau permitido — início do `Break` bem mais
      cedo (`breakStartOffsetMinutes = -4`) somado a fim do `Break` bem mais tarde
      (`breakEndOffsetMinutes = +4`) e início do expediente bem mais tarde
      (`startOffsetMinutes = +4`) — `requiredEndOffsetMinutes` pode ultrapassar o
      teto "normal" de variação (`4`). Nesse caso `endOffsetUpperBound` é elevado
      para `requiredEndOffsetMinutes`, e `endOffsetMinutes` é sorteado como um valor
      único determinístico igual a `requiredEndOffsetMinutes` (intervalo de
      amostragem de largura zero) — ou seja, **o fim do expediente é esticado o
      quanto for necessário para nunca violar a jornada efetiva**, mesmo que isso
      signifique uma variação de fim de expediente maior que os "até 4 minutos"
      nominais nesse cenário extremo e pouco provável (probabilidade baixa: exige que
      três sorteios independentes caiam simultaneamente nos extremos opostos). Este
      comportamento é intencional e documentado — a garantia de jornada mínima tem
      prioridade sobre o teto nominal de variação.

  ```csharp
  public class WorkLogGenerationService
  {
      private const int MaxVarianceMinutes = 4;
      private static readonly TimeOnly WorkdayBaseStart = new(7, 0);
      private static readonly TimeOnly BreakStart = new(11, 0);
      private static readonly TimeOnly BreakEnd = new(12, 0);
      private readonly IRandomProvider _randomProvider;

      public WorkLogGenerationService(IRandomProvider randomProvider) { ... }

      public GeneratedWorkday GenerateWorkday(DateOnly date, decimal effectiveDailyWorkHours)
      {
          var baseStart = new DateTimeOffset(date.ToDateTime(WorkdayBaseStart), TimeSpan.Zero);
          var breakStart = new DateTimeOffset(date.ToDateTime(BreakStart), TimeSpan.Zero);
          var breakEnd = new DateTimeOffset(date.ToDateTime(BreakEnd), TimeSpan.Zero);

          var startOffsetMinutes = _randomProvider.NextInt(-MaxVarianceMinutes, MaxVarianceMinutes);

          if (effectiveDailyWorkHours <= 4m)
          {
              var endOffsetMinutes = _randomProvider.NextInt(startOffsetMinutes, MaxVarianceMinutes);
              var singlePeriodEnd = baseStart.AddHours((double)effectiveDailyWorkHours).AddMinutes(endOffsetMinutes);
              return GeneratedWorkday.WithoutBreak(baseStart.AddMinutes(startOffsetMinutes), singlePeriodEnd);
          }

          var breakStartOffsetMinutes = _randomProvider.NextInt(-MaxVarianceMinutes, MaxVarianceMinutes);
          var breakEndOffsetMinutes = _randomProvider.NextInt(breakStartOffsetMinutes, MaxVarianceMinutes);

          // Piso do offset de fim de expediente: compensa a variação do Break para
          // garantir que manhã + tarde nunca fiquem abaixo da jornada efetiva.
          var requiredEndOffsetMinutes = startOffsetMinutes - breakStartOffsetMinutes + breakEndOffsetMinutes;
          var endOffsetUpperBound = Math.Max(requiredEndOffsetMinutes, MaxVarianceMinutes);
          var endOffsetMinutes = _randomProvider.NextInt(requiredEndOffsetMinutes, endOffsetUpperBound);

          var morningEnd = breakStart.AddMinutes(breakStartOffsetMinutes);
          var breakEndAdjusted = breakEnd.AddMinutes(breakEndOffsetMinutes);
          var afternoonEnd = breakEnd.AddHours((double)(effectiveDailyWorkHours - 4m)).AddMinutes(endOffsetMinutes);

          return GeneratedWorkday.WithBreak(
              morningStart: baseStart.AddMinutes(startOffsetMinutes),
              morningEnd: morningEnd,
              breakStart: morningEnd,
              breakEnd: breakEndAdjusted,
              afternoonStart: breakEndAdjusted,
              afternoonEnd: afternoonEnd);
      }
  }
  ```
  `GeneratedWorkday` é um tipo de retorno simples (record) do próprio serviço,
  expondo `IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> RegularAttendancePeriods`
  (1 ou 2 itens) e `(DateTimeOffset Start, DateTimeOffset End)? BreakPeriod` (nulo
  quando `effectiveDailyWorkHours <= 4`, com horários variáveis quando presente).
  `IRandomProvider` continua mockável em teste para forçar todos os offsets
  (`startOffsetMinutes`, `breakStartOffsetMinutes`, `breakEndOffsetMinutes`,
  `endOffsetMinutes`) e checar a fórmula em todos os ramos (jornada `<=4h`, `==4h`,
  `>4h` com break variando para mais cedo/mais tarde/nos dois sentidos).

**Results**
- Novo `Results/MonthClosingResult.cs`:
  ```csharp
  public class MonthClosingResult
  {
      public MonthClosing MonthClosing { get; }
      public IReadOnlyList<EmployeeWorkLogGenerationSummary> Summaries { get; }
      public MonthClosingResult(MonthClosing monthClosing, IReadOnlyList<EmployeeWorkLogGenerationSummary> summaries) { ... }
  }

  public class EmployeeWorkLogGenerationSummary
  {
      public Guid EmployeeId { get; }
      public string EmployeeName { get; }
      public int GeneratedCount { get; }
      public int SkippedCount { get; }
      public EmployeeWorkLogGenerationSummary(Guid employeeId, string employeeName, int generatedCount, int skippedCount) { ... }
  }
  ```
  **Decisão sobre o que `GeneratedCount`/`SkippedCount` contam:** contam **dias úteis**
  processados (gerados vs. pulados), não o número bruto de registros `EmployeeWorkLog`
  inseridos. Um dia "gerado" pode ter inserido 1 registro (jornada `<=4h`, sem `Break`)
  ou 3 registros (jornada `>4h`: manhã + `Break` + tarde) — internamente isso é
  irrelevante para o resumo, que responde à pergunta de negócio "quantos dias de
  presença foram gerados para este funcionário". Justificativa: o resumo exibido ao
  usuário ficaria confuso se contasse registros de intervalo junto com dias de
  presença (ex.: "23 gerados" poderia parecer 23 dias quando na verdade seriam ~15
  dias + 8 intervalos). Documentado também no XML doc do
  `EmployeeWorkLogGenerationSummary`.
  (agregado não persistido, mesmo padrão de `EmployeeDetailResult` — é o "relatório" pedido
  pelo PO, item de decisão #1: não é tela separada nem entidade persistida, é o retorno do
  próprio `POST /month-closings`.)

**Use-case**
- Novo `UseCases/MonthClosings/CloseMonthUseCase.cs`:
  - Injeta `IEmployeeRepository`, `IEmployeeWorkLogRepository`, `IMonthClosingRepository`,
    `GetSystemSettingsUseCase`, `WorkLogGenerationService`, `IValidator<MonthClosing>`.
  - `ExecuteAsync(MonthClosing input, CancellationToken cancellationToken = default)`:
    1. `ValidateAndThrowAsync(input, ...)` (mês 1-12, ano válido, mês estritamente
       passado).
    2. Busca `IMonthClosingRepository.GetByMonthYearAsync(input.Month, input.Year, ...)`;
       se já existir, lança `DomainException("Month {mm}/{yyyy} has already been closed.")`
       — **sem** gerar nenhum worklog (bloqueio de refechamento, critério de aceite #6).
    3. Carrega todos os funcionários (`IEmployeeRepository.ListAllAsync`) e o
       `SystemSettings` vigente (via `GetSystemSettingsUseCase`).
    4. Calcula `rangeStart = new DateTimeOffset(input.Year, input.Month, 1, 0,0,0, TimeSpan.Zero)`
       e `rangeEnd = rangeStart.AddMonths(1)`.
    5. Busca de uma vez os `RegularAttendance` já existentes no período (todos os
       funcionários) via `ListByTypeAndDateRangeAsync(WorkLogType.RegularAttendance, ...)`,
       agrupando localmente em um `HashSet<(Guid EmployeeId, DateOnly Date)>` para
       checagem O(1) de duplicidade (critério de aceite #5 — `Absence`/`Overtime` não
       entram nesse conjunto, logo não bloqueiam nada). **Idempotência por dia tratada
       como unidade atômica `RegularAttendance` + `Break`**: a checagem continua
       baseada exclusivamente na existência de `RegularAttendance` naquele dia — não é
       criada nenhuma checagem própria para `Break`. Isso é seguro porque, nesta mesma
       execução do use-case, `RegularAttendance` e `Break` de um dado dia são sempre
       gerados juntos (passo 7) ou nenhum dos dois é gerado; a única forma de existir
       um `Break` "órfão" sem `RegularAttendance` correspondente seria uma edição
       manual fora deste fluxo, cenário que já está fora de escopo (worklogs gerados
       são editáveis/excluíveis individualmente pelo fluxo manual existente, conforme
       "Fora de escopo").
    6. Cria o `MonthClosing` (novo `Guid`, `CreatedAtUtc`/`UpdatedAtUtc = now`) — **é
       persistido mesmo se, ao final, nenhum worklog novo for gerado** (ex.: todos os dias
       já tinham `RegularAttendance`), porque o "fechamento" em si é o evento de negócio
       que deve ficar registrado e impedir refechamento futuro do mesmo período.
    7. Para cada funcionário, calcula `effectiveDailyWorkHours = employee.DailyWorkHours ??
       systemSettings.DefaultDailyWorkHours` e itera todas as datas do mês
       (`DateOnly` de 1 até `DateTime.DaysInMonth(year, month)`) filtrando
       `DayOfWeek is Monday..Friday`:
       - se `(employee.Id, date)` já está no conjunto de existentes → incrementa
         `skippedCount`, não gera nada (nem `RegularAttendance` nem `Break`);
       - senão → chama `WorkLogGenerationService.GenerateWorkday(date, effectiveDailyWorkHours)`,
         obtém `GeneratedWorkday`, e monta:
         - um `EmployeeWorkLog` por item de `RegularAttendancePeriods` (1 ou 2),
           `Type = WorkLogType.RegularAttendance`;
         - se `BreakPeriod` não for nulo, mais um `EmployeeWorkLog` com
           `Type = WorkLogType.Break` para aquele período;
         - todos com `Id = Guid.NewGuid()`, `EmployeeId`, `MonthClosingId =
           monthClosing.Id`, chamando `CalculateDuration()` em cada um,
           `CreatedAtUtc`/`UpdatedAtUtc = now`;
         - acumula todos na lista a inserir, incrementa `generatedCount` em **1** (por
           dia, não por registro — ver decisão em `Results/MonthClosingResult.cs`).
       - acumula um `EmployeeWorkLogGenerationSummary` por funcionário.
    8. Persiste: `IMonthClosingRepository.AddAsync(monthClosing, ...)` seguido de
       `IEmployeeWorkLogRepository.AddRangeAsync(workLogsToCreate, ...)`.
    9. Retorna `new MonthClosingResult(monthClosing, summaries)`.

### Infrastructure (EF Core, repositórios, integrações)

- `Persistence/Configurations/MonthClosingConfiguration.cs` (novo):
  - `ToTable("month_closings", t => t.HasCheckConstraint("ck_month_closings_month_range", "month >= 1 AND month <= 12"))`.
  - `HasKey(m => m.Id)`.
  - `Property(m => m.Month).IsRequired()`; `Property(m => m.Year).IsRequired()`.
  - `Property(m => m.CreatedAtUtc).IsRequired()`; `Property(m => m.UpdatedAtUtc).IsRequired()`.
  - `HasIndex(m => new { m.Year, m.Month }).IsUnique()` (constraint de unicidade por
    mês/ano — critério de aceite #6).
- `Persistence/Configurations/EmployeeWorkLogConfiguration.cs` (alterado):
  - Adicionar `builder.Property(w => w.MonthClosingId);` (nullable, sem `.IsRequired()`).
  - `builder.HasIndex(w => w.MonthClosingId);`.
  - `builder.HasOne<MonthClosing>().WithMany().HasForeignKey(w => w.MonthClosingId).OnDelete(DeleteBehavior.Restrict);`
    (`Restrict`, não `Cascade` — não existe (nem está no escopo) exclusão de
    `MonthClosing`; evita apagar acidentalmente worklogs gerados caso essa operação seja
    adicionada no futuro).
  - Nenhuma mudança na configuração da coluna `type` além de já usar
    `HasConversion<string>()` — confirmar que o `maxLength` já configurado comporta o
    maior valor do enum (`RegularAttendance`, 17 caracteres; `Break`, 5 caracteres —
    ambos cabem folgadamente no `maxLength(20)`/`character varying(20)` já existente).
- `Persistence/WorkLogManagerDbContext.cs`: adicionar `DbSet<MonthClosing> MonthClosings => Set<MonthClosing>();`.
- `Persistence/Repositories/EmployeeWorkLogRepository.cs` (alterado): implementar
  `AddRangeAsync` (`AddRangeAsync` do `DbSet` + um único `SaveChangesAsync`) e
  `ListByTypeAndDateRangeAsync` (`Where(w => w.Type == type && w.StartDate >= rangeStart
  && w.StartDate < rangeEnd)`).
- Nova `Persistence/Repositories/MonthClosingRepository.cs` implementando
  `IMonthClosingRepository` (`GetByMonthYearAsync` via `FirstOrDefaultAsync(m => m.Month
  == month && m.Year == year)`; `AddAsync` via `AddAsync` + `SaveChangesAsync`, mesmo
  padrão dos demais repositórios).
- Nova `Infrastructure/Services/SystemRandomProvider.cs` implementando `IRandomProvider`
  (usa `Random.Shared.Next(minInclusive, maxInclusive + 1)`; registrado como
  implementação de infraestrutura porque encapsula uma dependência de "tempo/estado
  externo" não determinística, mesmo padrão dos repositórios).

**Migration** (gerar via `dotnet ef migrations add AddMonthClosing --project
src/WorkLogManager.Infrastructure --startup-project src/WorkLogManager.Api`, **não
executar** `database update`):

| Tabela | Coluna | Tipo | Constraint |
|---|---|---|---|
| `month_closings` (nova) | `id` | `uuid` | PK |
| `month_closings` | `month` | `integer` | `NOT NULL`, `CHECK (month >= 1 AND month <= 12)` |
| `month_closings` | `year` | `integer` | `NOT NULL` |
| `month_closings` | `created_at_utc` | `timestamp with time zone` | `NOT NULL` |
| `month_closings` | `updated_at_utc` | `timestamp with time zone` | `NOT NULL` |
| `month_closings` | índice único | — | `UNIQUE (year, month)` |
| `employee_work_logs` (alterada) | `month_closing_id` | `uuid` | `NULL`, FK → `month_closings.id`, `ON DELETE RESTRICT`, índice não-único |
| `employee_work_logs` | `type` | (sem alteração de coluna) | valores `RegularAttendance` e `Break` são aditivos sobre a coluna `character varying(20)` já existente (`HasConversion<string>()`); confirmado que `"RegularAttendance"` (17 caracteres) e `"Break"` (5 caracteres) cabem no `maxLength(20)` atual sem necessidade de alterar o tipo/tamanho da coluna |

### Api (endpoints, DTOs)

- `Dtos/MonthClosings/CreateMonthClosingRequest.cs`:
  ```csharp
  public record CreateMonthClosingRequest(int Month, int Year);
  ```
- `Dtos/MonthClosings/EmployeeWorkLogGenerationSummaryResponse.cs`:
  ```csharp
  public record EmployeeWorkLogGenerationSummaryResponse(Guid EmployeeId, string EmployeeName, int GeneratedCount, int SkippedCount);
  ```
  (`GeneratedCount`/`SkippedCount` contam dias, não registros — mesma semântica de
  `EmployeeWorkLogGenerationSummary` na Application; documentado no XML doc do record).
- `Dtos/MonthClosings/MonthClosingResponse.cs` (propriedades `init`, seguindo a convenção
  já usada para DTOs de resposta agregados como `EmployeeDetailResponse`, já que combina
  campos de `MonthClosing` e da lista de `Summaries` do `MonthClosingResult`):
  ```csharp
  public record MonthClosingResponse
  {
      public Guid Id { get; init; }
      public int Month { get; init; }
      public int Year { get; init; }
      public DateTimeOffset CreatedAtUtc { get; init; }
      public IReadOnlyList<EmployeeWorkLogGenerationSummaryResponse> Summaries { get; init; } = [];
  }
  ```
- `Mapping/Profiles/MonthClosingMappingProfile.cs` (novo):
  - `CreateMap<CreateMonthClosingRequest, MonthClosing>()` ignorando `Id`, `CreatedAtUtc`,
    `UpdatedAtUtc`.
  - `CreateMap<MonthClosingResult, MonthClosingResponse>()` com `ForMember` mapeando
    `Id`/`Month`/`Year`/`CreatedAtUtc` a partir de `src.MonthClosing.X` e `Summaries` a
    partir de `src.Summaries`.
  - `CreateMap<EmployeeWorkLogGenerationSummary, EmployeeWorkLogGenerationSummaryResponse>()`.
- `Endpoints/MonthClosingsEndpoints.cs` (novo):
  ```csharp
  var group = app.MapGroup("/month-closings").WithTags("MonthClosings");

  group.MapPost("/", async (
      CreateMonthClosingRequest request,
      CloseMonthUseCase useCase,
      IMapper mapper,
      CancellationToken cancellationToken) =>
  {
      var monthClosing = mapper.Map<MonthClosing>(request);
      var result = await useCase.ExecuteAsync(monthClosing, cancellationToken);
      return Results.Created($"/month-closings/{result.MonthClosing.Id}", mapper.Map<MonthClosingResponse>(result));
  });
  ```
  Erros de validação (`ValidationException`) e de refechamento duplicado
  (`DomainException`) já são traduzidos para HTTP 400 pelo middleware existente
  (`ExceptionHandlingMiddleware`) — sem necessidade de tratamento especial (mantém o
  padrão do projeto; o PO cita "400/409" como aceitável e 400 já é o padrão usado para
  toda violação de regra de negócio neste código-base).
- `Program.cs`: registrar `AddScoped<IEmployeeWorkLogRepository, EmployeeWorkLogRepository>`
  (já existe, sem mudança de registro), `AddScoped<IMonthClosingRepository,
  MonthClosingRepository>()`, `AddScoped<IRandomProvider, SystemRandomProvider>()`,
  `AddScoped<WorkLogGenerationService>()`, `AddScoped<CloseMonthUseCase>()`, e
  `app.MapMonthClosingsEndpoints();`. `AddValidatorsFromAssembly` já cobre o novo
  `MonthClosingValidator` automaticamente (mesmo assembly).

## Front-end — mudanças em `app/`

- `src/lib/api/types.ts`:
  - `WorkLogType` passa a `"Absence" | "Overtime" | "RegularAttendance" | "Break"`.
  - Novo `CloseMonthPayload { month: number; year: number }`.
  - Novos `EmployeeWorkLogGenerationSummary { employeeId: string; employeeName: string; generatedCount: number; skippedCount: number }`
    (contagem em dias, não em registros — mesma semântica do back-end)
    e `MonthClosingResult { id: string; month: number; year: number; createdAtUtc: string; summaries: EmployeeWorkLogGenerationSummary[] }`.
- `src/lib/api/monthClosings.ts` (novo, camada pura de acesso a dados):
  ```ts
  export function closeMonth(payload: CloseMonthPayload): Promise<MonthClosingResult> {
    return apiClient.post<MonthClosingResult>("/month-closings", payload)
  }
  ```
- `src/features/month-closing/` (nova feature):
  - `hooks/useCloseMonth.ts`: `useMutation` chamando `closeMonth`; `onSuccess` invalida a
    lista de funcionários (`useEmployees` query key) e, de forma ampla, qualquer query de
    detalhe de funcionário já em cache (`queryClient.invalidateQueries({ predicate: (q) =>
    q.queryKey[0] === "employee" })`, mesmo padrão de `employeeQueryKey` usado em
    `useCreateWorkLog`), já que o fechamento pode ter gerado worklogs para vários
    funcionários simultaneamente.
  - `MonthCloseModal.tsx`: componente de dois passos (state local `step: "select" |
    "summary"`), reaproveitando `Dialog`/`DialogContent`/`DialogHeader`/`DialogTitle`/
    `DialogFooter`/`Button`/`Label`/`Input` do `components/ui`:
    - Passo `"select"`: um `<Input type="month">` (mesmo padrão nativo já usado no projeto
      para `type="date"`/`type="datetime-local"`/`type="time"`) com atributo `max` fixado
      no mês civil anterior ao atual (calculado no `open` do modal, formato `YYYY-MM`),
      valor inicial = mês anterior ao atual. Botão "Prosseguir" desabilitado enquanto
      `isPending` ou valor vazio/inválido; ao clicar, parseia `"YYYY-MM"` em
      `{ month, year }` e chama `useCloseMonth().mutateAsync`. Em caso de sucesso, guarda
      o `MonthClosingResult` retornado e troca para o passo `"summary"`; em caso de erro
      (`ApiError`), exibe toast de erro (`sonner`) com a mensagem do backend (cobre
      tentativa de refechar mês já fechado).
    - Passo `"summary"`: mostra o mês/ano fechado e uma `Table` com uma linha por
      funcionário (`employeeName`, `generatedCount`, `skippedCount` — rotulados como
      "dias gerados"/"dias pulados" na UI, para deixar explícito que a contagem é por
      dia útil e não por registro individual de worklog), e um botão "Fechar" que reseta
      o modal (`onOpenChange(false)`, volta `step` para `"select"` no próximo `open`).
  - Nenhuma tela de resumo separada/roteada é criada — cumpre a decisão do PO (item #1)
    de que o "relatório" é apenas o resultado do fechamento exibido no próprio fluxo.
- `src/features/employees/EmployeeListPage.tsx`: adicionar um segundo `Button` (variante
  `outline`, ícone `CalendarCheck`/`CalendarClock` de `lucide-react`) "Fechar mês" ao lado
  do botão "Novo funcionário" existente, controlando `isMonthCloseModalOpen` e renderizando
  `<MonthCloseModal open={...} onOpenChange={...} />`.

## Passos de implementação

1. `Entities/WorkLogType.cs`: adicionar `RegularAttendance` e `Break`.
2. `Entities/EmployeeWorkLog.cs`: adicionar `MonthClosingId`.
3. Criar `Entities/MonthClosing.cs`.
4. Criar `Validators/MonthClosingValidator.cs`.
5. Atualizar `Interfaces/IEmployeeWorkLogRepository.cs` (`AddRangeAsync`,
   `ListByTypeAndDateRangeAsync`); criar `Interfaces/IMonthClosingRepository.cs` e
   `Interfaces/IRandomProvider.cs`.
6. Criar `Services/WorkLogGenerationService.cs` (incl. tipo de retorno
   `GeneratedWorkday`, com ramos de jornada `<=4h` e `>4h`, geração/omissão do
   `Break`, variação pseudoaleatória independente do início/fim do `Break` e cálculo
   compensatório de `endOffsetMinutes` para garantir a jornada mínima trabalhada).
7. Criar `Results/MonthClosingResult.cs` (incl. `EmployeeWorkLogGenerationSummary`).
8. Criar `UseCases/MonthClosings/CloseMonthUseCase.cs` (montagem de 1-3
   `EmployeeWorkLog` por dia gerado a partir de `GeneratedWorkday`; contagem de
   `generatedCount`/`skippedCount` por dia).
9. Atualizar `Persistence/Configurations/EmployeeWorkLogConfiguration.cs` (coluna/índice/
   FK `MonthClosingId`); criar `Persistence/Configurations/MonthClosingConfiguration.cs`.
10. Atualizar `Persistence/WorkLogManagerDbContext.cs` (`DbSet<MonthClosing>`).
11. Atualizar `Persistence/Repositories/EmployeeWorkLogRepository.cs`; criar
    `Persistence/Repositories/MonthClosingRepository.cs`; criar
    `Infrastructure/Services/SystemRandomProvider.cs`.
12. Gerar a migration `AddMonthClosing` (`dotnet ef migrations add`, sem `database update`).
13. Criar DTOs em `Api/Dtos/MonthClosings/`.
14. Criar `Api/Mapping/Profiles/MonthClosingMappingProfile.cs`.
15. Criar `Api/Endpoints/MonthClosingsEndpoints.cs`.
16. Atualizar `Api/Program.cs` (DI + `MapMonthClosingsEndpoints`).
17. Atualizar `app/src/lib/api/types.ts` (novos valores `"RegularAttendance"` e
    `"Break"` de `WorkLogType`, novos tipos de payload/resultado).
18. Criar `app/src/lib/api/monthClosings.ts`.
19. Criar `app/src/features/month-closing/hooks/useCloseMonth.ts`.
20. Criar `app/src/features/month-closing/MonthCloseModal.tsx`.
21. Atualizar `app/src/features/employees/EmployeeListPage.tsx` (botão + modal).
22. Escrever testes de back-end (ver "Estratégia de testes") e atualizar
    `AutoMapperConfigurationTests.cs` e `EntityFactory.cs` (fábrica de `MonthClosing`).
23. Escrever testes de front-end (`MonthCloseModal.test.tsx` e, se necessário, um teste de
    parsing do valor `"YYYY-MM"` do `<input type="month">`, ao lado do componente).

## Mapeamento critério de aceite → passos

| Critério de aceite | Passo(s) |
|---|---|
| #1 Botão "Fechar mês" na tela inicial | 21 |
| #2 Modal com seletor mês/ano, padrão = mês anterior, atual/futuro bloqueados | 20, 21 |
| #3 "Prosseguir" gera `RegularAttendance` para dias úteis, todos os funcionários, jornada efetiva | 1, 2, 3, 6, 8, 20 |
| #4 Variação ±4min sem duração < jornada efetiva | 6, 8 |
| #5 Duplicidade: só `RegularAttendance` bloqueia; `Absence`/`Overtime` não | 5, 8 |
| #6 `MonthClosing` único por (mês, ano), refechar retorna erro | 3, 4, 8, 9 (índice único) |
| #7 `EmployeeWorkLog` gerados relacionados ao `MonthClosing` | 2, 8, 9 |
| #8 Resumo pós-"Prosseguir" (gerado/pulado por funcionário) | 7, 8, 13, 14, 15, 20 |
| #9 Mês atual/futuro bloqueado (front e back) | 4, 20 |
| Horário-base 07:00 UTC | 6 |
| Geração do `Break` (horário-base 11:00-12:00) junto com `RegularAttendance` quando jornada > 4h | 1, 6, 8 |
| Jornada dividida em manhã/tarde ao redor do intervalo, sem cair abaixo da jornada efetiva | 6, 8 |
| Idempotência tratada como unidade atômica `RegularAttendance` + `Break` | 5, 8 |
| **Variação pseudoaleatória também no início/fim do `Break` (até 4min), sem que a soma trabalhada fique abaixo da jornada efetiva (ajuste desta revisão — v3)** | 6, 8 |

## Estratégia de testes

Back-end (xUnit + Moq, `tests/WorkLogManager.Application.Tests`):

| Cenário | Teste |
|---|---|
| Jornada 3h (`<=4h`), offsets controlados via `IRandomProvider` mockado | `WorkLogGenerationServiceTests`: um único período `RegularAttendance`, `Break` nulo, duração == jornada efetiva (ou jornada + variação máxima, conforme offsets); `IRandomProvider` mockado nunca é chamado para offsets de `Break` nesse ramo. |
| Jornada exatamente 4h (caso de borda) | `WorkLogGenerationServiceTests`: cai no ramo `<=4h` — um único período `07:00`(+offset)-`11:00`(+offset), `Break` nulo. |
| Jornada 6h (`>4h`), offsets de `Break` neutros (`breakStartOffset = breakEndOffset = 0`) | `WorkLogGenerationServiceTests`: comportamento idêntico ao das versões anteriores do plano — dois períodos `RegularAttendance` (manhã `07:00`(+startOffset)-`11:00`; tarde `12:00`-`13:00`(+endOffset)) + `Break` `11:00`-`12:00`; soma das durações de manhã+tarde == 6h + `(endOffset - startOffset)`. |
| Jornada 6h, `Break` varia para **mais cedo** (`breakStartOffset = -4`, `breakEndOffset = -4`, `startOffset = 0`) | `WorkLogGenerationServiceTests`: manhã termina às `10:56`; tarde começa às `11:56`; `requiredEndOffsetMinutes = 0 - (-4) + (-4) = 0` ⇒ `endOffsetMinutes` sorteado em `[0, 4]`; soma manhã+tarde `>= 6h` sempre, verificado para os extremos do range sorteado. |
| Jornada 6h, `Break` varia para **mais tarde** (`breakStartOffset = 4`, `breakEndOffset = 4`, `startOffset = 0`) | `WorkLogGenerationServiceTests`: manhã termina às `11:04`; tarde começa às `12:04`; `requiredEndOffsetMinutes = 0 - 4 + 4 = 0` ⇒ mesmo piso do cenário anterior; soma manhã+tarde `>= 6h`. |
| Jornada 6h, pior caso combinado (`startOffset = 4`, `breakStartOffset = -4`, `breakEndOffset = 4`) | `WorkLogGenerationServiceTests`: `requiredEndOffsetMinutes = 4 - (-4) + 4 = 12` (acima do teto nominal de 4) ⇒ `endOffsetUpperBound = 12`, `endOffsetMinutes` sorteado deterministicamente como `12` (range de largura zero); soma manhã+tarde == exatamente `6h` (caso limite, delta zero) — comprova que mesmo no cenário mais adverso a garantia não é violada, ainda que o offset de fim de expediente extrapole os "até 4 minutos" nominais. |
| Jornada 8h, `startOffset = -4`, `breakStartOffset`/`breakEndOffset` variados | `WorkLogGenerationServiceTests`: soma das durações trabalhadas sempre `>= 8h`, verificado por fórmula (`effective + endOffset - requiredEndOffsetMinutes >= effective`). |
| Jornada 7.5h (decimal), `>4h`, `Break` com offsets não neutros | `WorkLogGenerationServiceTests`: duração total trabalhada bate com `7.5h + (endOffsetMinutes - requiredEndOffsetMinutes)`; `Break` presente com horários variados conforme os offsets sorteados. |
| Duração do `Break` nunca cai abaixo de 60 minutos | `WorkLogGenerationServiceTests`: para qualquer combinação de `breakStartOffsetMinutes`/`breakEndOffsetMinutes` dentro de `[-4,4]` respeitando `breakEndOffsetMinutes >= breakStartOffsetMinutes`, `breakEnd - breakStart >= 60min` (teste parametrizado). |
| Qualquer combinação de offsets sorteados dentro de `[-4,4]` (e do piso compensatório quando aplicável), jornadas `<=4h` e `>4h` | `WorkLogGenerationServiceTests`: propriedade geral — soma das durações trabalhadas `>= effectiveDailyWorkHours` sempre respeitada (teste `[Theory]` parametrizado cobrindo os extremos e combinações intermediárias dos quatro offsets). |
| Mês de 28 dias (fev, ano não bissexto) | `CloseMonthUseCaseTests`: contagem de dias úteis gerados bate com o esperado (ex.: fev/2027); para cada dia com jornada `>4h`, 3 `EmployeeWorkLog` são inseridos (2 `RegularAttendance` + 1 `Break`). |
| Mês de 30 e 31 dias | `CloseMonthUseCaseTests`: mesma checagem para abr/2026 (30) e jan/2020 (31, mês fixo claramente no passado). |
| Todos os dias do mês já têm `RegularAttendance` para o funcionário | `CloseMonthUseCaseTests`: `AddRangeAsync` chamado com lista vazia (nenhum `RegularAttendance` nem `Break`), `skippedCount` == nº de dias úteis, `MonthClosing` ainda assim é criado. |
| Dia com `Absence`/`Overtime` existente mas sem `RegularAttendance` | `CloseMonthUseCaseTests`: dia é gerado normalmente (não pulado), incluindo o `Break` quando aplicável. |
| Geração dos dois/três registros por dia | `CloseMonthUseCaseTests`: para um funcionário com jornada `>4h` e um único dia útil no mês (mês de teste fixado), `AddRangeAsync` é chamado com exatamente 2 `RegularAttendance` + 1 `Break`, todos com o mesmo `MonthClosingId` e `EmployeeId`. |
| Geração de um dia com jornada `<=4h` | `CloseMonthUseCaseTests`: `AddRangeAsync` chamado com exatamente 1 `RegularAttendance` e nenhum `Break` para aquele dia. |
| Idempotência considerando os dois tipos | `CloseMonthUseCaseTests`: dia já com `RegularAttendance` existente é pulado por completo — nenhum `Break` é inserido para esse dia mesmo com jornada `>4h`; `ListByTypeAndDateRangeAsync` é chamado apenas com `WorkLogType.RegularAttendance` (nunca com `WorkLogType.Break`). |
| Mês/ano inválido (`Month=13`, `Month=0`) | `MonthClosingValidatorTests` + `CloseMonthUseCaseTests` (lança `ValidationException`, nenhum repositório é chamado). |
| Mês atual | `MonthClosingValidatorTests`: rejeitado. |
| Mês futuro | `MonthClosingValidatorTests`: rejeitado. |
| Mês passado válido | `MonthClosingValidatorTests`: aceito. |
| Fechamento duplicado (mês/ano já existe) | `CloseMonthUseCaseTests`: `IMonthClosingRepository.GetByMonthYearAsync` retorna não-nulo → `DomainException`, nenhum `AddRangeAsync`/`AddAsync` chamado. |
| Funcionário com `DailyWorkHours == null` | `CloseMonthUseCaseTests`: usa `SystemSettings.DefaultDailyWorkHours` (mock de `GetSystemSettingsUseCase`/`ISystemSettingsRepository`). |
| Sem funcionários cadastrados | `CloseMonthUseCaseTests`: cria `MonthClosing` com `Summaries` vazio, sem erro. |

Api (`tests/WorkLogManager.Api.Tests`):
- Atualizar `AutoMapperConfigurationTests.Configuration_AllProfiles_AreValid` para incluir
  `cfg.AddProfile<MonthClosingMappingProfile>();` (garante que os `ForMember`
  customizados do `MonthClosingResponse` estão corretos).

Front-end (Vitest + Testing Library, ao lado do componente):
- `MonthCloseModal.test.tsx`: valor padrão do seletor é o mês anterior ao mês do sistema
  (mockar `Date`/usar data fixa via `vi.setSystemTime`); atributo `max` do `input
  type="month"` corresponde ao mês anterior; ao submeter com sucesso, exibe o passo de
  resumo com as linhas retornadas pelo mock de `closeMonth` (rótulos "dias gerados"/
  "dias pulados"); ao submeter e receber `ApiError`, exibe toast de erro e permanece no
  passo de seleção.

## Riscos e dependências

- **Jornada `<=4h` não gera `Break`** é uma premissa assumida (documentada na seção
  "Back-end — Services"), não uma definição explícita do PO — se o time de produto
  esperar um `Break` mesmo para jornadas curtas, é um ajuste pontual no ramo `<=4h` de
  `GenerateWorkday`.
- **Cenário extremo em que o offset de fim de expediente extrapola os "até 4 minutos"
  nominais (nova consideração desta revisão — v3):** quando os quatro offsets
  sorteados (`startOffset`, `breakStartOffset`, `breakEndOffset`) caem simultaneamente
  no pior alinhamento possível (expediente começa tarde, intervalo começa cedo e
  termina tarde), a fórmula compensatória (`requiredEndOffsetMinutes`) pode exigir um
  `endOffsetMinutes` maior que 4 minutos para preservar a jornada mínima trabalhada —
  ver prova matemática e caso de borda documentados em "Regra de geração dos
  períodos". Isso é uma decisão de design deliberada (a garantia de não trabalhar
  menos que a jornada efetiva tem prioridade sobre o teto nominal de variação), mas é
  um efeito colateral que vale registrar: em cenários raros, o horário de saída
  registrado pode variar ligeiramente mais que os "até 4 minutos" normalmente
  esperados. Probabilidade baixa (exige três sorteios independentes nos extremos
  opostos simultaneamente) e sem impacto no critério de aceite #4, que exige apenas
  que a duração nunca fique abaixo da jornada efetiva — não que a variação de horário
  de saída fique estritamente limitada a 4 minutos em todos os cenários.
- Geração em lote depende de carregar todos os `RegularAttendance` do mês em memória para
  checar duplicidade; para o volume esperado deste sistema (uso interno, poucas dezenas
  de funcionários) isso é aceitável, mas não escala indefinidamente — não otimizar
  prematuramente fora desse escopo.
- `<input type="month">` tem suporte variável entre navegadores (bom suporte em Chrome/
  Edge; Firefox e Safari têm suporte parcial/mais recente); mesma limitação já aceita
  implicitamente pelo projeto ao usar `type="date"`/`type="datetime-local"` em
  `WorkLogFormModal`. Não é tratado como bloqueador.
  Assumido navegador padrão (Chrome) igual ao restante do projeto.
- `CloseMonthUseCase` faz duas escritas (`MonthClosing` + lote de `EmployeeWorkLog`) sem
  uma transação de banco explícita cobrindo as duas — como ambos os repositórios usam o
  mesmo `DbContext` (escopo por requisição), e cada `Add*Async` chama
  `SaveChangesAsync` internamente conforme o padrão já usado neste projeto (cada método de
  repositório salva individualmente), existe uma janela teórica entre o `AddAsync` do
  `MonthClosing` e o `AddRangeAsync` dos worklogs em que uma falha deixaria o
  `MonthClosing` persistido sem worklogs (indistinguível do caso legítimo "todos os dias
  já existiam"). Como o projeto não usa `TransactionScope`/transações explícitas em
  nenhum outro fluxo, este plano segue a mesma convenção; se o time quiser reforçar
  atomicidade aqui especificamente, é um ajuste pontual a decidir na implementação (ex.:
  `IEmployeeWorkLogRepository`/`IMonthClosingRepository` compartilhando uma transação
  explícita do `DbContext`), fora do escopo inicial deste plano.
- Nenhum endpoint de consulta (`GET /month-closings`, `GET /month-closings/{id}`) foi
  incluído no escopo, porque o PO não pediu navegação histórica de fechamentos — apenas o
  resumo imediato pós-"Prosseguir". Ver "Fora de escopo".

## Fora de escopo / não será feito

- Consideração de feriados na geração dos dias úteis (PO define explicitamente
  seg-sex, sem feriados).
- Seleção manual de quais funcionários incluir no fechamento (PO define: todos).
- Configuração do horário-base do expediente ou do horário-base do intervalo por
  funcionário/empresa — ambos permanecem constantes fixas em UTC nesta versão (`07:00`
  de início de expediente e `11:00`-`12:00` de intervalo), sujeitas apenas à variação
  pseudoaleatória de até 4 minutos descrita neste plano; não há suporte a customizar
  esses horários-base por funcionário ou por empresa.
- Endpoints de consulta/listagem/exclusão de `MonthClosing` (`GET`/`DELETE`) — não
  pedidos pelo PO; se necessários no futuro, viram uma tarefa própria.
- Edição/exclusão em lote dos `EmployeeWorkLog` gerados por um fechamento — o usuário já
  pode editar/excluir cada worklog individualmente pelo fluxo existente de
  `work-logs` (fora de escopo criar uma ação de "desfazer fechamento").
- Tela histórica de fechamentos anteriores na navegação principal.
- Internacionalização/timezone configurável para os horários-base do expediente e do
  intervalo gerados (fixos em UTC, mesmo padrão do restante do sistema).

---
## Status: aguardando aprovação

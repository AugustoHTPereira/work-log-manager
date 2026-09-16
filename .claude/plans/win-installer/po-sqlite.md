# Report complementar: troca de PostgreSQL por SQLite (revisão do plano `win-installer`)

> Este report complementa `report-po.md` (task original do instalador nativo). Não o
> substitui — o instalador nativo continua sendo o objetivo de produto. Este documento
> registra a decisão de produto adicional de trocar o provider de banco de dados de
> PostgreSQL para SQLite, em todo o projeto (não só no artefato do instalador), e a
> investigação técnica que embasa a revisão do plano de desenvolvimento.

## Decisão de produto

- SQLite substitui o PostgreSQL **completamente** em todo o projeto, inclusive no
  ambiente de desenvolvimento. Não é uma troca só para o artefato do instalador: um
  único provider de banco de dados, uma única connection string por ambiente
  (desenvolvimento / produção-instalador), um único conjunto de migrations.
- **Sem migração retroativa de dados.** Não existe hoje nenhum banco PostgreSQL real
  em produção com dados a preservar (a aplicação ainda não foi entregue à usuária
  final). As 6 migrations PostgreSQL já existentes (`InitialCreate`, `AddMonthClosing`,
  `ReplaceDailyWorkHoursWithWorkSchedulePeriods`, `AddNoteToEmployeeWorkLog`,
  `AddOriginToEmployeeWorkLog`, `AddSystemParameters`) devem ser apagadas e recriadas
  do zero como uma única migration `InitialCreate` nova para o provider SQLite, gerada
  a partir do modelo atual (já com todos os campos acumulados por essas 6 migrations).

## Motivação

Simplificar a operação da máquina da usuária final: SQLite é um arquivo único, sem
processo de servidor, sem porta de rede, sem serviço adicional do Windows para
provisionar/gerenciar. Isso elimina toda a complexidade do instalador embutir e
provisionar um PostgreSQL (instalador EDB de ~300+MB, serviço Windows adicional, porta
fixa, superusuário/senha), reduzindo drasticamente o tamanho e a fragilidade do
instalador.

## Investigação técnica (levantamento em `api/src/WorkLogManager.Infrastructure`)

- **Enums como string**: todas as propriedades de enum usam `.HasConversion<string>()`
  (`WorkSchedulePeriod.DayOfWeek`, `EmployeeWorkLog.Type`, `EmployeeWorkLog.Origin`,
  `SystemParameter.Param`, `SystemParameter.ValueType`) — portável, sem ajuste
  necessário para SQLite.
- **Check constraints**: `ck_employee_work_logs_end_date_after_start_date`,
  `ck_work_schedule_periods_end_after_start`, `ck_month_closings_month_range`, todos
  declarados via `t.HasCheckConstraint(...)` no `ToTable(...)` — o provider Sqlite do
  EF Core suporta `HasCheckConstraint` e traduz para `CHECK (...)` na DDL do SQLite;
  deve funcionar sem alteração, mas precisa ser validado na fase de implementação
  (gerar a migration e inspecionar a DDL resultante).
- **ATENÇÃO CRÍTICA — tipos SQL nativos do Postgres inexistentes no SQLite**:
  - `EmployeeConfiguration.HireDate` usa `.HasColumnType("date")`.
  - `WorkSchedulePeriodConfiguration.StartTime`/`EndTime` usam `.HasColumnType("time")`.
  - SQLite só tem 5 "type affinities" nativas (`TEXT`, `NUMERIC`, `INTEGER`, `REAL`,
    `BLOB`); `date`/`time` não existem como tipo nativo. Passar esses `HasColumnType`
    literais para o provider Sqlite é incorreto/arriscado. É necessário decidir e
    documentar a solução (ver decisão do analista técnico no plano de desenvolvimento).
- **`DateTimeOffset`** (`CreatedAtUtc`/`UpdatedAtUtc` em todas as entidades,
  `StartDate`/`EndDate` em `EmployeeWorkLog`): hoje sem `HasColumnType` explícito. O
  incidente registrado nesta sessão de trabalho ("Npgsql só aceita `DateTimeOffset` com
  `Offset=0`") é uma restrição específica do provider Npgsql — o provider Sqlite do EF
  Core grava `DateTimeOffset` como texto ISO-8601 preservando o offset armazenado, sem
  essa restrição. `BusinessTimeZone.cs` (`api/src/WorkLogManager.Application/Services`)
  já centraliza a normalização para UTC antes de persistir (`ToInstant(...)` sempre
  retorna `.ToUniversalTime()`, offset zero) — isso deve ser mantido como boa prática
  de "sempre persistir em UTC" independentemente do provider, mesmo que a restrição
  técnica original (Npgsql) deixe de existir.
- **Índices únicos compostos**: `MonthClosingConfiguration` (`Year`+`Month`, único) e
  `SystemParameterConfiguration` (`Param`+`Value`, único) são suportados igualmente
  pelo provider Sqlite, sem ajuste necessário.
- **`SystemParameterConfiguration.Value`** usa `.HasColumnType("text")` — `TEXT` é uma
  affinity nativa do SQLite, esse mapeamento continua válido sem alteração.
- **Testes**: nenhum teste de integração hoje (`api/tests/WorkLogManager.*.Tests`)
  depende de comportamento específico do provider Postgres; não há testes de
  `Infrastructure` que toquem `DbContext`/repositórios contra um banco real.
- **`.claude/docs/stack.md`** referencia PostgreSQL explicitamente no título e na
  seção final — precisa ser atualizado para refletir SQLite.
- **Connection strings atuais** (`appsettings.json`/`appsettings.Development.json`,
  `api/src/WorkLogManager.Api`) usam formato host/porta/usuário/senha do Npgsql — não
  fazem sentido para SQLite, que usa caminho de arquivo (`Data Source=...`).

## Impacto no plano `win-installer` já existente

O plano de desenvolvimento anterior (v1, `plano-desenvolvimento.md`, nunca aprovado)
previa embutir e provisionar um instalador do PostgreSQL (EDB) como parte do processo
de instalação Windows. Essa decisão de produto foi revertida: não há mais
PostgreSQL a provisionar. As seções do plano relacionadas (2.3, 2.4, 5, 6, 9) precisam
ser reescritas para refletir SQLite. As demais decisões (Serviço do Windows via
`UseWindowsService()`, processo único servindo front-end, Inno Setup, auto-migrate via
`Database.Migrate()` no startup, pipeline manual de build/release) permanecem válidas.

---
Gerado pelo agente `po`, complementar a `report-po.md`, a partir de decisões do usuário
e investigação técnica sobre o código atual em `api/src/WorkLogManager.Infrastructure`.

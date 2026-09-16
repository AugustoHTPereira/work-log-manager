# Code Review — `win-installer` (plano v2.1)

Revisor: agente `revisor`. Baseado em `git diff` contra `HEAD` (commit `2f009c9`), execução
real de `dotnet test`, `npm run build` e `npx vitest run`, e leitura integral dos arquivos
novos/alterados em `api/`, `app/` e `installer/`.

## 1. Resumo do veredito

**Aprovado.**

A implementação segue o plano v2.1 com fidelidade alta. A troca de provider
PostgreSQL → SQLite está correta, testada e sem regressões. A migration `InitialCreate`
bate exatamente com a especificação da seção 3.2 do plano. O `[UninstallDelete]` do `.iss`
reflete corretamente a decisão pós-aprovação v2.1 (remove tudo, incluindo o `.db`). Os
desvios documentados no resumo de implementação são pequenos, justificados e não
alteram o resultado funcional esperado. Não há lógica de negócio nova, nem violação de
camadas, nem nomes em português. Os achados abaixo são só sugestões/nitpicks — nenhum é
bloqueante.

## 2. Verificação dos testes (rodados por mim, não só conferidos no resumo)

- `dotnet test` (a partir de `api/`):
  - `WorkLogManager.Application.Tests`: **167/167** ✅
  - `WorkLogManager.Infrastructure.Tests`: **6/6** ✅ (inclui os 5 casos novos de
    `DatabaseMigrationTests`, um por tabela)
  - `WorkLogManager.Api.Tests`: **9/10** ✅ — única falha é
    `AutoMapperConfigurationTests.Configuration_AllProfiles_AreValid` (propriedade
    `Summaries` não mapeada em `MonthClosingResponse`). Confirmei via `git show
    HEAD:...MonthClosingResponse.cs` que a propriedade `Summaries` já existia em `HEAD`,
    antes de qualquer mudança desta task, e que nenhum arquivo relacionado a esse mapper
    foi tocado no diff — falha genuinamente pré-existente, não uma regressão.
  - Resultado bate exatamente com o alegado no `resumo-implementacao.md`.
- `npm run build` (front-end): falha com os dois erros de TypeScript pré-existentes
  (`EmployeeDetailPage.tsx` — `intervalToDuration` não usado; `use-param.ts` —
  incompatibilidade de tipo). Confirmei via `git log` que nenhum desses dois arquivos foi
  tocado por esta implementação (último commit que os tocou é anterior a esta task) —
  confirmado como pré-existente, não regressão.
- `npx vitest run`: **82/91** ✅ — as 9 falhas em `EmployeeListPage.test.tsx` (elemento
  "novo funcionário" não encontrado) e `workLogFilters.test.ts` (`origin` divergente)
  são exatamente as falhas pré-existentes documentadas; `git log` confirma que nenhum
  desses arquivos de teste foi tocado por esta implementação.
- Bônus: rodei `dotnet list ... package --vulnerable --include-transitive` no projeto
  `Infrastructure` para confirmar que o pin de `SQLitePCLRaw.lib.e_sqlite3` 3.53.3
  realmente elimina a vulnerabilidade transitiva (GHSA-2m69-gcr7-jv3q/CVE-2025-6965) —
  confirmado: "nenhum pacote vulnerável" após o pin.

## 3. Aderência ao plano — ponto a ponto

### 3.1 Troca de provider (SQLite)
- `WorkLogManager.Infrastructure.csproj`: `Npgsql.EntityFrameworkCore.PostgreSQL`
  removido, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.4 adicionado — conforme plano.
  O pin extra de `SQLitePCLRaw.lib.e_sqlite3` 3.53.3 não estava no plano literal, mas é
  uma mitigação de segurança razoável e correta, seguindo o mesmo padrão já
  estabelecido no repositório para `AutoMapper`/`Microsoft.AspNetCore.OpenApi`
  (comentário `NU1903` explicando o motivo). Validado como efetivo (seção 2 acima).
- `EmployeeConfiguration.HireDate` e `WorkSchedulePeriodConfiguration.StartTime`/
  `EndTime`: `.HasColumnType("date")`/`.HasColumnType("time")` removidos corretamente,
  `.IsRequired()` mantido — exatamente como especificado.
- `Program.cs`: `UseNpgsql(...)` → `UseSqlite(...)`, `.UseSnakeCaseNamingConvention()`
  mantido encadeado — correto.
- `appsettings.json` → `"Data Source=worklogmanager.db"`;
  `appsettings.Development.json` → `"Data Source=App_Data/worklogmanager-dev.db"` —
  conforme seção 2.4/passo 5-6 do plano. `App_Data/.gitkeep` criado, pasta vazia (sem
  `.db` esquecido), `.gitignore` raiz com `api/src/WorkLogManager.Api/App_Data/*.db`.

### 3.2 Migration `InitialCreate`
- As 6 migrations Postgres antigas (12 arquivos) foram removidas; uma migration nova
  única (`20260916015525_InitialCreate`) foi gerada e é a única presente.
- Conferi o conteúdo linha a linha contra a especificação da seção 3.2 do plano:
  todas as 5 tabelas presentes (`employees`, `employee_work_logs`,
  `work_schedule_periods`, `month_closings`, `system_parameters`), tipos de coluna
  (`TEXT`/`INTEGER` conforme esperado; `DateOnly`/`TimeOnly`/`DateTimeOffset` mapeados
  via conversão padrão do provider, sem `HasColumnType` residual do Postgres), todas as
  3 check constraints (`ck_employee_work_logs_end_date_after_start_date`,
  `ck_work_schedule_periods_end_after_start`, `ck_month_closings_month_range`), FKs com
  `onDelete` corretos (cascade `employee_work_logs`→`employees` e
  `work_schedule_periods`→`employees`; restrict `employee_work_logs`→`month_closings`),
  e todos os índices esperados (simples em `employee_id`/`month_closing_id`, únicos
  compostos em `year+month` e `param+value`, composto não-único em
  `employee_id+day_of_week`). Nenhuma tabela `system_settings`/`daily_work_hours`
  residual. Bate exatamente com a spec.
- Nota (não é regressão desta task): `work_schedule_periods.employee_id` está
  `nullable: true` na migration, mesmo aparecendo como FK "cascade delete" na
  redação da seção 3.2 do plano (que não afirma explicitamente not-null). Confirmei
  via `git show` que essa nulidade já existia na migration Postgres anterior
  (`ReplaceDailyWorkHoursWithWorkSchedulePeriods`) e na configuração atual
  (`builder.Property(p => p.EmployeeId)` sem `.IsRequired()`) — comportamento herdado,
  não introduzido por esta task. Fica registrado como observação, não como achado.
- **Regra de nunca aplicar a migration a um banco real**: resumo alega ter testado
  `Database.Migrate()` contra um arquivo SQLite descartável em pasta temporária, com
  limpeza ao final. Não há nenhum arquivo `.db`/`.db-shm`/`.db-wal` esquecido em
  `App_Data/` nem em qualquer lugar rastreado pelo git — confirmado por inspeção direta
  do diretório. Nenhuma evidência de banco compartilhado/real tocado. A regra da sessão
  (arquivo SQLite descartável = permitido) foi respeitada.
- **Namespace/pasta das migrations**: `Persistence/Migrations/`, namespace
  `WorkLogManager.Infrastructure.Persistence.Migrations` — confirmado nos dois arquivos
  novos e no snapshot, consistente com a convenção original do projeto (namespace =
  caminho de pasta). Desvio documentado no resumo (geração inicial em `Migrations/` na
  raiz, movida manualmente) é plausível e o resultado final está correto.

### 3.3 `Program.cs` — Windows Service / SPA hosting / auto-migrate
- `builder.Host.UseWindowsService();` logo após `CreateBuilder` — correto, no-op fora
  de serviço Windows.
- Bloco de `dbContext.Database.Migrate()` em `using (var scope = ...)`, logo após
  `builder.Build()` e antes de qualquer middleware — correto, migra antes de aceitar
  requisições.
- `app.UseDefaultFiles(); app.UseStaticFiles();` registrados antes do mapeamento dos
  endpoints de API; `app.MapFallbackToFile("index.html");` registrado **depois** do
  mapeamento de todos os endpoints de API — ordem correta: rotas de API são resolvidas
  antes do fallback, então uma rota de API existente nunca cai no fallback SPA. Validado
  também pelos testes automatizados (`SinglePageAppHostingTests`) e pela validação
  manual do desenvolvedor (`curl` contra `/employees`, `/some/client/route`,
  `/favicon.svg`).
- `WorkLogManager.Api.csproj`: `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>` e
  `Microsoft.Extensions.Hosting.WindowsServices` 10.0.4 adicionados — conforme plano.
  Nenhum `PackageReference` direto a `Microsoft.EntityFrameworkCore.Sqlite` foi
  adicionado à `Api` (mantém o padrão de camadas já existente: a `Api` só referencia o
  provider transitivamente via `Infrastructure`, igual ao que já acontecia com
  `UseNpgsql` antes desta task) — sem violação de camadas.

### 3.4 Testes novos
- `DatabaseMigrationTests` (Infrastructure.Tests): 5 casos parametrizados, um por
  tabela esperada, cada um rodando `Database.Migrate()` contra um arquivo SQLite
  temporário real e consultando `sqlite_master` para confirmar a existência da tabela.
  Exercita o comportamento real (não é um teste vazio/trivial) — se qualquer tabela
  faltasse na migration, o teste falharia de verdade. `Dispose()` limpa o arquivo e
  fecha o pool de conexões corretamente.
- `SinglePageAppHostingTests` (Api.Tests): usa `WebApplicationFactory<Program>` com
  `ContentRoot` apontando para uma pasta temporária com um `wwwroot/index.html` stub e
  connection string sobrescrita para um arquivo SQLite temporário. Dois casos: endpoint
  de API real (`GET /employees`) retorna JSON (confirma que o fallback não intercepta
  rotas de API), e uma rota não-API sem arquivo correspondente cai no `index.html` do
  stub (confirma o fallback SPA). Ambos exercitam comportamento real de roteamento, não
  "passam por não falhar" — um erro de ordem no `Program.cs` (ex. `MapFallbackToFile`
  antes dos endpoints) faria o primeiro teste falhar de verdade.

### 3.5 Instalador (`installer/`) — revisão por leitura (não compilável neste ambiente)
- `WorkLogManager.iss`:
  - `[Setup]`: `AppId` fixo, `PrivilegesRequired=admin`, `DefaultDirName={autopf}\...` —
    conforme plano.
  - `[Dirs]`: cria `{commonappdata}\WorkLogManager` — conforme plano.
  - `[Code]`: `IsUpgrade()`/`StopAndRemoveExistingService()` detecta serviço existente
    via `sc.exe query` e para/remove antes de `[Files]` sobrescrever o executável,
    evitando lock de arquivo — coerente com a seção 5 do plano.
  - `[Run]`: `sc.exe create` com `binPath=`/`start= auto`/`DisplayName=` corretamente
    escapado (aspas Pascal duplicadas, documentado em comentário); `reg.exe add` grava
    `ASPNETCORE_ENVIRONMENT=Production` como `REG_MULTI_SZ` na chave de ambiente do
    serviço; `net.exe start` inicia o serviço — bate com a seção 5.
  - `[UninstallRun]`: para e remove o serviço.
  - **`[UninstallDelete]` — ponto de atenção explícito do pedido de revisão**: contém
    `Type: filesandordirs; Name: "{app}"` **e**
    `Type: filesandordirs; Name: "{commonappdata}\{#MyAppName}"`, removendo o diretório
    de instalação inteiro **e** a pasta `ProgramData` (que contém
    `worklogmanager.db`), com comentário explícito no `.iss` referenciando a decisão
    v2.1 e a "seção 12" do plano. **Confirmado: reflete corretamente a decisão pós-
    aprovação v2.1** (remove tudo, sem preservar o banco) — não há nenhum resquício da
    lógica antiga de preservação do `.db` no uninstall.
- `build.ps1`: publica a API self-contained `win-x64`, builda o front-end (`npm ci &&
  npm run build`), copia `dist/` para `wwwroot/` do publish, localiza `ISCC.exe`
  (PATH ou caminho padrão) e compila o `.iss`, passando a versão via
  `WORKLOGMANAGER_VERSION`. Sintaxe PowerShell coerente, tratamento de erro via
  `$LASTEXITCODE`/`throw` em cada etapa. Coerente com o plano.
- `appsettings.Production.json`: arquivo estático com a connection string fixa
  apontando para `%ProgramData%\WorkLogManager\worklogmanager.db`, copiado via
  `[Files]` em vez de gerado em `[Run]`. Desvio pequeno e bem justificado no resumo
  (a seção 2.4 do plano já descrevia o valor como estático/fixo; gerar JSON multi-linha
  via `echo`/`cmd.exe` seria mais frágil) — resultado funcional idêntico ao
  especificado, decisão de engenharia razoável.
- `README.md`: pré-requisitos, passo a passo de build, comportamento documentado de
  fresh install/upgrade/uninstall (já refletindo a decisão v2.1), plano de teste manual
  em Windows, e nota explícita sobre a limitação de não poder compilar neste ambiente.
  Claro e coerente com o restante do pacote.
- `.gitignore` (raiz e local): `installer/publish/`, `installer/output/` ignorados,
  sem `installer/vendor/` residual (correto — não há mais binário de terceiros).

### 3.6 `.claude/docs/stack.md`
Título e seção final atualizados de PostgreSQL para SQLite, mantendo a convenção
snake_case/plural; texto novo documenta a decisão de não usar `HasColumnType`
específico de outro provider e a localização do arquivo de banco fora do controle de
versão (dev: `App_Data/`; produção: `%ProgramData%\WorkLogManager\`). Consistente com o
plano (seção 9).

### 3.7 Front-end
`app/.env.production` criado com `VITE_API_BASE_URL=` vazio — única mudança em `app/`,
exatamente conforme a seção 4 do plano ("sem alteração" além disso). Nenhum componente,
hook ou tela tocados.

## 4. Camadas / SOLID / DDD / convenções

- **Camadas**: `Api` não ganhou lógica de negócio nova; a única lógica adicionada
  (`Database.Migrate()`, `UseWindowsService()`, `UseStaticFiles`/`MapFallbackToFile`) é
  puramente de infraestrutura de hosting, não de domínio, e já era prevista/aprovada no
  plano como composição do `Program.cs` (padrão já existente no projeto para esse tipo
  de bootstrap). `Infrastructure` só trocou o provider EF Core e ajustou mapeamento —
  não referencia `Api`. `Application` não foi tocada. Sem violação de camadas.
- **SOLID/DDD**: nenhuma entidade, use-case ou regra de negócio alterada — escopo
  estritamente de infraestrutura de persistência/empacotamento, como o plano exigia
  (seção 11, "fora de escopo").
- **EF Core**: mapeamento snake_case preservado (`EFCore.NamingConventions`,
  inalterado); `HasColumnType` Postgres-específico removido corretamente nos dois
  únicos pontos afetados; migration gerada, inspecionada e **não aplicada** a nenhum
  banco real, coerente com a spec da seção 3.2.
- **Idioma**: nenhuma classe/método/variável nova em português — `DatabaseMigrationTests`,
  `SinglePageAppHostingTests`, `SinglePageAppWebApplicationFactory`, `IsUpgrade`,
  `StopAndRemoveExistingService`, etc. — todos em inglês, consistente com o resto do
  código-base.

## 5. Achados

Nenhum achado **Bloqueante**.

### Sugestões
1. **`SinglePageAppHostingTests` cria um `WorkLogManagerDbContext` real contra um
   arquivo SQLite temporário via `WebApplicationFactory`, mas não limpa arquivos
   auxiliares `-wal`/`-shm` do SQLite no `Dispose()`** (só remove o `.db` principal). Em
   execução local isso não vaza nada crítico (pasta temp do SO), mas por consistência
   com `DatabaseMigrationTests` (que também não limpa `-wal`/`-shm`, mas usa
   `ClearAllPools()` antes), considere fechar/dar flush explícito da conexão do
   `WebApplicationFactory` antes de deletar o arquivo, para evitar deixar artefatos
   `-wal`/`-shm` órfãos em execuções repetidas locais. Não bloqueante — não afeta CI/
   corretude do teste em si.
2. O pin de `SQLitePCLRaw.lib.e_sqlite3` 3.53.3 é um desvio não previsto literalmente
   no plano; está bem justificado e documentado, mas fica registrado aqui para
   rastreabilidade formal — nenhuma ação necessária, só uma nota para o histórico da
   task.

### Nitpicks
1. Em `WorkLogManager.iss`, o comentário do `[UninstallDelete]` referencia "seção 12"
   do plano por nome — se o plano for renumerado no futuro, o comentário pode ficar
   desatualizado. Cosmético, sem impacto funcional.
2. `installer/build.ps1` usa `$Iscc = Get-Command "ISCC.exe" ...` e depois reatribui
   `$Iscc = $Iscc.Source` dentro do próprio `if`/`else` — funciona, mas poderia usar um
   nome de variável separado (`$IsccPath`) para maior clareza. Estilo, não corretude.

## 6. Conclusão

**Aprovado.** Implementação fiel ao plano v2.1 em ambas as frentes (troca de provider
testável e instalador Windows não testável neste ambiente). Testes automatizados
existentes continuam verdes nas mesmas proporções alegadas pelo desenvolvedor, com as
mesmas falhas pré-existentes confirmadas por mim via `git log`/`git show` como
anteriores a esta task, sem nenhuma regressão introduzida. Nenhuma violação de
camadas/SOLID/DDD/idioma encontrada. A decisão de uninstall v2.1 (remover tudo,
incluindo o banco) está corretamente refletida no `.iss`.

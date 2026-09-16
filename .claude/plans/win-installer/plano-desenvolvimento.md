# Plano de desenvolvimento — win-installer (v2 — SQLite)

Task-id: `win-installer`

> **Versão 2 deste plano.** Substitui a versão anterior (v1), que nunca foi aprovada
> nem implementada, e que previa provisionar um PostgreSQL local embutido no instalador
> Windows. O usuário decidiu trocar o provider de banco de dados de PostgreSQL por
> SQLite em **todo o projeto** (não só no artefato do instalador). Ver
> `po-sqlite.md` (complementar a `report-po.md`) para o report do PO desta
> revisão.

### Changelog v1 → v2

- **Provider de banco de dados**: PostgreSQL (Npgsql) → SQLite, em desenvolvimento e em
  produção. Uma única connection string por ambiente, um único conjunto de migrations.
- **Removido inteiramente**: provisionamento do PostgreSQL pelo instalador (instalador
  EDB embutido, serviço Windows separado do banco, porta fixa `5433`, superusuário/
  senha fixos, `installer/vendor/postgresql-windows-x64.exe`).
- **Migrations**: as 6 migrations PostgreSQL existentes são apagadas; uma migration
  `InitialCreate` nova é gerada para SQLite a partir do modelo atual acumulado. Sem
  migração retroativa de dados (não há banco de produção real hoje).
- **Connection string**: de host/porta/usuário/senha (Npgsql) para caminho de arquivo
  (`Data Source=...`, SQLite).
- **Lógica de upgrade no instalador (Inno Setup)**: simplificada — não precisa mais
  parar/pular um serviço Postgres separado; só precisa preservar o arquivo `.db` do
  SQLite existente ao atualizar (nunca sobrescrever/apagar).
- **Tamanho estimado do instalador**: cai de ~400-500 MB (runtime .NET + instalador
  completo do Postgres) para provavelmente <150 MB (só o runtime .NET self-contained +
  front-end; o provider `Microsoft.EntityFrameworkCore.Sqlite` é leve).
- **Mapeamento EF Core**: revisão de `HasColumnType("date")`/`HasColumnType("time")`
  (tipos nativos do Postgres, inexistentes no SQLite) em `EmployeeConfiguration` e
  `WorkSchedulePeriodConfiguration`.
- Todas as demais decisões da v1 permanecem válidas e estão reincorporadas neste
  documento: Serviço do Windows via `UseWindowsService()`, processo único servindo
  front-end via `UseStaticFiles`/`MapFallbackToFile`, auto-migrate via
  `Database.Migrate()` no startup, Inno Setup como tecnologia de empacotamento,
  pipeline manual de build/release.

## 1. Resumo técnico da solução

Transformar o WorkLogManager, hoje pensado como dois processos separados (API Kestrel +
front-end servido pelo Vite dev server) sobre um PostgreSQL, em um artefato único,
self-contained, publicável como serviço do Windows, com um banco de dados **SQLite**
embutido (arquivo local, sem servidor separado), mais um instalador nativo (`.exe`,
gerado com Inno Setup) que:

1. Copia os binários publicados da API (self-contained, `win-x64`), já com o build de
   produção do front-end embutido em `wwwroot/`.
2. Registra a API como Serviço do Windows (`sc.exe create`, start automático), em vez de
   depender de a usuária abrir um terminal ou atalho manualmente.
3. Inicia o serviço — a própria aplicação, ao subir, chama `Database.Migrate()` de forma
   idempotente, criando o arquivo SQLite (se ainda não existir) e aplicando qualquer
   migration pendente antes de aceitar requisições.
4. Em atualizações (nova execução do instalador sobre uma instalação existente), para o
   serviço, substitui os binários, **preserva o arquivo `.db` já existente** (nunca o
   sobrescreve/apaga) e reinicia o serviço — o boot seguinte aplica as migrations novas
   sobre os dados já existentes.

Como não há mais processo de banco separado para provisionar, o instalador fica
significativamente mais simples e menor que a v1: não há binário de terceiros embutido,
não há segunda porta/serviço a gerenciar, não há segredo de superusuário de banco a
manter fora do repositório.

Mudança de código mínima e cirúrgica: troca do provider EF Core em
`WorkLogManager.Infrastructure.csproj`/`Program.cs`, revisão pontual de dois
`HasColumnType` Postgres-específicos, regeneração das migrations, mais os 3 blocos já
previstos na v1 em `Program.cs` (hosting como Windows Service, static files + SPA
fallback, auto-migrate no startup) + ajuste de build do front-end para gerar chamadas
relativas. Todo o empacotamento (instalador, serviço, wizard) continua vivendo fora do
código de aplicação, em uma pasta nova `installer/` na raiz do repositório, sem tocar
nas camadas `Api`/`Application`/`Infrastructure` além do estritamente necessário.

## 2. Decisões de engenheiro sênior

### 2.1 Inicialização automática — Serviço do Windows via Generic Host
**Mantido da v1, sem alteração.** `Microsoft.Extensions.Hosting.WindowsServices`
(`builder.Host.UseWindowsService()`) faz o mesmo executável Kestrel/Minimal API rodar
tanto como console (dev) quanto como serviço (produção), sem novo projeto na solution,
sem duplicar a composition root. É um no-op fora do Windows/fora de um serviço, não
quebra `dotnet run` local.

### 2.2 Tecnologia de empacotamento — Inno Setup
**Mantido da v1, sem alteração.** Inno Setup: gratuito, wizard gráfico nativo, suporta
detectar upgrade vs. instalação nova via `AppId` fixo, padrão de facto para instaladores
desktop pequenos/médios. Ver justificativa completa na v1 (preservada por referência,
sem repetir aqui o comparativo com WiX/MSI, que continua válido).

### 2.3 Banco de dados — SQLite substitui PostgreSQL por completo
**Decisão: SQLite em todos os ambientes** (desenvolvimento e produção/instalador), via
pacote `Microsoft.EntityFrameworkCore.Sqlite`, substituindo
`Npgsql.EntityFrameworkCore.PostgreSQL`.

- **Por que trocar em todo o projeto, não só no instalador**: manter dois providers
  (Postgres em dev, SQLite em produção) obrigaria a manter dois conjuntos de
  migrations/configurações Fluent API compatíveis com ambos os dialetos SQL, dobrando o
  custo de manutenção e o risco de divergência entre o que é testado localmente e o que
  roda na máquina da usuária. Rodar SQLite também em desenvolvimento elimina a
  necessidade de o desenvolvedor ter um Postgres local rodando, e garante que qualquer
  coisa testada localmente é exatamente o que a usuária final vai rodar.
- **Sem migração retroativa de dados**: não existe banco de produção real hoje. As 6
  migrations Postgres existentes (`InitialCreate`, `AddMonthClosing`,
  `ReplaceDailyWorkHoursWithWorkSchedulePeriods`, `AddNoteToEmployeeWorkLog`,
  `AddOriginToEmployeeWorkLog`, `AddSystemParameters`) e o snapshot do modelo são
  apagados; uma migration `InitialCreate` nova, específica do provider Sqlite, é gerada
  a partir do estado atual do modelo (já com todos os campos acumulados por essas 6
  migrations — `Note`, `Origin`, `SystemParameter`, `WorkSchedulePeriod`, etc.).
- **Tipos de coluna Postgres-específicos — decisão sobre `date`/`time`**: SQLite só tem
  5 "type affinities" nativas (`TEXT`, `NUMERIC`, `INTEGER`, `REAL`, `BLOB`); `date` e
  `time` (usados hoje via `HasColumnType("date")` em `EmployeeConfiguration.HireDate` e
  `HasColumnType("time")` em `WorkSchedulePeriodConfiguration.StartTime`/`EndTime`) não
  existem como tipo nativo do SQLite.
  **Decisão: remover os `HasColumnType("date")`/`HasColumnType("time")` explícitos e
  deixar o provider Sqlite usar sua conversão padrão para `DateOnly`/`TimeOnly`**
  (suportada nativamente desde EF Core 8+: grava como texto no formato ISO-8601,
  `"yyyy-MM-dd"` para `DateOnly` e `"HH:mm:ss"` para `TimeOnly`, com round-trip exato via
  `ValueConverter` embutido do provider). Justificativa: esses `HasColumnType` só
  existiam para nomear o tipo nativo do Postgres explicitamente — sem eles, o EF Core já
  infere corretamente o tipo CLR (`DateOnly`/`TimeOnly`) e aplica a conversão certa para
  qualquer provider, sem precisar de um `HasConversion` manual (que seria redundante e
  mais código para manter). Ordenação lexicográfica de texto ISO-8601 preserva a
  ordenação cronológica, então índices/ordenação continuam funcionando corretamente.
- **`DateTimeOffset`** (`CreatedAtUtc`/`UpdatedAtUtc` em todas as entidades,
  `StartDate`/`EndDate` em `EmployeeWorkLog`): sem `HasColumnType` explícito hoje, sem
  alteração necessária. O provider Sqlite grava `DateTimeOffset` como texto ISO-8601
  preservando o offset armazenado, sem a restrição do Npgsql ("`DateTimeOffset` só é
  aceito com `Offset=0`", que motivou o uso de `.ToUniversalTime()` em
  `BusinessTimeZone.ToInstant(...)`). **Decisão: manter `BusinessTimeZone.cs` como
  está**, sempre normalizando para UTC antes de persistir — não porque o SQLite exija
  (não exige), mas porque "sempre persistir instantes em UTC" é a prática correta e
  já está centralizada num único lugar; não há benefício em relaxar essa normalização
  agora, e mudar isso seria escopo desnecessário para esta task.
- **Check constraints** (`ck_employee_work_logs_end_date_after_start_date`,
  `ck_work_schedule_periods_end_after_start`, `ck_month_closings_month_range`,
  declarados via `t.HasCheckConstraint(...)` em `ToTable(...)`): o provider Sqlite
  suporta `HasCheckConstraint`, traduzindo para `CHECK (...)` nativo do SQLite. Mantidos
  sem alteração de código; validação da DDL gerada é um passo explícito da
  implementação (inspecionar o `.cs` da migration nova antes de considerar concluído).
- **`SystemParameterConfiguration.Value`** (`HasColumnType("text")`): `TEXT` é uma
  affinity nativa do SQLite — mantido sem alteração.
- **Índices únicos compostos** (`MonthClosingConfiguration` em `Year`+`Month`,
  `SystemParameterConfiguration` em `Param`+`Value`): suportados igualmente pelo
  provider Sqlite, sem alteração de código.
- **Enums como string** (`.HasConversion<string>()` em `DayOfWeek`, `Type`, `Origin`,
  `Param`, `ValueType`): portáveis, sem alteração.
- **`Guid` como chave primária**: sem tipo `uuid` nativo, o provider Sqlite armazena
  `Guid` como `TEXT` (36 caracteres) por padrão — comportamento built-in do provider,
  sem necessidade de configuração explícita.

### 2.4 Connection string / configuração na máquina de destino
**Revisado da v1 — sem PostgreSQL, sem porta/usuário/senha; caminho de arquivo.**

- **Desenvolvimento**: `api/src/WorkLogManager.Api/appsettings.Development.json` passa
  a ter `"ConnectionStrings:WorkLogManagerDb": "Data Source=App_Data/worklogmanager-dev.db"`
  — caminho relativo ao diretório de trabalho do processo (que, em `dotnet run`, é o
  diretório do projeto `WorkLogManager.Api`), dentro de uma subpasta nova
  `App_Data/` (criada automaticamente pelo SQLite ao abrir a conexão, desde que a pasta
  já exista — passo de implementação cria a pasta com um `.gitkeep` e adiciona o
  conteúdo de `App_Data/*.db` ao `.gitignore`, para não versionar o banco de
  desenvolvimento de ninguém). `appsettings.json` (valor "base", herdado em qualquer
  ambiente sem override) passa a ter o mesmo formato com um nome de arquivo genérico
  (`worklogmanager.db`), como valor padrão sensato.
- **Produção (instalador)**: o processo roda como Serviço do Windows sob a conta
  `LocalSystem` (padrão do `sc.exe create` sem `obj=`), que não tem um perfil de usuário
  "normal" — por isso `%LOCALAPPDATA%` não é apropriado (resolveria para o profile do
  usuário de sistema, não da usuária final). **Decisão: usar
  `%ProgramData%\WorkLogManager\worklogmanager.db`** (equivalente à constante
  `{commonappdata}` do Inno Setup), pasta de dados de máquina, apropriada para um
  serviço que roda independente de sessão de usuário logada. O instalador (seção
  `[Code]`/`[Dirs]` do `.iss`) cria essa pasta na instalação (se não existir) e grava um
  `appsettings.Production.json` em `{app}` com
  `"ConnectionStrings:WorkLogManagerDb": "Data Source=C:\\ProgramData\\WorkLogManager\\worklogmanager.db"`.
  O serviço do Windows continua registrado com
  `ASPNETCORE_ENVIRONMENT=Production` (mesma mecânica via registry da v1), fazendo o
  ASP.NET Core carregar esse arquivo por cima do `appsettings.json` padrão.
- Diferente da v1 (que tinha uma senha fixa de superusuário do Postgres a manter fora
  do repositório), não há mais nenhum segredo de banco a gerenciar — a connection
  string de produção é só um caminho de arquivo, sem credenciais.

### 2.5 Preservação de dados em upgrade — simplificado
**Revisado da v1.** Não há mais um serviço Postgres/diretório de dados de banco
separado a "pular" em upgrades. A única coisa a preservar é o arquivo
`%ProgramData%\WorkLogManager\worklogmanager.db`, que fica **fora** do diretório de
instalação (`{app}`, tipicamente `Program Files\WorkLogManager`) — o Inno Setup, ao
copiar os arquivos de `[Files]` para `{app}` em um upgrade, nunca toca em
`%ProgramData%`, então o arquivo do banco é preservado automaticamente, sem precisar de
lógica condicional dedicada no `[Code]` do `.iss` (diferente da v1, que precisava
detectar upgrade para decidir se rodava ou não o instalador do Postgres). A única
lógica de upgrade que permanece necessária é parar o serviço antes de sobrescrever o
executável e reiniciá-lo depois (padrão, já previsto na v1).

### 2.6 Pipeline de build/release
**Mantido da v1, sem alteração de fundo.** Manual nesta primeira versão, script
`installer/build.ps1`, automação via GitHub Actions fora de escopo. O passo de "baixar
o instalador do PostgreSQL antes do build" (`installer/README.md`, v1) é **removido**
— não há mais binário de terceiros a baixar/versionar fora do git.

## 3. Back-end — mudanças por camada

### 3.1 `Application`
Nenhuma mudança de regra de negócio, use-case ou entidade. `BusinessTimeZone.cs`
mantido como está (ver seção 2.3). Nenhuma entidade/value object novo.

### 3.2 `Infrastructure`
- **`WorkLogManager.Infrastructure.csproj`**: remover `PackageReference
  Npgsql.EntityFrameworkCore.PostgreSQL`, adicionar `PackageReference
  Microsoft.EntityFrameworkCore.Sqlite` (versão alinhada à major do
  `Microsoft.EntityFrameworkCore` já usado, `10.x`). `EFCore.NamingConventions`
  (snake_case) é agnóstico de provider — mantido sem alteração.
- **`Persistence/Configurations/EmployeeConfiguration.cs`**: remover
  `.HasColumnType("date")` da propriedade `HireDate` (mantém `.IsRequired()`).
- **`Persistence/Configurations/WorkSchedulePeriodConfiguration.cs`**: remover
  `.HasColumnType("time")` de `StartTime` e `EndTime` (mantém `.IsRequired()`).
- Demais `Configurations/*.cs` (`EmployeeWorkLogConfiguration`,
  `MonthClosingConfiguration`, `SystemParameterConfiguration`): sem alteração de
  código — check constraints, conversões de enum, índices únicos e
  `HasColumnType("text")` continuam válidos no provider Sqlite (ver seção 2.3).
- **Migrations** (`Persistence/Migrations/`): apagar os 10 arquivos das 6 migrations
  Postgres existentes (`*.cs` + `*.Designer.cs` de `InitialCreate`, `AddMonthClosing`,
  `ReplaceDailyWorkHoursWithWorkSchedulePeriods`, `AddNoteToEmployeeWorkLog`,
  `AddOriginToEmployeeWorkLog`, `AddSystemParameters`) e
  `WorkLogManagerDbContextModelSnapshot.cs`. Gerar uma migration nova única,
  `InitialCreate`, com
  `dotnet ef migrations add InitialCreate --project src/WorkLogManager.Infrastructure
  --startup-project src/WorkLogManager.Api`, a partir do modelo atual já com os
  `HasColumnType` removidos (seção 3.2 acima) — o resultado deve conter todas as
  tabelas/colunas/constraints acumuladas hoje (`employees`, `employee_work_logs`,
  `work_schedule_periods`, `month_closings`, `system_parameters`, com todos os campos
  já existentes: `Note`, `Origin`, etc.). **Regra vigente mantida**: esta migration é
  só gerada como arquivo (spec), nunca aplicada a nenhum banco por nenhum agente deste
  fluxo — `dotnet ef database update` é sempre ação manual do responsável.
  Especificação esperada da migration (para orientar a implementação/revisão):
  - `employees`: `id` (TEXT/Guid, PK), `name` (TEXT, maxlength 200, not null), `role`
    (TEXT, maxlength 200, not null), `hire_date` (TEXT/DateOnly, not null), `created_at_utc`
    (TEXT/DateTimeOffset, not null), `updated_at_utc` (TEXT/DateTimeOffset, not null).
  - `employee_work_logs`: `id` (PK), `employee_id` (FK → `employees.id`, cascade
    delete), `type` (TEXT, maxlength 20, not null), `start_date`/`end_date`
    (TEXT/DateTimeOffset, not null), `duration_seconds` (INTEGER/long, not null),
    `created_at_utc`/`updated_at_utc` (not null), `month_closing_id` (FK nullable →
    `month_closings.id`, restrict delete), `note` (TEXT, maxlength 255, nullable),
    `origin` (TEXT, maxlength 20, not null); índices em `employee_id` e
    `month_closing_id`; check constraint `end_date >= start_date`.
  - `work_schedule_periods`: `id` (PK), `employee_id` (FK → `employees.id`, cascade
    delete), `day_of_week` (TEXT, maxlength 20, not null), `start_time`/`end_time`
    (TEXT/TimeOnly, not null), `created_at_utc`/`updated_at_utc` (not null); índice
    composto `employee_id`+`day_of_week`; check constraint `end_time > start_time`.
  - `month_closings`: `id` (PK), `month`/`year` (INTEGER, not null),
    `created_at_utc`/`updated_at_utc` (not null); índice único composto
    `year`+`month`; check constraint `month >= 1 AND month <= 12`.
  - `system_parameters`: `id` (PK), `param` (TEXT, maxlength 50, not null), `value`
    (TEXT, not null), `value_type` (TEXT, maxlength 20, not null),
    `created_at_utc`/`updated_at_utc` (not null); índice único composto `param`+`value`.
  - Nenhuma tabela `system_settings`/`daily_work_hours` (substituída por
    `work_schedule_periods` já em `ReplaceDailyWorkHoursWithWorkSchedulePeriods`, v1 das
    migrations Postgres — não deve reaparecer na migration nova).

### 3.3 `Api`
- **`Program.cs`**: trocar `options.UseNpgsql(builder.Configuration.GetConnectionString("WorkLogManagerDb"))`
  por `options.UseSqlite(builder.Configuration.GetConnectionString("WorkLogManagerDb"))`
  (mesma chamada a `.UseSnakeCaseNamingConvention()` encadeada, sem alteração). Um novo
  `using Microsoft.EntityFrameworkCore;` já existe (o método `UseSqlite` é um pacote de
  extensão do próprio `Microsoft.EntityFrameworkCore.Sqlite`, referenciado pela
  `Infrastructure` e consumido pela `Api` transitivamente, igual hoje com `UseNpgsql`).
- **`appsettings.json`**: connection string trocada para
  `"Data Source=worklogmanager.db"` (valor padrão/fallback).
- **`appsettings.Development.json`**: connection string trocada para
  `"Data Source=App_Data/worklogmanager-dev.db"` (ver seção 2.4).
- Os 3 blocos já previstos na v1 (`UseWindowsService()`, `UseStaticFiles`/
  `MapFallbackToFile`, `Database.Migrate()` no startup) permanecem exatamente como
  especificado na v1 — ver seção 6 (passos de implementação) para a lista consolidada.
- **`WorkLogManager.Api.csproj`**: mantido `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>`
  (v1, sem alteração) para publish self-contained.

## 4. Front-end — mudanças em `app/`
**Sem alteração em relação à v1.** `app/.env.production` com `VITE_API_BASE_URL=`
(vazio), fazendo o axios usar `baseURL: ""` (chamadas relativas, mesma origem que serve
`index.html`). Nenhum componente, tela, hook ou componente `shadcn/ui` novo — a troca de
banco de dados é inteiramente transparente ao front-end (ele já consome a API via DTOs
JSON, sem qualquer acoplamento a Postgres/SQLite).

## 5. Instalador — novos arquivos (fora de `app/`/`api/`)

Nova pasta `installer/` na raiz do repositório (estrutura geral mantida da v1, com as
seguintes revisões):

- `installer/WorkLogManager.iss` — script Inno Setup:
  - `[Setup]`: `AppId` fixo (GUID gerado uma vez), `PrivilegesRequired=admin`
    (necessário para registrar o serviço do Windows — continua precisando de admin
    mesmo sem instalar banco separado), `DefaultDirName={autopf}\WorkLogManager`.
  - `[Dirs]`: cria `{commonappdata}\WorkLogManager` (equivalente a
    `%ProgramData%\WorkLogManager`) se não existir, com permissão de escrita para o
    serviço (conta `LocalSystem` já tem acesso total a `ProgramData` por padrão — sem
    necessidade de `icacls` adicional).
  - `[Code]`: função que detecta se já existe uma instalação anterior (verifica se o
    serviço `WorkLogManagerApi` existe) — se sim, é upgrade: para o serviço antes de
    copiar arquivos, **não mexe em `%ProgramData%\WorkLogManager\worklogmanager.db`**
    (fica fora de `{app}`, nunca é tocado pela seção `[Files]`); se não, é instalação
    nova (nenhum provisionamento de banco necessário — o próprio processo cria o
    arquivo `.db` ao rodar `Database.Migrate()` no primeiro start).
  - `[Files]`: copia `installer/publish/api/*` (saída do `dotnet publish`, já incluindo
    `wwwroot/`) para `{app}`.
  - `[Run]`:
    - grava `appsettings.Production.json` em `{app}` com a connection string SQLite
      apontando para `{commonappdata}\WorkLogManager\worklogmanager.db` (arquivo
      estático, já com o valor final — sem substituição dinâmica necessária, já que não
      há mais senha/porta gerados a incorporar).
    - `sc.exe create WorkLogManagerApi binPath= "\"{app}\WorkLogManager.Api.exe\""
      start= auto DisplayName= "WorkLogManager"`.
    - `reg add "HKLM\SYSTEM\CurrentControlSet\Services\WorkLogManagerApi\Environment"
      /t REG_MULTI_SZ /d "ASPNETCORE_ENVIRONMENT=Production"`.
    - `net start WorkLogManagerApi`.
  - `[UninstallRun]`: `net stop WorkLogManagerApi` + `sc.exe delete WorkLogManagerApi`.
  - `[UninstallDelete]`: remove `{app}` (diretório de instalação inteiro) e
    `{commonappdata}\WorkLogManager` (incluindo `worklogmanager.db`), tipo `filesandordirs`
    para ambos. **Decisão revisada nesta aprovação (v2.1)**: ao contrário da v1/v2
    original (que preservava o `.db` deliberadamente no uninstall), o usuário pediu
    explicitamente que o desinstalador remova TUDO — aplicativo instalado e banco de
    dados — sem deixar resíduo na máquina. Não há confirmação/prompt adicional além do
    fluxo padrão de desinstalação do Windows (Painel de Controle/Configurações >
    Aplicativos), que já pede confirmação antes de desinstalar.
- `installer/build.ps1` — script PowerShell de orquestração: `dotnet publish` da API
  (`win-x64`, self-contained), build do front-end e cópia para `wwwroot/`, compilação
  do `.iss` via `ISCC.exe`. **Removido** o passo da v1 de garantir que
  `installer/vendor/postgresql-windows-x64.exe` existe antes do build — não há mais
  esse binário.
- `installer/README.md` — passo a passo manual: como instalar o Inno Setup, como rodar
  `build.ps1`, como publicar manualmente no GitHub Releases. **Removido** o passo da v1
  de baixar o instalador oficial do PostgreSQL.
- `.gitignore` (raiz) — adicionar `installer/publish/`, `installer/output/`. (Não há
  mais `installer/vendor/` — nada de terceiros a ignorar.)

## 6. Passos de implementação

1. `api/src/WorkLogManager.Infrastructure/WorkLogManager.Infrastructure.csproj`: remover
   `PackageReference Npgsql.EntityFrameworkCore.PostgreSQL`, adicionar
   `PackageReference Microsoft.EntityFrameworkCore.Sqlite` (versão `10.x`).
2. `Persistence/Configurations/EmployeeConfiguration.cs`: remover
   `.HasColumnType("date")` de `HireDate`.
3. `Persistence/Configurations/WorkSchedulePeriodConfiguration.cs`: remover
   `.HasColumnType("time")` de `StartTime` e `EndTime`.
4. `api/src/WorkLogManager.Api/Program.cs`: trocar `UseNpgsql(...)` por
   `UseSqlite(...)` na configuração do `DbContext`.
5. `api/src/WorkLogManager.Api/appsettings.json`: trocar connection string para
   `"Data Source=worklogmanager.db"`.
6. `api/src/WorkLogManager.Api/appsettings.Development.json`: trocar connection string
   para `"Data Source=App_Data/worklogmanager-dev.db"`; criar
   `api/src/WorkLogManager.Api/App_Data/.gitkeep`; adicionar `App_Data/*.db` ao
   `.gitignore` da `Api` (ou ao `.gitignore` raiz, com o path completo).
7. Apagar os 10 arquivos das 6 migrations Postgres existentes (`InitialCreate`,
   `AddMonthClosing`, `ReplaceDailyWorkHoursWithWorkSchedulePeriods`,
   `AddNoteToEmployeeWorkLog`, `AddOriginToEmployeeWorkLog`, `AddSystemParameters`,
   `*.cs`+`*.Designer.cs`) e `WorkLogManagerDbContextModelSnapshot.cs`, em
   `api/src/WorkLogManager.Infrastructure/Persistence/Migrations/`.
8. Gerar a migration nova `InitialCreate` (SQLite) com `dotnet ef migrations add
   InitialCreate --project src/WorkLogManager.Infrastructure --startup-project
   src/WorkLogManager.Api`, a partir do modelo já ajustado pelos passos 2-3. Inspecionar
   o `.cs` gerado e conferir contra a especificação da seção 3.2 (tabelas, colunas,
   constraints, índices). **Não aplicar a nenhum banco.**
9. Rodar a suíte de testes existente (`dotnet test`) para garantir que nada quebrou por
   causa da troca de provider — nenhum teste hoje depende de Postgres (ver seção 8),
   mas a suíte completa deve continuar verde.
10. `Program.cs`: adicionar `builder.Host.UseWindowsService();` (requer novo
    `PackageReference Microsoft.Extensions.Hosting.WindowsServices` em
    `WorkLogManager.Api.csproj`) e `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>`.
11. `Program.cs`: adicionar `app.UseDefaultFiles(); app.UseStaticFiles();` e, ao final
    (após o mapeamento dos endpoints de API), `app.MapFallbackToFile("index.html");`.
12. `Program.cs`: adicionar o bloco de `dbContext.Database.Migrate()` em um scope, logo
    após `var app = builder.Build();`.
13. `app/.env.production`: criar com `VITE_API_BASE_URL=`.
14. Criar pasta `installer/` com `WorkLogManager.iss` (seção 5, incluindo `[Dirs]` para
    `%ProgramData%\WorkLogManager`), `build.ps1`, `README.md`, `.gitignore` local para
    `publish/`/`output/`.
15. Testar localmente (ambiente de desenvolvimento, nunca a máquina da usuária final):
    `npm run build` em `app/`, copiar `dist/` para uma pasta `wwwroot` de teste dentro
    de `api/src/WorkLogManager.Api/bin/.../publish/`, rodar a API publicada localmente
    com uma connection string SQLite apontando para um arquivo descartável, e confirmar
    que abrir `http://localhost:5244/` serve o front-end e que as chamadas de API
    funcionam na mesma origem.
16. Testar o auto-migrate contra um arquivo SQLite novo/descartável (apagar o arquivo
    `.db` de teste, rodar a API publicada, confirmar que ela cria o arquivo e aplica a
    migration `InitialCreate` sozinha ao iniciar) — trivial com SQLite (não depende de
    infraestrutura externa, ao contrário do Postgres na v1).
17. Escrever/ajustar o script `.iss` e `build.ps1` (não executável neste ambiente
    macOS/Linux — revisão de sintaxe manual e documentação de como o usuário deve rodar
    em Windows).
18. Atualizar `.claude/docs/stack.md`: trocar as referências a PostgreSQL por SQLite
    (título do documento e seção final "PostgreSQL" → "SQLite", ajustando o conteúdo —
    ver seção 9 abaixo para o texto sugerido).

## 7. Mapeamento critério de aceite → passo(s)

| Critério de aceite (report do PO, `report-po.md`) | Passos |
|---|---|
| 1. Release no GitHub com instalador identificável | Passo 14 (`installer/`), Passo 17 (`.iss`/`build.ps1`), seção 2.6 (upload manual documentado em `installer/README.md`) |
| 2. Instalação sem CLI, com wizard | Passo 14/17 — Inno Setup gera wizard gráfico nativo |
| 3. Migrations aplicadas automaticamente | Passo 12 (auto-migrate no startup) + seção 5 (`[Run]` reinicia o serviço em upgrade) |
| 4. Processo único servindo API + front-end | Passos 10-13, 15 |
| 5. Atualização preserva dados | Seção 2.5 (arquivo `.db` fica fora de `{app}`, nunca tocado pelo `[Files]` do instalador) |
| 6. Banco de dados local provisionado automaticamente | Seção 2.3 (SQLite substitui Postgres — arquivo criado pelo próprio `Database.Migrate()` no primeiro start, sem instalador de banco separado) |

Decisão adicional do usuário (esta revisão), sem critério de aceite formal do PO, mas
tratada como requisito obrigatório desta versão: **SQLite em todo o projeto (dev +
produção), sem migração retroativa de dados** — coberta pelos passos 1-9.

## 8. Estratégia de testes

- **Back-end (xUnit)**: nenhum teste hoje (`api/tests/WorkLogManager.Application.Tests`,
  `WorkLogManager.Api.Tests`, `WorkLogManager.Infrastructure.Tests`) depende de
  comportamento específico do provider Postgres — confirmado por inspeção: não há
  testes de `Infrastructure`/`DbContext`/repositório contra um banco real hoje (só
  `Reporting` em `Infrastructure.Tests`). A suíte completa deve ser rodada
  (`dotnet test`) após a troca de provider (passo 9) como regressão geral — nenhum
  teste novo é estritamente necessário para a troca em si.
  - Teste de integração novo (`WorkLogManager.Api.Tests`, mantido da v1) com
    `WebApplicationFactory<Program>` cobrindo: requisição a um endpoint de API
    existente (ex. `GET /employees`) continua retornando JSON normalmente com
    `UseStaticFiles`/`MapFallbackToFile` registrados; requisição a uma rota não-API sem
    arquivo correspondente cai no fallback e retorna `index.html` (fixture com
    `wwwroot`/`index.html` de stub). Este teste, ao usar `WebApplicationFactory`, pode
    apontar para um arquivo SQLite descartável/temporário (`Data Source=:memory:` ou um
    arquivo em pasta temp) — trivial de configurar, diferente da v1 (que não conseguia
    testar nada dependente de Postgres real neste ambiente).
- **Auto-migrate**: com SQLite, passa a ser **testável de forma barata e local**
  (diferente da v1, que dependia de um Postgres real e por isso tratava isso só como
  teste manual). Recomendado: um teste de integração (ou passo manual documentado, à
  escolha do desenvolvedor na implementação) que aponta o `DbContext` para um arquivo
  temporário novo, roda `Database.Migrate()` e confirma que todas as tabelas esperadas
  existem depois. Ainda assim, a validação "de ponta a ponta" (publicar, rodar o `.exe`,
  confirmar que ele cria o arquivo em `%ProgramData%` na primeira execução) continua
  sendo um passo manual do desenvolvedor em ambiente Windows (não neste fluxo de
  agentes).
- **Front-end**: sem alteração em relação à v1 — nenhuma lógica nova para testar, nenhum
  teste Vitest novo necessário.
- **Instalador (`.iss`/`build.ps1`)**: sem alteração em relação à v1 — fora do alcance
  de teste automatizado neste ambiente (macOS/Linux). Teste manual do usuário em
  máquina/VM Windows real, incluindo confirmar explicitamente o cenário de upgrade:
  instalar, criar alguns dados via a aplicação, rodar o instalador de novo (nova versão)
  por cima, e verificar que os dados continuam lá depois do upgrade.

## 9. `.claude/docs/stack.md` — atualização necessária

O documento tem hoje título "Stack e convenções — .NET 10 + React (shadcn) +
PostgreSQL" e uma seção final "## PostgreSQL" com convenções de nomenclatura de
tabela/coluna. Passo 18 desta implementação deve:
- Trocar o título para "... + SQLite".
- Trocar a seção final "## PostgreSQL" para "## SQLite", mantendo a convenção de
  snake_case/plural para nomes de tabela (via `EFCore.NamingConventions`, inalterado) e
  ajustando o texto para deixar de mencionar Postgres especificamente, mencionando em
  vez disso que o arquivo de banco vive fora do controle de versão (dev: pasta
  `App_Data/`; produção: `%ProgramData%\WorkLogManager\`).

## 10. Riscos e dependências

- **Ambiente de desenvolvimento é macOS/Linux**: nenhum agente deste fluxo consegue
  compilar/rodar o `.iss` do Inno Setup nem validar o `.exe` final de ponta a ponta —
  mantido da v1. A troca de banco em si (SQLite) é totalmente testável neste ambiente
  (ao contrário do provisionamento de Postgres da v1), o que reduz a superfície de risco
  específica desta revisão.
- **Tamanho do instalador cai significativamente**: sem o instalador do PostgreSQL
  (~300+ MB), a estimativa cai de ~400-500 MB (v1) para provavelmente **<150 MB**
  (runtime .NET self-contained + front-end + provider Sqlite, que é leve). Reduz
  também o tempo de download pela usuária final.
- **Concorrência de escrita do SQLite**: SQLite serializa escritas (um writer por vez);
  aceitável para esta aplicação (uso single-tenant, uma única usuária, baixíssima
  concorrência). Nenhuma ação adicional necessária, mas vale documentar como premissa
  consciente (não seria adequado para múltiplos usuários concorrentes escrevendo
  simultaneamente em alto volume).
- **Backup de dados**: diferente de um servidor Postgres (que poderia ter backup
  automatizado por ferramentas de infraestrutura), o backup do banco da usuária final
  passa a ser "copiar um arquivo" (`%ProgramData%\WorkLogManager\worklogmanager.db`) —
  mais simples operacionalmente, mas é responsabilidade manual da usuária/do
  desenvolvedor, sem mecanismo automatizado nesta task (fora de escopo, seção 11).
- **Migração a cada boot do serviço**: mantido da v1 — `Database.Migrate()` roda toda
  vez que o serviço inicia, aceitável por ser instalação single-tenant sem concorrência.
- **Sem pipeline CI**: mantido da v1 — processo de build/release 100% manual.
- **Elevação de privilégio (UAC)**: mantido da v1 (`PrivilegesRequired=admin`,
  necessário para registrar o serviço do Windows) — porém o risco/complexidade
  associado à v1 de também precisar rodar um instalador de terceiros (Postgres) em modo
  elevado deixa de existir.

## 11. Fora de escopo / não será feito

- Conteinerização/Docker (já descartado pelo usuário em decisão anterior).
- Auto-update automático.
- HTTPS/certificado — Kestrel serve em HTTP simples em `localhost`.
- Pipeline de CI/CD (GitHub Actions) para build automatizado do instalador.
- Backup/restore automatizado do arquivo SQLite — fica a critério de um processo manual
  fora deste fluxo (ex. copiar o arquivo `.db` periodicamente).
- Qualquer migração retroativa de dados de um banco PostgreSQL pré-existente — não há
  banco de produção real hoje; decisão explícita do usuário de não migrar dados.
- Tela de configuração de connection string para a usuária final — valor fixo, definido
  pelo instalador.
- Qualquer alteração de regra de negócio, use-case, entidade, endpoint ou tela
  existente além do estritamente necessário para a troca de provider — esta task
  continua sendo, em essência, infraestrutura de empacotamento/deploy + troca de
  provider de persistência.
- Autenticação/autorização — já fora de escopo do sistema como um todo.

## 12. Alterações pós-aprovação (v2.1)

Aprovado pelo usuário com uma alteração adicional em relação ao texto original do
plano: o desinstalador (`[UninstallRun]`/`[UninstallDelete]` do `.iss`) deve remover
**tudo** — o diretório de instalação inteiro E o arquivo `worklogmanager.db` em
`%ProgramData%\WorkLogManager` — em vez de preservar o banco de dados, como estava
especificado originalmente na seção 5 (`[UninstallRun]`). Ver seção 5 já atualizada
acima. Nenhuma outra mudança de escopo foi solicitada.

## Status: APROVADO (v2.1, com o ajuste de uninstall acima incorporado)

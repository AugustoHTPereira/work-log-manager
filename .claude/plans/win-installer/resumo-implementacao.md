# Resumo de implementação — win-installer (plano v2.1)

Task-id: `win-installer`. Implementação seguiu integralmente o plano aprovado em
`.claude/plans/win-installer/plano-desenvolvimento.md` (v2.1), sem scope creep.

## 1. Arquivos criados/alterados por camada

### `Infrastructure`
- `api/src/WorkLogManager.Infrastructure/WorkLogManager.Infrastructure.csproj`:
  removido `Npgsql.EntityFrameworkCore.PostgreSQL`; adicionado
  `Microsoft.EntityFrameworkCore.Sqlite` 10.0.4 e (nota de auditoria NU1903 adicional,
  não prevista literalmente no plano mas necessária para manter o build sem
  vulnerabilidade conhecida) `SQLitePCLRaw.lib.e_sqlite3` 3.53.3 pinado diretamente,
  substituindo a versão 2.1.11 transitivamente trazida pelo provider Sqlite 10.0.4, que
  tem o advisory de alta severidade GHSA-2m69-gcr7-jv3q (CVE-2025-6965). Mesmo padrão já
  usado no projeto para o pin do `Microsoft.AspNetCore.OpenApi`.
- `Persistence/Configurations/EmployeeConfiguration.cs`: removido `.HasColumnType("date")`
  de `HireDate`.
- `Persistence/Configurations/WorkSchedulePeriodConfiguration.cs`: removido
  `.HasColumnType("time")` de `StartTime`/`EndTime`.
- `Persistence/Migrations/`: apagadas as 6 migrations Postgres existentes (12 arquivos)
  e o snapshot antigo. Gerada uma migration nova `InitialCreate`
  (`20260916015525_InitialCreate.cs`/`.Designer.cs`) via
  `dotnet ef migrations add InitialCreate --project src/WorkLogManager.Infrastructure
  --startup-project src/WorkLogManager.Api`, mais `WorkLogManagerDbContextModelSnapshot.cs`
  regenerado. **Não aplicada a nenhum banco** (regra da sessão respeitada).
  - Desvio pontual necessário: a ferramenta `dotnet ef` gerou os arquivos em
    `Persistence/Migrations` do projeto Infrastructure normalmente, mas em uma pasta
    `Migrations/` na raiz do projeto (namespace `WorkLogManager.Infrastructure.Migrations`)
    em vez de `Persistence/Migrations/` — provavelmente porque não havia mais nenhuma
    migration existente para a ferramenta usar como âncora de convenção após o passo 7
    apagar tudo. Movidos manualmente para `Persistence/Migrations/` e o namespace
    ajustado para `WorkLogManager.Infrastructure.Persistence.Migrations`, mantendo a
    convenção original do projeto (namespace = caminho de pasta). Build e testes
    confirmam que a migration continua sendo descoberta corretamente.
  - Conteúdo da migration inspecionado e conferido linha a linha contra a especificação
    da seção 3.2 do plano: todas as 5 tabelas (`employees`, `employee_work_logs`,
    `work_schedule_periods`, `month_closings`, `system_parameters`), tipos de coluna
    (TEXT/INTEGER conforme esperado, `DateOnly`/`TimeOnly`/`DateTimeOffset` mapeados como
    TEXT via conversão padrão do provider), check constraints, FKs (cascade/restrict) e
    índices (únicos e compostos) batem exatamente com o esperado. Nenhuma tabela
    `system_settings`/`daily_work_hours` residual.

### `Api`
- `Program.cs`:
  - `UseNpgsql(...)` → `UseSqlite(...)`.
  - `builder.Host.UseWindowsService();` adicionado logo após `CreateBuilder`.
  - Bloco de `dbContext.Database.Migrate()` em scope dedicado, logo após `builder.Build()`.
  - `app.UseDefaultFiles(); app.UseStaticFiles();` antes do mapeamento dos endpoints;
    `app.MapFallbackToFile("index.html");` após o mapeamento dos endpoints de API.
- `WorkLogManager.Api.csproj`: adicionado `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>`
  e `PackageReference Microsoft.Extensions.Hosting.WindowsServices` 10.0.4.
- `appsettings.json`: connection string trocada para `"Data Source=worklogmanager.db"`.
- `appsettings.Development.json`: connection string trocada para
  `"Data Source=App_Data/worklogmanager-dev.db"`.
- `App_Data/.gitkeep`: criado (pasta vazia versionada, conteúdo `.db` ignorado).

### Raiz do repositório
- `.gitignore`: adicionadas as entradas `api/src/WorkLogManager.Api/App_Data/*.db` e
  `installer/publish/`/`installer/output/`.
- `.claude/docs/stack.md`: título trocado para "... + SQLite"; seção final
  "## PostgreSQL" trocada para "## SQLite", com o texto ajustado conforme a seção 9 do
  plano (menção ao arquivo de banco fora do controle de versão, `App_Data/` em dev e
  `%ProgramData%\WorkLogManager\` em produção; nota sobre não usar `HasColumnType`
  específico de outro provider).

### `app/`
- `.env.production`: criado com `VITE_API_BASE_URL=` (vazio), fazendo o client axios
  (`app/src/lib/api/client.ts`) usar `baseURL: ""` em build de produção (chamadas
  relativas à mesma origem).

### `installer/` (novo, infraestrutura de empacotamento — não compilável/testável neste
ambiente macOS/Linux, conforme escopo do plano)
- `WorkLogManager.iss`: script Inno Setup completo — `[Setup]` (AppId fixo gerado uma
  vez, `PrivilegesRequired=admin`, `DefaultDirName={autopf}\WorkLogManager`), `[Dirs]`
  (`{commonappdata}\WorkLogManager`), `[Files]` (publish output + `appsettings.
  Production.json` estático), `[Code]` (detecção de upgrade via `sc.exe query`, stop +
  delete do serviço existente antes de sobrescrever binários), `[Run]` (`sc.exe create`,
  `reg.exe add` para `ASPNETCORE_ENVIRONMENT=Production`, `net.exe start`),
  `[UninstallRun]` (stop + delete do serviço) e `[UninstallDelete]` **já com a revisão
  v2.1**: remove `{app}` inteiro E `{commonappdata}\WorkLogManager` (incluindo o `.db`),
  sem preservar nada, com comentário explícito documentando essa decisão pós-aprovação.
- `appsettings.Production.json`: arquivo estático (não gerado em runtime) com a
  connection string fixa `Data Source=C:\ProgramData\WorkLogManager\worklogmanager.db`,
  copiado via `[Files]` — conforme a seção 2.4 do plano ("sem substituição dinâmica
  necessária"). Desvio pequeno em relação ao texto literal da seção 5 (que descrevia a
  gravação do arquivo dentro de `[Run]`): optei por copiá-lo como `[Files]` estático em
  vez de gerá-lo via comando `echo`/redirecionamento em `[Run]`, porque (a) o próprio
  texto da seção 2.4 já deixa claro que o valor é estático e fixo, sem necessidade de
  geração dinâmica, e (b) gerar um JSON multi-linha via `cmd /C echo` é frágil e
  propenso a erro de escaping, o que seria pior para manutenibilidade. O resultado final
  em `{app}\appsettings.Production.json` é idêntico ao especificado.
- `build.ps1`: script PowerShell — limpa builds anteriores, `dotnet publish` da API
  (`win-x64`, self-contained), `npm ci && npm run build` do front-end, copia `dist/`
  para `wwwroot/` do publish, localiza `ISCC.exe` (PATH ou caminho padrão de instalação)
  e compila o `.iss`, passando a versão via variável de ambiente
  `WORKLOGMANAGER_VERSION` (lida pelo `.iss` via `GetEnv` do pré-processador).
- `README.md`: pré-requisitos (SDK .NET 10, Node.js, Inno Setup 6), passo a passo de
  build, descrição do comportamento do instalador (fresh install / upgrade / uninstall,
  já refletindo a decisão v2.1 de remover tudo no uninstall), passo de publicação manual
  no GitHub Releases, plano de teste manual em Windows, e nota explícita sobre a
  limitação de não poder compilar/testar neste ambiente.
- `.gitignore` (local, `installer/`): ignora `publish/` e `output/`.

## 2. Mapeamento passo do plano → arquivo(s)

| Passo (seção 6 do plano) | Arquivo(s) |
|---|---|
| 1 | `api/src/WorkLogManager.Infrastructure/WorkLogManager.Infrastructure.csproj` |
| 2 | `Persistence/Configurations/EmployeeConfiguration.cs` |
| 3 | `Persistence/Configurations/WorkSchedulePeriodConfiguration.cs` |
| 4 | `api/src/WorkLogManager.Api/Program.cs` (UseSqlite) |
| 5 | `api/src/WorkLogManager.Api/appsettings.json` |
| 6 | `appsettings.Development.json`, `App_Data/.gitkeep`, `.gitignore` |
| 7 | Exclusão das 12 arquivos de migrations Postgres |
| 8 | `Persistence/Migrations/20260916015525_InitialCreate.cs`/`.Designer.cs`, `WorkLogManagerDbContextModelSnapshot.cs` |
| 9 | `dotnet test` executado (ver seção 3 abaixo) |
| 10 | `Program.cs` (`UseWindowsService`), `WorkLogManager.Api.csproj` (pacote + RuntimeIdentifiers) |
| 11 | `Program.cs` (`UseDefaultFiles`/`UseStaticFiles`/`MapFallbackToFile`) |
| 12 | `Program.cs` (bloco `Database.Migrate()`) |
| 13 | `app/.env.production` |
| 14 | `installer/WorkLogManager.iss`, `installer/appsettings.Production.json`, `installer/build.ps1`, `installer/README.md`, `installer/.gitignore` |
| 15/16 | Testado localmente via `dotnet publish` + `npm run build` + execução manual (ver seção 4) |
| 17 | Revisão de sintaxe manual do `.iss`/`build.ps1` documentada em `installer/README.md` |
| 18 | `.claude/docs/stack.md` |

## 3. Mapeamento critério de aceite → teste(s)

| Critério de aceite | Cobertura |
|---|---|
| 3. Migrations aplicadas automaticamente | `api/tests/WorkLogManager.Infrastructure.Tests/Persistence/DatabaseMigrationTests.cs` (5 casos, um por tabela esperada: `employees`, `employee_work_logs`, `work_schedule_periods`, `month_closings`, `system_parameters` — roda `Database.Migrate()` contra um arquivo SQLite temporário e confirma que cada tabela existe via `sqlite_master`). Validado também manualmente end-to-end no passo 15/16 (ver seção 4). |
| 4. Processo único servindo API + front-end | `api/tests/WorkLogManager.Api.Tests/Hosting/SinglePageAppHostingTests.cs`: `Get_ExistingApiEndpoint_ReturnsJson` (endpoint de API existente `GET /employees` continua retornando JSON com `UseStaticFiles`/`MapFallbackToFile` registrados) e `Get_NonApiRouteWithoutMatchingFile_FallsBackToIndexHtml` (rota não-API sem arquivo correspondente cai no fallback `index.html`, usando um `wwwroot`/`index.html` de stub via `WebApplicationFactory<Program>` com `ContentRoot` e connection string sobrescritos). Validado também manualmente (seção 4). |
| 5. Atualização preserva dados | Coberto por revisão do `.iss` (arquivo `.db` fica fora de `{app}`, nunca tocado por `[Files]`) — não testável automaticamente neste ambiente; ver `installer/README.md`, seção "Manual test plan". |
| 6. Banco de dados local provisionado automaticamente | Mesmos testes de `DatabaseMigrationTests` + validação manual do passo 16 (auto-migrate cria o arquivo `.db` do zero ao iniciar a API publicada). |
| Troca de provider (decisão do usuário, sem critério formal do PO) | Toda a suíte `dotnet test` (167 + 6 + 9/10 testes, ver seção 5) roda em verde contra o provider Sqlite, sem nenhum teste dependente de comportamento específico do Postgres. |

## 4. Validação manual local (passos 15-16 do plano)

Executado neste ambiente (permitido — arquivo SQLite local descartável, não é banco de
produção real):

1. `npm run build` falhou por erros de TypeScript pré-existentes e não relacionados a
   esta task (`EmployeeDetailPage.tsx` — `intervalToDuration` não usado;
   `use-param.ts` — incompatibilidade de tipo), confirmados via `git status` como
   arquivos não tocados por esta implementação (erros já presentes na branch antes desta
   task). Para não bloquear a validação do mecanismo de hosting/SPA fallback, usei o
   `app/dist/` já existente no repositório (build anterior, válido) como front-end de
   teste.
2. `dotnet publish api/src/WorkLogManager.Api -c Release -o <pasta temporária>` —
   publicação bem-sucedida (build framework-dependente, sem `-r win-x64`, já que o
   binário `win-x64` não pode ser executado neste ambiente macOS; o objetivo aqui é
   validar o comportamento de hosting/migração, não o publish self-contained Windows em
   si, que é uma etapa mecânica do `dotnet publish` já coberta por `build.ps1`).
3. Copiado `app/dist/*` para `<publish>/wwwroot/`.
4. Executado o `.dll` publicado com `ASPNETCORE_ENVIRONMENT=Production` e
   `ConnectionStrings__WorkLogManagerDb` apontando para um arquivo `.db` novo/descartável
   em pasta temporária.
5. Log confirmou: `Database.Migrate()` criou o arquivo do zero, aplicou a migration
   `InitialCreate` (todas as 5 tabelas, constraints e índices, visíveis no log de SQL
   gerado pelo EF Core) — sozinho, sem nenhuma ação manual além de iniciar o processo.
6. `curl` confirmou: `GET /` retorna `index.html` (`text/html`, 200); `GET /some/client/
   route` (rota inexistente, sem arquivo correspondente) cai no fallback e também
   retorna `index.html` (SPA fallback funcionando); `GET /employees` retorna `[]`
   (`application/json`, 200 — endpoint de API real funcionando na mesma origem);
   `GET /favicon.svg` retorna 200 (asset estático servido).
7. Processo encerrado e todos os artefatos temporários (publish, `.db`, `.db-wal`,
   `.db-shm`, log) removidos ao final — nada ficou no repositório nem em `/tmp` além do
   scratchpad da sessão, que também foi limpo.

## 5. Resultado dos testes automatizados

- `dotnet test` (back-end, suíte completa):
  - `WorkLogManager.Application.Tests`: 167/167 aprovados.
  - `WorkLogManager.Infrastructure.Tests`: 6/6 aprovados (1 pré-existente de
    `MonthClosingReportRendererTests` + 5 novos de `DatabaseMigrationTests`).
  - `WorkLogManager.Api.Tests`: 9/10 aprovados — a única falha é a pré-existente
    `AutoMapperConfigurationTests.Configuration_AllProfiles_AreValid` (propriedade
    `Summaries` não mapeada em `MonthClosingResponse`), já conhecida e fora de escopo
    desta task, confirmada inalterada em relação ao estado anterior (mesma falha, mesma
    causa, nenhuma regressão introduzida). Os 2 novos testes de
    `SinglePageAppHostingTests` passam.
- `npm run build` (front-end): falha por erros de TypeScript pré-existentes e não
  relacionados a esta task (`EmployeeDetailPage.tsx`, `use-param.ts`), confirmados via
  `git status` como não tocados por esta implementação — fora de escopo, não corrigidos
  conforme instrução de não piorar/corrigir falhas pré-existentes não listadas
  explicitamente no plano.
- `npx vitest run` (front-end): 82/91 aprovados — as 9 falhas em 3 arquivos
  (`EmployeeListPage.test.tsx`, `workLogFilters.test.ts`, presumivelmente
  `EmployeeDetailPage.test.tsx`) são exatamente as falhas pré-existentes conhecidas e
  documentadas como fora de escopo desta task; nenhuma falha nova introduzida.

## 6. Desvios do plano e justificativas

1. **Pin adicional de `SQLitePCLRaw.lib.e_sqlite3` 3.53.3** no
   `WorkLogManager.Infrastructure.csproj`, não mencionado literalmente no plano.
   Justificativa: o pacote `Microsoft.EntityFrameworkCore.Sqlite` 10.0.4 (versão exigida
   pelo plano) traz transitivamente `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, que dispara
   `NU1903` por uma vulnerabilidade de alta severidade conhecida (CVE-2025-6965). Segui o
   mesmo padrão de mitigação já usado no projeto (ex. pin de `Microsoft.AspNetCore.OpenApi`
   por motivo idêntico, documentado no `WorkLogManager.Api.csproj`), com comentário
   explicativo no `.csproj`.
2. **Localização dos arquivos de migration**: `dotnet ef migrations add` gerou os
   arquivos em `Migrations/` (raiz do projeto Infrastructure) em vez de
   `Persistence/Migrations/`, provavelmente por não haver mais nenhuma migration restante
   para servir de âncora de convenção de pasta após o passo 7. Movidos manualmente de
   volta para `Persistence/Migrations/` e namespace ajustado, sem alteração de conteúdo
   funcional — mantém a convenção original do projeto.
3. **`appsettings.Production.json` como arquivo estático copiado via `[Files]`**, em vez
   de gerado via comando em `[Run]` (a leitura literal da seção 5 do plano poderia
   sugerir geração em `[Run]`, mas a seção 2.4 já deixa explícito que é "arquivo
   estático, já com o valor final — sem substituição dinâmica necessária"). Optei pela
   abordagem mais simples e robusta (cópia de arquivo estático) em vez de um comando
   `echo`/redirecionamento frágil para gerar JSON multi-linha via `cmd.exe`. Resultado
   funcional idêntico ao especificado.
4. Nenhum outro desvio de escopo. Nenhum item do plano foi implementado além do
   especificado; nenhuma migration foi aplicada a banco real; nenhum comando `.iss`
   foi compilado/executado neste ambiente.

## 7. Adição pós-aprovação: automação via GitHub Actions

Solicitação direta do usuário (fora do plano original, que marcava a automação como
"melhoria futura fora de escopo" — ver seção 12 do plano): criar um workflow de GitHub
Actions que gere o instalador `.exe` automaticamente ao publicar uma release no GitHub e
anexe o resultado como asset dessa release.

### Investigação prévia
- `installer/build.ps1` já aceitava `-Version` e usava `npm ci && npm run build` (não
  `pnpm`, apesar de o repositório também versionar `app/pnpm-lock.yaml` além do
  `app/package-lock.json`) — mantive `npm`/`package-lock.json` no workflow para ficar
  consistente com o `build.ps1` já aprovado, em vez de trocar de gerenciador de pacote
  sem necessidade (mudança fora do pedido, teria sido scope creep).
- `installer/WorkLogManager.iss` lê a versão de `GetEnv("WORKLOGMANAGER_VERSION")`, já
  setada pelo `build.ps1` a partir do parâmetro `-Version` — nenhuma mudança necessária
  no `.iss`.
- Confirmado `TargetFramework net10.0` em todos os `.csproj` do back-end.
- Confirmado que não havia `.github/` no repositório antes desta mudança.

### Arquivos criados/alterados
- `.github/workflows/build-installer.yml` (novo): workflow `Build Windows Installer`.
  - Gatilhos: `release: types: [published]` (principal) e `workflow_dispatch` (manual,
    com input opcional `version`, default `0.0.0-dev`, para testar o pipeline sem
    publicar uma release real).
  - `runs-on: windows-latest` (obrigatório — `ISCC.exe`/Inno Setup e o `.exe` gerado só
    existem em Windows).
  - `permissions: contents: write` (necessário para o upload de asset na release).
  - Passos: `actions/checkout@v4` → `actions/setup-dotnet@v4` (`dotnet-version:
    "10.0.x"`) → `actions/setup-node@v4` (`node-version: "22"`, cache `npm` com
    `cache-dependency-path: app/package-lock.json`) → instalação do Inno Setup via
    `choco install innosetup --no-progress -y` → passo "Determine installer version"
    (deriva a versão da tag da release, removendo um `v` inicial se presente, ex. `v1.0.0`
    → `1.0.0`; usa o input manual quando disparado via `workflow_dispatch`) → execução de
    `./installer/build.ps1 -Version <versão resolvida>` → upload do `.exe` como
    artifact do workflow (`actions/upload-artifact@v4`, sempre) → anexo do `.exe` à
    release via `softprops/action-gh-release@v2` (só quando `github.event_name ==
    'release'`).
- `installer/README.md`: nova seção "Publishing a release (automated)" descrevendo o
  fluxo via Actions; a seção antiga de publicação manual foi preservada, renomeada para
  "Publishing a release (manual fallback)" e reposicionada como alternativa; texto
  inicial do arquivo e a seção final de limitações conhecidas atualizados para
  mencionar o novo workflow.
- Nenhuma alteração em `installer/build.ps1` ou `installer/WorkLogManager.iss` foi
  necessária — ambos já suportavam o parâmetro de versão e rodam corretamente a partir
  de qualquer diretório de trabalho (`$PSScriptRoot`/caminhos relativos ao script, não
  ao cwd do runner).

### Decisões e desvios
- Não troquei `npm` por `pnpm` no `build.ps1`/workflow apesar de `app/pnpm-lock.yaml`
  existir no repositório: o pedido não pediu explicitamente essa troca, e `build.ps1`
  (já aprovado em task anterior) usa `npm ci`, que funciona corretamente com o
  `package-lock.json` também versionado. Trocar o gerenciador de pacote seria uma
  mudança de escopo maior, não solicitada; se o projeto de fato usar pnpm como padrão,
  isso deveria ser tratado como um plano/ajuste à parte.
- Versão do Node fixada em `22` (LTS ativa) — não há `.nvmrc`/`engines`/`packageManager`
  no repositório indicando uma versão específica; ajustável facilmente no workflow se
  o time definir uma versão-alvo diferente.
- `actions/setup-dotnet@v4` com `dotnet-version: "10.0.x"` — compatível com
  `net10.0` (GA) usado em todos os `.csproj`.

### Limitação de validação (documentada também no `installer/README.md`)
Assim como o `.iss`/`build.ps1` na implementação original, este workflow **não pôde ser
executado nem testado neste ambiente**: não há como disparar GitHub Actions localmente,
nem acesso a um runner `windows-latest` real, nem uma release real para disparar o
evento `release: published`. A sintaxe YAML foi validada com `python3 -c "import yaml;
yaml.safe_load(...)"` (parse bem-sucedido), e o conteúdo foi revisado manualmente
linha a linha contra a documentação das actions usadas (`actions/checkout`,
`actions/setup-dotnet`, `actions/setup-node`, `actions/upload-artifact`,
`softprops/action-gh-release`) e do comando `choco install innosetup`. O primeiro
disparo real (seja via publicação de uma release, seja via `workflow_dispatch` manual)
deve ser acompanhado de perto pelos logs da aba Actions do GitHub antes de confiar no
pipeline para uma release de produção.

## 8. Ajuste pós-aprovação: porta explícita (5000) + atalhos de abertura

Solicitação direta do usuário (fora do plano original): o instalador nunca configurava
explicitamente a porta em que a API/front-end ficam disponíveis quando instalados como
Serviço do Windows. `launchSettings.json` (5244/7119) só vale para `dotnet run`; sem
`ASPNETCORE_URLS`, o Kestrel cairia no default implícito `http://localhost:5000`. Decisão
confirmada pelo usuário: manter a porta 5000, mas torná-la explícita, e adicionar atalhos
(Área de Trabalho + Menu Iniciar) que abrem o navegador direto em
`http://localhost:5000`, sem exigir que a usuária final saiba/digite um endereço.

### Arquivos alterados
- `installer/WorkLogManager.iss`:
  - **`[Run]` — bug pré-existente corrigido junto com o pedido**: a linha `reg.exe add`
    original apontava para a chave
    `HKLM\SYSTEM\CurrentControlSet\Services\WorkLogManagerApi\Environment` (tratando
    `Environment` como *subchave*) e setava seu valor padrão (sem `/v`). O Service
    Control Manager do Windows não lê essa localização — ele lê um **valor nomeado**
    `Environment` (REG_MULTI_SZ) diretamente na chave do próprio serviço
    (`HKLM\...\Services\WorkLogManagerApi`, valor `Environment`). Ou seja,
    `ASPNETCORE_ENVIRONMENT=Production` provavelmente nunca era de fato aplicado ao
    processo do serviço. Corrigido para `reg.exe add "HKLM\...\Services\
    WorkLogManagerApi" /v Environment /t REG_MULTI_SZ ...` — necessário para que a nova
    variável `ASPNETCORE_URLS` (e a já existente `ASPNETCORE_ENVIRONMENT`) realmente
    surtam efeito.
  - **`[Run]` — `ASPNETCORE_URLS` adicionada**: mesmo comando `reg.exe add`, usando a
    flag `/s "|"` (documentada do `reg.exe`) para escrever múltiplas strings
    REG_MULTI_SZ a partir de um único `/d`, já que o separador padrão (`\0`) não pode
    ser digitado na linha de comando de `[Run]` do Inno Setup. Valor final:
    `ASPNETCORE_ENVIRONMENT=Production|ASPNETCORE_URLS=http://localhost:5000` (o `.iss`
    passa isso para `reg.exe` com `/s "|"`, que expande para duas strings
    REG_MULTI_SZ). Optei por um único `reg add` (em vez de dois `reg add` separados)
    por atomicidade e por já ser a forma como a linha original estava escrita — não vi
    motivo para trocar por dois comandos.
  - **`[Icons]` (nova seção)**: dois atalhos do tipo "URL shortcut" — `Filename:
    "http://localhost:5000"` em vez de um caminho de arquivo, sintaxe suportada
    nativamente pelo Inno Setup para gerar um `.lnk` cujo alvo é uma URL, sem precisar
    de um arquivo `.url` separado. Um em `{autodesktop}\WorkLogManager` e outro em
    `{autoprograms}\WorkLogManager`, ambos com `IconFilename` apontando para o `.exe`
    instalado (só para um ícone reconhecível, sem efeito funcional).
  - **`[UninstallDelete]`**: nenhuma entrada nova necessária — confirmado que o
    desinstalador do Inno Setup remove automaticamente todo atalho criado via
    `[Icons]` (inclusive atalhos de URL, que também são `.lnk` normais rastreados no
    log de desinstalação), documentado com um comentário explicando essa confirmação.
- `installer/README.md`: passo "Fresh install" do plano de teste manual atualizado para
  mencionar a porta fixa 5000 e para incluir a verificação dos dois novos atalhos.

### Arquivos verificados sem necessidade de alteração
- `installer/build.ps1`: não faz nenhuma suposição sobre porta (só publica/compila);
  nada a ajustar.
- `installer/appsettings.Production.json`: só contém `ConnectionStrings`, nenhuma
  configuração de `Urls`/Kestrel; a porta é inteiramente definida via
  `ASPNETCORE_URLS` no registro do serviço, não neste arquivo — nada a ajustar.

### Limitação de validação
Como em toda a task `win-installer`, o `.iss` não pôde ser compilado/testado neste
ambiente (macOS/Linux, sem Inno Setup). A sintaxe de `[Icons]` (atalho de URL) e a flag
`/s` de `reg.exe` foram revisadas manualmente contra a documentação oficial do Inno
Setup e do `reg.exe`, mas devem ser validadas end-to-end por um desenvolvedor em uma
máquina Windows antes de confiar na próxima release do instalador — em particular,
confirmar visualmente que os dois atalhos aparecem corretamente e abrem o navegador, e
que `sc.exe qc WorkLogManagerApi`/inspeção do registro mostram o valor `Environment`
correto após uma instalação real.
Nenhum arquivo `.cs` foi tocado neste ajuste; não houve necessidade de rodar
`dotnet test`.

## Bug fix: `installer/build.ps1` usava `npm ci` contra o lockfile errado

### Causa raiz
O repositório tem dois lockfiles em `app/`: `pnpm-lock.yaml` (o mantido de fato —
atualizado a cada instalação de dependência via `pnpm`/`npx shadcn add` ao longo da
sessão) e `package-lock.json` (parado, desatualizado). O `installer/build.ps1` foi
escrito originalmente usando `npm ci`, que instala a partir de `package-lock.json` — e
esse lockfile estava fora de sincronia com `app/package.json` (faltavam `cmdk@1.1.1` e
`date-fns@4.4.0`, entre outros pacotes instalados depois via `pnpm`). Resultado: o job
`win-installer` do GitHub Actions falhava em `npm ci` com "can only install packages
when your package.json and package-lock.json ... are in sync".

### Correção aplicada
- `installer/build.ps1`:
  - `npm ci` → `pnpm install --frozen-lockfile` (equivalente estrito do `npm ci` no
    pnpm: falha se `pnpm-lock.yaml` estiver desatualizado em relação a
    `app/package.json`, preservando a mesma garantia de build reprodutível).
  - `npm run build` → `pnpm run build`.
  - Comentário do cabeçalho (`.DESCRIPTION`) atualizado de "Node.js/npm" para
    "Node.js/pnpm".
  - Não havia outras ocorrências de `npm`/`npx` no arquivo.
- `.github/workflows/build-installer.yml`:
  - Adicionado o passo `pnpm/action-setup@v4` (`version: 10`, alinhado com a versão de
    pnpm usada localmente — `app/package.json` não declara `packageManager`, então a
    versão precisa ser explícita) antes do `actions/setup-node@v4`, para que o runner
    Windows tenha o binário `pnpm` disponível (sem isso, `pnpm install` falharia com
    "command not found").
  - `actions/setup-node@v4`: `cache: "npm"` + `cache-dependency-path:
    app/package-lock.json` → `cache: "pnpm"` + `cache-dependency-path:
    app/pnpm-lock.yaml`, para que o cache de dependências do Actions passe a chavear
    pelo lockfile realmente usado no build.
- `installer/README.md`: pré-requisito "Node.js (LTS) e npm" atualizado para "Node.js
  (LTS) e pnpm", com link para as instruções de instalação do pnpm.
- **Não** removido `app/package-lock.json` — decisão mantida de tasks anteriores de não
  apagar nenhum lockfile por conta própria; apenas parou de ser usado no processo de
  build do instalador.

### Validação feita
- `pnpm install --frozen-lockfile` rodado localmente em `app/` (macOS): sucesso,
  "Lockfile is up to date, resolution step is skipped" — confirma que `pnpm-lock.yaml`
  está de fato em sincronia com `app/package.json` (ao contrário do `package-lock.json`
  antigo), validando a mesma lógica de `--frozen-lockfile` que rodará no runner
  Windows.
- `pnpm run build` rodado localmente em `app/`: **falhou**, mas por um motivo
  inteiramente não relacionado a este bug fix — dois erros de `tsc` pré-existentes no
  repositório (não introduzidos por esta alteração; confirmado via `git status`, que
  não mostra nenhuma modificação nesses arquivos):
  - `src/features/employees/EmployeeDetailPage.tsx(55,1)`: `TS6133` —
    `intervalToDuration` importado e nunca usado.
  - `src/hooks/use-param.ts(18,12)`: `TS2322` — incompatibilidade de tipo no retorno.
  Esses erros já existiam no código publicado (commit `2f009c9`) e são ortogonais ao
  problema de lockfile relatado; corrigi-los está fora do escopo deste bug fix
  (npm→pnpm no instalador) e não foram tocados aqui. **Sinalizando explicitamente**: o
  build do instalador continuará falhando em CI até que esses dois erros de TypeScript
  sejam corrigidos separadamente — isso deve ser tratado como uma tarefa própria antes
  da próxima tentativa de gerar um release.
- O restante do pipeline (`dotnet publish`, cópia para `wwwroot`, compilação com Inno
  Setup) permanece não validável neste ambiente (macOS/Linux, sem Inno Setup/Windows),
  como já documentado nas seções anteriores deste resumo.

## Bug fix: dois erros de TypeScript pré-existentes bloqueando `pnpm run build`

Esses dois erros (sinalizados no bug fix anterior acima e em várias outras tasks ao
longo da sessão) foram finalmente corrigidos.

### 1. `app/src/features/employees/EmployeeDetailPage.tsx(55,1)` — `TS6133`

**Causa raiz**: `intervalToDuration` (de `date-fns`) era um import morto. Investiguei o
uso de duração/tempo em todo o arquivo: a única exibição de duração de worklog na
tabela usa `formatDuration(workLog.durationSeconds)` (de `@/lib/worklog-duration.ts`),
uma função própria do projeto que já formata segundos totais em `"1d 2h 30m"` sem
depender de `date-fns` — não há nenhum cálculo de duração incompleto ou local próximo à
linha 55 que devesse usar `intervalToDuration`. Concluí que era resíduo de uma
refatoração anterior que passou a usar `formatDuration` e deixou o import não removido.

**Correção**: removida a linha `import { intervalToDuration } from "date-fns";` de
`EmployeeDetailPage.tsx`. Nenhuma outra alteração no arquivo.

### 2. `app/src/hooks/use-param.ts(18,12)` — `TS2322`

**Causa raiz**: `useParam(param: string, ...)` indexava o contexto
(`ParamProviderType`, de `@/components/ParamProvider.tsx`) via
`context[param as keyof typeof context]`. Como `param` era tipado como `string`
genérico, `keyof ParamProviderType` inclui as três chaves do contexto —
`allowManageClosedWorkLogs: boolean`, `isLoading: boolean` e
`raw: { [key: string]: string | undefined }` — então o TypeScript inferia o tipo do
valor lido como a união de todos os tipos possíveis, incluindo o objeto `raw`, que não
é atribuível a `string | number | boolean | undefined` (o tipo de retorno declarado do
hook).

**Correção aplicada**: em vez de silenciar com `as any`/`@ts-ignore` (avaliado e
descartado — mascararia um problema real: o hook nunca deveria aceitar `"raw"` ou
`"isLoading"` como `param`, já que não fazem sentido para o uso pretendido de
"ler um parâmetro de sistema com fallback"), restringi a assinatura à interseção certa:
```ts
type ParamKey = Exclude<keyof ParamProviderType, "raw" | "isLoading">;

export function useParam(
  param: ParamKey,
  defaultValue: string | boolean | number,
): { value: string | boolean | number | undefined } {
  ...
  const value = context[param];
  return { value: value !== undefined ? value : defaultValue };
}
```
Com `ParamKey` restrito (hoje, só `"allowManageClosedWorkLogs"`), `context[param]`
resolve para `boolean`, compatível com o tipo de retorno declarado, sem nenhum cast.
Isso também melhora a segurança de tipo do único call site
(`EmployeeDetailPage.tsx`, `useParam("allowManageClosedWorkLogs", false)`): passar uma
string arbitrária como `"raw"` agora é erro de compilação, o que é o comportamento
correto.

### Validação feita
- `pnpm run build` (`tsc -b && vite build`) em `app/`: **sucesso**, sem nenhum erro de
  TypeScript, gerando `dist/` normalmente.
- `npx tsc --noEmit`: sem erros.
- `npx vitest run`: 82/91 aprovados — as 9 falhas remanescentes, em exatamente os 3
  arquivos já conhecidos e fora de escopo (`EmployeeListPage.test.tsx`,
  `workLogFilters.test.ts`, `EmployeeDetailPage.test.tsx`), continuam falhando pelos
  mesmos motivos de sempre (não relacionados a este fix). Inspecionei especificamente as
  falhas de `EmployeeDetailPage.test.tsx`: o erro é `"useParam must be used within a
  ParamProvider"`, ou seja, falha de setup de teste (o teste renderiza a página sem
  envolvê-la em `ParamProvider`), não um erro de tipo — confirmando que `vitest`
  realmente não roda `tsc -b` (usa apenas transformação via esbuild/vite) e que essas
  falhas já existiam antes e são inteiramente ortogonais aos dois bugs corrigidos aqui.
- `pnpm run lint` (`oxlint`): 0 erros; apenas warnings pré-existentes e não relacionados
  (`react(only-export-components)`, `react(set-state-in-effect)`) em arquivos não
  tocados por este fix.
- Cadeia completa de build do instalador, reproduzindo os passos de
  `installer/build.ps1` manualmente (exceto a compilação do `.iss`, que exige Windows +
  Inno Setup):
  - `pnpm install --frozen-lockfile` em `app/`: sucesso ("Lockfile is up to date").
  - `pnpm run build` em `app/`: sucesso (ver acima).
  - `dotnet publish api/src/WorkLogManager.Api -c Release -r win-x64 --self-contained
    true -p:PublishSingleFile=false -o <pasta temporária>`: sucesso, publicando o
    executável self-contained `win-x64` normalmente (único warning é o `NU1903`
    pré-existente e já documentado do AutoMapper, sem relação com este fix).
  - Confirmado, portanto, que a cadeia `pnpm install --frozen-lockfile` → `pnpm run
    build` → `dotnet publish` (exatamente os passos 2 e 3 de `build.ps1`) roda de ponta
    a ponta sem erro neste ambiente — o job `win-installer` do GitHub Actions deve voltar
    a passar até a etapa de Inno Setup (não testável fora de Windows).

### Arquivos alterados
- `app/src/features/employees/EmployeeDetailPage.tsx`: removido import morto
  `intervalToDuration`.
- `app/src/hooks/use-param.ts`: `param: string` → `param: ParamKey` (tipo derivado de
  `ParamProviderType`, excluindo `"raw"`/`"isLoading"`), eliminando a necessidade do
  cast `as keyof typeof context`.

# Plano de desenvolvimento: Gestão de horas trabalhadas de funcionários (Work Log Manager)

Task-id: worklog-manager

> Nota sobre o estado do repositório: `api/` e `app/` estão vazios (projeto greenfield).
> Este plano inclui, portanto, o bootstrap da solution .NET e do projeto React, além da
> feature em si. `<ProjectName>` = `WorkLogManager` (a ser refletido em
> `docs/padroes-desenvolvimento.md` durante a implementação).

> **Nota de revisão (v2)**: esta versão incorpora ajustes solicitados pelo responsável
> após revisão da v1: (1) uso de AutoMapper para mapeamento DTO ↔ domínio; (2) métodos
> de extensão `.ToModel()`/`.ToResponse()` como forma idiomática de invocar o
> AutoMapper; (3) renomeação dos campos de auditoria para `CreatedAtUtc`/
> `UpdatedAtUtc`; (4) layout de front-end sem sidebar, com `Navbar` fixo full-width e
> conteúdo centralizado via `container`; (5) separação explícita de camadas no
> front-end, com hooks customizados isolando os componentes de página do acesso a
> dados. Os pontos não mencionados nos ajustes permanecem como na v1.

## Premissas assumidas (pontos que o PO deixou "inferidos", sem pergunta direta ao usuário)

Como o usuário não foi questionado especificamente sobre estes pontos, adoto as
seguintes decisões técnicas razoáveis, documentadas aqui para validação no momento da
aprovação do plano:

1. **Confirmação antes de excluir** (worklog e funcionário): sim, ambos os fluxos de
   exclusão abrem um `AlertDialog` (shadcn/ui) de confirmação antes de efetivar o
   `DELETE`.
2. **Validação de data final < data inicial**: é erro de validação, bloqueado tanto no
   front (impede submit, mensagem inline) quanto no back-end (entidade
   `EmployeeWorkLog` nunca aceita `EndDate < StartDate`, lança exceção de domínio
   traduzida para HTTP 400).
3. **Tela de configuração de horas padrão do sistema**: existe uma tela/rota simples
   `/settings` com um único formulário (campo "horas padrão por dia") e um endpoint
   `GET/PUT /system-settings`. Não há multi-tenant nem múltiplos perfis de
   configuração — é um registro único (singleton) em nível de sistema.
4. **Autenticação/autorização**: não mencionada pelo PO em nenhum momento; assumo que
   está **fora de escopo** desta tarefa (aplicação single-user, uso interno do
   empresário). Registrado como risco abaixo.
5. **Convenção de conversão de mês/ano no parser de duração**: mês = 30 dias, ano =
   365 dias (aproximação fixa e documentada no código, não calendário real). Semana =
   7 dias (exata).
6. **Duração em segundos é sempre derivada de `StartDate`/`EndDate` no back-end**
   (nunca recebida como número bruto do cliente) — o campo textual "worklog" é uma
   conveniência de UX no front-end para preencher as datas; o contrato da API só
   troca `type`, `startDate`, `endDate` (a duração em segundos é calculada pela
   entidade e devolvida na resposta, não é um input de escrita).
7. **AutoMapper convive com mapeamento manual via extension methods**: a Api usa
   AutoMapper como motor de mapeamento reflexivo (convention-based + `Profile`s para
   os casos que fogem da convenção), mas o código de endpoints nunca chama
   `IMapper.Map<>()` diretamente — sempre através de métodos de extensão
   `.ToModel()` (request → entidade/objeto de domínio) e `.ToResponse()` (entidade/
   resultado → DTO de response), que internamente delegam para o `IMapper` injetado.
   Isso mantém a legibilidade do código de endpoint (`request.ToModel(mapper)`,
   `employee.ToResponse(mapper)`) e concentra toda a configuração de mapeamento
   (inclusive os casos com `ForMember`/resolvers customizados) nos `Profile`s do
   AutoMapper, evitando duplicar lógica de mapeamento manual "na mão" espalhada pelos
   endpoints.

## Resumo técnico da solução

Solução full-stack para cadastro de funcionários e apontamento de horas extras/faltas.
Back-end .NET 10 com três camadas (`Api`/`Application`/`Infrastructure`) seguindo
use-cases explícitos por ação; três tabelas no PostgreSQL (`employees`,
`employee_work_logs`, `system_settings`) via EF Core com snake_case; mapeamento
DTO ↔ domínio na `Api` via AutoMapper, invocado através de métodos de extensão
`.ToModel()`/`.ToResponse()`. Front-end React + shadcn/ui com layout sem sidebar
(`Navbar` fixo no topo, conteúdo centralizado via `container`), camadas explícitas
(`lib/api` para acesso HTTP puro, `hooks` para TanStack Query, `features/<feature>`
para páginas que só consomem hooks), com uma tela de listagem de funcionários (clique
= detalhe, clique-direito ou pressionar-e-segurar = menu com opção de novo worklog),
tela de detalhe do funcionário (informações + CRUD de worklogs) e uma tela de
configuração do padrão de horas do sistema. Um parser de duração textual (ex.: "1h
30m") é implementado nas duas pontas (back-end para eventual reuso/validação,
front-end para a sincronização bidirecional data↔duração no modal), com tabela de
unidades fixa e case-sensitive.

## Back-end — mudanças por camada

### Application (use-cases, entidades, interfaces)

**Entidades de domínio**

- `Employee`: `Id` (Guid), `Name` (string, obrigatório), `Role` (string, obrigatório),
  `HireDate` (DateOnly, obrigatório), `DailyWorkHours` (decimal?, opcional — se
  informado deve ser > 0), `CreatedAtUtc` (DateTimeOffset), `UpdatedAtUtc`
  (DateTimeOffset). Construtor e método `Update(name, role, hireDate,
  dailyWorkHours)` validam os invariantes (lançam `DomainException` se nome/cargo
  vazios ou `dailyWorkHours <= 0`) e atualizam `UpdatedAtUtc = DateTimeOffset.UtcNow`.
- `WorkLogType` (enum): `Absence` (falta), `Overtime` (hora extra).
- `EmployeeWorkLog`: `Id` (Guid), `EmployeeId` (Guid), `Type` (WorkLogType),
  `StartDate` (DateTimeOffset), `EndDate` (DateTimeOffset), `DurationSeconds` (long,
  calculado), `CreatedAtUtc` (DateTimeOffset), `UpdatedAtUtc` (DateTimeOffset).
  Construtor e método `Update(type, startDate, endDate)` validam `EndDate >=
  StartDate`, recalculam `DurationSeconds = (EndDate - StartDate).TotalSeconds` e
  atualizam `UpdatedAtUtc = DateTimeOffset.UtcNow`.
- `SystemSettings`: `Id` (Guid fixo/singleton), `DefaultDailyWorkHours` (decimal, >
  0), `UpdatedAtUtc` (DateTimeOffset). Método `UpdateDefaultDailyWorkHours(decimal)`
  atualiza `UpdatedAtUtc = DateTimeOffset.UtcNow`.
- `EmployeeDetailResult` (objeto de resultado da Application, não é entidade
  persistida): agrega `Employee`, `IReadOnlyList<EmployeeWorkLog>` e
  `EffectiveDailyWorkHours` (decimal), retornado por `GetEmployeeByIdUseCase`. Existe
  para que a `Api` tenha um único objeto a mapear para `EmployeeDetailResponse` via
  AutoMapper (ver seção de mapeamento abaixo).

**Convenção de datas**: todos os campos de auditoria são sempre gravados em UTC
(`DateTimeOffset.UtcNow`, consistente com o tipo já usado em `StartDate`/`EndDate`).
Nenhuma conversão de fuso horário é feita no back-end; se necessário, é
responsabilidade do front-end exibir em horário local.

**Serviço de domínio (Application, sem dependências externas)**

- `WorkLogDurationParser` (classe estática ou serviço sem estado):
  - `Parse(string input) -> long` (segundos totais). Tokeniza por espaços, cada token
    é `<número><unidade>` (ex.: `1h`, `30m`); unidade **case-sensitive**:
    `s`=segundo, `m`=minuto, `h`=hora, `d`=dia, `S`=semana(7d), `M`=mês(30d),
    `A`=ano(365d). Lança `DomainException` para token inválido ou unidade
    desconhecida.
  - `Format(long totalSeconds) -> string`: formata como `"1h 32m"` (mostra apenas
    componentes não-zero, unidades grandes→pequenas: usa apenas `d`/`h`/`m`/`s` na
    saída, mesmo que a entrada aceite `S`/`M`/`A` — evita ambiguidade de exibição).
    Usado principalmente como referência de contrato para o front-end (que replica a
    mesma lógica em TypeScript); pode ser usado em testes e, opcionalmente, em uma
    resposta de API que inclua o worklog formatado.

**Use-cases — Employee**

- `CreateEmployeeUseCase`
- `UpdateEmployeeUseCase`
- `DeleteEmployeeUseCase`
- `GetEmployeeByIdUseCase` (retorna `EmployeeDetailResult`: `Employee` + lista de
  `EmployeeWorkLog` + `EffectiveDailyWorkHours` =
  `Employee.DailyWorkHours ?? SystemSettings.DefaultDailyWorkHours`)
- `ListEmployeesUseCase`

**Use-cases — EmployeeWorkLog**

- `CreateEmployeeWorkLogUseCase` (valida existência do `Employee`)
- `UpdateEmployeeWorkLogUseCase`
- `DeleteEmployeeWorkLogUseCase`
- `ListEmployeeWorkLogsUseCase` (por `EmployeeId`, ordenado por `StartDate desc`)

**Use-cases — SystemSettings**

- `GetSystemSettingsUseCase` (cria com valor default na primeira leitura, se ainda
  não existir linha)
- `UpdateSystemSettingsUseCase`

**Interfaces (ports)**

- `IEmployeeRepository`: `AddAsync`, `UpdateAsync`, `DeleteAsync`, `GetByIdAsync`,
  `ListAllAsync`.
- `IEmployeeWorkLogRepository`: `AddAsync`, `UpdateAsync`, `DeleteAsync`,
  `GetByIdAsync`, `ListByEmployeeIdAsync`.
- `ISystemSettingsRepository`: `GetAsync`, `UpsertAsync`.

### Infrastructure (EF Core, repositórios, integrações)

- `WorkLogManagerDbContext` com `UseSnakeCaseNamingConvention()`.
- `EmployeeConfiguration`, `EmployeeWorkLogConfiguration`, `SystemSettingsConfiguration`
  (Fluent API): tipos de coluna, constraints, índices, FK, conversão de enum
  `WorkLogType` para `string` (`HasConversion<string>()`), mapeamento de
  `CreatedAtUtc`/`UpdatedAtUtc` para `timestamptz` (a convenção snake_case gera as
  colunas `created_at_utc`/`updated_at_utc` automaticamente a partir do nome C#).
- Repositórios: `EmployeeRepository`, `EmployeeWorkLogRepository`,
  `SystemSettingsRepository` implementando as interfaces acima.

**Migration**: `InitialCreate` (gerada com `dotnet ef migrations add InitialCreate
--project src/WorkLogManager.Infrastructure --startup-project src/WorkLogManager.Api`
na fase de implementação) — tabelas/colunas:

| Tabela             | Coluna                   | Tipo         | Constraint                                             |
| ------------------ | ------------------------ | ------------ | ------------------------------------------------------ |
| employees          | id                       | uuid         | PK                                                     |
| employees          | name                     | varchar(200) | not null                                               |
| employees          | role                     | varchar(200) | not null                                               |
| employees          | hire_date                | date         | not null                                               |
| employees          | daily_work_hours         | numeric(5,2) | null, check (> 0 quando não nulo)                      |
| employees          | created_at_utc           | timestamptz  | not null                                               |
| employees          | updated_at_utc           | timestamptz  | not null                                               |
| employee_work_logs | id                       | uuid         | PK                                                     |
| employee_work_logs | employee_id              | uuid         | not null, FK → employees(id) on delete cascade, índice |
| employee_work_logs | type                     | varchar(20)  | not null (`Absence`/`Overtime`)                        |
| employee_work_logs | start_date               | timestamptz  | not null                                               |
| employee_work_logs | end_date                 | timestamptz  | not null, check (end_date >= start_date)               |
| employee_work_logs | duration_seconds         | bigint       | not null                                               |
| employee_work_logs | created_at_utc           | timestamptz  | not null                                               |
| employee_work_logs | updated_at_utc           | timestamptz  | not null                                               |
| system_settings    | id                       | uuid         | PK (linha única/singleton, id fixo conhecido)          |
| system_settings    | default_daily_work_hours | numeric(5,2) | not null, check (> 0), default 8.00                    |
| system_settings    | updated_at_utc           | timestamptz  | not null                                               |

A migration deve incluir um `INSERT` (via `migrationBuilder.InsertData` ou seed no
`OnModelCreating`) da linha singleton de `system_settings` com o valor padrão
(8 horas), para que `GetSystemSettingsUseCase` sempre encontre um registro.

### Api (endpoints, DTOs, mapeamento)

Endpoints agrupados via `MapGroup`, um grupo por feature:

**Employees** (`/employees`)

- `GET /employees` → lista (`EmployeeSummaryResponse[]`)
- `GET /employees/{id}` → detalhe (`EmployeeDetailResponse`, inclui
  `EffectiveDailyWorkHours` e `WorkLogs: EmployeeWorkLogResponse[]`)
- `POST /employees` (`CreateEmployeeRequest` → `EmployeeSummaryResponse`, 201)
- `PUT /employees/{id}` (`UpdateEmployeeRequest` → `EmployeeSummaryResponse`, 200)
- `DELETE /employees/{id}` → 204 (cascade remove worklogs associados)

**Employee work logs** (`/employees/{employeeId}/work-logs`)

- `POST /employees/{employeeId}/work-logs` (`CreateEmployeeWorkLogRequest` →
  `EmployeeWorkLogResponse`, 201)
- `PUT /employees/{employeeId}/work-logs/{id}` (`UpdateEmployeeWorkLogRequest` →
  `EmployeeWorkLogResponse`, 200)
- `DELETE /employees/{employeeId}/work-logs/{id}` → 204

**System settings** (`/system-settings`)

- `GET /system-settings` → `SystemSettingsResponse`
- `PUT /system-settings` (`UpdateSystemSettingsRequest` → `SystemSettingsResponse`)

DTOs (records na `Api`): `CreateEmployeeRequest{Name,Role,HireDate,DailyWorkHours?}`,
`UpdateEmployeeRequest{Name,Role,HireDate,DailyWorkHours?}`,
`EmployeeSummaryResponse{Id,Name,Role,HireDate,DailyWorkHours?}`,
`EmployeeDetailResponse{Id,Name,Role,HireDate,DailyWorkHours?,EffectiveDailyWorkHours,WorkLogs}`,
`CreateEmployeeWorkLogRequest{Type,StartDate,EndDate}`,
`UpdateEmployeeWorkLogRequest{Type,StartDate,EndDate}`,
`EmployeeWorkLogResponse{Id,EmployeeId,Type,StartDate,EndDate,DurationSeconds}`,
`SystemSettingsResponse{DefaultDailyWorkHours}`,
`UpdateSystemSettingsRequest{DefaultDailyWorkHours}`.

Nenhum destes DTOs expõe `CreatedAtUtc`/`UpdatedAtUtc` hoje (o PO não pediu
auditoria visível na UI); caso isso mude no futuro, os campos de response devem se
chamar `CreatedAtUtc`/`UpdatedAtUtc` para manter a convenção, e o mapeamento é
automático via AutoMapper por convenção de nomes.

**Mapeamento DTO ↔ domínio (AutoMapper + extension methods)**

- Pacotes: `AutoMapper` e `AutoMapper.Extensions.Microsoft.DependencyInjection` (ou o
  registro manual equivalente do AutoMapper 13+, que já expõe `AddAutoMapper` sem o
  pacote separado — a confirmar a versão exata na implementação) adicionados ao
  projeto `Api`.
- `Profile`s ficam em `Api/Mapping/Profiles/`, um por feature:
  - `EmployeeMappingProfile`: `CreateEmployeeRequest`/`UpdateEmployeeRequest` →
    `Employee` (usado apenas para os campos de entrada do construtor/`Update`, ver
    nota abaixo), `Employee` → `EmployeeSummaryResponse`, `EmployeeDetailResult` →
    `EmployeeDetailResponse` (com `ForMember` mapeando `Id`/`Name`/`Role`/`HireDate`/
    `DailyWorkHours` a partir de `EmployeeDetailResult.Employee`, e
    `EffectiveDailyWorkHours`/`WorkLogs` diretamente do `EmployeeDetailResult`).
  - `EmployeeWorkLogMappingProfile`: `CreateEmployeeWorkLogRequest`/
    `UpdateEmployeeWorkLogRequest` → tupla/objeto de parâmetros de entrada,
    `EmployeeWorkLog` → `EmployeeWorkLogResponse`.
  - `SystemSettingsMappingProfile`: `SystemSettings` → `SystemSettingsResponse`,
    `UpdateSystemSettingsRequest` → parâmetros de entrada do use-case.
  - **Nota sobre entidades com construtor validante**: como `Employee` e
    `EmployeeWorkLog` protegem seus invariantes em construtor/`Update` (não têm
    setters públicos livres), o AutoMapper não constrói a entidade diretamente a
    partir do request nos casos de `Create`; em vez disso, `.ToModel()` para esses
    casos mapeia o request para um DTO/parâmetro de entrada simples (ou o próprio
    use-case recebe os campos primitivos do request desestruturados), e quem invoca
    o construtor da entidade é o use-case. O AutoMapper é usado com todo o proveito
    nos mapeamentos "de saída" (entidade/resultado → response), que é o caso onde
    reflexão por convenção economiza mais código repetitivo.
- Registro em DI: `builder.Services.AddAutoMapper(typeof(EmployeeMappingProfile).Assembly);`
  em `Program.cs` da `Api`.
- Métodos de extensão ficam em `Api/Mapping/Extensions/`:
  - `RequestMappingExtensions.cs`: `CreateEmployeeRequest.ToModel(IMapper mapper)`,
    `UpdateEmployeeRequest.ToModel(IMapper mapper)`,
    `CreateEmployeeWorkLogRequest.ToModel(IMapper mapper)`,
    `UpdateEmployeeWorkLogRequest.ToModel(IMapper mapper)`, etc. — cada um chama
    `mapper.Map<T>(this)` internamente.
  - `ResponseMappingExtensions.cs`: `Employee.ToResponse(IMapper mapper)`,
    `EmployeeDetailResult.ToResponse(IMapper mapper)`,
    `EmployeeWorkLog.ToResponse(IMapper mapper)`,
    `SystemSettings.ToResponse(IMapper mapper)` — idem, chamam `mapper.Map<T>(this)`.
  - Os endpoints recebem `IMapper mapper` como parâmetro injetado (Minimal API
    resolve automaticamente via DI) e escrevem, por exemplo:
    `var employee = await createUseCase.ExecuteAsync(request.ToModel(mapper));` e
    `return Results.Ok(employee.ToResponse(mapper));`. Isso evita chamadas soltas
    a `mapper.Map<>()` espalhadas pelo código de endpoint, mantendo a legibilidade
    de "ler a intenção" (`request.ToModel()`, `entity.ToResponse()`) e concentrando
    toda a configuração de mapeamento nos `Profile`s.

Tratamento de erros: `DomainException` da Application mapeada para 400 (Bad Request)
com corpo `{ message }`; "not found" (Employee/WorkLog inexistente) mapeado para 404.
Middleware/handler de exceção único no `Program.cs` da `Api`.

## Front-end — mudanças em `app/`

Bootstrap: Vite + React + TypeScript, Tailwind, shadcn/ui, React Router, TanStack
Query (para cache/mutations das chamadas HTTP), react-hook-form + zod (formulários).

**Camadas e convenção de responsabilidade (explícita)**

O front-end é organizado em camadas com responsabilidade única; a convenção adotada
é: **componentes de página (`features/<feature>/*Page.tsx`) nunca chamam
`lib/api` diretamente — sempre passam por um hook da camada `hooks`.**

- `app/src/lib/api/` — camada de acesso a dados pura (fetch/HTTP), sem estado de
  cache nem React. Só funções tipadas por endpoint (`getEmployees()`,
  `createEmployee(payload)` etc.) e o client base (`client.ts`).
- `app/src/hooks/` (organizados por feature, ex.: `app/src/features/employees/hooks/`,
  `app/src/features/work-logs/hooks/`, `app/src/features/settings/hooks/`) — hooks
  customizados que encapsulam TanStack Query, chamando as funções de `lib/api`:
  `useEmployees()` (`useQuery`, lista), `useEmployee(id)` (`useQuery`, detalhe),
  `useCreateEmployee()`, `useUpdateEmployee()`, `useDeleteEmployee()` (`useMutation`,
  com invalidação de cache da lista/detalhe), `useEmployeeWorkLogs(employeeId)`
  (implícito dentro de `useEmployee`, já que o detalhe retorna os worklogs
  embutidos), `useCreateWorkLog()`, `useUpdateWorkLog()`, `useDeleteWorkLog()`,
  `useSystemSettings()`, `useUpdateSystemSettings()`. Nenhum componente de página
  importa `useQuery`/`useMutation` diretamente — sempre um hook desta camada.
- `app/src/components/` — componentes de UI reutilizáveis e genéricos, sem lógica de
  dados: `components/ui/` (shadcn), `components/Navbar.tsx`, `components/Layout.tsx`
  (ver seção de layout abaixo).
- `app/src/features/<feature>/` — páginas e componentes específicos de cada feature
  (`employees`, `work-logs`, `settings`), cada uma com subpastas `components/` (se
  necessário) e `hooks/`, consumindo apenas os hooks da própria feature — nunca
  `lib/api` diretamente.
- `app/src/lib/worklog-duration.ts` — utilitário puro (parser/formatter), sem
  dependência de React nem de rede.

**Layout (sem sidebar, `Navbar` fixo full-width, conteúdo centralizado)**

- `app/src/components/Navbar.tsx`: barra de navegação fixa no topo (`fixed top-0
  left-0 right-0` ou `sticky top-0`, `w-full`, com `z-index` e fundo sólido/blur para
  ficar acima do conteúdo ao rolar a página). A `<nav>` ocupa 100% da largura da
  tela, mas o conteúdo interno (logo/título + links `Funcionários`/`Configurações`)
  fica dentro de um `div` com a classe utilitária `container mx-auto` (mesma
  centralização usada no resto da aplicação), para alinhar visualmente com o
  conteúdo abaixo.
- `app/src/components/Layout.tsx` (ou `AppShell.tsx`): componente que renderiza
  `<Navbar />` + um `<main>` com `container mx-auto` (e padding vertical/top extra
  para compensar a altura do navbar fixo, ex. `pt-20`) envolvendo `<Outlet />` do
  React Router. Não há componente de sidebar em nenhuma tela.
- `app/tailwind.config.ts`: garantir que o plugin/config do `container` do Tailwind
  esteja habilitado com `center: true` (o preset padrão gerado pelo `shadcn init` já
  inclui isso normalmente; validar na implementação) para que `container mx-auto`
  centralize com largura máxima responsiva.
- Nenhuma tela usa `w-screen`/`max-w-none` no conteúdo principal — todo o conteúdo de
  página vive dentro do `<main>` do `Layout`, que já aplica o `container`.

**Estrutura de arquivos**

- `app/src/components/ui/` — componentes shadcn instalados: `table`, `button`,
  `dialog`, `alert-dialog`, `input`, `label`, `select`, `form`, `context-menu`,
  `sonner` (toast).
- `app/src/components/Navbar.tsx`, `app/src/components/Layout.tsx` — ver seção de
  layout acima.
- `app/src/features/employees/`
  - `EmployeeListPage.tsx` — tabela de funcionários + botão "+"; usa `ContextMenu`
    (shadcn, baseado em Radix) envolvendo cada linha para o clique-direito (desktop);
    no mobile o `contextmenu` nativo é disparado por pressionar-e-segurar na maioria
    dos navegadores — usa `onContextMenu` com `preventDefault()`; clique simples
    (`onClick`) navega para `/employees/:id`. Consome `useEmployees()` (lista) da
    camada de hooks.
  - `EmployeeFormModal.tsx` — modal de criar/editar funcionário (reaproveitado),
    campos: nome, cargo, data de admissão, horas/dia (opcional). Consome
    `useCreateEmployee()`/`useUpdateEmployee()`.
  - `EmployeeDetailPage.tsx` — dados do funcionário (com botões editar/excluir,
    reaproveitando `EmployeeFormModal` e `AlertDialog` de confirmação) + tabela de
    worklogs (ícones lápis/lixeira por linha). Consome `useEmployee(id)` e
    `useDeleteEmployee()`.
  - `hooks/useEmployees.ts`, `hooks/useEmployee.ts`, `hooks/useCreateEmployee.ts`,
    `hooks/useUpdateEmployee.ts`, `hooks/useDeleteEmployee.ts` (ou agrupados em um
    único `hooks/useEmployees.ts` com múltiplos hooks exportados — decisão de
    granularidade fica para a implementação, desde que a regra de "página nunca
    chama `lib/api` direto" seja respeitada).
- `app/src/features/work-logs/`
  - `WorkLogFormModal.tsx` — modal de criar/editar worklog: campos tipo (Select),
    data inicial, data final (inputs datetime-local) e campo texto "worklog"
    (duração). Sincronização bidirecional (ver seção dedicada abaixo). Ao abrir para
    criação, `startDate = endDate = now`. Consome `useCreateWorkLog()`/
    `useUpdateWorkLog()`.
  - `hooks/useCreateWorkLog.ts`, `hooks/useUpdateWorkLog.ts`,
    `hooks/useDeleteWorkLog.ts`.
  - `DeleteConfirmDialog.tsx` (ou uso direto do `AlertDialog` do shadcn) — reutilizado
    para excluir funcionário e worklog; fica em `app/src/components/` por ser
    genérico o bastante para as duas features.
- `app/src/features/settings/`
  - `SystemSettingsPage.tsx` — formulário único com "horas padrão por dia". Consome
    `useSystemSettings()`/`useUpdateSystemSettings()`.
  - `hooks/useSystemSettings.ts`.
- `app/src/lib/api/` — `client.ts` (fetch wrapper com base URL/erro padronizado),
  `employees.ts`, `workLogs.ts`, `systemSettings.ts` (funções tipadas por endpoint,
  sem nenhuma referência a `useQuery`/`useMutation`).
- `app/src/lib/worklog-duration.ts` — `parseDuration(input: string): number`
  (segundos) e `formatDuration(totalSeconds: number): string` (`"1h 32m"`),
  replicando exatamente a tabela de unidades e a convenção mês=30d/ano=365d do
  back-end (código compartilhado apenas "conceitualmente" — TS e C# ficam em
  repositórios/projeto separados, não há compartilhamento de pacote nesta fase).
- Rotas (`react-router`): todas envolvidas pelo `Layout` (`Navbar` + `container`):
  `/` (lista), `/employees/:id` (detalhe), `/settings`.

**Sincronização bidirecional data ↔ duração (worklog field) no `WorkLogFormModal`**

- Estado local com um campo `lastEditedField: "dates" | "duration" | null` para evitar
  loop de re-render/recalculo circular.
- `onChange` de `startDate` ou `endDate`: seta `lastEditedField = "dates"`, recalcula
  `durationSeconds = endDate - startDate` (em segundos) e atualiza o campo texto via
  `formatDuration(durationSeconds)` — só quando `lastEditedField !== "duration"`
  durante o próprio ciclo de atualização (usa `useEffect` com a flag, ou atualização
  direta sem useEffect + guarda para não reprocessar o próprio evento).
- `onChange` (com debounce ~300-500ms, ou `onBlur`) do campo texto "worklog":
  seta `lastEditedField = "duration"`; tenta `parseDuration(valor)`; se válido,
  recalcula `endDate = startDate + duration` e atualiza o input de data final; se
  inválido, mantém o texto mas marca erro de validação inline (sem alterar datas).
- Validação final antes do submit: `endDate >= startDate` (senão bloqueia submit com
  mensagem de erro), reforçando a mesma regra do back-end.
- Requisição ao backend envia apenas `{type, startDate, endDate}` (não envia texto de
  duração nem segundos calculados no cliente — fonte da verdade é o servidor). O
  envio efetivo é feito pelo hook `useCreateWorkLog()`/`useUpdateWorkLog()`, nunca
  pelo componente de modal chamando `lib/api` diretamente.

## Passos de implementação

1. Criar solution `.NET`: `api/WorkLogManager.sln`, projetos
   `src/WorkLogManager.Api`, `src/WorkLogManager.Application`,
   `src/WorkLogManager.Infrastructure`, `tests/WorkLogManager.Application.Tests`, com
   as referências de projeto corretas (`Api`→`Application`; `Infrastructure`→
   `Application`; `Api`→`Infrastructure` só para DI/composition root).
2. `Application`: criar `WorkLogType` (enum) e entidade `Employee` com invariantes e
   campos `CreatedAtUtc`/`UpdatedAtUtc`.
3. `Application`: criar entidade `EmployeeWorkLog` com invariantes (`EndDate >=
   StartDate`, cálculo de `DurationSeconds`) e campos `CreatedAtUtc`/`UpdatedAtUtc`.
4. `Application`: criar entidade `SystemSettings` com campo `UpdatedAtUtc`.
5. `Application`: criar `WorkLogDurationParser` (`Parse`/`Format`) com a tabela de
   unidades definida (s/m/h/d/S/M/A, case-sensitive, mês=30d, ano=365d).
6. `Application`: criar interfaces `IEmployeeRepository`, `IEmployeeWorkLogRepository`,
   `ISystemSettingsRepository`.
7. `Application`: implementar use-cases de `Employee` (Create/Update/Delete/GetById/
   List), incluindo o objeto de resultado `EmployeeDetailResult` retornado por
   `GetEmployeeByIdUseCase`.
8. `Application`: implementar use-cases de `EmployeeWorkLog`
   (Create/Update/Delete/ListByEmployee).
9. `Application`: implementar use-cases de `SystemSettings` (Get/Update).
10. `Infrastructure`: criar `WorkLogManagerDbContext` com
    `UseSnakeCaseNamingConvention()` e as três `IEntityTypeConfiguration` (colunas
    `created_at_utc`/`updated_at_utc`).
11. `Infrastructure`: implementar os três repositórios.
12. `Infrastructure`: gerar a migration `InitialCreate` (especificação já definida
    acima, com colunas `created_at_utc`/`updated_at_utc`) incluindo seed da linha
    singleton de `system_settings`.
13. `Api`: criar DTOs de request/response listados acima.
14. `Api`: adicionar pacote(s) do AutoMapper e criar `EmployeeMappingProfile`,
    `EmployeeWorkLogMappingProfile`, `SystemSettingsMappingProfile` em
    `Api/Mapping/Profiles/`.
15. `Api`: criar métodos de extensão `.ToModel()`/`.ToResponse()` em
    `Api/Mapping/Extensions/` (`RequestMappingExtensions.cs`,
    `ResponseMappingExtensions.cs`), cada um delegando para `IMapper`.
16. `Api`: criar `EmployeesEndpoints`, `EmployeeWorkLogsEndpoints`,
    `SystemSettingsEndpoints` (Minimal API, `MapGroup`), usando `.ToModel()`/
    `.ToResponse()` para o mapeamento DTO↔Application.
17. `Api`: configurar `Program.cs` (DI, DbContext, `AddAutoMapper`, CORS para origem
    do front em dev, exception handling middleware, mapeamento dos grupos de
    endpoint).
18. `Application.Tests`: testes de `WorkLogDurationParser`, invariantes de `Employee`
    e `EmployeeWorkLog`, e dos use-cases (repositórios mockados).
19. `Api` (teste rápido, best-effort): `AssertConfigurationIsValid()` do AutoMapper
    (garante que todos os `Profile`s estão configurados sem mapeamento pendente).
20. Bootstrap `app/`: Vite + React + TS, Tailwind (com `container` centralizado
    configurado), shadcn/ui, React Router, TanStack Query, react-hook-form + zod.
21. `app/src/lib/api/client.ts` + `employees.ts` + `workLogs.ts` +
    `systemSettings.ts` (camada de acesso a dados pura, sem hooks).
22. Camada de hooks: `useEmployees`, `useEmployee`, `useCreateEmployee`,
    `useUpdateEmployee`, `useDeleteEmployee`, `useCreateWorkLog`, `useUpdateWorkLog`,
    `useDeleteWorkLog`, `useSystemSettings`, `useUpdateSystemSettings` (TanStack
    Query), organizados em `app/src/features/<feature>/hooks/`.
23. `app/src/lib/worklog-duration.ts` (`parseDuration`/`formatDuration`) + testes.
24. `app/src/components/Navbar.tsx` + `app/src/components/Layout.tsx` (`Navbar` fixo
    full-width, conteúdo interno e `<main>` com `container` centralizado, sem
    sidebar).
25. `EmployeeListPage.tsx` (tabela, botão "+", `ContextMenu` para clique-direito/
    pressionar-e-segurar, navegação por clique simples), consumindo `useEmployees()`.
26. `EmployeeFormModal.tsx` (criar/editar funcionário), consumindo
    `useCreateEmployee()`/`useUpdateEmployee()`.
27. `EmployeeDetailPage.tsx` (informações do funcionário, editar/excluir funcionário,
    tabela de worklogs com ícones lápis/lixeira), consumindo `useEmployee(id)` e
    `useDeleteEmployee()`.
28. `WorkLogFormModal.tsx` (criar/editar worklog, incluindo a lógica de sincronização
    bidirecional data↔duração e valores default `now`/`now` na criação), consumindo
    `useCreateWorkLog()`/`useUpdateWorkLog()`.
29. `AlertDialog` de confirmação de exclusão reutilizado em funcionário e worklog
    (`app/src/components/DeleteConfirmDialog.tsx`).
30. `SystemSettingsPage.tsx` (`/settings`), consumindo `useSystemSettings()`/
    `useUpdateSystemSettings()`, + link de navegação no `Navbar`.
31. Configurar rotas (`/`, `/employees/:id`, `/settings`) envolvidas pelo `Layout`
    (`Navbar` fixo, sem sidebar, `container` centralizado).
32. Smoke test manual ponta a ponta (fora do escopo de testes automatizados) para
    validar a integração front↔back antes do code review.

## Mapeamento critério de aceite → passos

| Critério de aceite                                     | Passo(s)                                 |
| ------------------------------------------------------ | ----------------------------------------- |
| #1 (listagem + botão "+")                              | 25                                        |
| #2 (botão "+" cria funcionário)                        | 25, 26, 7, 13, 14, 15, 16                 |
| #3 (clique abre detalhe)                                | 25, 27, 7, 8, 13, 14, 15, 16              |
| #4 (clique-direito/segurar abre modal de worklog)      | 25, 28                                    |
| #5 (datas iniciais = agora)                             | 28                                        |
| #6 (parser de unidades)                                 | 5, 23, 28                                 |
| #7 (editar data recalcula worklog)                      | 23, 28                                    |
| #8 (editar worklog recalcula data final)                | 23, 28                                    |
| #9 (persistência tipo/datas/duração em segundos)        | 3, 8, 13, 14, 15, 16                      |
| #10 (listagem de worklogs no detalhe)                   | 8, 13, 14, 15, 16, 27                     |
| #11 (lápis abre edição preenchida)                       | 27, 28                                    |
| #12 (lixeira exclui, com confirmação)                    | 8, 13, 14, 15, 16, 27, 29                 |
| #13 (config. de horas padrão do sistema)                | 4, 9, 13, 14, 15, 16, 30                  |
| #14 (horas/dia específicas por funcionário prevalecem)  | 2, 7, 13, 14, 15, 16, 27                  |
| #15 (CRUD completo de funcionário)                       | 2, 7, 13, 14, 15, 16, 25, 26, 27, 29       |

## Estratégia de testes

| Critério de aceite                             | Cenário de teste                                                                                                                                                                                                              |
| ---------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| #6                                             | `WorkLogDurationParserTests`: `Parse("1h 30m")` = 5400s; `Parse("2M")` = 2*30*86400s; `Parse("1A")` = 365*86400s; `Parse("1S")` = 7*86400s; token inválido/unidade desconhecida/case errado (`"1H"`) lança `DomainException`. |
| #6/#7/#8                                       | `worklog-duration.test.ts` (Vitest): mesmos casos replicados em TS para `parseDuration`/`formatDuration`, garantindo paridade com o back-end.                                                                                 |
| #9                                             | `CreateEmployeeWorkLogUseCaseTests`: dado `startDate`/`endDate`, `DurationSeconds` resultante é `(end-start).TotalSeconds`; `EndDate < StartDate` lança exceção e o repositório não é chamado.                                |
| #12                                            | `DeleteEmployeeWorkLogUseCaseTests`: exclusão remove apenas o worklog do id informado; excluir worklog inexistente retorna erro "not found".                                                                                  |
| #13/#14                                        | `GetEmployeeByIdUseCaseTests`: `EffectiveDailyWorkHours` retorna `Employee.DailyWorkHours` quando definido, e `SystemSettings.DefaultDailyWorkHours` quando nulo.                                                             |
| #15                                            | `CreateEmployeeUseCaseTests`, `UpdateEmployeeUseCaseTests`, `DeleteEmployeeUseCaseTests`: casos de sucesso e de validação (nome/cargo vazios, `DailyWorkHours <= 0`).                                                         |
| (mapeamento, transversal)                      | Teste de `AssertConfigurationIsValid()` do AutoMapper (passo 19): falha o build/CI se algum `Profile` tiver mapeamento incompleto/ambíguo.                                                                                    |
| #1/#3/#10 (fluxo de UI, se houver tempo/infra) | Testes de componente (Testing Library) para `EmployeeListPage` (renderiza linhas + botão "+") e `EmployeeDetailPage` (renderiza worklogs), como cobertura best-effort — não bloqueantes para aprovação.                       |
| #1/#3/#10 (hooks, se houver tempo/infra)       | Testes de hook (`renderHook` do Testing Library, com `QueryClientProvider` mockado) para `useEmployees`/`useEmployee`, como cobertura best-effort — não bloqueantes para aprovação.                                          |

## Riscos e dependências

- **Ausência de autenticação**: a aplicação fica aberta a qualquer usuário com acesso
  à URL. Assumido como aceitável para este MVP (uso interno), mas deve ser validado
  explicitamente com o responsável antes de ir para produção.
- **Evento `contextmenu` em mobile**: nem todos os navegadores/touch-devices disparam
  `contextmenu` de forma consistente em "pressionar e segurar"; caso o smoke test
  (passo 32) mostre inconsistência, pode ser necessário um fallback com
  `onTouchStart`/`onTouchEnd` + timer manual (não incluído no escopo original deste
  plano, seria um ajuste pontual).
- **Convenção mês=30d/ano=365d**: é uma aproximação; se o negócio precisar de meses/anos
  calendário reais (considerando fevereiro, anos bissextos etc.), o parser precisará
  ser revisto — fora de escopo aqui pois não foi solicitado.
- **Mapeamento AutoMapper para entidades com construtor validante**: como `Employee`
  e `EmployeeWorkLog` não têm setters públicos livres, o AutoMapper "puro" (baseado
  em convenção de propriedades) não consegue construir essas entidades diretamente a
  partir de um request em todos os casos — a implementação precisa decidir, por
  entidade, entre (a) mapear request → objeto de parâmetros simples e deixar o
  use-case chamar o construtor/`Update`, ou (b) configurar um `ConstructUsing`/
  `ForMember` explícito no `Profile`. Isso é detalhado na seção de mapeamento acima,
  mas a escolha fina fica para a implementação, caso a caso.
- **Projeto greenfield**: como `api/` e `app/` estão vazios, os passos 1 e 20
  (bootstrap) aumentam o tamanho da primeira entrega; risco de escopo
  "infraestrutura" competir com o tempo da feature em si, mas é inevitável para a
  primeira tarefa do repositório.
- **`docs/padroes-desenvolvimento.md`** ainda está com placeholders; a implementação
  deve preenchê-lo com o nome real da solution (`WorkLogManager`) e as convenções
  efetivamente adotadas neste plano (incluindo AutoMapper + `.ToModel()`/
  `.ToResponse()`, camadas de front-end com `hooks/`, e o layout sem sidebar), para
  que as próximas tarefas herdem o padrão.

## Fora de escopo / não será feito

- Autenticação/autorização de usuários.
- Relatórios/dashboards agregados de horas extras/faltas (ex.: totais mensais,
  exportação).
- Internacionalização (i18n) — front-end em português fixo.
- Suporte a múltiplas empresas/organizações (multi-tenant) — `system_settings` é um
  singleton global.
- Calendário real para conversão de meses/anos no parser de duração (usa aproximação
  fixa 30d/365d).
- Exibição de `CreatedAtUtc`/`UpdatedAtUtc` na UI (campos existem no banco/entidades
  para auditoria, mas nenhum DTO de response os expõe nesta versão).

---

## Adendo pós-review (ajustes arquiteturais)

> Aplicado após o code review (`.claude/plans/worklog-manager/code-review.md`), a pedido
> explícito do responsável. Este adendo **substitui** a abordagem de mapeamento descrita
> na seção "Mapeamento DTO ↔ domínio (AutoMapper + extension methods)" acima; o restante
> do plano permanece válido.

1. **FluentValidation nos use-cases**: cada use-case de Create/Update
   (`CreateEmployeeUseCase`, `UpdateEmployeeUseCase`, `CreateEmployeeWorkLogUseCase`,
   `UpdateEmployeeWorkLogUseCase`, `UpdateSystemSettingsUseCase`) injeta
   `IValidator<T>` (FluentValidation, pacotes `FluentValidation` e
   `FluentValidation.DependencyInjectionExtensions` no projeto `Application`) e chama
   `ValidateAndThrowAsync` no início de `ExecuteAsync`, antes de tocar no repositório.
   `CreateEmployeeUseCase`/`UpdateEmployeeUseCase` compartilham `EmployeeValidator`; o
   mesmo vale para o par Create/Update de `EmployeeWorkLog` com
   `EmployeeWorkLogValidator`. `ValidationException` (FluentValidation) é tratada pelo
   mesmo `ExceptionHandlingMiddleware` e mapeada para HTTP 400, ao lado de
   `DomainException`.
2. **Entidades atravessam a fronteira Api → Application diretamente**: os use-cases de
   Create/Update recebem a entidade de domínio (`Employee`, `EmployeeWorkLog`,
   `SystemSettings`) como parâmetro, populada pelo AutoMapper a partir do request na
   Api — não existe mais nenhum objeto de parâmetros intermediário
   (`Api/Mapping/Models/` foi removido). Use-cases de `Update` recebem o id da rota
   como parâmetro separado, já que ele não faz parte do corpo do request.
3. **Entidades simplificadas (decisão de design)**: como a validação de "regras de
   entrada do usuário" migrou para os validadores FluentValidation, `Employee`,
   `EmployeeWorkLog` e `SystemSettings` deixaram de ter construtor/`Update` validante e
   passaram a ser property bags com setters `internal` (mutáveis apenas de dentro do
   assembly `Application`; o projeto de testes ganha acesso via `InternalsVisibleTo`),
   populados por reflexão pelo AutoMapper. Isso resolve o atrito original entre
   "entidade com invariantes protegidas" e "AutoMapper mapeando por convenção" citado
   no plano original. O domínio não fica totalmente anêmico: `EmployeeWorkLog` mantém
   `CalculateDuration()` (deriva `DurationSeconds` de `StartDate`/`EndDate` e ainda
   guarda a invariante "end >= start" como defesa em profundidade, por ser um valor
   derivado/computado, não uma entrada bruta do usuário) e todas as entidades ganham um
   método `Touch()` para centralizar a atualização de `UpdatedAtUtc`.
4. **Mapeamento direto Request → Entity nos `Profile`s do AutoMapper**: os `Profile`s
   mapeiam `CreateEmployeeRequest`/`UpdateEmployeeRequest` → `Employee`,
   `CreateEmployeeWorkLogRequest`/`UpdateEmployeeWorkLogRequest` → `EmployeeWorkLog`,
   `UpdateSystemSettingsRequest` → `SystemSettings`, ignorando explicitamente
   (`.ForMember(..., opt => opt.Ignore())`) os campos que o use-case é responsável por
   atribuir/derivar (`Id`, `CreatedAtUtc`, `UpdatedAtUtc`, `DurationSeconds`,
   `EmployeeId` no corpo do work log).
5. **Métodos de extensão `.ToModel()`/`.ToResponse()` removidos**: `Api/Mapping/Extensions/`
   foi eliminado; os endpoints chamam `IMapper.Map<>()` explicitamente (ex.:
   `var employee = mapper.Map<Employee>(request);` /
   `return Results.Ok(mapper.Map<EmployeeSummaryResponse>(employee));`).

Ver `docs/padroes-desenvolvimento.md` (seções "Mapeamento DTO ↔ domínio" e "Validação
de entrada") para o detalhamento final dessas convenções.

---

## Status: aguardando aprovação

Nenhuma implementação deve começar até este plano ser aprovado explicitamente pelo
responsável.

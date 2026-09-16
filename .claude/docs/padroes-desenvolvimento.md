# Padrões de desenvolvimento deste projeto

> Preencha com os padrões reais deste repositório. Os agentes leem este arquivo como
> fonte da verdade para convenções, em conjunto com `.claude/docs/stack.md`.

## Nome da solution / projeto

- `<ProjectName>`: `WorkLogManager`

## Nomenclatura

- Classes, métodos, variáveis, comentários, nomes de migration: inglês (sempre).
- Use-cases: `<Ação><Entidade>UseCase` (ex.: `CreateEmployeeUseCase`,
  `GetEmployeeByIdUseCase`, `DeleteEmployeeWorkLogUseCase`).
- Componentes React: PascalCase, um componente por arquivo.
- Tabelas/colunas no banco: snake_case (via `EFCore.NamingConventions`,
  `.UseSnakeCaseNamingConvention()` no `DbContext`).

## Estrutura de pastas do front-end (`app/`)

- `src/components/ui/` — componentes shadcn instalados (não recriar do zero).
- `src/components/` — componentes genéricos reutilizáveis entre features
  (`Navbar.tsx`, `Layout.tsx`, `DeleteConfirmDialog.tsx`).
- `src/features/<feature>/` — páginas e componentes específicos de cada feature
  (`employees`, `work-logs`, `settings`), com subpasta `hooks/` própria.
- `src/hooks` (dentro de cada `features/<feature>/hooks/`) — hooks customizados que
  encapsulam TanStack Query (`useQuery`/`useMutation`), chamando `lib/api`.
  **Regra obrigatória**: componentes de página (`features/<feature>/*Page.tsx`) nunca
  chamam `lib/api` diretamente — sempre passam por um hook da camada `hooks`.
- `src/lib/api/` — camada de acesso a dados pura (fetch/HTTP via `client.ts`), sem
  estado de cache nem React: `employees.ts`, `workLogs.ts`, `systemSettings.ts`,
  `types.ts` (tipos espelhando os DTOs da Api).
- `src/lib/worklog-duration.ts` — utilitário puro (`parseDuration`/`formatDuration`),
  sem dependência de React nem de rede; espelha a lógica de
  `WorkLogManager.Application.Services.WorkLogDurationParser` do back-end.

## Layout (sem sidebar)

- Não há componente de sidebar em nenhuma tela.
- `src/components/Navbar.tsx`: barra fixa no topo (`fixed top-0 left-0 right-0`,
  `w-full`), com o conteúdo interno dentro de `container mx-auto`.
- `src/components/Layout.tsx`: `<Navbar />` + `<main>` com `container mx-auto` (e
  `pt-20` para compensar a altura do navbar fixo) envolvendo `<Outlet />` do React
  Router. Todas as rotas (`/`, `/employees/:id`, `/settings`) são filhas desse layout.
- Nenhuma tela usa `w-screen`/`max-w-none` no conteúdo principal.
- Este projeto usa Tailwind CSS v4 (config via `@theme`/CSS, sem `tailwind.config.ts`
  clássico); a centralização do `container` é aplicada explicitamente combinando as
  classes `container mx-auto` em vez de depender de `center: true` no config JS (que
  não existe mais no formato v4).

## Estrutura de pastas do back-end (`api/`)

- `src/WorkLogManager.Api/Endpoints/<Feature>Endpoints.cs` — Minimal API, `MapGroup`
  por feature.
- `src/WorkLogManager.Api/Dtos/<Feature>/` — records de request/response.
- `src/WorkLogManager.Api/Mapping/Profiles/` — `Profile`s do AutoMapper, um por
  feature, mapeando request DTOs diretamente para entidades de domínio (ver seção de
  mapeamento abaixo).
- `src/WorkLogManager.Api/Middleware/` — middleware único de tratamento de exceções.
- `src/WorkLogManager.Application/Entities/` — entidades de domínio.
- `src/WorkLogManager.Application/Validators/` — `AbstractValidator<T>` (FluentValidation)
  por tipo de entidade de entrada (ver seção de validação abaixo).
- `src/WorkLogManager.Application/Services/` — serviços de domínio sem estado (ex.:
  `WorkLogDurationParser`).
- `src/WorkLogManager.Application/UseCases/<Feature>/` — um use-case por classe.
- `src/WorkLogManager.Application/Interfaces/` — interfaces (ports) implementadas pela
  Infrastructure.
- `src/WorkLogManager.Application/Results/` — objetos de resultado agregados (não
  persistidos) retornados por use-cases (ex.: `EmployeeDetailResult`).
- `src/WorkLogManager.Application/Common/` — exceções compartilhadas (`DomainException`,
  `NotFoundException`).
- `src/WorkLogManager.Infrastructure/Persistence/` — `WorkLogManagerDbContext`,
  `Configurations/` (Fluent API), `Repositories/`, `Migrations/`.

## Mapeamento DTO ↔ domínio (Api) — convenção atual (pós-review)

> Esta seção substitui a convenção original (v2 do plano) de AutoMapper +
> `.ToModel()`/`.ToResponse()` + objetos de parâmetros intermediários. Ver
> `.claude/plans/worklog-manager/plano-desenvolvimento.md`, seção "Adendo pós-review",
> para o histórico da decisão.

- A Api usa AutoMapper (`Profile`s em `Api/Mapping/Profiles`), mas os endpoints chamam
  `IMapper.Map<>()` **explicitamente** — não existem mais métodos de extensão
  `.ToModel()`/`.ToResponse()` nem objetos de parâmetros intermediários
  (`Api/Mapping/Extensions/`, `Api/Mapping/Models/` foram removidos).
- Os `Profile`s mapeiam o request DTO **diretamente** para a entidade de domínio da
  `Application` (`CreateEmployeeRequest`/`UpdateEmployeeRequest` → `Employee`,
  `CreateEmployeeWorkLogRequest`/`UpdateEmployeeWorkLogRequest` → `EmployeeWorkLog`,
  `UpdateSystemSettingsRequest` → `SystemSettings`). Campos que o use-case é
  responsável por atribuir/derivar (`Id`, `CreatedAtUtc`, `UpdatedAtUtc`,
  `DurationSeconds`, `EmployeeId` no corpo do work log) são explicitamente ignorados no
  `Profile` (`.ForMember(dest => dest.X, opt => opt.Ignore())`).
- Endpoint típico:
  ```csharp
  var employee = mapper.Map<Employee>(request);
  var created = await createUseCase.ExecuteAsync(employee, cancellationToken);
  return Results.Created($"/employees/{created.Id}", mapper.Map<EmployeeSummaryResponse>(created));
  ```
- Os use-cases de Create/Update recebem a **entidade de domínio** (`Employee`,
  `EmployeeWorkLog`, `SystemSettings`) diretamente como parâmetro — nunca um objeto de
  parâmetros intermediário nem uma lista de primitivos soltos. Use-cases de `Update`
  também recebem o id da rota separadamente (ex.:
  `ExecuteAsync(Guid employeeId, Employee employee, ...)`), já que o id normalmente vem
  da rota, não do corpo do request.
- Entidades (`Employee`, `EmployeeWorkLog`, `SystemSettings`) não têm mais construtor
  validante nem `Update` validante: são property bags simples com setters `internal`
  (mutáveis apenas de dentro do assembly `Application`, via `InternalsVisibleTo` para o
  projeto de testes), populadas por reflexão pelo AutoMapper. `EmployeeWorkLog` mantém
  um método de domínio, `CalculateDuration()`, que deriva `DurationSeconds` a partir de
  `StartDate`/`EndDate` e ainda guarda a invariante "end date >= start date" como
  defesa em profundidade (é um valor derivado, não um dado de entrada bruto). Todas as
  entidades têm um método `Touch()` para atualizar `UpdatedAtUtc`.
- DTOs de response com `ForMember` customizado (ex.: `EmployeeDetailResponse`, que
  agrega campos de `EmployeeDetailResult.Employee` e do próprio resultado) continuam
  declarados com propriedades `init` em vez de construtor posicional, para que o
  AutoMapper resolva o mapeamento por membro em vez de tentar casar argumentos de
  construtor.

## Validação de entrada (Application) — FluentValidation

- Toda validação de "regras de entrada do usuário" (nome/cargo obrigatórios, horas
  diárias > 0, data final >= data inicial) migrou das entidades para validadores
  FluentValidation em `src/WorkLogManager.Application/Validators/`: um
  `AbstractValidator<T>` por tipo de entidade de entrada (`EmployeeValidator`,
  `EmployeeWorkLogValidator`, `SystemSettingsValidator`).
- Cada use-case que recebe uma entidade como input injeta `IValidator<T>`
  (FluentValidation) e chama `ValidateAndThrowAsync` no início de `ExecuteAsync`, antes
  de tocar no repositório. `ValidationException` (FluentValidation) é tratada pelo
  mesmo middleware de exceções e mapeada para HTTP 400, assim como `DomainException`.
- Registro em DI: `builder.Services.AddValidatorsFromAssembly(typeof(EmployeeValidator).Assembly)`
  em `Program.cs` da `Api` (pacote `FluentValidation.DependencyInjectionExtensions`).
- `CreateEmployeeUseCase`/`UpdateEmployeeUseCase` compartilham `EmployeeValidator`
  (mesmas regras para criar e atualizar); o mesmo vale para
  `CreateEmployeeWorkLogUseCase`/`UpdateEmployeeWorkLogUseCase` com
  `EmployeeWorkLogValidator`.

## Tratamento de erros

- `DomainException` (Application) → HTTP 400 com corpo `{ message }`.
- `ValidationException` (FluentValidation, lançada pelos use-cases via
  `ValidateAndThrowAsync`) → HTTP 400 com corpo `{ message }` (mensagens de todas as
  regras violadas concatenadas).
- `NotFoundException` (Application) → HTTP 404 com corpo `{ message }`.
- Tradução feita por um único middleware (`Api/Middleware/ExceptionHandlingMiddleware.cs`)
  registrado em `Program.cs`.

## Autenticação/autorização (se aplicável)

- Fora de escopo nesta versão do sistema (aplicação single-user, uso interno).

## Commits e branches

- Os agentes deste fluxo NUNCA fazem commit — apenas alteram arquivos no working
  directory. Convenção de commit/branch é aplicada manualmente pelo responsável.

## Outras convenções relevantes

- Campos de auditoria em todas as entidades: `CreatedAtUtc`/`UpdatedAtUtc`
  (`DateTimeOffset`, sempre `DateTimeOffset.UtcNow`).
- Migrations do EF Core são sempre geradas via `dotnet ef migrations add <Name>
--project src/WorkLogManager.Infrastructure --startup-project src/WorkLogManager.Api`
  e nunca aplicadas a um banco real pelos agentes (`dotnet ef database update` é ação
  manual do responsável).
- Testes de back-end: xUnit + Moq, em `tests/WorkLogManager.Application.Tests`
  (use-cases e entidades) e `tests/WorkLogManager.Api.Tests` (validação de
  configuração do AutoMapper via `AssertConfigurationIsValid()`).
- Testes de front-end: Vitest + Testing Library, arquivos `*.test.ts`/`*.test.tsx`
  colocados ao lado do código testado.

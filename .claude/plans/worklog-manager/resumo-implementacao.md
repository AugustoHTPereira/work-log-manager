# Resumo da implementação: Work Log Manager

Task-id: `worklog-manager`

Implementação completa do plano aprovado em `plano-desenvolvimento.md`, incluindo o
bootstrap greenfield da solution .NET (`api/`) e do projeto React (`app/`).

## Back-end (`api/`)

Solution `WorkLogManager.sln` com 3 projetos de produção + 2 de teste:

### `src/WorkLogManager.Application`
- `Common/DomainException.cs`, `Common/NotFoundException.cs` — exceções traduzidas
  pela Api em 400/404.
- `Entities/Employee.cs`, `Entities/EmployeeWorkLog.cs`, `Entities/WorkLogType.cs`,
  `Entities/SystemSettings.cs` — construtores/`Update` validantes, `CreatedAtUtc`/
  `UpdatedAtUtc` em UTC.
- `Services/WorkLogDurationParser.cs` — `Parse`/`Format`, tabela s/m/h/d/S/M/A
  case-sensitive, mês=30d/ano=365d.
- `Interfaces/IEmployeeRepository.cs`, `IEmployeeWorkLogRepository.cs`,
  `ISystemSettingsRepository.cs`.
- `Results/EmployeeDetailResult.cs`.
- `UseCases/Employees/*` (Create/Update/Delete/GetById/List),
  `UseCases/EmployeeWorkLogs/*` (Create/Update/Delete/ListByEmployee),
  `UseCases/SystemSettings/*` (Get/Update, com criação lazy do singleton).

### `src/WorkLogManager.Infrastructure`
- `Persistence/WorkLogManagerDbContext.cs` (`UseSnakeCaseNamingConvention()`).
- `Persistence/Configurations/EmployeeConfiguration.cs`,
  `EmployeeWorkLogConfiguration.cs` (FK cascade, check `end_date >= start_date`,
  conversão de enum para string), `SystemSettingsConfiguration.cs` (seed do
  singleton via `HasData`).
- `Persistence/Repositories/EmployeeRepository.cs`, `EmployeeWorkLogRepository.cs`,
  `SystemSettingsRepository.cs`.
- `Persistence/Migrations/20260914233436_InitialCreate.cs` — gerada com
  `dotnet ef migrations add InitialCreate --project src/WorkLogManager.Infrastructure
  --startup-project src/WorkLogManager.Api`, **não aplicada** a nenhum banco. Colunas
  em snake_case (`created_at_utc`, `updated_at_utc`, etc.), constraints exatamente
  como especificado no plano, `InsertData` do singleton `system_settings` com 8h.

### `src/WorkLogManager.Api`
- `Dtos/Employees/*`, `Dtos/EmployeeWorkLogs/*`, `Dtos/SystemSettings/*` — records
  conforme especificação do plano.
- `Mapping/Profiles/EmployeeMappingProfile.cs`, `EmployeeWorkLogMappingProfile.cs`,
  `SystemSettingsMappingProfile.cs` — AutoMapper `Profile`s.
- `Mapping/Models/EmployeeModels.cs`, `EmployeeWorkLogModels.cs`,
  `SystemSettingsModels.cs` — modelos de entrada simples (`CreateEmployeeModel` etc.)
  usados porque `Employee`/`EmployeeWorkLog` têm construtor validante sem setters
  públicos (nota do plano na seção de mapeamento).
- `Mapping/Extensions/RequestMappingExtensions.cs`, `ResponseMappingExtensions.cs`
  — `.ToModel()`/`.ToResponse()` delegando para `IMapper`.
- `Endpoints/EmployeesEndpoints.cs`, `EmployeeWorkLogsEndpoints.cs`,
  `SystemSettingsEndpoints.cs` — Minimal API via `MapGroup`.
- `Middleware/ExceptionHandlingMiddleware.cs` — único middleware de exceção
  (`DomainException` → 400, `NotFoundException` → 404).
- `Program.cs` — DI (repositórios, use-cases), `AddAutoMapper`, `AddDbContext` +
  `UseSnakeCaseNamingConvention`, CORS para `http://localhost:5173`, exception
  middleware, mapeamento dos 3 grupos de endpoints.
- `appsettings.json` — `ConnectionStrings:WorkLogManagerDb` (placeholder local).

### Testes back-end
- `tests/WorkLogManager.Application.Tests` (xUnit + Moq): 36 testes —
  `WorkLogDurationParserTests`, `EmployeeTests`, `EmployeeWorkLogTests`,
  `CreateEmployeeUseCaseTests`, `UpdateEmployeeUseCaseTests`,
  `DeleteEmployeeUseCaseTests`, `GetEmployeeByIdUseCaseTests`,
  `CreateEmployeeWorkLogUseCaseTests`, `DeleteEmployeeWorkLogUseCaseTests`.
- `tests/WorkLogManager.Api.Tests` (xUnit): 1 teste —
  `AutoMapperConfigurationTests.Configuration_AllProfiles_AreValid`
  (`AssertConfigurationIsValid()`).
- **Desvio do plano**: o plano só menciona `tests/WorkLogManager.Application.Tests` na
  criação da solution (passo 1), mas o passo 19 pede um teste de
  `AssertConfigurationIsValid()` do AutoMapper — que vive na `Api`, não pode ser
  testado a partir da `Application` sem violar a regra de dependência
  (`Application` nunca referencia `Api`). Criei um segundo projeto de teste,
  `tests/WorkLogManager.Api.Tests`, apenas para esse teste de configuração.
- Resultado: `dotnet test` → **37 aprovados, 0 falhas**.

## Front-end (`app/`)

Bootstrap Vite + React 19 + TypeScript, Tailwind CSS v4 (via `@tailwindcss/vite`,
sem `tailwind.config.ts` clássico — ver nota de desvio abaixo), shadcn/ui (estilo
`new-york`), React Router, TanStack Query, react-hook-form + zod, Vitest + Testing
Library.

### Camadas
- `src/lib/api/client.ts`, `employees.ts`, `workLogs.ts`, `systemSettings.ts`,
  `types.ts` — acesso HTTP puro, sem hooks.
- `src/lib/worklog-duration.ts` — `parseDuration`/`formatDuration`, espelhando o
  back-end.
- `src/features/employees/hooks/` (`useEmployees`, `useEmployee`,
  `useCreateEmployee`, `useUpdateEmployee`, `useDeleteEmployee`),
  `src/features/work-logs/hooks/` (`useCreateWorkLog`, `useUpdateWorkLog`,
  `useDeleteWorkLog`), `src/features/settings/hooks/` (`useSystemSettings`,
  `useUpdateSystemSettings`) — TanStack Query, únicos consumidores de `lib/api`.
- `src/components/Navbar.tsx`, `src/components/Layout.tsx` — layout sem sidebar,
  navbar fixo full-width com conteúdo em `container mx-auto`, `<main>` com
  `container mx-auto pt-20`.
- `src/components/DeleteConfirmDialog.tsx` — `AlertDialog` genérico reutilizado.
- `src/features/employees/EmployeeListPage.tsx`, `EmployeeFormModal.tsx`,
  `EmployeeDetailPage.tsx`.
- `src/features/work-logs/WorkLogFormModal.tsx` — sincronização bidirecional
  data↔duração com `lastEditedField` (`useRef`), debounce de ~400ms no campo de
  duração.
- `src/features/settings/SystemSettingsPage.tsx`.
- `src/App.tsx` (rotas `/`, `/employees/:id`, `/settings` dentro de `Layout`),
  `src/main.tsx` (`QueryClientProvider`, `BrowserRouter`, `Toaster`).
- `components.json`, `vite.config.ts` (alias `@/*`, plugin Tailwind v4),
  `vitest.config.ts`, `src/test/setup.ts`.

### Testes front-end
- `src/lib/worklog-duration.test.ts` — 11 testes (paridade com
  `WorkLogDurationParserTests` do back-end).
- `src/features/employees/hooks/useEmployees.test.tsx` — 1 teste de hook
  (best-effort, `renderHook` + `QueryClientProvider`).
- `src/features/employees/EmployeeListPage.test.tsx` — 1 teste de componente
  (best-effort, renderiza linha de funcionário + botão "novo funcionário").
- Resultado: `npx vitest run` → **13 aprovados, 0 falhas**.
- `npx tsc -b` e `npm run build` executados sem erros; `npm run lint` (oxlint) sem
  erros (apenas 3 warnings pré-existentes/esperados: 2 em arquivos gerados pelo
  shadcn CLI, 1 sobre `setState` dentro de `useEffect` no `WorkLogFormModal`, que é a
  forma intencional de resetar o formulário ao abrir o modal).

## Mapeamento passo do plano → arquivo(s)

| Passo | Arquivo(s) |
| --- | --- |
| 1 | `api/WorkLogManager.sln`, `api/src/*/*.csproj`, `api/tests/WorkLogManager.Application.Tests/*.csproj` |
| 2 | `Entities/WorkLogType.cs`, `Entities/Employee.cs` |
| 3 | `Entities/EmployeeWorkLog.cs` |
| 4 | `Entities/SystemSettings.cs` |
| 5 | `Services/WorkLogDurationParser.cs` |
| 6 | `Interfaces/I*Repository.cs` |
| 7 | `UseCases/Employees/*` |
| 8 | `UseCases/EmployeeWorkLogs/*` |
| 9 | `UseCases/SystemSettings/*` |
| 10 | `Infrastructure/Persistence/WorkLogManagerDbContext.cs`, `Configurations/*` |
| 11 | `Infrastructure/Persistence/Repositories/*` |
| 12 | `Infrastructure/Persistence/Migrations/20260914233436_InitialCreate.cs` |
| 13 | `Api/Dtos/**/*.cs` |
| 14 | `Api/Mapping/Profiles/*` |
| 15 | `Api/Mapping/Extensions/*`, `Api/Mapping/Models/*` |
| 16 | `Api/Endpoints/*` |
| 17 | `Api/Program.cs`, `Api/appsettings.json` |
| 18 | `api/tests/WorkLogManager.Application.Tests/**` |
| 19 | `api/tests/WorkLogManager.Api.Tests/Mapping/AutoMapperConfigurationTests.cs` |
| 20 | `app/package.json`, `app/vite.config.ts`, `app/components.json`, `app/src/index.css` |
| 21 | `app/src/lib/api/client.ts`, `employees.ts`, `workLogs.ts`, `systemSettings.ts` |
| 22 | `app/src/features/*/hooks/*` |
| 23 | `app/src/lib/worklog-duration.ts`, `worklog-duration.test.ts` |
| 24 | `app/src/components/Navbar.tsx`, `Layout.tsx` |
| 25 | `app/src/features/employees/EmployeeListPage.tsx` |
| 26 | `app/src/features/employees/EmployeeFormModal.tsx` |
| 27 | `app/src/features/employees/EmployeeDetailPage.tsx` |
| 28 | `app/src/features/work-logs/WorkLogFormModal.tsx` |
| 29 | `app/src/components/DeleteConfirmDialog.tsx` |
| 30 | `app/src/features/settings/SystemSettingsPage.tsx`, `Navbar.tsx` (link) |
| 31 | `app/src/App.tsx` |
| 32 | Não executado (smoke test manual ponta a ponta requer banco real e os dois servidores rodando; fora do escopo de testes automatizados, conforme o próprio plano indica, e não posso subir um Postgres real). Sinalizado como pendência para o responsável antes do code review/deploy. |

## Mapeamento critério de aceite → teste(s)

| Critério | Teste(s) |
| --- | --- |
| #1/#3/#10 (UI, best-effort) | `EmployeeListPage.test.tsx`, `useEmployees.test.tsx` |
| #2/#15 (CRUD funcionário) | `CreateEmployeeUseCaseTests`, `UpdateEmployeeUseCaseTests`, `DeleteEmployeeUseCaseTests` |
| #6 (parser) | `WorkLogDurationParserTests` (back-end), `worklog-duration.test.ts` (front-end) |
| #7/#8 (sincronização data↔duração) | `worklog-duration.test.ts` cobre a lógica pura de parse/format usada pelo `WorkLogFormModal`; a integração da sincronização em si (efeitos/estado do modal) não tem teste automatizado dedicado — cobertura best-effort não obrigatória pelo plano |
| #9 (duração calculada, EndDate < StartDate) | `EmployeeWorkLogTests`, `CreateEmployeeWorkLogUseCaseTests` |
| #12 (exclusão de worklog) | `DeleteEmployeeWorkLogUseCaseTests` |
| #13/#14 (horas padrão/efetivas) | `GetEmployeeByIdUseCaseTests` |
| (mapeamento, transversal) | `AutoMapperConfigurationTests` |

## Desvios do plano (com justificativa)

1. **Segundo projeto de teste `WorkLogManager.Api.Tests`**: necessário para o teste de
   `AssertConfigurationIsValid()` do AutoMapper (passo 19) sem violar a regra
   `Application` nunca referencia `Api`. O plano só citava explicitamente
   `WorkLogManager.Application.Tests` na criação da solution.
2. **Versões de pacote NuGet ajustadas**: o plano não fixou versões. Usei
   `Microsoft.EntityFrameworkCore`/`.Design` `10.0.4`, `Npgsql.EntityFrameworkCore.PostgreSQL`
   `10.0.3`, `EFCore.NamingConventions` `10.0.1`, `AutoMapper` `13.0.1` (última versão
   MIT antes da mudança de licenciamento em versões mais recentes do AutoMapper) — a
   combinação foi escolhida por ser a mínima compatível com .NET 10 sem conflitos de
   downgrade do NuGet.
3. **`EmployeeDetailResponse` declarado com propriedades `init` em vez de record
   posicional**: o AutoMapper 13 não conseguiu mapear um `ForMember` customizado para
   um record posicional (`AutoMapperConfigurationException: No available constructor`).
   A troca para propriedades `init`-only é mencionada no próprio plano como uma opção
   válida ("a escolha fina fica para a implementação, caso a caso" na nota sobre
   AutoMapper + construtor validante) e não muda o contrato JSON da API.
4. **Tailwind CSS v4 em vez de v3**: o `create-vite` + `npm install tailwindcss`
   disponível trouxe Tailwind v4, que não usa mais `tailwind.config.ts` com
   `container: { center: true }`. Repliquei a centralização exigida pelo plano
   aplicando explicitamente as classes `container mx-auto` no `Navbar`/`Layout` (o
   resultado visual é o mesmo). Documentado em `docs/padroes-desenvolvimento.md`.
5. **shadcn CLI mais recente usa o pacote npm `cn` em vez de `src/lib/utils.ts`**: os
   componentes gerados (`src/components/ui/*`) importam `cn` de `"cn"` (pacote npm)
   em vez de `@/lib/utils`. Mantive essa convenção (removi o `utils.ts` que eu havia
   criado manualmente, redundante) para não divergir do código gerado pelo shadcn.
6. **Passo 32 (smoke test manual ponta a ponta) não executado**: exigiria subir um
   PostgreSQL real e aplicar a migration, o que é vedado para este agente. Sinalizado
   como pendência explícita para o responsável antes do code review/deploy.

## Pendências para o responsável

- Rodar `dotnet ef database update` manualmente contra um PostgreSQL real (a migration
  `InitialCreate` já está gerada e revisada) e então executar o smoke test manual do
  passo 32.
- Validar a string de conexão em `api/src/WorkLogManager.Api/appsettings.json`
  (placeholder local) e ajustar `VITE_API_BASE_URL` no front, se necessário.

---

## Atualização pós-review (correções do `code-review.md` + mudanças arquiteturais)

Esta seção documenta o trabalho aplicado depois da aprovação com ressalvas. Duas
frentes: (1) os 4 itens do `code-review.md`; (2) a migração de mapeamento/validação
pedida explicitamente pelo responsável (ver "Adendo pós-review" em
`plano-desenvolvimento.md`).

### 1. Itens do code review

- **Testes de `GetSystemSettingsUseCase`/`UpdateSystemSettingsUseCase`**: criados
  `api/tests/WorkLogManager.Application.Tests/UseCases/SystemSettings/GetSystemSettingsUseCaseTests.cs`
  (singleton já existente é reaproveitado sem `UpsertAsync` extra; ausência de registro
  cria o default 8h e persiste) e `UpdateSystemSettingsUseCaseTests.cs` (atualização
  idempotente, criação lazy antes de atualizar, validação de horas não positivas).
- **Teste de integração da sincronização data↔duração**: criado
  `app/src/features/work-logs/WorkLogFormModal.test.tsx` (4 testes) cobrindo: editar
  datas recalcula o campo de duração; editar a duração (após o debounce de 400ms)
  recalcula a data final sem mexer na data inicial; token de duração inválido mostra
  erro inline e não altera as datas.
- **Avisos NuGet (`NU1903`)**:
  - `Microsoft.OpenApi` (transitivo via `Microsoft.AspNetCore.OpenApi`): resolvido
    fazendo bump de `Microsoft.AspNetCore.OpenApi` `10.0.1` → `10.0.12` (mesma major,
    só patches), que passa a exigir `Microsoft.OpenApi >= 2.12.0` (a vulnerabilidade
    GHSA-v5pm-xwqc-g5wc afeta até `2.7.4`). Nenhuma mudança de código necessária. O
    aviso não aparece mais em `dotnet build`/`dotnet test`.
  - `AutoMapper` `13.0.1`: **não foi possível resolver sem trocar de licença**. A
    vulnerabilidade GHSA-rvv3-g6hj-g44x (DoS por recursão não controlada em grafos de
    objeto profundamente aninhados) só tem correção a partir de `15.1.1`/`16.1.1+`.
    Confirmei (baixando os `.nuspec`/`LICENSE.md` de cada versão do NuGet) que o
    AutoMapper passou de licença MIT para RPL1.5/comercial (Lucky Penny Software) a
    partir da versão `15.0.0` — ou seja, toda versão MIT (`<= 14.0.0`) ainda está na
    faixa vulnerável, e toda versão corrigida já não é MIT. Mantive `13.0.1` e
    documentei a decisão como comentário no `.csproj` e em
    `docs/padroes-desenvolvimento.md`: o risco residual é aceito porque os tipos
    mapeados neste projeto são DTOs/entidades planos, sem grafos circulares/auto-
    referenciados, então a explorabilidade prática da recursão descontrolada é
    efetivamente nula aqui.
- **`.gitignore` na raiz**: criado cobrindo `bin/`/`obj/`/`*.user`/`.vs/` (.NET),
  `node_modules/`/`dist/`/`.vite/` (Node/Vite), `.DS_Store`, arquivos `.env*`.

### 2. Mudanças arquiteturais (FluentValidation + Entity direto na fronteira)

**Back-end (`api/`)**:
- `src/WorkLogManager.Application/Validators/EmployeeValidator.cs`,
  `EmployeeWorkLogValidator.cs`, `SystemSettingsValidator.cs` (novos) — um
  `AbstractValidator<T>` por entidade de entrada.
- `src/WorkLogManager.Application/WorkLogManager.Application.csproj` — adicionados
  `FluentValidation`/`FluentValidation.DependencyInjectionExtensions` `12.1.1`, e
  `InternalsVisibleTo` para `WorkLogManager.Application.Tests`.
- `src/WorkLogManager.Application/Entities/Employee.cs`, `EmployeeWorkLog.cs`,
  `SystemSettings.cs` — reescritas: sem construtor/`Update` validante, propriedades com
  setter `internal`, método `Touch()` em todas, `CalculateDuration()` em
  `EmployeeWorkLog` (guarda "end >= start" como defesa em profundidade, por ser valor
  derivado).
- `src/WorkLogManager.Application/UseCases/Employees/CreateEmployeeUseCase.cs`,
  `UpdateEmployeeUseCase.cs`,
  `src/WorkLogManager.Application/UseCases/EmployeeWorkLogs/CreateEmployeeWorkLogUseCase.cs`,
  `UpdateEmployeeWorkLogUseCase.cs`,
  `src/WorkLogManager.Application/UseCases/SystemSettings/GetSystemSettingsUseCase.cs`,
  `UpdateSystemSettingsUseCase.cs` — reescritos para receber a entidade diretamente,
  injetar `IValidator<T>` e chamar `ValidateAndThrowAsync` antes do repositório.
- `src/WorkLogManager.Api/Mapping/Profiles/*` — reescritos para mapear
  `Request → Entity` diretamente (com `.Ignore()` nos campos derivados/atribuídos pelo
  use-case), em vez de `Request → Model intermediário`.
- **Removidos**: `src/WorkLogManager.Api/Mapping/Extensions/` (`RequestMappingExtensions.cs`,
  `ResponseMappingExtensions.cs`) e `src/WorkLogManager.Api/Mapping/Models/`
  (`EmployeeModels.cs`, `EmployeeWorkLogModels.cs`, `SystemSettingsModels.cs`).
- `src/WorkLogManager.Api/Endpoints/EmployeesEndpoints.cs`,
  `EmployeeWorkLogsEndpoints.cs`, `SystemSettingsEndpoints.cs` — reescritos para chamar
  `mapper.Map<T>(...)` explicitamente.
- `src/WorkLogManager.Api/Program.cs` — adicionado
  `builder.Services.AddValidatorsFromAssembly(typeof(EmployeeValidator).Assembly)`.
- `src/WorkLogManager.Api/Middleware/ExceptionHandlingMiddleware.cs` — **desvio
  necessário não pedido explicitamente, mas exigido pela consistência funcional**:
  adicionado `catch (ValidationException)` → HTTP 400. Sem isso, os erros de validação
  do FluentValidation (que substituíram os antigos `DomainException` de entrada)
  vazariam como HTTP 500 em vez de 400, quebrando o contrato de erro já documentado no
  plano original ("erro de validação... bloqueado... no back-end, entidade nunca aceita
  X, lança exceção de domínio traduzida para HTTP 400").
- Testes: entidades (`EmployeeTests`, `EmployeeWorkLogTests`) reescritos para testar
  `Touch()`/`CalculateDuration()` em vez de construtor; testes de use-case reescritos
  para construir a entidade via um novo `TestHelpers/EntityFactory.cs` e passá-la ao
  use-case; adicionados `Validators/EmployeeValidatorTests.cs`,
  `EmployeeWorkLogValidatorTests.cs`, `SystemSettingsValidatorTests.cs`, e
  `UseCases/EmployeeWorkLogs/UpdateEmployeeWorkLogUseCaseTests.cs` (não existia antes).
  Resultado: `dotnet test` → **53 aprovados, 0 falhas** (52 em `Application.Tests` + 1
  em `Api.Tests`).

**Decisão de design (item "b" do pedido do usuário)**: as entidades deixaram de ter
qualquer validação de entrada própria; a única lógica de domínio que permanece nelas é
o cálculo derivado (`CalculateDuration`) e o `Touch()` de auditoria. Optei por manter o
guard de "end >= start" dentro de `CalculateDuration()` como defesa em profundidade
(mesmo já validado por `EmployeeWorkLogValidator`) porque `DurationSeconds` é um valor
computado pela própria entidade, não um dado bruto vindo do usuário — se algum código
futuro chamar `CalculateDuration()` fora do fluxo normal do use-case (sem passar pelo
validador), ainda assim não é possível gerar uma duração negativa. As demais regras
("nome obrigatório", "horas > 0") são puramente sobre o formato de entrada do usuário e
vivem exclusivamente nos validadores FluentValidation, sem duplicação na entidade.

**Front-end (`app/`)**: nenhuma mudança de código de produção nesta frente (as
mudanças arquiteturais do pedido do usuário são só de back-end); apenas o novo teste
`WorkLogFormModal.test.tsx` foi adicionado.

### Resultado final dos testes

- `dotnet test` (api/): **53 aprovados, 0 falhas**.
- `npm run test` (app/): **17 aprovados, 0 falhas** (13 anteriores + 4 novos no
  `WorkLogFormModal.test.tsx`).
- `npm run build` (app/, `tsc -b && vite build`): sem erros.

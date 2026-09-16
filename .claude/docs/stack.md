# Stack e convenções — .NET 10 + React (shadcn) + PostgreSQL

Lido pelos agentes `analista-tecnico`, `desenvolvedor` e `revisor` deste repositório.
Ajuste os pontos marcados conforme a realidade real do projeto.

## Estrutura do repositório
```
app/                                   # front-end React
api/
  src/
    <ProjectName>.Api/                 # minimal APIs, DI, composition root
    <ProjectName>.Application/         # use-cases, entidades de domínio, interfaces (ports)
    <ProjectName>.Infrastructure/      # EF Core, repositórios, integrações externas
  tests/
    <ProjectName>.Application.Tests/
  <ProjectName>.sln
```
> Ajuste `<ProjectName>` para o nome real da solution.

## Regra geral de idioma
Todo o código — nomes de classe, método, variável, comentário, nome de migration — é
escrito em **inglês**, independentemente do idioma usado na conversa com o usuário.
Reports, planos e reviews continuam em português.

## Back-end — .NET 10

### Camadas (projetos separados na solution)
- **`<ProjectName>.Api`**: só endpoints (Minimal APIs, agrupados por feature via
  `MapGroup`), DTOs de request/response, validação de entrada, mapeamento DTO ↔
  Application, composição de DI em `Program.cs`, middlewares. Não contém regra de
  negócio nem referencia EF Core diretamente.
- **`<ProjectName>.Application`**: regras de negócio e casos de uso. Cada ação
  relevante do sistema é um **use-case** explícito, uma classe com responsabilidade
  única (ex.: `CreateUserUseCase`, `DeactivateEventUseCase`) — SOLID (SRP). Contém
  também as entidades de domínio, value objects e agregados (DDD), e as **interfaces
  (ports)** que a Infrastructure implementa (ex.: `IUserRepository`,
  `IEventRepository`). Não depende de EF Core nem de nenhuma implementação concreta de
  infraestrutura — só de abstrações.
- **`<ProjectName>.Infrastructure`**: implementações concretas — `DbContext` do EF
  Core, mapeamento de entidades (Fluent API), repositórios que implementam as
  interfaces definidas na Application, integrações externas (email, storage, filas
  etc.), migrations.
- Regra de dependência: `Api` → `Application` → (abstrações); `Infrastructure` →
  `Application` (implementa as interfaces de lá). `Application` nunca referencia
  `Infrastructure` nem `Api`.

### SOLID e DDD na prática
- Injeção de dependência: interfaces na `Application`, implementação na
  `Infrastructure`, registro em `Program.cs` (`Api`).
- Use-cases pequenos e coesos — um caso de uso por classe, sem "god classes" de
  serviço genérico.
- Entidades de domínio protegem seus próprios invariantes (construtores/métodos que
  garantem estado válido, em vez de setters públicos livres).

### EF Core
- `DbContext` e mapeamentos ficam na `Infrastructure`.
- Tabelas e colunas em **snake_case** no banco — configure isso globalmente (ex.:
  pacote `EFCore.NamingConventions` com `.UseSnakeCaseNamingConvention()`, ou Fluent
  API explícita), mantendo as propriedades C# em PascalCase/inglês normalmente.
- **Migrations**: geradas com `dotnet ef migrations add <Name>` (nome em inglês, ex.:
  `AddUsersTable`), com `--project` apontando para `Infrastructure` e
  `--startup-project` para `Api`. Nenhum agente roda `dotnet ef database update` nem
  qualquer comando que aplique a migration em um banco real — isso é sempre uma ação
  manual do responsável.

### Testes
- xUnit para testes unitários dos use-cases e das entidades de domínio na
  `Application` (ajuste aqui se o projeto já usar outro framework, ex. NUnit).
- Ajuste também se houver testes de integração (`<ProjectName>.Api.Tests`) contra um
  banco de teste/in-memory.

## Front-end — React + shadcn/ui
- Diretório `app/`.
- Componentes de UI vêm do `shadcn/ui` (`npx shadcn add <component>`), instalados em
  `app/src/components/ui` — não recrie componentes que já existem no shadcn.
- Componentes de feature/domínio separados dos componentes de UI genéricos (ex.:
  `app/src/features/<feature>/`).
- Chamadas à API centralizadas em uma camada de client HTTP (ex.: `app/src/lib/api/`),
  nunca `fetch`/`axios` espalhado direto nos componentes.
- Ajuste aqui o bundler/roteador real (Vite, React Router, TanStack Query etc.) e o
  framework de teste (ex.: Vitest + Testing Library) — os valores acima são só um
  ponto de partida.

## PostgreSQL
- Tabelas em snake_case, plural (ex.: `users`, `event_registrations`), conforme
  convenção configurada no EF Core — não hardcode nome de coluna manualmente se a
  convenção global já resolve.
- Toda tabela nova precisa de chave primária explícita e, quando fizer sentido,
  constraints de integridade (FK, unique, not null) definidas na migration.

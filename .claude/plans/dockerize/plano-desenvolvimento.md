# Plano de desenvolvimento: Conteinerização do projeto (banco, API, app)

Task-id: dockerize

## Resumo técnico da solução

Conteinerizar os 4 componentes de execução do WorkLogManager — PostgreSQL, um
container dedicado de migrations EF Core, a API .NET 10 e o front-end React estático
servido via nginx — orquestrados por um único `docker-compose.yml` na raiz do
repositório. O banco usa volume nomeado persistente e healthcheck; o container de
migrations aguarda o banco saudável, aplica as migrations pendentes via um bundle
self-contained do EF Core e finaliza; a API só sobe depois que o migration runner
terminar com sucesso; o app (build estático Vite -> nginx) depende apenas do start da
API. Nenhuma regra de negócio é alterada — é puramente infraestrutura — mas duas
pequenas mudanças de código são necessárias para o projeto funcionar corretamente
dentro de containers: (1) tornar a lista de origens de CORS configurável (hoje
hardcoded para `http://localhost:5173`, o Vite dev server) e (2) nenhuma mudança na
connection string em si além de passar a ler o valor via variável de ambiente padrão
do ASP.NET Core, sem tocar em `appsettings.json`/`appsettings.Development.json`
existentes.

## Investigação do repositório (estado atual, confirmado nesta sessão)

- Nenhum artefato Docker existe hoje (`Dockerfile`, `docker-compose.yml`,
  `.dockerignore` — nenhum, em nenhum lugar do repo).
- Connection string: a chave usada no código é `ConnectionStrings:WorkLogManagerDb`
  (não `DefaultConnection`) — ver `Program.cs`:
  `builder.Configuration.GetConnectionString("WorkLogManagerDb")`. A variável de
  ambiente que sobrescreve isso via convenção do ASP.NET Core é
  `ConnectionStrings__WorkLogManagerDb`.
- `appsettings.json` aponta para `Host=localhost;Port=5432;Database=worklogmanager;
  Username=postgres;Password=postgres`; `appsettings.Development.json` sobrescreve com
  `Server=192.168.3.10;...` (indício de Postgres real rodando fora de container hoje —
  não tocar nesses arquivos, só sobrepor via env var no container).
- CORS está hardcoded em `Program.cs`: `policy.WithOrigins("http://localhost:5173")`
  — é o endereço do Vite dev server, não vai bater com a origem do app conteinerizado
  (nginx, porta diferente). Precisa virar configurável (lista de origens lida de
  `appsettings`/env var), senão o app dentro do container não consegue chamar a API por
  causa de CORS.
- `WorkLogManagerDbContext` (`api/src/WorkLogManager.Infrastructure/Persistence/`) tem
  construtor padrão via `DbContextOptions<>`, sem `IDesignTimeDbContextFactory`
  customizado — as migrations já são geradas hoje via
  `dotnet ef migrations add --project Infrastructure --startup-project Api`, então o
  container de migrations pode reaproveitar exatamente esse mesmo par
  `--project`/`--startup-project`.
- Front-end (`app/`): `VITE_API_BASE_URL` já existe e é lido em
  `src/lib/api/client.ts` (`import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5244"`)
  — mecanismo de configuração da URL da API já pronto, só falta ser passado como
  build-arg do Docker (Vite resolve `import.meta.env.*` em **build time**, não em
  runtime; isso é uma limitação inerente de SPA estática, documentada abaixo em
  "Riscos e dependências", não um bug a corrigir).
- `app/package.json` scripts: `dev` (vite), `build` (`tsc -b && vite build`), `preview`,
  `lint` (oxlint), `test` (vitest). Sem servidor de produção configurado.
- Dois lockfiles em `app/`: `pnpm-lock.yaml` (modificado nos commits recentes, uso
  ativo) e `package-lock.json` (parado). Decisão: usar `pnpm` no Dockerfile do app.
  Duplicidade de lockfiles documentada como achado a ser resolvido pelo responsável do
  projeto fora desta task — **nenhum lockfile será apagado por esta tarefa**.
- `WorkLogManager.sln`: projetos `Api`, `Application`, `Infrastructure` +
  `Application.Tests`, `Api.Tests`, `Infrastructure.Tests`. `Infrastructure` referencia
  `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 e `EFCore.NamingConventions` 10.0.1;
  `Api` referencia `Microsoft.EntityFrameworkCore.Design` 10.0.4 (necessário para rodar
  `dotnet ef` a partir de `Api` como startup project).
- `launchSettings.json` usa porta `5244` (http) / `7119` (https) em dev local sem
  Docker — não é tocado por esta task; dentro do container a API vai escutar em outra
  porta, definida via `ASPNETCORE_URLS`, sem depender de `launchSettings.json` (esse
  arquivo só é usado pelo comando `dotnet run`/Visual Studio, não pela imagem Docker
  publicada).
- `.env` já está no `.gitignore` raiz (`'.env'`, `.env.local`, `.env.*.local` — linhas
  7, 505-507); nenhum arquivo real de segredo será commitado por esta task.

## Back-end — mudanças por camada

### Application (use-cases, entidades, interfaces)
- Nenhuma mudança. Esta task é pura infraestrutura de execução, sem novo caso de uso,
  entidade ou regra de negócio.

### Infrastructure (EF Core, repositórios, integrações)
- Nenhuma mudança de mapeamento/entidade/repositório.
- **Migration**: nenhuma migration de schema necessária — as migrations existentes
  (`InitialCreate`, `AddMonthClosing`,
  `ReplaceDailyWorkHoursWithWorkSchedulePeriods`, `AddNoteToEmployeeWorkLog`,
  `AddOriginToEmployeeWorkLog`, `AddSystemParameters`) continuam sendo a fonte da
  verdade; o container de migrations apenas as **aplica** contra o banco
  conteinerizado (equivalente a `dotnet ef database update`), não gera nenhuma nova.

| Tabela | Coluna | Tipo | Constraint |
|---|---|---|---|
| (nenhuma alteração de schema nesta task) | | | |

### Api (endpoints, DTOs)
- Alteração pontual em `Program.cs`: extrair a lista de origens de CORS hardcoded
  (`http://localhost:5173`) para uma seção de configuração lida de
  `appsettings.json`/variável de ambiente (ex.: `Cors:AllowedOrigins` como array,
  sobrescrevível via `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, etc., ou uma
  única env var `CORS_ALLOWED_ORIGINS` separada por vírgula e splitada em
  `Program.cs` — escolha a opção mais simples na implementação, mantendo o default
  atual `http://localhost:5173` em `appsettings.json` para não quebrar o `dotnet run`
  local sem Docker). Nenhum outro endpoint, DTO ou middleware muda.
- Nenhum DTO novo. Nenhum endpoint novo (endpoint de health-check é avaliado como
  opcional — ver "Fora de escopo").
- A connection string continua vindo de `ConnectionStrings:WorkLogManagerDb`; dentro do
  container isso é sobrescrito via variável de ambiente `ConnectionStrings__WorkLogManagerDb`
  (convenção nativa do `IConfiguration` do ASP.NET Core — nenhuma linha de código nova
  necessária para isso, só a variável de ambiente no `docker-compose.yml`).

## Front-end — mudanças em `app/`
- Nenhum componente de tela muda.
- Nenhum componente `shadcn/ui` novo.
- Nenhuma chamada de API nova — `VITE_API_BASE_URL` já é o mecanismo de configuração
  existente (`app/.env` local continua valendo para `pnpm dev` fora de Docker); dentro
  do container essa env var é passada como **build-arg** do Docker (não runtime),
  porque o Vite resolve `import.meta.env.*` em tempo de build e o resultado do build é
  servido como arquivos estáticos pelo nginx.

## Arquivos Docker a criar (especificação)

### `/.dockerignore` (raiz, contexto do compose)
Cobrir `**/bin/`, `**/obj/`, `**/node_modules/`, `.git/`, `.claude/`, `app/dist/`,
`**/*.user`, `.env`.

### `/api/.dockerignore`
`bin/`, `obj/`, `**/bin/`, `**/obj/`, `.vs/`, `*.user`.

### `/api/Dockerfile` (imagem da API)
Multi-stage:
1. **build**: `mcr.microsoft.com/dotnet/sdk:10.0` — copia `WorkLogManager.sln` e todos
   os `*.csproj` primeiro (cache de `dotnet restore`), roda
   `dotnet restore WorkLogManager.sln`, depois copia o restante do código e roda
   `dotnet publish src/WorkLogManager.Api/WorkLogManager.Api.csproj -c Release -o /app/publish
   --no-restore`.
2. **runtime**: `mcr.microsoft.com/dotnet/aspnet:10.0` — copia `/app/publish`, define
   `ENV ASPNETCORE_URLS=http://+:8080`, `EXPOSE 8080`, `USER app` (usuário não-root já
   presente nas imagens oficiais do .NET), `ENTRYPOINT ["dotnet", "WorkLogManager.Api.dll"]`.
- Nenhuma tentativa de migrar o banco dentro deste Dockerfile/entrypoint — migrations
  ficam 100% isoladas no container dedicado (ver abaixo).

### `/api/Dockerfile.migrations` (imagem do migration runner)
Multi-stage, usando o padrão de **EF Core bundle** (recomendado pela própria
documentação do EF Core para rodar migrations em containers, evita carregar o SDK
completo em runtime):
1. **build**: `mcr.microsoft.com/dotnet/sdk:10.0` — copia a solution inteira (mesmo
   contexto do `/api/Dockerfile`), roda `dotnet restore`, instala a ferramenta
   `dotnet-ef` (`dotnet tool install --global dotnet-ef --version 10.*`, ajusta `PATH`),
   e gera o bundle self-contained apontando para os projetos corretos:
   `dotnet ef migrations bundle --project src/WorkLogManager.Infrastructure
   --startup-project src/WorkLogManager.Api --self-contained -r linux-x64
   -o /app/efbundle --configuration Release`.
2. **runtime**: imagem mínima compatível com o runtime self-contained (ex.:
   `mcr.microsoft.com/dotnet/runtime-deps:10.0`) — copia só o executável `/app/efbundle`
   gerado no stage anterior, `ENTRYPOINT ["/app/efbundle", "--connection",
   "$(ConnectionStrings__WorkLogManagerDb)"]` (o bundle aceita a connection string via
   `--connection` ou lê a variável de ambiente padrão — confirmar na implementação qual
   forma o bundle gerado prioriza e usar essa; documentar a escolha final no código).
- Este container não expõe porta nenhuma; roda, aplica migrations pendentes, sai com
  código 0 em sucesso (usado pela condição `service_completed_successfully` do
  `depends_on` da API) ou código != 0 em falha (compose não sobe a API).

### `/app/.dockerignore`
`node_modules/`, `dist/`, `.env`, `.env.local`.

### `/app/Dockerfile` (imagem do front-end)
Multi-stage:
1. **build**: `node:22-slim` (ou LTS ativa equivalente) — habilita `corepack enable`
   (para obter `pnpm` sem instalar globalmente à parte), copia
   `package.json`/`pnpm-lock.yaml`, roda `pnpm install --frozen-lockfile`, copia o
   restante do código, recebe `ARG VITE_API_BASE_URL` e injeta como env var antes do
   build (`ENV VITE_API_BASE_URL=$VITE_API_BASE_URL`), roda `pnpm build` (gera
   `dist/`).
2. **runtime**: `nginx:1.27-alpine` — copia `dist/` do stage anterior para
   `/usr/share/nginx/html`, copia `app/nginx.conf` customizado para
   `/etc/nginx/conf.d/default.conf`, `EXPOSE 80`.
- **Não** usar `package-lock.json`/`npm ci` neste Dockerfile (decisão: `pnpm`,
  conforme investigação de uso ativo do `pnpm-lock.yaml`).

### `/app/nginx.conf`
Config mínima de SPA: `try_files $uri $uri/ /index.html;` na location `/`, mais
`gzip on` e cache-control razoável para assets com hash (`/assets/*`). Sem proxy
reverso para a API — o browser chama a API diretamente via `VITE_API_BASE_URL`
(baked no build), não via nginx.

### `/docker-compose.yml` (raiz)
4 serviços:

```yaml
services:
  db:
    image: postgres:17-alpine
    environment:
      POSTGRES_DB: ${POSTGRES_DB:-worklogmanager}
      POSTGRES_USER: ${POSTGRES_USER:-postgres}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-postgres}
    volumes:
      - worklogmanager-db-data:/var/lib/postgresql/data
    ports:
      - "${POSTGRES_HOST_PORT:-5433}:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-postgres} -d ${POSTGRES_DB:-worklogmanager}"]
      interval: 5s
      timeout: 5s
      retries: 10

  migrations:
    build:
      context: ./api
      dockerfile: Dockerfile.migrations
    environment:
      ConnectionStrings__WorkLogManagerDb: "Host=db;Port=5432;Database=${POSTGRES_DB:-worklogmanager};Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD:-postgres}"
    depends_on:
      db:
        condition: service_healthy
    restart: "no"

  api:
    build:
      context: ./api
      dockerfile: Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Production}
      ConnectionStrings__WorkLogManagerDb: "Host=db;Port=5432;Database=${POSTGRES_DB:-worklogmanager};Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD:-postgres}"
      CORS_ALLOWED_ORIGINS: ${CORS_ALLOWED_ORIGINS:-http://localhost:3000}
    ports:
      - "${API_HOST_PORT:-8080}:8080"
    depends_on:
      migrations:
        condition: service_completed_successfully

  app:
    build:
      context: ./app
      dockerfile: Dockerfile
      args:
        VITE_API_BASE_URL: ${VITE_API_BASE_URL:-http://localhost:8080}
    ports:
      - "${APP_HOST_PORT:-3000}:80"
    depends_on:
      api:
        condition: service_started

volumes:
  worklogmanager-db-data:
```

Observações de design incorporadas no YAML acima (a serem confirmadas/ajustadas na
implementação, não re-inventadas):
- Porta do host do Postgres mapeada para `5433` por padrão (não `5432`), para evitar
  conflito com o Postgres real já rodando localmente/na rede (`localhost:5432`,
  `192.168.3.10:5432`) mencionado no report do PO. A porta **dentro** da rede Docker
  continua `5432` — só o mapeamento de host muda.
- `VITE_API_BASE_URL` aponta para `http://localhost:8080` por padrão (a porta do host
  publicada pela API), não para o nome do serviço `api` — porque quem executa o
  fetch/axios é o **browser do usuário**, fora da rede Docker, então precisa de um
  endereço alcançável do host, não do nome DNS interno do compose.
- `CORS_ALLOWED_ORIGINS` no serviço `api` aponta para `http://localhost:3000` (porta
  publicada do app) pelo mesmo motivo inverso: é o navegador, chamando a partir dessa
  origem, quem precisa estar liberado no CORS da API.
- Nenhuma credencial de banco fica hardcoded no `docker-compose.yml` nem nos
  Dockerfiles — tudo vem de variáveis de ambiente com defaults de desenvolvimento
  (`postgres`/`postgres`/`worklogmanager`), sobrescrevíveis via arquivo `.env` na raiz
  (já coberto pelo `.gitignore` existente). Em um deploy real futuro, o responsável
  troca esses valores por segredos reais do orquestrador usado (Docker Swarm secrets,
  variáveis de ambiente do provedor, Vault, etc.) — fora do escopo desta task
  implementar isso.

### `/.env.example` (raiz, versionado)
Documenta todas as variáveis usadas pelo `docker-compose.yml` com os defaults de
desenvolvimento local, para o responsável copiar para `.env` (não versionado) e
ajustar em produção: `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`,
`POSTGRES_HOST_PORT`, `API_HOST_PORT`, `APP_HOST_PORT`, `ASPNETCORE_ENVIRONMENT`,
`CORS_ALLOWED_ORIGINS`, `VITE_API_BASE_URL`.

## Passos de implementação

1. Alterar `api/src/WorkLogManager.Api/Program.cs`: extrair a origem CORS hardcoded
   para configuração (`appsettings.json` com default `["http://localhost:5173"]` +
   leitura de env var `CORS_ALLOWED_ORIGINS`, separada por vírgula, sobrescrevendo a
   lista quando presente). Não alterar `appsettings.Development.json`.
2. Criar `/api/.dockerignore`.
3. Criar `/api/Dockerfile` (build multi-stage SDK -> aspnet runtime, conforme
   especificação acima).
4. Criar `/api/Dockerfile.migrations` (build multi-stage SDK com `dotnet ef migrations
   bundle` -> runtime mínimo, conforme especificação acima).
5. Criar `/app/.dockerignore`.
6. Criar `/app/Dockerfile` (build multi-stage `node` com `pnpm` -> `nginx`, recebendo
   `ARG VITE_API_BASE_URL`).
7. Criar `/app/nginx.conf` (SPA fallback + gzip + cache de assets).
8. Criar `/.dockerignore` na raiz (contexto do build do compose, se algum serviço vier
   a usar `context: .`; hoje cada serviço tem `context` próprio em `./api`/`./app`, mas
   manter por precaução/consistência).
9. Criar `/docker-compose.yml` na raiz com os 4 serviços (`db`, `migrations`, `api`,
   `app`), volume nomeado `worklogmanager-db-data`, healthcheck do `db`,
   `depends_on`/`condition` encadeados conforme especificado.
10. Criar `/.env.example` na raiz documentando todas as variáveis com os defaults de
    desenvolvimento.
11. Atualizar `README.md` (seção nova, curta) com o comando único para subir tudo
    (`docker compose up --build`) e a lista de portas padrão expostas no host
    (banco `5433`, API `8080`, app `3000`), além de mencionar a limitação de
    `VITE_API_BASE_URL` ser build-time (trocar a URL da API exige rebuild da imagem do
    app, não só reiniciar o container).
12. Validação manual do responsável (fora do que os agentes podem executar, já que
    nenhum agente sobe Docker/conecta a banco real neste fluxo): `docker compose up
    --build`, confirmar que `migrations` finaliza com sucesso antes de `api` subir, que
    `api` responde em `http://localhost:8080`, que `app` carrega em
    `http://localhost:3000` e consegue chamar a API sem erro de CORS, e que
    `docker compose down` seguido de `docker compose up` preserva os dados (volume
    nomeado).

## Mapeamento critério de aceite → passos

| Critério de aceite (report do PO) | Passo(s) |
|---|---|
| Container para o banco de dados PostgreSQL | 9, 10 |
| Container para a API (.NET 10) | 1, 2, 3, 9, 10, 11 |
| Container para o front-end (React/Vite) | 5, 6, 7, 9, 10, 11 |
| Rede/variáveis de ambiente para os serviços se comunicarem (connection string não mais `localhost`/IP fixo) | 9, 10 |
| Ambiente alvo dev local e produção; build multi-stage; secrets via env vars | 3, 4, 6, 9, 10 |
| `docker-compose.yml` na raiz, um único comando | 9, 11 |
| App como build estático (nginx), não Vite dev server | 6, 7 |
| Volume nomeado persistente para o banco | 9 |
| Container dedicado de migrations, separado da API, com ordem de dependência banco → migrations → api → app | 4, 9 |
| Uso de `pnpm` no Dockerfile do app; duplicidade de lockfiles documentada, não resolvida | 6 (este plano, seção de investigação) |

## Estratégia de testes

Esta task é infraestrutura de execução (Docker/compose), não adiciona regra de negócio
testável por xUnit/Vitest. A cobertura de testes automatizados existente
(`WorkLogManager.Application.Tests`, `WorkLogManager.Api.Tests`,
`WorkLogManager.Infrastructure.Tests`, testes de front `*.test.ts(x)`) não muda.

| Item | Estratégia |
|---|---|
| Alteração de CORS configurável em `Program.cs` | Nenhum teste automatizado novo necessário (mudança de configuração/plumbing, sem lógica de negócio); validação é manual via `docker compose up` (passo 12) confirmando que o app consegue chamar a API sem erro de CORS. Se o revisor considerar necessário, um teste de integração leve em `WorkLogManager.Api.Tests` pode assertar que `CorsPolicy` é construída com as origens esperadas a partir de configuração — não obrigatório para esta task. |
| Dockerfiles / compose / migration bundle | Sem teste automatizado aplicável (infraestrutura); validado manualmente pelo responsável (passo 12), fora do que os agentes deste fluxo podem executar (nenhum agente sobe container nem conecta a banco real). |

## Riscos e dependências

- **`VITE_API_BASE_URL` é resolvido em build-time pelo Vite**: se a URL pública da API
  mudar (ex.: deploy em outro domínio/porta), é necessário rebuildar a imagem do app,
  não basta trocar uma variável de ambiente do container em runtime. Isso é uma
  limitação inerente de SPA com build estático servido por nginx puro (sem
  entrypoint de `envsubst`). Documentado no README (passo 11); resolver com um
  entrypoint que reescreve um `env.js`/faz `envsubst` nos assets no start do container
  fica como possível melhoria futura, fora do escopo desta task.
- **Conflito de porta 5432 com Postgres real existente**: mitigado mapeando o host
  para `5433` por padrão, mas se o responsável já usa `5433` para outra coisa, precisa
  ajustar `POSTGRES_HOST_PORT` no `.env`.
- **`dotnet ef migrations bundle` self-contained para `linux-x64`**: assume que o
  container roda em host Linux/amd64 (padrão do Docker Desktop e da maioria dos
  provedores de deploy). Se o time usar Apple Silicon com emulação ou deploy em
  arquitetura ARM, o bundle precisa ser gerado para `linux-arm64` — decisão de
  implementação a confirmar conforme o ambiente real de build/deploy (pode virar
  build-arg do `Dockerfile.migrations` em vez de valor fixo).
- **Forma exata de passar a connection string ao bundle do EF Core** (`--connection`
  vs. variável de ambiente `ConnectionStrings__WorkLogManagerDb` lida pelo bundle via
  `IConfiguration` interno): o comportamento exato depende da versão do `dotnet-ef`
  10.x; a implementação deve testar localmente qual forma o bundle gerado aceita e
  documentar a escolha final no `ENTRYPOINT`/`CMD` do `Dockerfile.migrations`.
- **`Npgsql` 10.0.3 / EF Core 10.0.4 / imagem `postgres:17-alpine`**: compatibilidade
  não testada explicitamente nesta investigação (é a combinação padrão esperada, sem
  motivo para incompatibilidade conhecida), mas vale confirmar na implementação/CI
  antes de considerar a task concluída.
- Este plano não cobre a resolução do achado "dois lockfiles simultâneos em `app/`"
  além de documentá-lo — é uma decisão do responsável do projeto, fora desta task.

## Fora de escopo / não será feito

- Pipeline de CI/CD (build/push de imagens, deploy automatizado).
- Resolução do incidente de conexão indevida a bancos Postgres reais existentes fora
  de container (`localhost:5432`, `192.168.3.10:5432`) — mencionado só como contexto
  de risco de conflito de porta.
- Migração/atualização de dados de bancos existentes para o container novo.
- Apagar ou consolidar os lockfiles duplicados (`pnpm-lock.yaml` vs.
  `package-lock.json`) em `app/` — só documentado como achado.
- Endpoint de health-check dedicado na API (`/health`) para uso em
  `healthcheck:`/orquestrador — não solicitado no report do PO; pode ser proposto como
  task futura separada se o responsável quiser um healthcheck de aplicação (hoje só o
  banco tem healthcheck no compose, conforme pedido).
- Mecanismo de runtime-config (`envsubst`/`env.js`) para o app trocar a URL da API sem
  rebuild — documentado como risco/limitação, não implementado nesta task.
- Qualquer alteração de schema/migration nova — as migrations existentes são só
  aplicadas, não alteradas.

---
## Status: aguardando aprovação

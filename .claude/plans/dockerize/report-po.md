## Report do PO — Conteinerização do projeto (banco, API, app)

### Investigação prévia (contexto técnico levantado)
- Connection string atual (dev): `api/src/WorkLogManager.Api/appsettings.json` aponta para `Host=localhost;Port=5432;Database=worklogmanager;Username=postgres;Password=postgres`. `appsettings.Development.json` sobrescreve com `Server=192.168.3.10;Database=WorkLogManager;User Id=postgres;Password=postgres;`. Há indício de Postgres real rodando fora de containers hoje (local e/ou em outra máquina da rede) — possível conflito de porta 5432 se o container do banco também publicar essa porta no host.
- Nenhum artefato Docker existe hoje no repositório (sem Dockerfile, docker-compose, .dockerignore, em nenhum lugar).
- .NET: `TargetFramework` é `net10.0`. Solution com múltiplos projetos (`Api`, `Application`, `Infrastructure` + 3 projetos de teste). `Api` referencia `Microsoft.EntityFrameworkCore.Design`; migrations geradas a partir de `Infrastructure` com `--startup-project Api`.
- Front-end: `app/package.json` define `dev` (vite), `build` (`tsc -b && vite build`), `preview`, `lint`, `test`. Sem servidor de produção configurado hoje.
- Dois lockfiles simultâneos em `app/`: `pnpm-lock.yaml` e `package-lock.json` (ambiguidade pré-existente, não causada por esta tarefa).
- Nenhuma referência a Docker em `.claude/docs/`, `README.md`, `app/README.md`.

### Objetivo
Conteinerizar o projeto WorkLogManager para que banco de dados (PostgreSQL), API (.NET 10) e front-end (React/Vite) rodem cada um em seu próprio container.

### Escopo
- Container para o banco de dados PostgreSQL.
- Container para a API (.NET 10).
- Container para o front-end (React/Vite).
- Configuração de rede/variáveis de ambiente para os serviços se comunicarem entre si (connection string apontando para o serviço de banco containerizado, não mais `localhost`/`192.168.3.10`).

### Fora de escopo
- Pipeline de CI/CD.
- Resolução do incidente de conexão indevida a bancos reais relatado anteriormente nesta sessão (mencionado só como contexto de possível conflito de porta).
- Migração/atualização de dados de bancos existentes para o container novo.

### Regras de negócio / restrições
- Nenhuma regra de negócio — solicitação de infraestrutura/ambiente de execução.
- Restrição observada: já existe conectividade hoje com Postgres reais fora de container em `localhost:5432` e `192.168.3.10:5432` — possível conflito de porta no host.

---
Gerado pelo agente `po` a partir do texto fornecido pelo usuário.

### Decisões complementares do usuário (incorporadas ao plano técnico)
- Ambiente alvo: desenvolvimento local e produção/deploy futuro. Build multi-stage otimizado, secrets via variáveis de ambiente (não hardcoded), boas práticas de produção.
- Orquestração: `docker-compose.yml` na raiz do repositório, subindo tudo com um único comando.
- Container do app (front-end): build estático multi-stage (Node para build, nginx para servir), não Vite dev server.
- Persistência do banco: volume Docker nomeado, sobrevive a `docker compose down`/`up`.
- Migrations: container dedicado, separado da API, que roda as migrations do EF Core uma vez e finaliza (não é a API que migra sozinha). Isso expande o escopo original de 3 para 4 serviços: banco, migration runner, API, app. Ordem de dependência: banco -> migration runner (aguarda banco saudável) -> API (aguarda migration runner completar com sucesso) -> app (depende da API só em runtime, pode subir em paralelo).
- Gerenciador de pacotes do front-end: `pnpm` (decisão confirmada após investigação nesta sessão — ver seção de investigação do plano técnico). Duplicidade de lockfiles (`pnpm-lock.yaml` + `package-lock.json`) documentada como achado a ser limpo pelo responsável do projeto, fora desta task.

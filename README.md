# WorkLogManager

Aplicação de gestão de registros de trabalho (worklogs) e funcionários, com back-end
.NET 10 (`api/`) e front-end React + shadcn/ui (`app/`).

- Se você vai **desenvolver** neste repositório, veja [Desenvolvimento](#desenvolvimento)
  abaixo.
- Se você só quer **instalar e usar** o aplicativo, veja
  [Instalação (usuária final)](#instalação-usuária-final) abaixo.

Este repositório também usa um fluxo de subagentes de Claude Code para planejar e
implementar features com aprovação humana obrigatória — veja
[Fluxo de agentes de desenvolvimento](#fluxo-de-agentes-de-desenvolvimento) mais abaixo.

## Desenvolvimento

### Back-end (`api/`)

Stack: .NET 10, Minimal APIs, EF Core, SQLite (arquivo local, sem servidor de banco
para instalar/rodar).

Rodar a API em modo desenvolvimento:

```bash
cd api
dotnet run --project src/WorkLogManager.Api
```

A API sobe em `http://localhost:5244` (perfil `http` de
`src/WorkLogManager.Api/Properties/launchSettings.json`; há também um perfil `https` em
`https://localhost:7119`). Em desenvolvimento (`ASPNETCORE_ENVIRONMENT=Development`), a
connection string aponta para `App_Data/worklogmanager-dev.db`
(`appsettings.Development.json`): o arquivo SQLite é criado e migrado automaticamente no
startup da aplicação (`Database.Migrate()`), sem precisar de Postgres, Docker ou qualquer
setup manual de banco.

Rodar os testes:

```bash
cd api
dotnet test
```

Alterações de schema são feitas via migration do EF Core, nunca aplicadas diretamente em
um banco real:

```bash
dotnet ef migrations add <NomeDaMigration> \
  --project src/WorkLogManager.Infrastructure \
  --startup-project src/WorkLogManager.Api
```

### Front-end (`app/`)

Stack: React + TypeScript + Vite + shadcn/ui. Gerenciador de pacotes: `pnpm`.

```bash
cd app
pnpm install
pnpm dev
```

O Vite dev server sobe em `http://localhost:5173`. `app/.env` já aponta
`VITE_API_BASE_URL` para `http://localhost:5244` (a API rodando localmente, ver acima).

Rodar os testes:

```bash
cd app
pnpm test
```

Detalhes específicos do template Vite/React (lint, React Compiler, etc.) estão em
[`app/README.md`](app/README.md).

## Instalação (usuária final)

Esta seção é para quem só quer **usar** o WorkLogManager em um computador Windows, sem
precisar instalar nada de desenvolvimento (sem .NET, Node, banco de dados, etc.).

1. Acesse a página de **Releases** do repositório no GitHub e baixe o instalador mais
   recente (arquivo `WorkLogManagerSetup-<versão>.exe`).
2. Execute o arquivo baixado. O Windows pode pedir confirmação de administrador (UAC) —
   isso é esperado, pois o instalador registra o WorkLogManager como um serviço do
   Windows que já fica disponível assim que o computador liga.
3. Siga o assistente de instalação (wizard) até o fim.
4. Ao concluir, o instalador cria atalhos na **Área de Trabalho** e no **Menu Iniciar**.
   Basta clicar em qualquer um deles para abrir o WorkLogManager no navegador, em
   `http://localhost:5000`.

Não é necessário instalar ou configurar nenhum banco de dados: os dados ficam salvos
localmente no computador e são preservados entre atualizações do aplicativo.

### Atualizando

Para atualizar para uma nova versão, baixe o instalador mais recente na página de
Releases e execute-o novamente sobre a instalação existente — o processo é o mesmo de
uma instalação nova. Seus dados são preservados durante a atualização.

### Desinstalando

Ao desinstalar o WorkLogManager (em *Configurações > Aplicativos*), todos os dados
salvos são removidos junto com o programa. Se precisar manter os dados, faça um backup
antes de desinstalar (veja [`installer/README.md`](installer/README.md) para o caminho
exato do arquivo de banco de dados).

> Detalhes técnicos de como o instalador é construído (para quem for gerar/publicar
> releases) estão em [`installer/README.md`](installer/README.md).

## Fluxo de agentes de desenvolvimento

Conjunto de subagentes de Claude Code, específico para este repositório (`app/` React
front-end, `api/` .NET 10 back-end), que planeja e implementa features/alterações com
aprovação humana obrigatória em dois pontos: plano de desenvolvimento e (quando
necessário) resultado do code review.

### Estrutura

```
.claude/
  agents/
    po.md                  # interpreta o texto e gera o report da tarefa
    analista-tecnico.md    # define o task-id, cria .claude/plans/<task-id>/, gera o plano
    desenvolvedor.md       # implementa o plano aprovado + migrations EF Core + testes
    revisor.md             # confronta implementação x plano e gera code review
  commands/
    dev-flow.md            # /dev-flow — orquestra os 4 agentes em sequência
  docs/
    stack.md                       # convenções técnicas .NET/React/PostgreSQL (edite!)
    padroes-desenvolvimento.md     # padrões específicos deste repositório (edite!)
  .claude/templates/
    report-po.md
    plano-desenvolvimento.md
    code-review.md
```

### Instalação (dos agentes)

1. Copie `.claude/`, `.claude/docs/` e `.claude/templates/` para a raiz deste repositório.
2. Edite `.claude/docs/stack.md` e `.claude/docs/padroes-desenvolvimento.md` com o nome real da
   solution (`<ProjectName>`), bundler/framework de teste do front, e qualquer
   convenção própria do repositório.
3. Adicione `.claude/plans/` ao `.gitignore` (são artefatos de planejamento, não
   precisam ir para o controle de versão — ajuste se preferir versioná-los).

### Uso

Fluxo completo, orquestrado automaticamente:

```
/dev-flow
Como usuário administrador, preciso poder desativar um evento sem excluí-lo.
Critério de aceite: ao desativar, o evento some da listagem pública mas continua
visível no painel admin com um badge "inativo".
```

O comando vai: rodar o `po` → mostrar o report → rodar o `analista-tecnico` (que cria o
`task-id` e o diretório, já quebrando o plano por camada — `Application`/
`Infrastructure`/`Api`/front-end) → mostrar o plano → **parar e pedir aprovação** →
rodar o `desenvolvedor` → rodar o `revisor` → mostrar o code review → (se necessário)
voltar ao `desenvolvedor` para aplicar correções, até aprovação final.

Também é possível invocar cada agente isoladamente, passando o `task-id` quando
aplicável.

### Garantias do fluxo

- Nenhum agente se conecta a um PostgreSQL real: alterações de schema viram apenas
  migrations do EF Core (`dotnet ef migrations add`), nunca aplicadas
  (`dotnet ef database update`) pelos agentes.
- Nenhum agente faz `git commit`/`git push` — commit e push são sempre manuais.
- Implementação só começa depois de aprovação explícita do plano de desenvolvimento.
- Camadas são respeitadas: `Api` (endpoints) → `Application` (use-cases, entidades,
  interfaces) ← `Infrastructure` (implementações concretas, EF Core).
- Todo código gerado é em inglês; toda comunicação com você continua em português.
- Cada critério de aceite é rastreado do report do PO → passo do plano → teste →
  verificação no code review, com histórico completo em `.claude/plans/<task-id>/`.

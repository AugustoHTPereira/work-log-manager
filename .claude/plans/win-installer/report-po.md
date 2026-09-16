# Report da tarefa: Instalador nativo do aplicativo para máquina da esposa (fora de Docker)

## Objetivo
Permitir que o usuário publique o WorkLogManager na máquina pessoal da esposa como uma instalação nativa (não conteinerizada): ela deve conseguir baixar o pacote de instalação mais recente do GitHub e instalá-lo, com o próprio instalador subindo o ambiente atualizado — incluindo a aplicação das migrations de banco de dados na conexão configurada.

## Contexto relevante já investigado nesta sessão
- Existe uma task anterior (`dockerize`) cujo plano de conteinerização foi aprovado pelo analista técnico mas depois recusado/adiado pelo usuário. Esta nova task segue por um caminho diferente: instalação nativa direto na máquina Windows, não containers.
- Stack confirmada: `api/src/WorkLogManager.Api/WorkLogManager.Api.csproj`: .NET 10, ASP.NET Core, hospedado via Kestrel. Projeto referencia `Microsoft.EntityFrameworkCore.Design` (migrations geradas a partir de `Infrastructure` com `--startup-project Api`). `app/package.json`: React 19 + Vite 8, `build` gera artefatos estáticos; hoje não há servidor de produção configurado para os arquivos estáticos (sem `UseStaticFiles`/SPA fallback observado — confirmar em `Program.cs`).
- Connection string configurável via `appsettings`/ambiente (`ConnectionStrings:WorkLogManagerDb`).
- Não há nenhum pipeline de build/release (`.github/workflows` inexistente), sem `CHANGELOG.md`, sem Dockerfile/compose.

## Escopo
- Criação de um mecanismo de empacotamento/instalação do aplicativo (API .NET 10 + front-end React/Vite) para execução nativa Windows (não conteinerizado).
- O instalador deve, ao ser executado, aplicar as migrations do EF Core na base de dados especificada na configuração como parte do processo de instalação/atualização.
- Mecanismo de distribuição via GitHub Releases: a usuária final baixa o pacote de instalação mais recente e instala.
- Definição de como API e front-end são executados/hospedados na máquina de destino.
- PostgreSQL local, provisionado pelo instalador.

## Fora de escopo
- Conteinerização (Docker) — descartada pelo usuário.
- Auto-update automático (confirmado como fora de escopo pelo usuário — modelo é manual via GitHub Releases).

## Critérios de aceite
1. Existe uma versão publicada no GitHub (Releases) com um pacote de instalação identificável da última versão.
2. A usuária baixa e executa o instalador sem exigir conhecimento técnico avançado (sem linha de comando, wizard visual).
3. Ao final da instalação/atualização, as migrations pendentes do EF Core são aplicadas automaticamente na base de dados configurada, sem intervenção manual da usuária.
4. Após a instalação, tanto a API quanto o front-end ficam acessíveis e funcionais na máquina dela (processo único, API servindo o front-end).
5. Atualizar (baixar e rodar um novo instalador sobre uma instalação existente) preserva os dados já existentes no banco — sem perda de dados.
6. PostgreSQL roda localmente na máquina dela, provisionado como parte da instalação inicial.

## Regras de negócio / restrições
- "O patch deve subir o ambiente atualizado aplicando as migrations na base de dados especificada nas configurações" — decisão de produto do usuário para o instalador final, distinta e não violadora da regra vigente nesta sessão de que nenhum AGENTE deste fluxo de desenvolvimento aplica migrations a bancos reais durante o trabalho de desenvolvimento — essa regra continua vigente para PO/Analista/Desenvolvedor/Revisor.
- Distribuição via GitHub Releases.
- Atualização manual, sem auto-update.
- Processo único: API serve o front-end.
- PostgreSQL local.

---
Gerado pelo agente `po` a partir do texto fornecido pelo usuário. Decisões complementares fornecidas pelo usuário e incorporadas.

## Decisões complementares do usuário (incorporadas no plano técnico)
- Sistema operacional alvo: Windows. Instalador nativo (`.exe`, via Inno Setup — ver plano técnico).
- PostgreSQL roda localmente na máquina da esposa, provisionado pelo próprio instalador.
- Atualização manual via GitHub Releases, sem auto-update.
- Processo único: a API .NET serve os arquivos estáticos do front-end (mudança necessária em `Program.cs`, confirmada como inexistente hoje).
- "Instalador aplica migrations" é uma decisão de produto para o artefato final entregue à esposa — o desenvolvedor projeta/testa esse mecanismo em ambiente descartável próprio, nunca o executa contra o banco real da esposa.

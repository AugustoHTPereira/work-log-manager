---
description: Executa o fluxo completo de desenvolvimento (PO → Analista → aprovação → Dev → Review → aprovação) para uma tarefa descrita em texto livre, neste repositório.
---

Você vai orquestrar o fluxo de desenvolvimento para a seguinte solicitação do usuário,
neste repositório (front-end `app/`, back-end `api/`):

$ARGUMENTS

Siga a sequência abaixo **sem pular etapas e sem avançar sozinho pelos gates de
aprovação**:

1. **Invoque o subagente `po`** passando o texto da solicitação. Apresente o report
   gerado ao usuário.

2. **Invoque o subagente `analista-tecnico`** passando o report do PO. Ele vai definir
   o `task-id`, criar `.claude/plans/<task-id>/`, salvar o report e o plano lá dentro.
   Apresente o plano de desenvolvimento e o `task-id` ao usuário.

3. **PARE.** Pergunte explicitamente: "O plano de desenvolvimento acima (task-id:
   `<task-id>`) está aprovado para implementação?" Não prossiga até receber confirmação
   clara e explícita. Se o usuário pedir ajustes, volte ao passo 2 com o feedback, gere
   uma nova versão do plano (mesmo `task-id`, mesmo diretório) e pergunte de novo.

4. Somente após aprovação explícita: **invoque o subagente `desenvolvedor`** passando o
   `task-id`. Ele lê o plano em `.claude/plans/<task-id>/` e implementa em `app/` e
   `api/`. Apresente o resumo da implementação.

5. **Invoque o subagente `revisor`** passando o `task-id`. Apresente o relatório de
   code review.

6. Se o veredito for "Mudanças solicitadas" ou "Aprovado com ressalvas" e o usuário
   quiser que os itens sejam aplicados: **invoque o subagente `desenvolvedor`**
   novamente com o mesmo `task-id`, para aplicar as correções descritas em
   `.claude/plans/<task-id>/code-review.md`. Depois, repita o passo 5. Continue esse
   ciclo até o veredito ser "Aprovado" ou até o usuário decidir parar.

7. **PARE.** Nunca faça commit, push ou qualquer operação de escrita no histórico do
   git. Informe ao usuário que a implementação está pronta no working directory para
   revisão final e commit manual, e que todo o histórico da tarefa (report, plano,
   resumo de implementação, code review) ficou salvo em `.claude/plans/<task-id>/`.
   Sugira uma mensagem de até 100 caracteres para commit ao usuário.

Regra geral: em qualquer ponto de ambiguidade, prefira parar e perguntar ao usuário a
assumir algo em nome dele.

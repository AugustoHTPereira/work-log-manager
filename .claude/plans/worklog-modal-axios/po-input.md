# Report do PO: melhorias no modal de worklog (modo simples/avançado) + migração para axios

Task-id: worklog-modal-axios

> Tarefa incremental sobre o sistema já implementado (task-id anterior:
> `worklog-manager`, plano em `.claude/plans/worklog-manager/plano-desenvolvimento.md`).
> Não é uma reabertura daquela tarefa — ganha task-id e diretório próprios.

## Contexto

O modal de criação/edição de worklog (`app/src/features/work-logs/WorkLogFormModal.tsx`)
hoje sempre expõe dois campos `datetime-local` ("Data inicial" e "Data final"), com
sincronização bidirecional com um campo de texto de duração (via `lastEditedField`).
Para o caso comum (a maioria dos worklogs começa e termina no mesmo dia), isso obriga o
usuário a preencher data duas vezes. O PO recebeu uma melhoria de UX: um modo "simples"
com campos "Data" + "Início" + "Fim" (hora), e um checkbox para alternar para o modo
"avançado" atual (datas completas), para os casos em que o worklog cruza a meia-noite.

Paralelamente, foi decidido migrar toda a camada de acesso HTTP do front-end
(`app/src/lib/api/`) de `fetch` nativo para `axios`.

O back-end NÃO é alterado nesta tarefa: o contrato da API continua
`{ type, startDate, endDate }` (ambos `DateTimeOffset`/ISO 8601), tanto em
`CreateWorkLogPayload` quanto em `UpdateWorkLogPayload`.

## Critérios de aceite

1. Ao criar um novo worklog, o modal abre no modo simples: checkbox de "modo avançado"
   desmarcado, com campos "Data", "Início" e "Fim" (em vez de "Data inicial"/"Data
   final").
2. Ao editar um worklog cujo `startDate` e `endDate` caem em dias calendário
   diferentes (considerando o fuso local do navegador), o checkbox de modo avançado
   deve vir marcado automaticamente, mostrando "Data inicial"/"Data final" como hoje.
   Se `startDate` e `endDate` caem no mesmo dia calendário, o checkbox vem desmarcado
   (modo simples), decompondo os valores em Data/Início/Fim.
3. No modo simples, se o horário de "Fim" for anterior ao horário de "Início" (mesma
   "Data" informada), o sistema assume automaticamente que "Fim" ocorre no dia seguinte
   à "Data" — sem bloquear o usuário, sem diálogo de confirmação.
4. Ao marcar/desmarcar o checkbox de modo avançado com dados já preenchidos no
   formulário, os valores são convertidos e preservados entre os dois formatos (o
   formulário não é resetado):
   - Simples → avançado: Data + Início vira "Data inicial" (mesma data e hora); Data +
     Fim vira "Data final" (aplicando a regra de virada de dia da simples, se for o
     caso, antes da conversão).
   - Avançado → simples: "Data" recebe a data de "Data inicial"; "Início" recebe a hora
     de "Data inicial"; "Fim" recebe a hora de "Data final" (aplicando a regra de
     virada de dia ao reconstituir "Fim" a partir da hora, se "Data final" cair no dia
     seguinte a "Data inicial").
5. A sincronização já existente entre os campos de data/hora e o campo de duração
   (texto tipo "1h 30m") continua funcionando nos dois modos (editar datas recalcula a
   duração; editar a duração recalcula o fim), sem regressão do comportamento atual
   coberto pelos testes existentes de `WorkLogFormModal.test.tsx`.
6. Toda a camada `app/src/lib/api/` (`client.ts` e os módulos que o usam:
   `employees.ts`, `workLogs.ts`, `systemSettings.ts`) passa a usar `axios` em vez de
   `fetch` nativo, preservando o comportamento observável: mesmos erros lançados como
   `ApiError` (com `status` e mensagem extraída do corpo de erro quando disponível),
   mesmo tratamento de resposta `204 No Content` (retorna `undefined`), mesma base URL
   configurável via `VITE_API_BASE_URL`.
7. O contrato do back-end não muda: o payload enviado continua
   `{ type, startDate, endDate }` em ambos os modos.

## Decisões já tomadas pelo usuário (não estão mais em aberto)

- Estado inicial do checkbox ao criar: sempre desmarcado (modo simples).
- Estado inicial do checkbox ao editar: calculado a partir de `startDate`/`endDate` do
  worklog (dias calendário diferentes → marcado; mesmo dia → desmarcado).
- Regra de virada de dia no modo simples: aplicada silenciosamente, sem confirmação.
- Conversão entre modos: bidirecional e sem perda de dados, incluindo ao alternar
  múltiplas vezes.
- Escopo do axios: migração completa da camada `lib/api/`, não apenas de um endpoint.

## Pontos inferidos pelo PO (sem pergunta direta ao usuário)

- Texto do label do checkbox de modo avançado e dos campos "Data"/"Início"/"Fim": a
  cargo do Tech Lead/implementação, desde que claros em português (ex.: "Editar data
  inicial e final separadamente", "Data", "Início", "Fim").
- Comportamento de sincronização com o campo de duração: deve continuar idêntico ao
  atual (mesma lógica de recálculo, mesmo debounce), apenas operando sobre os valores
  compostos (Data+Início / Data+Fim) quando em modo simples.
- Biblioteca `axios` deve ser adicionada como dependência de produção em
  `app/package.json` (ainda não presente no projeto).

## Fora de escopo

- Qualquer alteração de contrato/endpoint no back-end (`api/`).
- Qualquer alteração em `worklog-duration.ts` (parser/formatter de duração).
- Suporte a fusos horários diferentes do fuso local do navegador.

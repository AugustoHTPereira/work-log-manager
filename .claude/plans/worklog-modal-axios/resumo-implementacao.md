# Resumo de implementação: worklog-modal-axios

Task-id: `worklog-modal-axios`. Implementação restrita a `app/`, sem mudanças em `api/`,
conforme o plano aprovado.

## Arquivos criados/alterados

### `app/` (front-end)
- `app/package.json` — dependência `axios` (`^1.20.0`) adicionada via `npm install axios`.
- `app/src/lib/api/client.ts` — reescrito para usar `axios.create({ baseURL, validateStatus: () => true })`
  em vez de `fetch`, preservando a API pública `apiClient.{get,post,put,delete}`, a classe
  `ApiError` e o comportamento observável (erro 4xx/5xx com mensagem do corpo ou default,
  204 → `undefined`).
- `app/src/lib/api/client.test.ts` (novo) — testes com `vi.mock("axios")` cobrindo GET
  bem-sucedido, 4xx com corpo `{message}`, 4xx sem corpo JSON (mensagem default), e 204
  (`undefined`).
- `app/src/lib/api/employees.ts`, `workLogs.ts`, `systemSettings.ts` — revisados, sem
  alteração (já dependem só de `apiClient`).
- `app/src/components/ui/checkbox.tsx` (novo) — gerado via `npx shadcn add checkbox`.
- `app/src/features/work-logs/worklog-date-mode.ts` (novo) — funções puras
  `simpleToAdvanced`, `advancedToSimple`, `isMultiDay`, e `toIsoString`/
  `toDatetimeLocalValue` movidas para cá (antes viviam dentro de `WorkLogFormModal.tsx`),
  com comentário de cabeçalho explicando a regra de virada de dia.
- `app/src/features/work-logs/worklog-date-mode.test.ts` (novo) — 9 testes cobrindo a
  tabela "Estratégia de testes" do plano (virada de dia, sem virada, `start === end`,
  decomposição same-day/next-day, round-trips nos dois sentidos, `isMultiDay` true/false).
- `app/src/features/work-logs/WorkLogFormModal.tsx` — reescrito conforme a modelagem de
  estado do plano: novo estado `isAdvancedMode`/`simpleDate`/`simpleStartTime`/
  `simpleEndTime`; `useEffect` de abertura calcula o modo inicial (`isMultiDay` na edição,
  `false` na criação) e popula os dois conjuntos de estado; `applyDates` extraído como
  helper comum de recálculo de duração/erro, usado tanto por `handleDateChange` (modo
  avançado, comportamento inalterado) quanto por `handleSimpleDateChange`/
  `handleSimpleTimeChange` (modo simples); `handleAdvancedModeChange` recalcula os campos
  simples via `advancedToSimple` apenas ao entrar no modo simples (toggle do checkbox),
  conforme a "alternativa mais simples adotada" descrita no plano — `handleDurationChange`
  não foi alterado, os campos do modo simples só ficam desatualizados até o próximo toggle
  ou abertura do modal, exatamente como o plano especificou; JSX condicional novo
  (`Checkbox` do shadcn + `Label` "Editar data inicial e final separadamente", campos
  Data/Início/Fim no modo simples, campos Data inicial/Data final no modo avançado).
- `app/src/features/work-logs/WorkLogFormModal.test.tsx` — reescrito: os 4 testes
  originais de sincronização duração↔datas foram preservados na íntegra (mesma asserção),
  movidos para dentro de um `describe("advanced mode ...")` com um helper
  `openInAdvancedMode()` que marca o checkbox antes de interagir com os inputs
  `datetime-local` (já que o padrão de abertura agora é o modo simples); adicionado um
  `describe("simple mode ...")` com o equivalente operando nos inputs Data/Início/Fim; e um
  `describe("acceptance criteria #1-#5")` com um teste por critério de aceite do plano.

## Mapeamento passo do plano → arquivo(s)

| Passo do plano | Arquivo(s) |
|---|---|
| 1. Dependência axios | `app/package.json` |
| 2. Reescrever `client.ts` | `app/src/lib/api/client.ts` |
| 3. Revisar `employees.ts`/`workLogs.ts`/`systemSettings.ts` | revisados, sem alteração |
| 4. `client.test.ts` | `app/src/lib/api/client.test.ts` |
| 5. `npx shadcn add checkbox` | `app/src/components/ui/checkbox.tsx` |
| 6. `worklog-date-mode.ts` | `app/src/features/work-logs/worklog-date-mode.ts` |
| 7. `worklog-date-mode.test.ts` | `app/src/features/work-logs/worklog-date-mode.test.ts` |
| 8. Alterar `WorkLogFormModal.tsx` | `app/src/features/work-logs/WorkLogFormModal.tsx` |
| 9. Ajustar testes existentes | `app/src/features/work-logs/WorkLogFormModal.test.tsx` (describe "advanced mode") |
| 10. Novos testes #1–#5 | `app/src/features/work-logs/WorkLogFormModal.test.tsx` (describe "acceptance criteria #1-#5") |
| 11. `npm run lint`/`test`/`build` | executados, ver seção "Resultado dos testes" |

## Mapeamento critério de aceite → teste(s)

| Critério | Teste(s) |
|---|---|
| #1 (modal abre em modo simples ao criar) | `WorkLogFormModal.test.tsx` → "#1 opens in simple mode when creating a new worklog" |
| #2 (checkbox marcado automaticamente ao editar multi-dia) | `WorkLogFormModal.test.tsx` → "#2 checks the advanced mode checkbox automatically..." e "#2 (complement) leaves the advanced mode checkbox unchecked..." |
| #3 (virada de dia no modo simples) | `worklog-date-mode.test.ts` → `simpleToAdvanced` (com/sem virada, `start===end`); `WorkLogFormModal.test.tsx` → "#3 applies the day-rollover rule..." |
| #4 (conversão preserva dados entre modos) | `worklog-date-mode.test.ts` → round-trips; `WorkLogFormModal.test.tsx` → "#4 preserves data through a simple -> advanced -> simple round-trip..." |
| #5 (sincronização com duração nos dois modos) | `WorkLogFormModal.test.tsx` → describes "advanced mode"/"simple mode" (4 testes herdados + 2 novos) e "#5 keeps duration synchronization working through both modes" |
| #6 (migração para axios) | `client.test.ts` (4 testes) |
| #7 (contrato do back-end inalterado) | nenhuma mudança em `api/`; payload montado da mesma forma em `handleSubmit` |

## Resultado dos testes

- `npm test` (dentro de `app/`): **38 testes, 6 arquivos, todos passando**.
- `npm run lint`: sem erros. 3 warnings pré-existentes de `react(only-export-components)`/
  `react(set-state-in-effect)` — o warning em `WorkLogFormModal.tsx` já existia no padrão
  original (o `useEffect` de abertura já chamava `setState` síncrono antes desta mudança);
  não é uma regressão introduzida por esta tarefa.
- `npm run build`: `tsc -b && vite build` concluído sem erros de tipo.

## Desvios do plano

Nenhum desvio funcional. Único detalhe de implementação decidido durante o desenvolvimento
(dentro do espaço já previsto pelo plano como "decisão de implementação"): `toIsoString`/
`toDatetimeLocalValue` foram movidas de `WorkLogFormModal.tsx` para
`worklog-date-mode.ts`, exatamente a alternativa sugerida explicitamente no plano ("sugestão:
mover `toIsoString`/`toDatetimeLocalValue` para `worklog-date-mode.ts` também, já que passam
a ser consumidos por mais de um lugar").

Os testes que cobriam a sincronização duração→modo simples/avançado precisaram ser escritos
respeitando a decisão de design explícita do plano de **não** recalcular os campos do modo
simples a cada edição de duração (só na abertura do modal e no toggle do checkbox) — os
testes verificam esse comportamento alternando o checkbox após o debounce da duração, em vez
de esperar atualização "ao vivo" dos inputs de modo simples, que o próprio plano descarta
como comportamento esperado.

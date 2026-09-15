# Plano de desenvolvimento — worklog-dur-sync-fix

> Bug fix pontual no front-end. Não há mudanças de back-end (`api/`), banco de dados ou
> contrato de API — o payload enviado em `handleSubmit` já usa `startDate`/`endDate`
> como fonte da verdade e já está correto; o bug é puramente de exibição/estado em
> `WorkLogFormModal.tsx`.

## Resumo técnico da solução

`WorkLogFormModal.tsx` mantém `startDate`/`endDate` (modo avançado) como fonte única da
verdade, e `simpleDate`/`simpleStartTime`/`simpleEndTime` (modo simples) como uma
**projeção derivada** dessa fonte. Essa projeção só é recalculada em dois pontos hoje: no
`useEffect` de abertura do modal (linhas 64-100) e em `handleAdvancedModeChange` quando o
checkbox é desmarcado (linhas 150-159).

`handleDurationChange` (linhas 161-185) altera `endDate` via `setEndDate(...)` (linha
176) dentro do `setTimeout` de debounce, mas **nunca** chama `setSimpleDate`/
`setSimpleEndTime`. Resultado: com o checkbox desmarcado, o usuário está olhando para os
inputs de "Data"/"Início"/"Fim" (JSX condicional nas linhas 271-301), que continuam
mostrando o `simpleEndTime` antigo, enquanto o `endDate` real (usado no submit) já foi
atualizado. Daí a divergência tela vs. dado salvo.

A correção mínima é: dentro do `setTimeout` de `handleDurationChange`, depois de calcular
`newEnd` e antes/depois de `setEndDate`, também recalcular e aplicar
`simpleDate`/`simpleEndTime` a partir de `startDate` (que não muda) e do novo `endDate`,
reaproveitando `advancedToSimple` (já importado, já usado em outros dois pontos do mesmo
arquivo) — **mas apenas quando o modo simples estiver ativo** (`!isAdvancedMode`), e
**apenas no branch de sucesso do `parseDuration`** (nunca no `catch`, preservando o
critério #5 herdado da feature anterior).

Não é necessária nenhuma mudança em `worklog-date-mode.ts`: `advancedToSimple` já
implementa exatamente a regra pedida na decisão #2 do PO — `date` vem sempre de
`startDate`, `endTime` vem da hora de `endDate` (independente do dia), sem forçar
mudança de modo (ver docstring nas linhas 63-69 do arquivo).

## Diagnóstico técnico preciso (causa raiz)

Arquivo: `app/src/features/work-logs/WorkLogFormModal.tsx`

```tsx
// linhas 161-185 (estado atual)
const handleDurationChange = (value: string) => {
  lastEditedField.current = "duration"
  setDurationText(value)

  if (debounceTimer.current) {
    clearTimeout(debounceTimer.current)
  }

  debounceTimer.current = setTimeout(() => {
    try {
      const durationSeconds = parseDuration(value)
      setDurationError(null)

      if (startDate) {
        const newEnd = new Date(new Date(startDate).getTime() + durationSeconds * 1000)
        setEndDate(toDatetimeLocalValue(newEnd.toISOString()))   // <-- só atualiza a fonte de verdade
        setDateError(null)
      }
    } catch (error) {
      if (error instanceof DurationParseError) {
        setDurationError(error.message)
      }
    }
  }, 400)
}
```

`setEndDate` (linha 176) só atualiza o estado do modo avançado. Não existe nenhuma
chamada a `setSimpleDate`/`setSimpleStartTime`/`setSimpleEndTime` nesse fluxo. Compare
com `handleAdvancedModeChange` (linhas 150-159), que é o único outro lugar (fora do
`useEffect` de abertura) onde a projeção simples é recalculada a partir de
`startDate`/`endDate` — e só roda quando o usuário desmarca o checkbox, não quando a
duração muda.

Como o closure do `setTimeout` captura `startDate` do render em que `handleDurationChange`
foi chamado (React state via closure), usar a variável `startDate` diretamente dentro do
callback é seguro aqui pelo mesmo motivo que já é seguro hoje para `setEndDate` (o valor
de `startDate` não é alterado por nenhuma outra ação concorrente entre a digitação e o
disparo do debounce, dado o design atual do formulário).

## Mudança mínima proposta em `WorkLogFormModal.tsx`

Único trecho alterado: o corpo do `try` dentro do `setTimeout` de `handleDurationChange`.
Não mexer em `handleAdvancedModeChange`, no `useEffect` de abertura, em
`worklog-date-mode.ts`, nem em nenhum outro handler.

```tsx
debounceTimer.current = setTimeout(() => {
  try {
    const durationSeconds = parseDuration(value)
    setDurationError(null)

    if (startDate) {
      const newEndIso = new Date(new Date(startDate).getTime() + durationSeconds * 1000).toISOString()
      const newEnd = toDatetimeLocalValue(newEndIso)
      setEndDate(newEnd)
      setDateError(null)

      if (!isAdvancedMode) {
        const simple = advancedToSimple(startDate, newEnd)
        setSimpleDate(simple.date)
        setSimpleEndTime(simple.endTime)
        // simpleStartTime não muda: startDate não foi alterado por este handler.
      }
    }
  } catch (error) {
    if (error instanceof DurationParseError) {
      setDurationError(error.message)
    }
  }
}, 400)
```

Notas de design que justificam a granularidade da mudança:

- `isAdvancedMode` é lido do closure do componente (estado do render corrente), igual a
  `startDate`; não precisa de `ref` adicional porque o usuário não consegue alternar o
  checkbox e ver o resultado do debounce anterior de forma ambígua — no pior caso, o
  callback aplica `setSimpleDate`/`setSimpleEndTime` em modo avançado (`isAdvancedMode`
  true), o que é inofensivo pois esses estados não são renderizados nesse modo e serão
  re-sincronizados normalmente da próxima vez que o modo simples for reativado (mesmo
  padrão de "sincronização best-effort" que `applyDates` já usa hoje implicitamente).
  Ainda assim, a guarda `if (!isAdvancedMode)` evita trabalho e um possível flash de
  dados obsoletos se o usuário voltar ao modo simples exatamente entre o toggle e o
  disparo do debounce.
- Não usar `simpleDate` recalculado como dependência de nada além do próprio
  `setSimpleDate`: a "Data" exibida no modo simples sempre segue `startDate` (dia do
  início), nunca o dia de `endDate` — coerente com `advancedToSimple` e com a decisão #2
  do PO (rollover não move a "Data" exibida).
- Critério #4 (alternar checkbox depois não regride o valor) já é coberto de graça: como
  a correção grava o novo `endDate` corretamente tanto no estado avançado quanto no
  simples, `handleAdvancedModeChange` (que recalcula a partir de `startDate`/`endDate`
  atualizados) vai produzir o mesmo resultado que já está em tela — não há necessidade de
  tocar nesse handler.
- Critério #5 (duração inválida não altera modo simples) já é garantido pela mudança
  estar inteiramente dentro do `try`, após `parseDuration(value)` ter validado com
  sucesso — o `catch` continua idêntico ao atual.

## Back-end — mudanças por camada

Nenhuma. Não há alteração em `api/src/WorkLogManager.Api`,
`api/src/WorkLogManager.Application` nem `api/src/WorkLogManager.Infrastructure`. Não há
migration nova. Confirmado por leitura do `handleSubmit` (linhas 187-216): o payload já
usa `startDate`/`endDate` (fonte única de verdade), que já estavam corretos antes desta
correção — o bug era só de exibição.

## Front-end — mudanças em `app/`

- `app/src/features/work-logs/WorkLogFormModal.tsx`: alterar apenas o corpo do `try`
  dentro do `setTimeout` de `handleDurationChange` (linhas 161-185), conforme diff acima.
  Nenhum componente `shadcn/ui` novo, nenhuma chamada de API nova, nenhuma prop nova.
- `app/src/features/work-logs/worklog-date-mode.ts`: nenhuma mudança — `advancedToSimple`
  já é reaproveitado como está.
- `app/src/features/work-logs/WorkLogFormModal.test.tsx`: dois testes existentes
  documentam explicitamente o comportamento antigo (bug) como esperado e precisam ser
  atualizados na implementação (não é código de produção, mas está listado aqui porque
  faz parte do "fix" e será tocado no mesmo PR):
  - Linhas 137-152 (`"recalculates the underlying end date when the duration text field
    changes (debounced), reflected once switched to advanced mode"`): o comentário e o
    nome do teste descrevem o bug ("reflected once switched to advanced mode") como
    comportamento esperado. Deve ser reescrito para asserir que `simpleEndTime` já reflete
    o novo valor **antes** de qualquer clique no checkbox (ver "Estratégia de testes"
    abaixo).
  - Linhas 239-266 (`"#5 keeps duration synchronization working through both modes"`): o
    comentário nas linhas 252-254 (`"by design, the simple fields are only recomputed on
    open/toggle, not on every keystroke"`) descreve exatamente a causa raiz do bug como
    "by design". Deve ser atualizado para asserir que `simpleEndTime` já está correto
    antes do toggle, mantendo o restante do fluxo (toggle para conferir o modo avançado)
    como checagem adicional de não-regressão.

## Passos de implementação

1. Ler novamente `WorkLogFormModal.tsx` e `worklog-date-mode.ts` no início da
   implementação para confirmar que nada mudou desde este plano (checagem de
   sincronização, não repetição de trabalho).
2. Aplicar a mudança mínima em `handleDurationChange` descrita acima (guarda
   `if (!isAdvancedMode)` + `advancedToSimple(startDate, newEnd)` + `setSimpleDate` +
   `setSimpleEndTime`).
3. Atualizar os dois testes existentes em `WorkLogFormModal.test.tsx` (linhas 137-152 e
   239-266) que hoje codificam o bug como comportamento esperado, ajustando nome,
   comentário e asserções para o novo comportamento (ver "Estratégia de testes").
4. Adicionar os quatro casos de teste novos listados em "Estratégia de testes" (duração
   válida atualiza Fim sem toggle; duração inválida não altera Fim; rollover para o dia
   seguinte mantém Data original; toggle após edição de duração não regride o valor).
5. Rodar a suíte de front-end (`npm run test` ou equivalente configurado em `app/`,
   restrito aos arquivos `worklog-date-mode.test.ts` e `WorkLogFormModal.test.tsx`
   primeiro, depois a suíte completa) e o lint/typecheck do `app/`.
6. Revisão manual do diff: confirmar que nenhum outro handler
   (`handleDateChange`/`handleSimpleDateChange`/`handleSimpleTimeChange`/
   `handleAdvancedModeChange`/`handleSubmit`) foi tocado.

## Mapeamento critério de aceite → passo(s)

| Critério de aceite (PO) | Passo(s) |
|---|---|
| #1 Editar duração válida atualiza "Fim" na tela sem alternar o checkbox | Passo 2 (mudança em `handleDurationChange`); validado no passo 4, teste "duração válida atualiza Fim imediatamente" |
| #2 Valor persistido consistente com "Fim" exibido | Coberto de graça pelo passo 2: `endDate` (usado no submit) e `simpleEndTime` (exibido) passam a ser atualizados no mesmo evento, a partir da mesma fonte (`newEnd`); validado indiretamente pelos testes do passo 4 (asserir `simpleEndTime` e, via toggle para o modo avançado, `endInput.value`, derivam do mesmo `newEnd`) |
| #3 Rollover para o dia seguinte: Fim mostra só HH:mm, Data não muda | Passo 2 (reaproveita `advancedToSimple`, que já implementa essa regra); validado no passo 4, teste de rollover |
| #4 Alternar checkbox depois não regride o valor | Nenhuma mudança necessária em `handleAdvancedModeChange` (já recalcula a partir de `startDate`/`endDate` atualizados); validado no passo 4, teste de toggle pós-edição |
| #5 Duração inválida não altera campos do modo simples | Passo 2 (mudança fica inteiramente dentro do bloco de sucesso do `try`, após `parseDuration`); validado no passo 4, teste de duração inválida |

## Estratégia de testes (Vitest, `WorkLogFormModal.test.tsx`)

Reaproveitar os helpers já existentes no arquivo (`getSimpleDateInputs`,
`getAdvancedDateInputs`, `getAdvancedModeCheckbox`, `getDurationInput`,
`renderWithProviders`) — nenhum helper novo é necessário.

1. **Atualizar** (linhas 137-152): renomear para algo como `"recalculates the visible
   simple-mode end time when the duration text field changes (debounced), without
   needing to toggle to advanced mode"` e trocar a asserção final: em vez de clicar no
   checkbox e checar `endInput.value`, checar diretamente `endTimeInput!.value` (modo
   simples ainda ativo) logo após o `await` do debounce.
2. **Novo — duração inválida não altera Fim**: no modo simples, definir
   Data/Início/Fim, disparar `handleDurationChange` com um token inválido (ex.: `"1H"`,
   mesmo valor usado no teste equivalente do modo avançado nas linhas 112-121), aguardar
   o debounce, e asserir que (a) a mensagem de erro aparece (`screen.findByText`) e (b)
   `endTimeInput!.value` permanece com o valor anterior.
3. **Novo — rollover mantém Data original**: no modo simples, Data = `2026-01-01`,
   Início = `22:00`, Fim = `22:00` (duração inicial 0s); editar duração para um valor que
   ultrapasse a meia-noite (ex.: `"4h"`, resultando em fim às `02:00` do dia seguinte);
   aguardar o debounce; asserir que `dateInput!.value` continua `"2026-01-01"` e
   `endTimeInput!.value` é `"02:00"` (sem sufixo de dia, conforme decisão #2 do PO).
   Complementar clicando no checkbox de modo avançado e conferindo que `endInput.value`
   é `"2026-01-02T02:00"`, confirmando que o dado persistido é consistente (critério #2).
4. **Novo — toggle após edição de duração não regride**: no modo simples, editar
   Início/Fim, editar a duração para um novo valor, aguardar o debounce, conferir
   `endTimeInput!.value` já atualizado; então clicar no checkbox (entra em modo
   avançado) e clicar de novo (volta ao modo simples); asserir que `endTimeInput!.value`
   permanece o mesmo valor correto após o round-trip (não houve regressão para o valor
   anterior à edição de duração).
5. **Atualizar** (linhas 239-266, teste `"#5 keeps duration synchronization working
   through both modes"`): remover/corrigir o comentário que descreve o comportamento
   antigo como "by design"; inserir, logo após o `await` do debounce e antes do clique no
   checkbox, uma asserção de que `endTimeInput!.value` já é `"10:00"` (mesmo valor que
   hoje só é conferido depois do toggle). Manter o restante do teste (toggle + conferência
   do modo avançado + edição em modo avançado) como está, já que continua válido.

Não há necessidade de testes em `worklog-date-mode.test.ts`: nenhuma função pura desse
módulo foi alterada, e a suíte existente já cobre `advancedToSimple`/`simpleToAdvanced`/
`isMultiDay` isoladamente.

## Riscos e dependências

- Nenhuma dependência de back-end, migration ou API nova.
- Risco baixo: a mudança é um `if` + 2 chamadas de `setState` adicionais, isoladas dentro
  de um bloco já existente e já coberto por debounce/try-catch. O principal risco é
  esquecer de atualizar os dois testes que hoje "travam" o bug como comportamento
  esperado — se não forem atualizados, a suíte pode ficar inconsistente (um teste
  asserindo o valor certo cedo, outro só depois do toggle) sem necessariamente falhar,
  mascarando a regressão. Mitigado pelo passo 3 do plano de implementação.
- Timezone: os testes usam horários que não cruzam a meia-noite em UTC de forma ambígua
  com o fuso da máquina de CI, seguindo o mesmo cuidado já documentado no teste "#2" do
  arquivo atual (comentário sobre dois dias completos em UTC). O novo teste de rollover
  (item 3 da Estratégia de testes) usa apenas campos `date`/`time` do modo simples, que
  não sofrem conversão de fuso (diferente de `datetime-local`), então não há risco
  equivalente.

## Fora de escopo / não será feito

- Nenhuma mudança em `worklog-date-mode.ts` (as funções puras já implementam a regra
  correta; o bug está apenas na falta de uma chamada em `WorkLogFormModal.tsx`).
- Nenhuma mudança em `handleDateChange`, `handleSimpleDateChange`,
  `handleSimpleTimeChange`, `handleAdvancedModeChange` ou no `useEffect` de abertura do
  modal — nenhum desses tem o bug reportado.
- Nenhuma mudança de contrato de API, DTO, use-case ou entidade no back-end.
- Nenhuma refatoração do padrão `lastEditedField`/debounce existente, nem introdução de
  `useEffect` reativo a `startDate`/`endDate` para recalcular o modo simples — a
  correção segue o padrão imperativo já usado em `handleAdvancedModeChange`, evitando
  reescrever mais do que o necessário para um bug fix pontual.

## Status: aguardando aprovação

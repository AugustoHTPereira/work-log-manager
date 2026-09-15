# Plano de desenvolvimento: modo simples/avançado no modal de worklog + axios

Task-id: worklog-modal-axios

> Tarefa incremental sobre o sistema `worklog-manager` já implementado. Report do PO em
> `.claude/plans/worklog-modal-axios/po-input.md`. Não há alteração de back-end.

## Resumo técnico da solução

Duas mudanças independentes, ambas restritas a `app/`:

1. `WorkLogFormModal.tsx` ganha um segundo modo de preenchimento de datas ("modo
   simples": campos "Data" + "Início" + "Fim") controlado por um `Checkbox` (shadcn/ui,
   ainda não instalado no projeto) rotulado algo como "Editar data inicial e final
   separadamente". O estado interno do formulário passa a ser mantido de duas formas
   coexistentes — os campos de "modo simples" (`simpleDate`, `simpleStartTime`,
   `simpleEndTime`) e os campos de "modo avançado" (`startDate`, `endDate`, já
   existentes, formato `datetime-local`) — sincronizados por funções puras de conversão
   sempre que o checkbox alterna, e sempre que o usuário edita um campo do modo
   corrente. O payload final enviado ao back-end (`{type, startDate, endDate}`)
   continua derivado a partir do modo avançado internamente: o modo simples é só uma
   camada de apresentação que se traduz para `startDate`/`endDate` ISO antes de
   montar/gravar o estado avançado (fonte única de verdade continua sendo
   `startDate`/`endDate`; o modo simples é derivado/sincronizado a partir dele e
   vice-versa, análogo ao que hoje já acontece entre datas e duração).
2. A camada `app/src/lib/api/` migra de `fetch` para `axios`: `client.ts` passa a criar
   uma instância `axios.create({ baseURL })`, com um helper que tenta preservar
   exatamente a mesma superfície pública (`apiClient.get/post/put/delete`) e o mesmo
   comportamento observável de erros/204 hoje coberto implicitamente pelos testes que
   mockam os módulos de `lib/api/*` (não há teste direto de `client.ts` hoje, então essa
   migração passa a ganhar seu próprio teste).

## Back-end — mudanças por camada

Nenhuma. Confirmado pelo PO como fora de escopo: endpoints, DTOs, use-cases e
validators de `WorkLog` em `api/` permanecem inalterados. `CreateWorkLogPayload`/
`UpdateWorkLogPayload` no front continuam `{ type, startDate, endDate }`.

## Front-end — mudanças em `app/`

### Componentes/telas afetados
- `app/src/features/work-logs/WorkLogFormModal.tsx` — reescrita da lógica de estado e
  do JSX dos campos de data.
- `app/src/features/work-logs/WorkLogFormModal.test.tsx` — testes existentes
  preservados (ajustados apenas se o DOM mudar) + novos testes de conversão de modo.
- `app/src/lib/api/client.ts` — reescrita interna usando axios, mesma API pública.
- `app/src/lib/api/employees.ts`, `workLogs.ts`, `systemSettings.ts` — não deveriam
  precisar mudar (já dependem só de `apiClient`), mas serão revisados para confirmar
  que nenhum deles usa `fetch`/`Response` diretamente.
- Novo: `app/src/lib/api/client.test.ts` (não existe teste de `client.ts` hoje).
- Novo utilitário puro: `app/src/features/work-logs/worklog-date-mode.ts` (nome
  sugerido) com as funções de conversão entre modo simples e avançado, para poder
  testá-las isoladamente do componente React (padrão já usado no projeto para
  `worklog-duration.ts`).

### Componentes `shadcn/ui` a usar
- `Checkbox` — ainda não instalado em `app/src/components/ui/`. Passo de implementação
  inclui rodar `npx shadcn add checkbox` (ou criar manualmente seguindo o padrão dos
  demais componentes em `components/ui/`, caso o CLI não esteja disponível no ambiente
  de implementação — mas a preferência é sempre o CLI, por convenção do projeto).
- `Input type="date"` e `Input type="time"` (componente `Input` já existente,
  reutilizado com `type` diferente) para os campos "Data", "Início" e "Fim" do modo
  simples.
- `Label` (já existente) para os novos campos.

### Chamadas de API
Nenhuma nova chamada de API. `useCreateWorkLog`/`useUpdateWorkLog` (em
`app/src/features/work-logs/hooks/`) permanecem inalterados — continuam recebendo
`{type, startDate, endDate}` já no formato ISO, montado pelo modal.

## Modelagem de estado do `WorkLogFormModal.tsx`

### Estado
```ts
const [isAdvancedMode, setIsAdvancedMode] = useState(false)

// Modo avançado (fonte de verdade para o payload, como hoje)
const [startDate, setStartDate] = useState("") // datetime-local
const [endDate, setEndDate] = useState("")     // datetime-local

// Modo simples (derivado/sincronizado a partir do avançado)
const [simpleDate, setSimpleDate] = useState("")      // <input type="date">
const [simpleStartTime, setSimpleStartTime] = useState("") // <input type="time">
const [simpleEndTime, setSimpleEndTime] = useState("")     // <input type="time">

const [durationText, setDurationText] = useState("")
const [durationError, setDurationError] = useState<string | null>(null)
const [dateError, setDateError] = useState<string | null>(null)
const lastEditedField = useRef<"dates" | "duration" | null>(null)
```

`startDate`/`endDate` (formato `datetime-local`, já usados hoje para montar o payload e
para a sincronização com duração) continuam sendo a fonte única de verdade interna.
Isso evita duplicar a lógica de sincronização com duração: ela continua olhando só para
`startDate`/`endDate`, independente do modo visível. O modo simples é uma "view" que
lê/escreve nesses mesmos valores através das funções de conversão.

### Funções puras de conversão (`worklog-date-mode.ts`)

```ts
interface SimpleModeValue {
  date: string       // "YYYY-MM-DD"
  startTime: string  // "HH:mm"
  endTime: string    // "HH:mm"
}

/** Combina Data + Início/Fim em datetime-local completos, aplicando a regra de
 * virada de dia: se endTime < startTime, o fim é assumido no dia seguinte a `date`. */
function simpleToAdvanced(value: SimpleModeValue): { startDate: string; endDate: string }

/** Decompõe um par startDate/endDate (datetime-local) no formato do modo simples:
 * date = data de startDate; startTime = hora de startDate; endTime = hora de endDate
 * (independente de endDate cair no dia seguinte — a informação do "dia seguinte" se
 * perde propositalmente ao entrar no modo simples, e é reconstituída ao sair dele via
 * a mesma regra de virada de dia). */
function advancedToSimple(startDate: string, endDate: string): SimpleModeValue

/** true se startDate e endDate (datetime-local) caem em dias de calendário diferentes,
 * usada para decidir o estado inicial do checkbox ao editar um worklog existente. */
function isMultiDay(startDate: string, endDate: string): boolean
```

Essas três funções são puras (sem `Date.now()`, sem I/O), testáveis diretamente, e
reutilizam `toDatetimeLocalValue`/o parsing de `Date` já usados no componente hoje (que
podem ser movidos para este novo arquivo ou mantidos em `WorkLogFormModal.tsx` e
importados — decisão de implementação; sugestão: mover `toIsoString`/
`toDatetimeLocalValue` para `worklog-date-mode.ts` também, já que passam a ser
consumidos por mais de um lugar).

### Fluxos

1. **Abrir para criar**: `isAdvancedMode = false`; `startDate = endDate = now`;
   `simpleDate/simpleStartTime/simpleEndTime` derivados de `advancedToSimple(now, now)`.
2. **Abrir para editar**: `startDate`/`endDate` vêm do `workLog`;
   `isAdvancedMode = isMultiDay(startDate, endDate)`; se `false`, também calcula
   `simpleDate/simpleStartTime/simpleEndTime` via `advancedToSimple` (para o modo
   simples já nascer preenchido, mesmo que não visível se o usuário desmarcar depois —
   na prática, se `isAdvancedMode` inicial é `false`, o modo simples é o visível de
   início, então precisa estar preenchido; se `isAdvancedMode` inicial é `true`, o modo
   simples fica "sujo" até o usuário desmarcar o checkbox, quando é recalculado).
3. **Editar campo no modo simples** (`handleSimpleDateChange`/`handleSimpleTimeChange`):
   atualiza o campo local de modo simples correspondente, recalcula
   `{startDate, endDate} = simpleToAdvanced(...)` e escreve em `startDate`/`endDate`
   (fonte de verdade), depois segue o mesmo caminho que hoje já existe em
   `handleDateChange` para recalcular `durationText` a partir do novo
   `startDate`/`endDate` e setar `lastEditedField.current = "dates"`.
4. **Editar campo no modo avançado**: mantém exatamente o `handleDateChange` atual (sem
   mudança de comportamento).
5. **Editar duração**: mantém exatamente o `handleDurationChange` atual — como ele já
   escreve em `endDate` (via `setEndDate`), o único ajuste necessário é, ao recalcular
   `endDate`, também recalcular `simpleEndTime`/`simpleDate` a partir do novo
   `startDate`/`endDate` via `advancedToSimple`, para o modo simples não ficar
   dessincronizado se o usuário alternar o checkbox logo em seguida. (Alternativa mais
   simples adotada: em vez de manter os dois conjuntos de estado sempre sincronizados a
   cada tecla, os campos do modo simples só são recalculados a partir de
   `startDate`/`endDate` a) na abertura do modal e b) no instante em que o checkbox é
   alternado — ver passo 6. Isso evita ter que espalhar recomputação em todo handler
   existente e reduz o risco de regressão nos testes já existentes de sincronização com
   duração. Essa é a abordagem escolhida.)
6. **Alternar o checkbox** (`handleAdvancedModeChange`):
   - Se está saindo do modo simples para o avançado (`checked = true`): não precisa
     fazer nada com `startDate`/`endDate` (já são a fonte de verdade, já refletem o que
     o usuário via no modo simples via `simpleToAdvanced` aplicado a cada edição).
   - Se está saindo do avançado para o simples (`checked = false`): recalcula
     `simpleDate/simpleStartTime/simpleEndTime = advancedToSimple(startDate, endDate)`.
   - Em ambos os casos, `durationText` não muda (ele já reflete `startDate`/`endDate`
     correntes, calculados independentemente do modo visível).

Com essa abordagem, `startDate`/`endDate` (datetime-local) seguem sendo a única fonte
de verdade viva a cada tecla digitada, e os campos do modo simples são "escritos" (via
`simpleToAdvanced`) sempre que editados diretamente, e "lidos/recalculados" (via
`advancedToSimple`) apenas ao entrar no modo simples (abertura do modal ou toggle do
checkbox) — nunca há dessincronização visível ao usuário porque ele só vê um modo por
vez, e a transição sempre recalcula o modo que vai ficar visível a partir da fonte de
verdade.

### Regra de virada de dia (critério #3)

Implementada dentro de `simpleToAdvanced`: dados `date`, `startTime`, `endTime`,
monta `start = date + startTime`; se `endTime < startTime` (comparação de string
`"HH:mm"` funciona lexicograficamente), `end = (date + 1 dia) + endTime`, senão
`end = date + endTime`. Comparação apenas entre horários — não compara contra `start`
por completo, exatamente como descrito no critério de aceite (usa apenas os horários).

### Envio do formulário (`handleSubmit`)

Sem mudança de lógica: continua montando o payload a partir de `startDate`/`endDate`
(datetime-local) via `toIsoString`, independente do modo visível — já que esses dois
campos são sempre mantidos atualizados (ver fluxo acima). A validação existente
("datas obrigatórias", "final não pode ser anterior ao inicial") permanece igual, como
defesa em profundidade — na prática nunca deve disparar a partir do modo simples porque
a regra de virada de dia impede `end < start`.

### Novo JSX condicional

```tsx
<div className="flex items-center gap-2">
  <Checkbox id="advanced-mode" checked={isAdvancedMode} onCheckedChange={handleAdvancedModeChange} />
  <Label htmlFor="advanced-mode">Editar data inicial e final separadamente</Label>
</div>

{isAdvancedMode ? (
  <>
    {/* Data inicial / Data final — inputs datetime-local existentes, sem mudança */}
  </>
) : (
  <>
    <div className="space-y-2">
      <Label>Data</Label>
      <Input type="date" value={simpleDate} onChange={...} />
    </div>
    <div className="grid grid-cols-2 gap-4">
      <div className="space-y-2">
        <Label>Início</Label>
        <Input type="time" value={simpleStartTime} onChange={...} />
      </div>
      <div className="space-y-2">
        <Label>Fim</Label>
        <Input type="time" value={simpleEndTime} onChange={...} />
      </div>
    </div>
    {dateError && <p className="text-sm text-destructive">{dateError}</p>}
  </>
)}
```

## Migração de `lib/api/` para axios

### `app/src/lib/api/client.ts`

```ts
import axios, { AxiosError } from "axios"

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5244"

export class ApiError extends Error { status: number; ... } // inalterado

const httpClient = axios.create({
  baseURL: API_BASE_URL,
  headers: { "Content-Type": "application/json" },
  validateStatus: () => true, // trata status manualmente, para replicar o comportamento atual (!response.ok)
})

async function request<TResponse>(path: string, config: AxiosRequestConfig): Promise<TResponse> {
  const response = await httpClient.request<TResponse>({ url: path, ...config })

  if (response.status < 200 || response.status >= 300) {
    const body = response.data as { message?: string } | null
    const message = body?.message ?? `Request to ${path} failed with status ${response.status}.`
    throw new ApiError(message, response.status)
  }

  if (response.status === 204) {
    return undefined as TResponse
  }

  return response.data
}

export const apiClient = {
  get: <TResponse>(path: string) => request<TResponse>(path, { method: "GET" }),
  post: <TResponse>(path: string, body: unknown) => request<TResponse>(path, { method: "POST", data: body }),
  put: <TResponse>(path: string, body: unknown) => request<TResponse>(path, { method: "PUT", data: body }),
  delete: <TResponse>(path: string) => request<TResponse>(path, { method: "DELETE" }),
}
```

Pontos de atenção para preservar comportamento observável:
- `validateStatus: () => true` é necessário para que erros HTTP (4xx/5xx) não sejam
  lançados pelo próprio axios como `AxiosError` antes de chegarmos à checagem manual —
  hoje o código trata isso lendo `response.ok`; a versão axios replica isso checando
  `response.status`.
- Falhas de rede genuínas (sem resposta, ex. servidor fora do ar) ainda lançam
  `AxiosError` do axios (sem `response`) — hoje o `fetch` também rejeitaria a Promise
  nesse caso (com `TypeError`) e nenhum teste depende desse caminho hoje; o
  comportamento (Promise rejeitada, sem virar `ApiError`) é preservado sem tratamento
  extra, mantendo paridade.
- `response.data` já vem parseado (axios faz `JSON.parse` automaticamente quando o
  `Content-Type` da resposta é `application/json`); isso substitui o
  `await response.json()` atual. Para 204 (corpo vazio), `response.data` normalmente é
  `""` — o `if (response.status === 204)` explícito garante que retornamos `undefined`
  antes de tentar usar `response.data` como `TResponse`.
- `employees.ts`, `workLogs.ts`, `systemSettings.ts` não mudam: já só usam
  `apiClient.get/post/put/delete`.

### Dependência

`app/package.json` ganha `"axios"` em `dependencies` (versão estável mais recente no
momento da implementação, ex. `^1.x`).

## Passos de implementação

1. Adicionar dependência `axios` em `app/package.json` (`npm install axios` dentro de
   `app/`).
2. Reescrever `app/src/lib/api/client.ts` para usar axios, preservando a API pública
   `apiClient.{get,post,put,delete}` e a classe `ApiError`, conforme especificação
   acima.
3. Revisar `employees.ts`, `workLogs.ts`, `systemSettings.ts` — confirmar que não
   precisam de alteração (só dependem de `apiClient`).
4. Criar `app/src/lib/api/client.test.ts` cobrindo: sucesso (GET retornando JSON),
   erro 4xx (mensagem extraída do corpo, `ApiError` com `status` correto), 204 (retorna
   `undefined`), erro 4xx sem corpo JSON (mensagem default). Mockar axios via
   `vi.mock("axios")` ou interceptar com um mock de `axios.create`.
5. Rodar `npx shadcn add checkbox` dentro de `app/` para instalar
   `app/src/components/ui/checkbox.tsx` (ou criar manualmente seguindo o padrão dos
   demais arquivos de `components/ui/` caso o CLI não funcione no ambiente).
6. Criar `app/src/features/work-logs/worklog-date-mode.ts` com `simpleToAdvanced`,
   `advancedToSimple`, `isMultiDay` (e, se optado, mover `toIsoString`/
   `toDatetimeLocalValue` para lá), com comentário de cabeçalho explicando a regra de
   virada de dia, no mesmo estilo de `worklog-duration.ts`.
7. Criar `app/src/features/work-logs/worklog-date-mode.test.ts` cobrindo os cenários
   descritos na seção "Estratégia de testes" abaixo.
8. Alterar `WorkLogFormModal.tsx`:
   - Adicionar estado `isAdvancedMode`, `simpleDate`, `simpleStartTime`,
     `simpleEndTime`.
   - Ajustar o `useEffect` de abertura do modal para calcular `isAdvancedMode` inicial
     (`isMultiDay` na edição; `false` na criação) e popular ambos os conjuntos de
     estado.
   - Adicionar `handleSimpleDateChange`/`handleSimpleTimeChange`, que recalculam
     `startDate`/`endDate` via `simpleToAdvanced` e reaproveitam a lógica de
     recálculo de duração hoje em `handleDateChange` (extrair um helper comum, ex.
     `applyDates(nextStart, nextEnd)`, chamado tanto pelo fluxo avançado quanto pelo
     simples, para não duplicar a lógica de `durationText`/`dateError`).
   - Adicionar `handleAdvancedModeChange`, que alterna `isAdvancedMode` e, ao entrar no
     modo simples, recalcula os três campos simples via `advancedToSimple`.
   - Atualizar o JSX conforme a seção "Novo JSX condicional".
   - Importar e renderizar `Checkbox` de `@/components/ui/checkbox`.
9. Ajustar `app/src/features/work-logs/WorkLogFormModal.test.tsx` se necessário (os
   testes atuais assumem o modo avançado visível por padrão ao abrir sem `workLog`; como
   o novo padrão é modo simples, os testes existentes de datetime-local precisam ou
   marcar o checkbox antes de interagir, ou ser convertidos para usar os inputs do modo
   simples — ver mapeamento de testes abaixo). Ajustar seletores conforme necessário.
10. Adicionar novos testes em `WorkLogFormModal.test.tsx` (ou arquivo dedicado) cobrindo
    os critérios de aceite #1–#5 (ver "Estratégia de testes").
11. Rodar `npm run lint` e `npm test` (dentro de `app/`) e `npm run build` (checagem de
    tipos) para validar que nada quebrou.

## Mapeamento critério de aceite → passos

| Critério de aceite | Passo(s) |
|---|---|
| #1 (modal abre em modo simples ao criar) | 8, 9, 10 |
| #2 (checkbox marcado automaticamente ao editar multi-dia) | 6, 7, 8, 10 |
| #3 (virada de dia no modo simples) | 6, 7, 8, 10 |
| #4 (conversão preserva dados entre modos) | 6, 7, 8, 10 |
| #5 (sincronização com duração continua funcionando nos dois modos) | 8, 9 |
| #6 (migração para axios) | 1, 2, 3, 4, 11 |
| #7 (contrato do back-end inalterado) | 2 (payload montado da mesma forma), nenhuma mudança em `api/` |

## Estratégia de testes

### `worklog-date-mode.test.ts` (Vitest, funções puras)

| Cenário | Verificação |
|---|---|
| `simpleToAdvanced` sem virada de dia (`start=08:00`, `end=17:00`) | `endDate` no mesmo dia de `date` |
| `simpleToAdvanced` com virada de dia (`start=22:00`, `end=02:00`) | `endDate` no dia seguinte a `date` |
| `simpleToAdvanced` com `start === end` | não considera virada de dia (duração 0, mesmo dia) |
| `advancedToSimple` com `startDate`/`endDate` no mesmo dia | `date`/`startTime`/`endTime` corretos, sem perda |
| `advancedToSimple` com `endDate` no dia seguinte | `endTime` extraído corretamente da hora, `date` = dia de `startDate` |
| Round-trip simples → avançado → simples preserva os três campos originais | idempotência da conversão |
| Round-trip avançado → simples → avançado preserva `startDate`/`endDate` originais (quando multi-dia é só de 1 dia de diferença) | idempotência |
| `isMultiDay` com mesmo dia calendário | retorna `false` |
| `isMultiDay` com dias calendário diferentes | retorna `true` |

### `WorkLogFormModal.test.tsx` (Vitest + Testing Library)

| Critério de aceite | Cenário de teste |
|---|---|
| #1 | Renderizar modal sem `workLog` (criação); checkbox não está marcado; inputs "Data"/"Início"/"Fim" visíveis (por label), inputs "Data inicial"/"Data final" ausentes. |
| #2 | Renderizar modal com `workLog` cujo `startDate`/`endDate` caem em dias diferentes → checkbox vem marcado, "Data inicial"/"Data final" visíveis. Outro teste: `workLog` no mesmo dia → checkbox desmarcado, modo simples visível com valores corretos. |
| #3 | No modo simples, preencher Data=2026-01-01, Início=22:00, Fim=02:00; marcar o checkbox (ou submeter) e verificar que `endDate` resultante (inspecionado via input datetime-local após marcar o checkbox, ou via o payload enviado ao mock de `createWorkLog`) cai em 2026-01-02T02:00. |
| #4 | Preencher modo simples (Data/Início/Fim), marcar checkbox → inputs "Data inicial"/"Data final" refletem os valores esperados; desmarcar novamente → volta a exibir os mesmos Data/Início/Fim originais (round-trip via UI). |
| #5 | Testes já existentes (linhas 37–97 do arquivo atual) continuam passando, adaptados para operar no modo simples por padrão (ou marcando o checkbox no `beforeEach`/início de cada teste que usa os inputs `datetime-local`, já que o padrão de abertura mudou). Adicionar um teste equivalente de sincronização duração ↔ datas operando via os inputs do modo simples (editar Início/Fim recalcula duração; editar duração recalcula Fim). |

### `client.test.ts` (Vitest, axios mockado)

| Critério de aceite | Cenário de teste |
|---|---|
| #6 | GET bem-sucedido retorna o corpo parseado; resposta com status 4xx e corpo `{message}` lança `ApiError` com essa mensagem e `status` correto; resposta 4xx sem corpo JSON válido usa mensagem default; resposta 204 retorna `undefined` em vez de tentar parsear corpo. |

## Riscos e dependências

- **Quebra dos testes existentes de `WorkLogFormModal.test.tsx`**: como o modo padrão
  de abertura muda de avançado para simples, os testes atuais que interagem
  diretamente com `input[type="datetime-local"]` deixam de encontrar esses elementos ao
  abrir o modal sem `workLog`. Precisam ser adaptados (marcar o checkbox primeiro, ou
  reescrever para o modo simples) — mapeado no passo 9, sem alterar a intenção original
  dos testes (continuam cobrindo a mesma regra de negócio de sincronização com
  duração).
- **Comparação lexicográfica de `"HH:mm"` para virada de dia**: só é válida porque o
  formato de `<input type="time">` é sempre `HH:mm` (2 dígitos, 24h) — não há edge case
  de formato de hora com AM/PM a considerar, mas vale reforçar no comentário do código.
- **Paridade de comportamento de erro do axios**: o uso de `validateStatus: () => true`
  é essencial para não introduzir uma diferença de comportamento onde o axios lançaria
  antes da checagem manual de `ApiError`; deve ser coberto explicitamente pelo teste de
  #6.
- **Biblioteca `Checkbox` do shadcn**: depende de `@radix-ui/react-checkbox` (via
  `radix-ui`, já presente como dependência agregada no projeto) — o comando
  `npx shadcn add checkbox` deve funcionar sem instalar pacotes novos além do próprio
  arquivo gerado, mas isso só se confirma na implementação.
- **Ambiguidade round-trip quando a duração é maior que 24h**: o modo simples só
  representa um intervalo de no máximo ~24h (data + hora de início + hora de fim, com
  no máximo 1 virada de meia-noite). Um worklog com duração multi-dia (ex.: 3 dias)
  jamais cai no modo simples porque `isMultiDay` verifica dias de calendário
  diferentes, então o checkbox nasce marcado e o usuário nunca vê uma decomposição
  ambígua — mas se o usuário, já no modo avançado com essa configuração, desmarcar o
  checkbox manualmente, a conversão via `advancedToSimple` vai "perder" a informação de
  quantos dias se passaram (o campo `date` some, só resta `date`+1 potencial). Decisão
  técnica: `advancedToSimple` sempre assume no máximo 1 dia de diferença (mesma
  premissa de `simpleToAdvanced`); se o usuário desmarcar o checkbox com um intervalo
  de mais de ~24h, o resultado exibido é a melhor aproximação (hora de início e hora de
  fim corretas, mas a informação "quantos dias" se perde ao entrar no modo simples,
  igual ao que já se perde hoje entre o formato e o texto de duração em outros
  cenários). Isso é aceitável porque o checkbox nasce marcado para esses casos e nada
  impede o usuário de deixá-lo marcado.

## Fora de escopo / não será feito

- Alterações em `api/` (endpoints, DTOs, use-cases, validators, migrations).
- Alterações em `worklog-duration.ts`.
- Suporte a fuso horário diferente do fuso local do navegador.
- Interceptors globais de axios (auth, retry, logging) — fora do pedido, a migração é
  estritamente de `fetch` para `axios` preservando comportamento.

---
## Status: aguardando aprovação

# Report do PO — bug fix: sincronização de duração no modo simples

## Bug reportado

No modo simples (checkbox desmarcado, campos Data/Início/Fim), editar o campo de texto
de duração atualiza corretamente `startDate`/`endDate` internamente, mas o campo visível
"Fim" (`simpleEndTime`) não reflete essa mudança na tela — só é recalculado se o usuário
alternar o checkbox de modo avançado.

**Causa raiz**: decisão de design da tarefa anterior de que os campos do modo simples só
são recomputados a partir de `startDate`/`endDate` em dois momentos (abertura do modal e
toggle do checkbox), não a cada edição indireta de `startDate`/`endDate` via duração.

## Decisões do usuário para a correção (requisitos definidos)

1. A correção deve fazer o modo simples recalcular `simpleEndTime` (e `simpleDate` se
   necessário) a cada edição VÁLIDA de duração — não só no toggle do checkbox. Em caso de
   duração inválida (`durationError` setado), os campos do modo simples NÃO devem ser
   recalculados com valor incorreto (mantém o comportamento de erro já existente,
   critério #5 do report original da feature).
2. Se a duração editada fizer o horário final cair no dia seguinte à "Data" exibida no
   modo simples: o campo "Fim" deve ser atualizado normalmente (mostrando só o horário
   HH:mm), e o campo "Data" permanece sendo a data de início — mesma convergência que
   `advancedToSimple` já usa hoje (não força troca automática para o modo avançado).

## Critérios de aceite

1. No modo simples, editar duração para valor válido atualiza "Fim" na tela
   imediatamente, sem precisar alternar o checkbox.
2. Valor persistido ao salvar é consistente com o "Fim" exibido em tela (sem divergência
   tela vs. dado salvo).
3. Se o "Fim" recalculado cair no dia seguinte à "Data", comportamento é claro e
   consistente (decisão do usuário: mostra só HH:mm, Data não muda).
4. Alternar o checkbox depois de editar a duração no modo simples não deve
   regressar/quebrar o valor já correto de "Fim".
5. Duração inválida não deve recalcular/alterar os campos do modo simples com valor
   incorreto.

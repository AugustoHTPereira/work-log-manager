# Report da tarefa: Fechamento de ponto (geração automática de apontamentos mensais)

## Objetivo
Permitir que o usuário feche um mês de apontamento de ponto, gerando automaticamente os `EmployeeWorkLog` dos dias úteis (segunda a sexta) para os funcionários.

## Escopo
- Botão "Fechar mês" na tela inicial, ao lado do "+" existente.
- Modal de seleção de mês/ano, valor padrão = mês anterior ao atual.
- Ação "Prosseguir": gera automaticamente `EmployeeWorkLog` para os dias úteis (seg-sex) do mês/ano selecionado, para todos os funcionários cadastrados.
- Variação aleatória de até 4 minutos no `StartDate` e até 4 minutos no `EndDate` de cada dia gerado.
- Nova entidade/tabela de registro de fechamento: mês, ano, `CreatedAtUtc`, `UpdatedAtUtc`.
- Relacionamento entre os `EmployeeWorkLog` gerados e o registro de fechamento.

## Decisões do usuário (a incorporar como requisitos definidos, não mais "em aberto")

1. **"Relatório"**: não é uma tela separada — é simplesmente a listagem dos worklogs gerados exibida como resultado do fechamento (ex.: ao concluir, mostrar a lista de worklogs criados, ou o usuário navega e vê os worklogs normalmente na tela de detalhe de cada funcionário). Decida a forma exata de exibição no plano (sugestão: modal ou página de resumo do fechamento mostrando quantidade de worklogs gerados por funcionário, com link para o detalhe).
2. **"d-1" do seletor**: mês civil anterior ao mês/ano atual (ex.: hoje setembro/2026 → padrão agosto/2026).
3. **Piso mínimo de duração**: NÃO é 8h fixo — é a jornada efetiva do próprio funcionário (`Employee.DailyWorkHours` quando definido, senão `SystemSettings.DefaultDailyWorkHours`, mesmo conceito de "EffectiveDailyWorkHours" já usado em `GetEmployeeByIdUseCase`). A variação de ±4 minutos em início/fim nunca pode fazer a duração cair abaixo dessa jornada efetiva — ou seja, a variação deve ser aplicada de forma que sempre resulte em duração >= jornada efetiva (ex.: se o funcionário varia para chegar mais tarde, deve compensar saindo mais tarde também, ou a lógica de geração deve garantir isso de alguma forma — detalhe a decidir no plano).
4. **Tipo do worklog gerado**: adicionar um novo valor ao enum `WorkLogType` existente (hoje só `Absence`/`Overtime`) para representar presença/trabalho normal — ex. `RegularAttendance`. Avalie o nome mais consistente com o padrão em inglês já usado (`Absence`, `Overtime` → sugestão `RegularAttendance` ou `Regular`). Isso implica migration para o back-end (alteração do `HasConversion<string>()` do enum, que já é string-based, então adicionar um valor novo é aditivo e não quebra dados existentes).
5. **Quais funcionários**: todos os funcionários cadastrados no sistema (sem seleção manual).
6. **Conflito com worklogs existentes**: para cada funcionário e cada dia útil do mês, o sistema deve pular a geração SOMENTE se já existir um worklog do tipo `RegularAttendance` (presença) naquele dia específico para aquele funcionário. Worklogs de `Absence` (falta) ou `Overtime` (hora extra) já existentes no mesmo dia NÃO impedem a geração do `RegularAttendance` daquele dia — ambos podem coexistir (ex.: um dia pode ter uma falta parcial e ainda receber o registro de presença gerado, ou uma hora extra e a presença gerada, sem conflito).
7. **Refechamento do mesmo mês/ano**: NÃO é permitido — deve haver constraint de unicidade no registro de fechamento por (mês, ano). Tentar fechar o mesmo mês/ano novamente deve ser bloqueado com erro (400/409), impedindo até a tentativa de gerar novos worklogs para aquele período.
8. **Restrição de período**: o modal deve bloquear seleção do mês atual e de meses futuros — só permite fechar meses passados já completos. Validar tanto no front (desabilitar no seletor) quanto no back-end (rejeitar requisição para mês atual/futuro).

## Regras de negócio adicionais a considerar no plano
- A geração cobre apenas segunda a sexta-feira do mês/ano selecionado (sem considerar feriados).
- A variação de ±4 minutos deve ser uma geração pseudoaleatória no back-end (fonte de aleatoriedade a definir — ex.: `Random` por execução, não precisa ser determinístico/seed fixo, mas deve ser testável — sugestão: extrair a lógica de "gerar horário do dia com variação" para um método/serviço puro que receba a variação já sorteada ou um `IRandomProvider`/similar injetável, para poder ser testado com valores controlados).
- Como o "worklog" de presença representa um dia inteiro de trabalho (não um apontamento pontual de falta/hora-extra), pense em como isso se encaixa no modelo atual de `EmployeeWorkLog` (mesma tabela com o novo `Type`, ou entidade separada — decida e justifique; a recomendação natural dado o pedido explícito do usuário de "o sistema deve relacionar os worklogs ao fechamento" é manter tudo em `EmployeeWorkLog`, adicionando uma FK opcional `MonthClosingId` nessa tabela).

## Critérios de aceite (consolidados com as decisões acima)
1. Botão "Fechar mês" visível na tela inicial.
2. Modal com seletor único de mês/ano, padrão = mês anterior ao atual, meses atual/futuro desabilitados/bloqueados.
3. "Prosseguir" gera `EmployeeWorkLog` (Type=RegularAttendance) para cada dia útil do mês/ano, para todos os funcionários, respeitando jornada efetiva de cada um.
4. Horários de início/fim variam em até ±4 minutos por dia, nunca resultando em duração menor que a jornada efetiva do funcionário.
5. Duplicidade: dias que já têm um `RegularAttendance` para aquele funcionário são pulados (não gera de novo); `Absence`/`Overtime` no mesmo dia não bloqueiam a geração do `RegularAttendance`.
6. Fechamento cria um registro em uma nova entidade/tabela de fechamento (mês, ano, `CreatedAtUtc`, `UpdatedAtUtc`), único por (mês, ano) — tentar fechar de novo o mesmo período retorna erro.
7. Todos os `EmployeeWorkLog` gerados por um fechamento ficam relacionados a esse registro de fechamento (FK).
8. Após "Prosseguir" bem-sucedido, o usuário vê alguma confirmação/resumo do que foi gerado (quantidade de worklogs criados, por funcionário).
9. Tentar fechar o mês atual ou um mês futuro é bloqueado (front e back).

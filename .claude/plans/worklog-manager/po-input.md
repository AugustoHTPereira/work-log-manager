# Entrada do PO (persistida para referência do plano de desenvolvimento)

## Objetivo
Permitir que o empresário (dono do negócio) registre e acompanhe apontamentos de horas extras e faltas dos seus funcionários, com base em uma carga horária diária padrão ou individualizada por funcionário.

## Escopo
- Cadastro/gestão de funcionários (employees).
- Configuração de quantidade de horas trabalhadas "padrão" do sistema.
- Configuração de quantidade de horas trabalhadas por dia específica por funcionário (podendo divergir do padrão).
- Registro de apontamentos (worklogs) do tipo "falta" ou "hora extra" para cada funcionário, contendo: tipo, data inicial, data final e a duração (diferença entre data inicial e final, armazenada em segundos).
- Tela inicial: listagem de todos os funcionários, com botão "+" acima da lista para adicionar novo funcionário.
- Interação de listagem de funcionários:
  - Clique sobre um registro de funcionário abre a tela de detalhe do funcionário.
  - Clique com botão direito (desktop) ou pressionar e segurar (mobile) sobre um registro de funcionário abre um modal para inserção de novo worklog para aquele funcionário.
- Modal de inserção/edição de worklog:
  - Campos: tipo (falta/hora extra), data inicial, data final, e um campo de "worklog" (duração) preenchível em formato textual livre e abreviado (ex.: "30m", "3s", "1a", "2M", "1h 30m"), que deve ser interpretado/parseado para a duração correspondente.
  - Ao abrir, os campos data inicial e data final devem vir preenchidos com a data/hora atual.
  - Os campos data inicial, data final e worklog (duração) devem estar interligados bidirecionalmente (definido pelo usuário — ver decisão 2 abaixo).
- Tela de detalhe do funcionário:
  - Exibe as informações do funcionário.
  - Exibe a listagem de worklogs registrados para aquele funcionário.
  - Permite excluir um worklog (ícone de lixeira).
  - Permite editar um worklog (ícone de lápis), reaproveitando o mesmo modal de inserção.
- Persistência dos dados em duas tabelas: `employees` (funcionários) e `employee_work_logs` (apontamentos).

## Critérios de aceite
1. Tela inicial exibe listagem de todos os funcionários cadastrados com botão "+" acima da listagem.
2. Clique no botão "+" permite adicionar novo funcionário.
3. Clique simples sobre um registro de funcionário abre a tela de detalhamento, exibindo informações do funcionário e worklogs registrados.
4. Clique com botão direito (desktop) ou pressionar e segurar (mobile) sobre um registro de funcionário abre modal para inserção de novo worklog.
5. Modal de worklog para inserção carrega data inicial e data final com data/hora atual.
6. Campo worklog aceita formato abreviado (ex.: "30m", "1h 30m", "1A", "2M", "3s") e interpreta corretamente conforme tabela de unidades definida.
7. Alterar data inicial ou final recalcula campo worklog, exibido em formato "1h 32m".
8. Alterar campo worklog recalcula data final (data inicial + duração).
9. Ao salvar, worklog contém tipo (falta ou hora extra), data inicial, data final e duração em segundos, associado ao funcionário.
10. Tela de detalhamento exibe listagem de worklogs do funcionário.
11. Ícone de lápis abre modal de edição preenchido com os dados do worklog.
12. Ícone de lixeira exclui o worklog (decidir se com confirmação).
13. Configuração de horas trabalhadas padrão do sistema, aplicável a todos os funcionários que não tiverem valor específico definido.
14. Definição de horas trabalhadas por dia específica por funcionário, prevalecendo sobre o padrão do sistema.
15. CRUD completo de funcionário: criar, visualizar, editar, excluir.

## Regras de negócio / restrições
- Worklog sempre associado a um funcionário específico.
- Worklog possui tipo obrigatório: falta ou hora extra.
- Duração do worklog derivada da diferença entre data inicial e final, armazenada em segundos.
- Campo de duração textual aceita entrada abreviada combinando unidades, interpretada automaticamente conforme tabela definida (decisão 1 abaixo).
- Configuração de horas trabalhadas "padrão" em nível de sistema, e configuração de horas trabalhadas por dia em nível de funcionário, que sobrepõe o padrão.
- Estrutura de dados mínima: tabela `employees` e tabela `employee_work_logs`.

## Decisões do usuário sobre pontos em aberto (recebidas após o report inicial)

1. **Unidades do campo worklog (parser de duração textual)**: case-sensitive, mapeamento:
   - `s` = segundo
   - `m` = minuto
   - `h` = hora
   - `d` = dia
   - `S` = semana (7 dias)
   - `M` = mês
   - `A` = ano
   O parser deve aceitar combinações como "1h 30m", "2M", "1A", etc., interpretando cada token conforme essa tabela, e converter tudo para uma duração total em segundos.

2. **Sincronização bidirecional entre data inicial, data final e campo worklog no modal**: editar as datas recalcula o campo worklog (formato "1h 32m"); editar o campo worklog recalcula a data final como (data inicial + duração parseada). Evitar loop infinito de atualização.

3. **Campos do cadastro de funcionário (employee)**: nome, cargo/função, data de admissão, e horas trabalhadas por dia (opcional — se não definido, usa a configuração padrão do sistema).

4. **CRUD completo de funcionário**: deve ser possível criar, visualizar, editar e excluir um funcionário (não apenas criar/visualizar).

## Pontos que permaneciam "inferidos" (sem pergunta específica ao usuário)
- Confirmação antes de excluir worklog/funcionário.
- Validação de data final < data inicial.
- Tela de configuração de horas padrão do sistema.

Esses pontos foram resolvidos como premissas técnicas assumidas pelo analista técnico, documentadas explicitamente no plano de desenvolvimento.

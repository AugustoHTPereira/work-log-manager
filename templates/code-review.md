# Code review: <título curto>

Task-id: <mesmo identificador do diretório .claude/plans/<task-id>/>

## Aderência ao plano aprovado
| Passo do plano | Implementado? | Observação |
|---|---|---|
| 1 | Sim/Não/Parcial | <observação> |

## Cobertura dos critérios de aceite
| Critério de aceite | Implementado | Coberto por teste | Observação |
|---|---|---|---|
| #1 | Sim/Não | Sim/Não | <observação> |

## Resultado dos testes
<saída resumida de `dotnet test` e do comando de teste do front>

## Arquitetura e convenções
| Verificação | Ok? | Observação |
|---|---|---|
| Camadas respeitadas (Api sem lógica de negócio / Application sem depender de Infrastructure) | Sim/Não | |
| SOLID / use-cases coesos | Sim/Não | |
| EF Core: snake_case + migration coerente com o plano | Sim/Não | |
| Front-end: uso de componentes shadcn/ui existentes | Sim/Não | |
| Todo o código em inglês | Sim/Não | |

## Achados

### Bloqueantes
- <descrição do problema, arquivo/linha, por que bloqueia>

### Sugestões
- <descrição da melhoria recomendada>

### Nitpicks
- <estilo/preferência, opcional>

---
## Veredito: Aprovado / Aprovado com ressalvas / Mudanças solicitadas

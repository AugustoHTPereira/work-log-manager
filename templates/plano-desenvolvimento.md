# Plano de desenvolvimento: <título curto>

Task-id: <mesmo identificador do diretório .claude/plans/<task-id>/>

## Resumo técnico da solução
<1 parágrafo>

## Back-end — mudanças por camada

### Application (use-cases, entidades, interfaces)
- <ex.: novo use-case `DeactivateEventUseCase`, entidade `Event` ganha método
  `Deactivate()`, nova interface `IEventRepository.UpdateAsync`>

### Infrastructure (EF Core, repositórios, integrações)
- <implementações de repositório afetadas>
- **Migration**: <nome em inglês da migration> — tabela/colunas afetadas:

| Tabela | Coluna | Tipo | Constraint |
|---|---|---|---|
| <tabela> | <coluna> | <tipo> | <constraint> |

<se não houver alteração de banco, escreva "Nenhuma migration necessária.">

### Api (endpoints, DTOs)
- <ex.: `PATCH /events/{id}/deactivate`, request/response DTO correspondente>

## Front-end — mudanças em `app/`
- Componentes/telas afetados: <lista>
- Componentes `shadcn/ui` a usar: <lista, ex.: `Switch`, `Badge`>
- Chamadas de API novas/alteradas: <lista>

## Passos de implementação
1. <passo granular e verificável — arquivos a criar/alterar>
2. ...

## Mapeamento critério de aceite → passos
| Critério de aceite | Passo(s) |
|---|---|
| #1 | 2, 3 |

## Estratégia de testes
| Critério de aceite | Cenário de teste |
|---|---|
| #1 | <descrição do teste xUnit/front> |

## Riscos e dependências
- <risco ou dependência>

## Fora de escopo / não será feito
- <item>

---
## Status: aguardando aprovação
Nenhuma implementação deve começar até este plano ser aprovado explicitamente pelo
responsável.

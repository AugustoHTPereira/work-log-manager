# Fluxo de agentes de desenvolvimento — .NET 10 + React (shadcn) + PostgreSQL

Conjunto de subagentes de Claude Code, específico para este repositório (`app/` React
front-end, `api/` .NET 10 back-end), que planeja e implementa features/alterações com
aprovação humana obrigatória em dois pontos: plano de desenvolvimento e (quando
necessário) resultado do code review.

## Estrutura

```
.claude/
  agents/
    po.md                  # interpreta o texto e gera o report da tarefa
    analista-tecnico.md    # define o task-id, cria .claude/plans/<task-id>/, gera o plano
    desenvolvedor.md       # implementa o plano aprovado + migrations EF Core + testes
    revisor.md             # confronta implementação x plano e gera code review
  commands/
    dev-flow.md            # /dev-flow — orquestra os 4 agentes em sequência
  docs/
    stack.md                       # convenções técnicas .NET/React/PostgreSQL (edite!)
    padroes-desenvolvimento.md     # padrões específicos deste repositório (edite!)
  .claude/templates/
    report-po.md
    plano-desenvolvimento.md
    code-review.md
```

## Instalação

1. Copie `.claude/`, `.claude/docs/` e `.claude/templates/` para a raiz deste repositório.
2. Edite `.claude/docs/stack.md` e `.claude/docs/padroes-desenvolvimento.md` com o nome real da
   solution (`<ProjectName>`), bundler/framework de teste do front, e qualquer
   convenção própria do repositório.
3. Adicione `.claude/plans/` ao `.gitignore` (são artefatos de planejamento, não
   precisam ir para o controle de versão — ajuste se preferir versioná-los).

## Uso

Fluxo completo, orquestrado automaticamente:

```
/dev-flow
Como usuário administrador, preciso poder desativar um evento sem excluí-lo.
Critério de aceite: ao desativar, o evento some da listagem pública mas continua
visível no painel admin com um badge "inativo".
```

O comando vai: rodar o `po` → mostrar o report → rodar o `analista-tecnico` (que cria o
`task-id` e o diretório, já quebrando o plano por camada — `Application`/
`Infrastructure`/`Api`/front-end) → mostrar o plano → **parar e pedir aprovação** →
rodar o `desenvolvedor` → rodar o `revisor` → mostrar o code review → (se necessário)
voltar ao `desenvolvedor` para aplicar correções, até aprovação final.

Também é possível invocar cada agente isoladamente, passando o `task-id` quando
aplicável.

## Garantias do fluxo

- Nenhum agente se conecta a um PostgreSQL real: alterações de schema viram apenas
  migrations do EF Core (`dotnet ef migrations add`), nunca aplicadas
  (`dotnet ef database update`) pelos agentes.
- Nenhum agente faz `git commit`/`git push` — commit e push são sempre manuais.
- Implementação só começa depois de aprovação explícita do plano de desenvolvimento.
- Camadas são respeitadas: `Api` (endpoints) → `Application` (use-cases, entidades,
  interfaces) ← `Infrastructure` (implementações concretas, EF Core).
- Todo código gerado é em inglês; toda comunicação com você continua em português.
- Cada critério de aceite é rastreado do report do PO → passo do plano → teste →
  verificação no code review, com histórico completo em `.claude/plans/<task-id>/`.

# Knowledge do Project Honorare

Este conjunto de documentos é o **contexto destilado** do projeto Honorare. Foi extraído de uma conversa longa de planejamento e organizado para servir como referência em conversas futuras com Claude.

## Como Claude deve usar isso

- **Sempre que perguntado sobre o projeto Honorare**, consultar estes documentos antes de responder.
- **Não inventar** decisões, regras ou estrutura que não estejam aqui ou no `CLAUDE.md` do repositório.
- **Tratar `DECISOES.md` como verdade vinculante** salvo se o usuário explicitamente decidir mudar uma delas (e nesse caso, o documento deve ser atualizado).
- **Tratar `DOMINIO.md` como conhecimento de negócio**, com a ressalva de que regras marcadas com `(verificar)` ainda precisam de confirmação do usuário.

## Os documentos

| Arquivo | Propósito | Quando consultar |
|---|---|---|
| `PROJETO.md` | Visão geral: produto, cliente, escopo do MVP, prazo | Toda conversa nova, para alinhar contexto |
| `ARQUITETURA.md` | Stack, monorepo, bounded contexts, princípios | Decisões técnicas, estrutura de código |
| `DOMINIO.md` | Glossário e regras UNIMED | Implementação de cálculo, entidades, validação |
| `DECISOES.md` | Lista de decisões com justificativa | Antes de propor algo que pareça contrariar uma decisão |
| `PROXIMOS_PASSOS.md` | Backlog ordenado das fatias | Para saber o que vem depois ou em que fase estamos |

## Como manter atualizado

Knowledge desatualizado é pior que knowledge ausente. Atualizar quando:

- **Decisão muda** → atualizar `DECISOES.md`
- **Regra de domínio descoberta** → atualizar `DOMINIO.md`
- **Arquitetura evolui** → atualizar `ARQUITETURA.md`
- **Fatia concluída** → marcar em `PROXIMOS_PASSOS.md`
- **Escopo do MVP muda** → atualizar `PROJETO.md`

Frequência mínima recomendada: revisar a cada 2 semanas, mesmo sem mudanças aparentes — algumas coisas mudam silenciosamente.

## Limites conhecidos

- Documentos refletem o estado da discussão até a data de criação. Decisões posteriores não estão aqui.
- Regras UNIMED em `DOMINIO.md` são baseadas em pesquisa pública e devem ser **validadas contra Instrução Geral do Rol Unimed** e contra os 15-20 casos reais antes de tratar como verdade absoluta.
- Estimativas de prazo em `PROXIMOS_PASSOS.md` assumem desenvolvimento focado solo com Claude Code. Não incluem férias, doença, intercorrências.

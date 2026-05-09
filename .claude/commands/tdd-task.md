# /tdd-task

Uso: `/tdd-task <spec-file> <task-id>`

Implemente exatamente uma task TDD. Sem desvios de escopo. Sem chamadas ao advisor().

## Argumentos

`$ARGUMENTS`

Primeiro arg = path do spec. Segundo = task ID.

## Passos

**1. Ler** — Leia só a seção da task solicitada (não o spec inteiro). Leia os arquivos de referência listados NA task (apenas os essenciais para entender o padrão). CLAUDE.md e MEMORY.md já estão no contexto — não releia.

**2. Red** — Escreva os testes antes do código de produção. Para backend: `dotnet build` deve compilar sem erros (crie skeleton mínimo se necessário). Para frontend: `pnpm -F admin-web test:ci` deve rodar.

**3. Green** — Código mínimo para passar os testes. Sem features extras.

**4. Commit** — Formato: `feat(scope): descrição (TASK-ID)`. Staged apenas arquivos da task. Rode os testes antes.

**5. Marcar** — Altere `[ ] pendente` → `[x] concluída` na task e no checklist final do spec.

## Regras críticas (erros frequentes neste projeto)

- `TreatWarningsAsErrors`: zero warnings no build .NET
- Namespace de teste não pode ser `Faturamento.Tests.Guia` — sombreia a classe `Guia`; use `Faturamento.Tests.Service` ou similar
- `CodigoTuss` tem `HasMaxLength(10)` — nunca exceder; use `tenantId.ToString("N")[..8]` como chave única
- Migration folder: `.editorconfig` suprimindo `IDE0005/IDE0161/CA1515/CA1861`
- Vitest: `vi.useFakeTimers()`, não `fakeAsync`
- Container de testes compartilhado: nunca assuma banco vazio; use `tenantId = Guid.NewGuid()` por teste
- `git add .` proibido — staged explícito por arquivo

## Saída

Reporte: testes criados/passando, hash do commit, próxima task.

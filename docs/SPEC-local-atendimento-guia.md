# SPEC — Local de Atendimento na Guia

**Pré-requisito:** F3.x (Guia + cálculo + recurso + importação CSV) em produção.

**Pós-condição:**

1. `Guia` tem campo texto livre `LocalAtendimento` (varchar 200, NOT NULL default `''`).
2. Importação do demonstrativo preenche o local a partir da coluna CSV `LOCAL ATENDIMENTO`.
3. Operador edita o local no formulário de guia; valor aparece no detalhe e na listagem.
4. Detalhe do recurso e PDF exibem o local **de cada guia** (Recurso não tem campo próprio).

**Ordem de execução:** LA-01 → LA-02 → LA-03 → LA-04 → LA-05. Cada task = 1 sessão; rodar build/test da task antes de concluir.

---

## Convenções (todas as tasks)

- TDD: teste falhando → mínimo p/ passar → refactor. Cobertura `Faturamento` ≥ 90%, Angular ≥ 80%.
- `.slnx` (não `.sln`). Warnings = errors. `Nullable enable`. Domínio PT, infra EN.
- Build: `dotnet build apps/backend/Honorare.slnx` · Test: `dotnet test apps/backend/Honorare.slnx`.
- EF migration roda de dentro de `apps/backend/App` (precisa `DOTNET_ROOT` + PATH p/ `dotnet-ef`).
- Campo: `varchar(200)`, guardado com `.Trim()`, default `string.Empty`.
- admin-web usa **tipos locais** (`*.types.ts`), não os gerados. Angular 20: `[selected]` não `[value]` em `<select>`; sem CurrencyPipe; `forkJoin` na edição (listas antes, valores depois).

Base backend: `apps/backend/App/Faturamento` · Base front: `apps/admin-web/src/app/admin/faturamento`.

---

## LA-01 · Backend: campo na Guia + API + migration ✅ concluída

**TDD — tests primeiro** (`tests/Faturamento.Tests`): criar guia c/ `LocalAtendimento` → persistido e presente em `GuiaDetalheDto`; atualizar guia altera o valor.

**`Guia.cs`:**

- Prop `public string LocalAtendimento { get; private set; } = string.Empty;`
- Param `localAtendimento` em `Create(...)` e `Atualizar(...)`, atribuir com `.Trim()`.
- `internal void AtualizarLocalAtendimento(string localAtendimento)` → `LocalAtendimento = localAtendimento.Trim(); AtualizadoEm = DateTimeOffset.UtcNow;` (usado em LA-02).

**`Configurations/GuiaConfiguration.cs`:** `builder.Property(g => g.LocalAtendimento).HasMaxLength(200);`

**`GuiaService.cs`:**

- `CriarGuiaCommand` + `AtualizarGuiaCommand`: add `string LocalAtendimento`.
- `GuiaDto` + `GuiaDetalheDto`: add `string LocalAtendimento`.
- `CriarAsync`/`AtualizarAsync`: repassar p/ `Guia.Create`/`guia.Atualizar`.
- Projeções `ListarAsync` e `ObterDetalheDtoInternalAsync`: select `g.LocalAtendimento` + map p/ DTO.

**`Endpoints/GuiaEndpoints.cs`:** `CriarGuiaRequest` + `AtualizarGuiaRequest` add `string? LocalAtendimento`; mapear ao command com `LocalAtendimento ?? string.Empty`.

**Migration:** `cd apps/backend/App && dotnet ef migrations add AddLocalAtendimentoGuia --output-dir Migrations --namespace App.Migrations`. Conferir: só add coluna em `guias`, NOT NULL default `''`.

**Pronto quando:** build + `dotnet test` verdes; migration gerada.

---

## LA-02 · Backend: importação CSV `LOCAL ATENDIMENTO` ✅ concluída

Arquivo `ImportacaoGuiaCsvService.cs` — lê colunas por header via `Col(cols, idx, "NOME")` (case-insensitive, `""` se ausente → retrocompatível). Agrupa linhas por `(Guia, DataServico)`.

**TDD — tests primeiro** (`Demonstrativo/ImportacaoGuiaCsvTests.cs`):

- Add `;LOCAL ATENDIMENTO` ao fim da const `CsvHeader`.
- Add param **opcional** `string localAtendimento = ""` ao fim do builder `CsvRow(...)` (mantém call sites atuais).
- Teste: linha com local → `guia.LocalAtendimento` correto.
- Teste backfill: guia existente vazia recebe valor; guia já preenchida **não** é sobrescrita.

**Implementação:**

- Record `LinhaCSV`: add `string LocalAtendimento`.
- `ParsearCsv`: popular com `Col(cols, idx, "LOCAL ATENDIMENTO")`.
- Criação da guia (`Guia.Create(...)`): passar `LocalAtendimento` da 1ª linha do grupo.
- Backfill: se guia já existe **e** `guia.LocalAtendimento` vazio **e** CSV traz valor → `guia.AtualizarLocalAtendimento(valor)`. Nunca sobrescrever valor preenchido.

**Pronto quando:** build + `dotnet test` verdes.

---

## LA-03 · Backend: local por-guia no Recurso (detalhe + PDF) ✅ concluída

Não tocar `Recurso.cs`, `RecursoConfiguration.cs`, commands/endpoints do recurso.

**TDD — tests primeiro:** `ObterPorIdAsync` e `ObterDadosPdfAsync` retornam `LocalAtendimento` nas guias.

**`RecursoService.cs`:**

- `GuiaNoRecursoDto`: add `string LocalAtendimento`; select `g.LocalAtendimento` em `ObterPorIdAsync` (query + map).
- `GuiaPdfData`: add `string LocalAtendimento`; select em `ObterDadosPdfAsync` (query + map).

**`Pdf/RecursoPdfDocument.cs` (`ComposeGuia`):** na linha de cabeçalho da guia (`Data | Guia: X | Beneficiário | Posição`), acrescentar `| {LocalAtendimento}` **apenas quando não vazio**.

**Pronto quando:** build + `dotnet test` verdes.

---

## LA-04 · Frontend: guia (tipos + form + listagem) ✅ concluída

**`guia.types.ts`:** add `localAtendimento: string` em `GuiaItem`, `CriarGuiaPayload`, `AtualizarGuiaPayload` (`GuiaDetalheItem` herda).

**`guia-form/guia-form.component.ts`:**

- `readonly localAtendimento = signal('')`.
- Input texto no template (padrão do `numeroGuia`): `[value]="localAtendimento()" (input)="localAtendimento.set($any($event.target).value)"`. SCSS conforme `STYLES.md` (`.input`, `space()`, mixins; sem hex/named colors).
- No `forkJoin` de edição: `this.localAtendimento.set(guia.localAtendimento ?? '')`.
- Add `localAtendimento: this.localAtendimento()` nos payloads criar + atualizar.

**`guia-list/guia-list.component.ts`:** add coluna "Local" (header + célula `{{ g.localAtendimento }}`) seguindo colunas existentes. Só exibição (sem ordenação).

**Client gerado:** `pnpm generate-api-client` (backend precisa compilar/rodar).

**TDD (Vitest):** payload do form inclui `localAtendimento`; edição popula o signal; lista renderiza a coluna.

**Pronto quando:** `pnpm -F admin-web lint && pnpm -F admin-web stylelint && pnpm -F admin-web test:ci` verdes.

---

## LA-05 · Frontend: local nas guias do recurso ✅ concluída

**`recurso.types.ts`:** add `localAtendimento: string` em `GuiaNoRecursoDto`.

**`recurso-guias/recurso-guias.component.ts`:** exibir `g.localAtendimento` no card de cada guia (junto de data/beneficiário), quando preenchido.

**TDD (Vitest):** card renderiza o local quando presente.

**Pronto quando:** `pnpm -F admin-web lint && pnpm -F admin-web stylelint && pnpm -F admin-web test:ci` verdes.

---

## Smoke final (após LA-01–LA-05)

`pnpm dev:up` + `pnpm -F admin-web dev`: importar CSV com `LOCAL ATENDIMENTO` → conferir na guia; criar/editar guia → detalhe + listagem; adicionar guia a recurso → detalhe + PDF (segmento "Local"). `dotnet ef database update` aplica a coluna.

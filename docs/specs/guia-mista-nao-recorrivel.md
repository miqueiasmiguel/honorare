# SPEC: Guia mista com procedimentos não recorríveis

> **Como executar:** uma task por sessão. Rode `/tdd-task docs/specs/guia-mista-nao-recorrivel.md <TASK-ID>`,
> deixe a sessão implementar+commitar, limpe o contexto (`/clear`), rode a próxima.
> CLAUDE.md e MEMORY.md já entram no contexto de cada sessão — as tasks NÃO os repetem.
> **O checkbox `[ ]`/`[x]` é a única memória entre sessões:** uma sessão fria se orienta
> só por ele. Comece pela primeira task ainda `[ ]`; nunca refaça uma já marcada `[x]`.

## Objetivo

Refinar a lógica de guias não recorríveis introduzida em `procedimentos-nao-recorriveis.md`.
Atualmente, qualquer guia com **algum** item não recorrível é bloqueada no lote. A nova
regra distingue dois casos:

- **Guia totalmente não recorrível:** todos os itens possuem código TUSS na lista → comportamento
  atual mantido (bloqueada no lote, operador ainda pode adicionar individualmente).
- **Guia mista:** tem ao menos um item não recorrível E ao menos um recorrível → recebe
  identificação visual distinta; é incluída pelo "Adicionar todas", mas os itens não
  recorríveis são **automaticamente excluídos do recurso** (`IncluidoNoRecurso = false`)
  ao serem importados, usando a infraestrutura já existente de `excluir-item-recurso.md`.

## Contexto compartilhado (válido para todas as tasks)

- **Dependências:** `TASK-NREC-01..06` (concluídas) e `TASK-EIR-01..03` (concluídas).
- `NaoRecorrivel` no `GuiaDto` muda de significado: passa a ser `true` apenas quando
  **todos** os itens da guia são não recorríveis (antes era "algum item").
- Novo campo `MistaComNaoRecorriveis` no `GuiaDto`: `true` quando a guia tem ao menos um
  item NR e ao menos um recorrível.
- Uma guia nunca terá `NaoRecorrivel = true` e `MistaComNaoRecorriveis = true` simultaneamente.
- No lote, ao adicionar uma guia mista, `ExcluirDoRecurso()` é chamado diretamente nos itens
  NR (sem passar por `AlterarInclusaoItemAsync`) — o invariante de "ao menos um item incluído"
  já está garantido pela própria definição de guia mista.
- Backend: warnings = errors; sem CQRS/MediatR/Repository/AutoMapper; `AppDbContext` único.
- O front usa serviços `HttpClient` escritos à mão — tipos TS mantidos à mão.

---

## Tasks

### TASK-GMIX-01 — Backend: diferenciar guia totalmente NR de guia mista em `ListarAsync`

- [x] concluída

**Objetivo:** `GuiaService.ListarAsync` passa a computar dois flags distintos:
`NaoRecorrivel = true` somente quando **todos** os itens da guia são NR;
`MistaComNaoRecorriveis = true` quando **alguns** (não todos) são NR.

**Depende de:** TASK-NREC-01 (campo `tenant.CodigosNaoRecorriveis`), TASK-NREC-03
(estrutura atual do bloco de computação em `ListarAsync`).

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Faturamento/GuiaService.cs:46-53` — record `GuiaDto` atual
- `apps/backend/App/Faturamento/GuiaService.cs:365-398` — bloco `counts` + `naoRecorriveis` + projeção
- Arquivo de teste existente de `GuiaService.ListarAsync` (rode
  `grep -rln "ListarAsync_DeveMarcarNaoRecorrivel" apps/backend/tests` e abra)

**Criar/Editar:**

- `apps/backend/App/Faturamento/GuiaService.cs` (editar: record + bloco de cálculo + projeção)
- Arquivo de teste de `GuiaService.ListarAsync` (editar: novos testes + ajuste dos existentes)

**Mudança no record** (adicionar `MistaComNaoRecorriveis` após `NaoRecorrivel`):

```csharp
internal sealed record GuiaDto(
    Guid Id, Guid PrestadorId, string PrestadorNome,
    Guid OperadoraId, string OperadoraNome, Guid? BeneficiarioId,
    string? BeneficiarioNome, string? BeneficiarioCarteira, string NumeroGuia,
    DateOnly DataAtendimento, SituacaoGuia Situacao, bool EhPacote,
    string Observacao, string LocalAtendimento, int TotalItens,
    DateTimeOffset CriadoEm, DateTimeOffset AtualizadoEm,
    bool NaoRecorrivel, bool MistaComNaoRecorriveis);
```

> `grep -rn "new GuiaDto(" apps/backend --include=*.cs` para encontrar toda construção do
> record e atualizar o novo argumento posicional.

**Novo bloco de cálculo** — substituir o bloco `naoRecorriveis` atual (linhas ~380-386)
pelo seguinte (manter a leitura do tenant e da lista `codigos` exatamente como estão):

```csharp
// conta itens NR por guia (group by)
var countNrPorGuia = codigos.Count == 0
    ? new Dictionary<Guid, int>()
    : await (from i in _db.ItensGuia
             join p in _db.Procedimentos on i.ProcedimentoId equals p.Id
             where ids.Contains(i.GuiaId) && codigos.Contains(p.CodigoTuss)
             group i by i.GuiaId into g
             select new { GuiaId = g.Key, Qtd = g.Count() })
            .ToDictionaryAsync(x => x.GuiaId, x => x.Qtd, ct);

// NaoRecorrivel: countNr == totalItens (todos os itens são NR)
var totalmenteNaoRecorrivelIds = countNrPorGuia.Keys
    .Where(id => countNrPorGuia[id] == counts.GetValueOrDefault(id, 0))
    .ToHashSet();

// MistaComNaoRecorriveis: tem algum NR, mas não todos
var mistaIds = countNrPorGuia.Keys
    .Except(totalmenteNaoRecorrivelIds)
    .ToHashSet();
```

**Projeção** — substituir os dois últimos argumentos de `new GuiaDto(...)`:

```csharp
totalmenteNaoRecorrivelIds.Contains(x.Id),
mistaIds.Contains(x.Id)));
```

**Testes (red primeiro) — editar o arquivo de testes existente de `ListarAsync`:**

- `ListarAsync_DeveMarcarNaoRecorrivel_QuandoTodosOsItensEstaoNaLista`
  (guia com 1 item NR → `NaoRecorrivel=true`, `MistaComNaoRecorriveis=false`)
- `ListarAsync_DeveMarcarMista_QuandoApenasAlgumItemEstaNaLista`
  (guia com 2 itens: 1 NR e 1 recorrível → `NaoRecorrivel=false`, `MistaComNaoRecorriveis=true`)
- `ListarAsync_NaoDeveMarcarNada_QuandoNenhumItemEstaNaLista`
  (guia com itens todos recorríveis → ambos `false`)
- `ListarAsync_NaoDeveExcluirGuiaMista_MesmoSendoMista`
  (guia mista continua no resultado da listagem)

> Atualize os testes existentes que verificavam `NaoRecorrivel=true` para guias com "algum"
> item NR — agora esses casos devem resultar em `MistaComNaoRecorriveis=true` com
> `NaoRecorrivel=false`, a menos que todos os itens sejam NR.

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde (cobertura Faturamento ≥ 90%)
- [ ] guia com todos os itens NR → `naoRecorrivel=true, mistaComNaoRecorriveis=false` no JSON
- [ ] guia com apenas alguns itens NR → `naoRecorrivel=false, mistaComNaoRecorriveis=true` no JSON

**Commit:** `feat(faturamento): distingue guia totalmente NR de guia mista (TASK-GMIX-01)`

---

### TASK-GMIX-02 — Backend: lote inclui guias mistas excluindo itens NR automaticamente

- [x] concluída

**Objetivo:** `RecursoService.AdicionarGuiasEmLoteAsync` passa a bloquear apenas guias
**totalmente** não recorríveis. Guias mistas são incluídas no lote, mas seus itens NR
recebem `IncluidoNoRecurso = false` automaticamente logo após a adição.

**Depende de:** TASK-GMIX-01 (semântica do filtro), TASK-EIR-01 (`ItemGuia.ExcluirDoRecurso()`).

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Faturamento/RecursoService.cs:449-470` — filtro atual de `codigos` e
  trecho `var guias = await q.ToListAsync(ct)` dentro de `AdicionarGuiasEmLoteAsync`
- Arquivo de teste do lote (rode
  `grep -rln "AdicionarGuiasEmLote" apps/backend/tests` e abra)

**Criar/Editar:**

- `apps/backend/App/Faturamento/RecursoService.cs` (editar: filtro + exclusão automática de itens NR)
- Arquivo de teste do lote (editar: novos cenários)

**Mudança no filtro** — substituir o bloco `if (codigos.Count > 0) { q = q.Where(...) }` atual:

```csharp
if (codigos.Count > 0)
{
    // Bloqueia apenas guias onde TODOS os itens são NR
    // (guias mistas passam — têm ao menos um item recorrível)
    q = q.Where(g => _db.ItensGuia.Any(i =>
        i.GuiaId == g.Id &&
        !_db.Procedimentos.Any(p => p.Id == i.ProcedimentoId && codigos.Contains(p.CodigoTuss))));
}
```

**Exclusão automática de itens NR em guias mistas** — logo após `var guias = await q.ToListAsync(ct)`,
antes do `foreach` que chama `guia.AdicionarAoRecurso(recurso)`:

```csharp
// Exclui automaticamente os itens NR das guias mistas que entrarão no recurso
if (codigos.Count > 0 && guias.Count > 0)
{
    var guiaIds = guias.Select(g => g.Id).ToList();
    var itensNaoRecorriveis = await (
        from i in _db.ItensGuia
        join p in _db.Procedimentos on i.ProcedimentoId equals p.Id
        where guiaIds.Contains(i.GuiaId) && codigos.Contains(p.CodigoTuss)
        select i
    ).ToListAsync(ct);
    foreach (var item in itensNaoRecorriveis)
    {
        item.ExcluirDoRecurso();
    }
}
```

> O invariante "ao menos um item incluído por guia" está garantido: só chegam aqui guias
> mistas (com ao menos um item recorrível). Não há risco de guia sem itens no recurso.

**Testes (red primeiro) — editar o arquivo de teste do lote:**

- `AdicionarGuiasEmLoteAsync_DeveIncluirGuiaMista_ExcluindoItensNaoRecorriveis`
  (guia com 2 itens: 1 NR e 1 recorrível → guia adicionada ao recurso; item NR tem
  `IncluidoNoRecurso=false`; item recorrível tem `IncluidoNoRecurso=true`)
- `AdicionarGuiasEmLoteAsync_DevePularGuiaTotalmenteNaoRecorrivel`
  (guia com 1 item NR → não adicionada; count retornado = 0)
- `AdicionarGuiasEmLoteAsync_DeveVincularTodas_QuandoListaVazia`
  (sem códigos configurados → comportamento original, sem exclusão de itens)
- `AdicionarGuiasEmLoteAsync_DevePularTotalmenteNR_EIncluirMista_NoMesmoLote`
  (lote com 3 guias: 1 totalmente NR, 1 mista, 1 normal → count=2; guia totalmente NR
  não adicionada; guia mista adicionada com item NR excluído; guia normal intacta)
- `AdicionarGuiaAsync_DeveVincularGuiaMista_SemExcluirItens`
  (escape hatch individual: guia mista adicionada com todos os itens incluídos)

> Para criar guias com múltiplos itens nos testes, adicione itens extras diretamente no
> contexto após `CriarGuiaAsync`, conforme o padrão de `RecursoItemInclusaoTests.cs`.

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde (cobertura Faturamento ≥ 90%)
- [ ] guia mista adicionada pelo lote: item NR com `incluidoNoRecurso=false` via `GET /api/v1/admin/recursos/{id}`
- [ ] guia totalmente NR não aparece no recurso após lote
- [ ] `AdicionarGuiaAsync` individual não exclui nenhum item

**Commit:** `feat(faturamento): lote inclui guias mistas e exclui itens NR automaticamente (TASK-GMIX-02)`

---

### TASK-GMIX-03 — Frontend: badge de guia mista na seleção do recurso

- [x] concluída

**Objetivo:** na tabela de candidatas do `recurso-guias`, exibir um badge distinto para
guias mistas (`mistaComNaoRecorriveis=true`), diferente do badge "Não recorrível" já
existente. O badge "Não recorrível" passa a aparecer **apenas** para guias totalmente NR.

**Depende de:** TASK-GMIX-01 (o backend já devolve `mistaComNaoRecorriveis`).

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md (atenção: sem hex/cor nomeada,
`space()` com steps válidos — **ler `apps/admin-web/STYLES.md` antes do SCSS**).

**Ler (só isto):**

- `apps/admin-web/src/app/admin/faturamento/guia.types.ts:38-57` — interface `GuiaItem`
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.ts:360-368`
  — `<td>` onde já está o badge "Não recorrível"
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.scss:356-367`
  — estilo do badge atual (padrão a imitar)
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.spec.ts`
  — setup do spy e helpers existentes

**Criar/Editar:**

- `apps/admin-web/src/app/admin/faturamento/guia.types.ts` (editar: novo campo)
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.ts`
  (editar: badge mista no template)
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.scss`
  (editar: estilo do badge mista)
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.spec.ts`
  (editar: novos testes)

**Tipo** — adicionar ao final da interface `GuiaItem` (após `naoRecorrivel?`):

```typescript
mistaComNaoRecorriveis?: boolean;
```

**Template** — substituir o bloco `@if (candidata.naoRecorrivel)` existente na `<td>`
do número da guia:

```html
@if (candidata.naoRecorrivel) {
<span class="recurso-guias__badge-nao-recorrivel">Não recorrível</span>
} @else if (candidata.mistaComNaoRecorriveis) {
<span class="recurso-guias__badge-mista">Contém não recorrível</span>
}
```

**SCSS** — adicionar logo após o bloco `&__badge-nao-recorrivel` existente:

```scss
// ── Guia mista badge ─────────────────────────────────────────────────────
&__badge-mista {
  @include text-label;

  display: inline-block;
  margin-left: space(2);
  padding: space(1) space(2);
  border-radius: space(1);
  color: var(--color-tinta);
  background-color: var(--color-areia);
}
```

> Use tokens de cor disponíveis em `STYLES.md`. Os tokens `--color-tinta` e `--color-areia`
> são da paleta base — se não existirem, verifique `STYLES.md` e escolha tokens neutros
> válidos. Nunca use hex, cor nomeada ou `font-size` cru.

**Testes (red primeiro) — editar `recurso-guias.component.spec.ts`:**

- Atualize os fixtures de `GuiaItem` que constroem candidatas para incluir
  `mistaComNaoRecorriveis: false` (campo opcional, mas inclua para consistência).
- `deve exibir badge "Contém não recorrível" quando candidata.mistaComNaoRecorriveis é true`
- `não deve exibir badge mista quando mistaComNaoRecorriveis é false`
- `deve exibir badge "Não recorrível" (e não badge mista) quando naoRecorrivel é true`
  (garante que os dois badges são mutuamente exclusivos no template)

**Aceite:**

- [ ] `pnpm -F admin-web lint` e `pnpm -F admin-web stylelint` sem warnings
- [ ] `pnpm -F admin-web test:ci` verde (cobertura ≥ 80%)
- [ ] `pnpm -F admin-web build` ok

**Commit:** `feat(admin-web): badge de guia mista na seleção do recurso (TASK-GMIX-03)`

---

## Checklist final

- [x] TASK-GMIX-01 — backend: flags `NaoRecorrivel` (todos) e `MistaComNaoRecorriveis` (alguns)
- [x] TASK-GMIX-02 — backend: lote inclui guias mistas, exclui itens NR automaticamente
- [x] TASK-GMIX-03 — frontend: badge de guia mista

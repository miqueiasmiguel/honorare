# SPEC: Excluir / reincluir itens de uma guia dentro de um recurso

> **Como executar:** uma task por sessão. Rode `/tdd-task docs/specs/excluir-item-recurso.md <TASK-ID>`,
> deixe a sessão implementar+commitar, limpe o contexto (`/clear`), rode a próxima.
> CLAUDE.md e MEMORY.md já entram no contexto de cada sessão — as tasks NÃO os repetem.
> **O checkbox `[ ]`/`[x]` é a única memória entre sessões:** uma sessão fria se orienta
> só por ele. Comece pela primeira task ainda `[ ]`; nunca refaça uma já marcada `[x]`.

## Objetivo

Permitir, na tela de detalhe do recurso (`recurso-guias`), **excluir um item de uma guia
do recurso de forma não-destrutiva** — o item some do PDF do recurso, mas **permanece na guia**
em todo o resto do sistema (faturamento, cálculo, portal). A operação é **reversível** (reincluir).
Implementada via uma flag booleana `IncluidoNoRecurso` no `ItemGuia` (default `true`).

"Pronto" geral: o operador exclui um item (com `confirm()`), ele aparece riscado no card e some
do PDF; consegue reincluí-lo; não consegue esvaziar uma guia (último item incluído é bloqueado);
ao remover a guia do recurso, todos os itens voltam a `IncluidoNoRecurso = true`.

## Contexto compartilhado (válido para todas as tasks)

- Bounded context: **Faturamento**. Cobertura mínima exigida: **90%**.
- `Guia → Recurso` é 1:1 via `guia.RecursoId` (nullable). Não existe tabela de junção recurso↔item.
- DbSet dos itens: `_db.ItensGuia` (`DbSet<ItemGuia>`).
- A semântica de erro do projeto: `InvalidOperationException` → HTTP **409** (via global exception handler).
  Os métodos-toggle de recurso já existentes (`AdicionarGuiaAsync`, `RemoverGuiaAsync`) usam esse
  padrão — lançam `InvalidOperationException` tanto para "não encontrado" quanto para regra violada.
  **Siga o mesmo padrão** (não use `Result<T>` aqui).
- Rota nova (decidida): `PATCH /api/v1/admin/recursos/{id}/guias/{guiaId}/itens/{itemId}/inclusao`
  com corpo `{ "incluido": bool }`. `incluido=false` exclui; `incluido=true` reinclui.
- As interfaces TS de recurso (`recurso.types.ts`) são **escritas à mão** neste app — NÃO há
  geração via OpenAPI para elas. Edite o tipo manualmente.

## Tasks

### TASK-EIR-01 — Flag `IncluidoNoRecurso` no `ItemGuia` (entidade + config + migration)

- [x] concluída

**Objetivo:** adicionar a propriedade `IncluidoNoRecurso` (bool, default `true`) ao `ItemGuia`,
com métodos de toggle, mapeamento EF e migration.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):** nada além dos trechos colados abaixo.

**Criar/Editar:**

- `apps/backend/App/Faturamento/ItemGuia.cs` (editar: nova propriedade + 2 métodos + setar no `Create`)
- `apps/backend/App/Faturamento/Configurations/ItemGuiaConfiguration.cs` (editar: mapear coluna)
- Nova migration em `apps/backend/App/Migrations/` (gerada pelo EF)
- `apps/backend/tests/Faturamento.Tests/Domain/ItemGuiaTests.cs` (novo — testes unitários)

**Estado atual de `ItemGuia.cs` (trechos a modificar):**

```csharp
// propriedades (após linha 18 `public string? MotivoGlosa ...`):
public DateTimeOffset CriadoEm { get; private set; }
// ↑ ADICIONAR logo após esta linha:
//   public bool IncluidoNoRecurso { get; private set; }

// métodos (junto aos outros internal void Set...):
internal void SetTempoAnestesicoMin(int? valor) => TempoAnestesicoMin = valor;
// ↑ ADICIONAR após:
//   internal void ExcluirDoRecurso() => IncluidoNoRecurso = false;
//   internal void ReincluirNoRecurso() => IncluidoNoRecurso = true;

// no factory Create(), dentro do object initializer (junto a ValorLiquidado = null):
ValorLiquidado = null,
CriadoEm = DateTimeOffset.UtcNow,
// ↑ ADICIONAR:
//   IncluidoNoRecurso = true,
```

**Mapeamento EF — adicionar em `ItemGuiaConfiguration.Configure` (após `builder.Property(i => i.CriadoEm).IsRequired();`):**

```csharp
builder.Property(i => i.IncluidoNoRecurso).IsRequired().HasDefaultValue(true);
```

**Migration (do diretório do projeto):**

```bash
cd apps/backend/App
dotnet ef migrations add AddIncluidoNoRecursoItemGuia --output-dir Migrations --namespace App.Migrations
```

- O `.editorconfig` que suprime IDE0005/IDE0161/CA1515/CA1861 **já existe** em
  `apps/backend/App/Migrations/.editorconfig` — não precisa criar.
- A migration deve adicionar a coluna `bool not null default true` em `itens_guia`.
  Como há `HasDefaultValue(true)`, linhas existentes recebem `true` automaticamente.

**Testes (red primeiro) — `ItemGuiaTests.cs` (unit puro, sem fixture):**

```csharp
namespace Faturamento.Tests.Domain; // namespace próprio — não sombrear App.Faturamento

using App.Catalog;
using App.Faturamento;

public sealed class ItemGuiaTests
{
    private static ItemGuia Novo() => ItemGuia.Create(
        Guid.NewGuid(), Guid.NewGuid(), PosicaoExecutor.Cirurgiao, 1.0m,
        ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null);
    // ... [Fact]s abaixo
}
```

- `Deve nascer com IncluidoNoRecurso true` (após `Create`).
- `ExcluirDoRecurso deve marcar IncluidoNoRecurso false`.
- `ReincluirNoRecurso deve marcar IncluidoNoRecurso true` (após excluir).

**Aceite (checklist objetivo):**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] migration `AddIncluidoNoRecursoItemGuia` criada (`.cs` + `.Designer.cs`) e `AppDbContextModelSnapshot.cs` atualizado
- [ ] `dotnet test apps/backend/Honorare.slnx` verde (incl. novos testes de `ItemGuiaTests`)

**Commit:** `feat(faturamento): flag IncluidoNoRecurso no item de guia (TASK-EIR-01)`

---

### TASK-EIR-02 — Lógica de exclusão/reinclusão no `RecursoService` + endpoint + PDF + reset

- [x] concluída

**Objetivo:** método `AlterarInclusaoItemAsync`, expor a flag no detalhe, filtrar itens excluídos
do PDF, resetar a flag ao remover a guia, e o endpoint PATCH.

**Depende de:** TASK-EIR-01. Contratos já existentes (não precisa abrir `ItemGuia.cs`):

```csharp
// ItemGuia (Faturamento) — já tem:
public bool IncluidoNoRecurso { get; private set; }
internal void ExcluirDoRecurso();      // => false
internal void ReincluirNoRecurso();    // => true
```

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Faturamento/RecursoService.cs:21-31` — record `ItemGuiaNoRecursoDto`
- `apps/backend/App/Faturamento/RecursoService.cs:226-252` — projeção de itens em `ObterPorIdAsync`
- `apps/backend/App/Faturamento/RecursoService.cs:347-360` — `RemoverGuiaAsync` (onde resetar)
- `apps/backend/App/Faturamento/RecursoService.cs:470-484` — query de itens em `ObterDadosPdfAsync`
- `apps/backend/App/Faturamento/Endpoints/RecursoEndpoints.cs:7-20,116-121` — MapGroup + handler de remover guia
- `apps/backend/tests/Faturamento.Tests/Recurso/RecursoCrudTests.cs:13-68,253-360` — fixture, helpers e padrão dos testes de recurso

**Criar/Editar:**

- `apps/backend/App/Faturamento/RecursoService.cs` (editar: DTO + método novo + 3 ajustes)
- `apps/backend/App/Faturamento/Endpoints/RecursoEndpoints.cs` (editar: rota + handler + request record)
- `apps/backend/tests/Faturamento.Tests/Recurso/RecursoItemInclusaoTests.cs` (novo)

**(a) DTO — adicionar campo final em `ItemGuiaNoRecursoDto` (linha 21-31):**

```csharp
internal sealed record ItemGuiaNoRecursoDto(
    Guid Id, string CodigoTuss, string DescricaoProcedimento,
    PosicaoExecutor PosicaoExecutor, decimal PercentualOrdem,
    ViaAcesso ViaAcesso, Acomodacao Acomodacao, bool EhUrgencia,
    decimal? ValorApurado, decimal? ValorLiquidado,
    bool IncluidoNoRecurso); // ← NOVO
```

Na projeção (`select new { ... i.ValorLiquidado, }` ~linha 226-243) adicionar `i.IncluidoNoRecurso,`
e na construção do DTO (~linha 247-251) passar `i.IncluidoNoRecurso` como último argumento.

**(b) Filtro no PDF — em `ObterDadosPdfAsync`, query `itensRaw` (~linha 470):**

```csharp
var itensRaw = await (
    from i in _db.ItensGuia
    where guiaIds.Contains(i.GuiaId) && i.IncluidoNoRecurso   // ← adicionar a 2ª condição
    join p in _db.Procedimentos on i.ProcedimentoId equals p.Id
    ...
```

**(c) Reset em `RemoverGuiaAsync` — antes do `SaveChangesAsync` (~linha 358):**

```csharp
var itensDaGuia = await _db.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync(ct);
foreach (var item in itensDaGuia)
{
    item.ReincluirNoRecurso();
}
guia.RemoverDoRecurso(todosLiquidados);
await _db.SaveChangesAsync(ct);
```

**(d) Método novo no `RecursoService` (padrão de `RemoverGuiaAsync` — lança `InvalidOperationException`):**

```csharp
internal async Task AlterarInclusaoItemAsync(
    Guid recursoId, Guid guiaId, Guid itemId, bool incluido, CancellationToken ct = default)
{
    var guia = await _db.Guias
        .FirstOrDefaultAsync(g => g.Id == guiaId && g.RecursoId == recursoId, ct)
        ?? throw new InvalidOperationException("Guia não encontrada neste recurso.");

    var item = await _db.ItensGuia
        .FirstOrDefaultAsync(i => i.Id == itemId && i.GuiaId == guiaId, ct)
        ?? throw new InvalidOperationException("Item não encontrado nesta guia.");

    if (!incluido)
    {
        var incluidos = await _db.ItensGuia.CountAsync(i => i.GuiaId == guiaId && i.IncluidoNoRecurso, ct);
        if (incluidos <= 1)
        {
            throw new InvalidOperationException("A guia ficaria sem itens no recurso.");
        }

        item.ExcluirDoRecurso();
    }
    else
    {
        item.ReincluirNoRecurso();
    }

    await _db.SaveChangesAsync(ct);
}
```

**(e) Endpoint — em `RecursoEndpoints.MapRecursoEndpoints` (após a linha do `RemoverGuiaAsync`, ~linha 18):**

```csharp
g.MapPatch("{id:guid}/guias/{guiaId:guid}/itens/{itemId:guid}/inclusao", AlterarInclusaoItemAsync);
```

Handler (espelha `RemoverGuiaAsync` ~linha 116-121) + request record (junto aos outros no fim do arquivo):

```csharp
private static async Task<IResult> AlterarInclusaoItemAsync(
    Guid id, Guid guiaId, Guid itemId, AlterarInclusaoItemRequest body,
    RecursoService service, CancellationToken ct)
{
    await service.AlterarInclusaoItemAsync(id, guiaId, itemId, body.Incluido, ct);
    return Results.NoContent();
}

internal sealed record AlterarInclusaoItemRequest(bool Incluido);
```

**Testes (red primeiro) — `RecursoItemInclusaoTests.cs`:**
Copie a infra (`BuildTenant`, `SeedCatalogAsync`, `CriarGuiaAsync`, fixture `[Collection(nameof(PostgresCollection))]`,
`FakeRecursoTenantUser`, `RecursoCrudNoopFileStorage`) do padrão em `RecursoCrudTests.cs:13-68`.
`CriarGuiaAsync` cria guia com **1 item**. Para uma guia com 2 itens, adicione um 2º direto no contexto:

```csharp
ctx.Add(ItemGuia.Create(
    guiaId, procedimentoId, PosicaoExecutor.PrimeiroAuxiliar, 0.5m,
    ViaAcesso.Convencional, Acomodacao.Enfermaria, false, null));
await ctx.SaveChangesAsync();
```

Cenários:

- `Excluir item de guia com 2 itens marca IncluidoNoRecurso false` (recarrega item do banco e confere).
- `Reincluir item volta IncluidoNoRecurso true`.
- `Excluir o último item incluído lança InvalidOperationException` (guia com 1 item → excluir lança).
- `ObterPorId expõe IncluidoNoRecurso no DTO` (exclui 1 de 2, confere a flag no `ItemGuiaNoRecursoDto`).
- `ObterDadosPdf omite item excluído` (guia 2 itens, exclui 1 → `GuiaPdfData.Itens` tem só 1).
- `RemoverGuia reseta IncluidoNoRecurso para true` (exclui item, remove guia do recurso, confere flag true).

> Para o teste de PDF, veja `RecursoPdfDataTests.cs:1-40` para o seed de tenant/logo (use `NoopFileStorage`).

**Aceite (checklist objetivo):**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde (novos testes incluídos)
- [ ] item excluído não aparece em `ObterDadosPdfAsync`; aparece (com flag) em `ObterPorIdAsync`
- [ ] excluir último item incluído resulta em 409 (`InvalidOperationException`)
- [ ] remover a guia do recurso reseta a flag de todos os itens

**Commit:** `feat(faturamento): exclui/reinclui item de guia no recurso (TASK-EIR-02)`

---

### TASK-EIR-03 — Frontend: botões Excluir/Reincluir no card, item riscado, serviço e tipos

- [ ] pendente

**Objetivo:** na tabela de itens do card (`recurso-guias`), botão "Excluir" (com `confirm()`) por
item incluído e "Reincluir" por item excluído; item excluído renderizado riscado/esmaecido.

**Depende de:** TASK-EIR-02. Contrato do endpoint (não precisa abrir backend):
`PATCH /api/v1/admin/recursos/{recursoId}/guias/{guiaId}/itens/{itemId}/inclusao` corpo `{ "incluido": bool }` → 204.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md (inclui regras de Angular 20, `[selected]`, `Intl`, escala `space()`).

**Ler (só isto):**

- `apps/admin-web/src/app/admin/faturamento/recurso.types.ts:25-36` — interface `ItemGuiaNoRecursoDto`
- `apps/admin-web/src/app/admin/faturamento/recurso.service.ts:64-68` — método `removerGuia` (padrão de PATCH/DELETE + `map(() => undefined)`)
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.ts:138-197` — tabela de itens (`guia-card__itens-table`) e `removerGuia`/`erroValidacao`
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.spec.ts:1-138` — setup de mocks, `makeItemGuiaNoRecurso`, `makeGuiaNoRecurso`, padrão dos testes

**Criar/Editar:**

- `apps/admin-web/src/app/admin/faturamento/recurso.types.ts` (editar: + campo na interface)
- `apps/admin-web/src/app/admin/faturamento/recurso.service.ts` (editar: + método)
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.ts` (editar: coluna ação + métodos)
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.scss` (editar: estilo riscado)
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.spec.ts` (editar: novos testes + mock)

**(a) Tipo — em `ItemGuiaNoRecursoDto` (recurso.types.ts:25-36), adicionar:**

```typescript
valorLiquidado: number | null;
incluidoNoRecurso: boolean; // ← NOVO
```

**(b) Serviço — em `recurso.service.ts` (junto a `removerGuia`):**

```typescript
alterarInclusaoItem(
  recursoId: string,
  guiaId: string,
  itemId: string,
  incluido: boolean,
): Observable<void> {
  return this._http
    .patch(
      `/api/v1/admin/recursos/${recursoId}/guias/${guiaId}/itens/${itemId}/inclusao`,
      { incluido },
    )
    .pipe(map(() => undefined));
}
```

**(c) Componente — métodos novos (espelham `removerGuia` em recurso-guias.component.ts:685-699):**

```typescript
excluirItem(guiaId: string, item: ItemGuiaNoRecursoDto): void {
  const ok = confirm(
    `Excluir o item «${item.descricaoProcedimento}» deste recurso? ` +
      'Ele não aparecerá no PDF, mas permanece na guia.',
  );
  if (!ok) {
    return;
  }
  this._alterarInclusaoItem(guiaId, item.id, false);
}

reincluirItem(guiaId: string, item: ItemGuiaNoRecursoDto): void {
  this._alterarInclusaoItem(guiaId, item.id, true);
}

private _alterarInclusaoItem(guiaId: string, itemId: string, incluido: boolean): void {
  const id = this.recursoId();
  if (!id) {
    return;
  }
  this.erroValidacao.set('');
  this._recursoService.alterarInclusaoItem(id, guiaId, itemId, incluido).subscribe({
    next: () => {
      this._carregar(id);
    },
    error: () => {
      this.erroValidacao.set('Erro ao alterar inclusão do item. Tente novamente.');
    },
  });
}
```

- Importe o tipo: `import type { GuiaNoRecursoDto, ItemGuiaNoRecursoDto, RecursoDto } from '../recurso.types';`
  (o arquivo já importa `GuiaNoRecursoDto, RecursoDto` na linha 6 — adicione `ItemGuiaNoRecursoDto`).

**(d) Template — na tabela `guia-card__itens-table` (linhas 138-186): adicione uma coluna de ação.**
No `<thead><tr>` acrescente `<th></th>` no fim. Em cada `<tr>` de item, marque a linha riscada e
adicione a célula de ação:

```html
@for (item of guia.itens; track item.id) {
<tr [class.guia-card__item--excluido]="!item.incluidoNoRecurso">
  <td>{{ item.codigoTuss }}</td>
  <!-- ... colunas existentes ... -->
  <td>
    @if (item.incluidoNoRecurso) {
    <button type="button" class="guia-card__item-excluir" (click)="excluirItem(guia.id, item)">Excluir</button>
    } @else {
    <button type="button" class="guia-card__item-reincluir" (click)="reincluirItem(guia.id, item)">Reincluir</button>
    }
  </td>
</tr>
}
```

**(e) SCSS — em `recurso-guias.component.scss`. Use tokens (ver STYLES.md); proibido hex/cor nomeada/px fora da escala `space()`.**

```scss
.guia-card__item--excluido {
  opacity: 0.5;
  text-decoration: line-through;
}
```

(Estilize `&__item-excluir` / `&__item-reincluir` como os outros botões pequenos do arquivo —
imite `&__remover` / `&__adicionar-item` já presentes. NÃO use `font-size`/`color` cru: use mixins
`@include text-*` e `var(--color-*)`.)

**Testes (red primeiro) — em `recurso-guias.component.spec.ts`:**

- No mock `recursoService` (~linha 100), adicione: `alterarInclusaoItem: vi.fn().mockReturnValue(of(undefined)),`
- `makeItemGuiaNoRecurso` (helper já existente) precisa incluir `incluidoNoRecurso: true` no default
  (adicione o campo no objeto base do helper para casar com o novo tipo).
- Cenários:
  - `excluirItem com confirm aceito chama alterarInclusaoItem com incluido=false`
    (use `vi.spyOn(window, 'confirm').mockReturnValue(true)`; verifique
    `expect(recursoService.alterarInclusaoItem).toHaveBeenCalledWith('rec-1', 'guia-1', 'item-1', false)`).
  - `excluirItem com confirm recusado NÃO chama o serviço` (`mockReturnValue(false)`).
  - `reincluirItem chama alterarInclusaoItem com incluido=true` (sem confirm).
  - `erro ao alterar inclusão exibe mensagem` (`mockReturnValue(throwError(() => new Error()))` →
    `erroValidacao()` não vazio).

**Aceite (checklist objetivo):**

- [ ] `pnpm -F admin-web test:ci` verde (cobertura ≥ 80%)
- [ ] `pnpm -F admin-web lint` e `pnpm -F admin-web stylelint` sem warnings
- [ ] `pnpm -F admin-web prettier:check` ok
- [ ] item com `incluidoNoRecurso=false` renderiza com classe `guia-card__item--excluido` e botão "Reincluir"
- [ ] `confirm()` recusado não dispara chamada ao serviço

**Commit:** `feat(admin-web): exclui/reinclui item de guia na tela de recurso (TASK-EIR-03)`

---

## Checklist final

- [x] TASK-EIR-01 — flag `IncluidoNoRecurso` (entidade + config + migration)
- [x] TASK-EIR-02 — serviço + endpoint + PDF + reset (backend)
- [ ] TASK-EIR-03 — frontend (card, botões, item riscado, serviço, tipos)

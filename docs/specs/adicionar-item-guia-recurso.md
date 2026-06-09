# SPEC: Adicionar item à guia a partir da tela de guias do recurso (Opção B — modal)

> **Como executar:** uma task por sessão. Rode `/tdd-task docs/specs/adicionar-item-guia-recurso.md <TASK-ID>`,
> deixe a sessão implementar+commitar, limpe o contexto (`/clear`), rode a próxima.
> CLAUDE.md e MEMORY.md já entram no contexto de cada sessão — as tasks NÃO os repetem.
> **O checkbox `[ ]`/`[x]` é a única memória entre sessões:** uma sessão fria se orienta
> só por ele. Comece pela primeira task ainda `[ ]`; nunca refaça uma já marcada `[x]`.

## Objetivo

Permitir que o TenantAdmin **adicione um novo item** a uma guia diretamente da tela de guias
do recurso (`recurso-guias`), sem sair do contexto do recurso. Um botão "+ Adicionar item" no
card expandido da guia abre um **modal** que reaproveita o `app-item-guia-form` existente; ao
salvar, um endpoint granular novo (`POST /api/v1/admin/guias/{id}/itens`) cria o item, apura
**somente esse item** pelo motor (a apuração é independente por item) e devolve a guia atualizada.
A tela recarrega o recurso e mostra o item novo.

**Pronto =** operador adiciona um item pela tela do recurso; o item aparece no card com seu
`ValorApurado` apurado; itens preexistentes (e seus `ValorLiquidado`/`MotivoGlosa`/`ValorApurado`
manual) ficam intactos.

## Decisões de produto já fechadas (valem para todas as tasks)

- **Apuração é append-only.** O motor (`UnimedRuleSet.ApurarItemAsync`) calcula cada item usando
  só os atributos do próprio item + a operadora — **não há contexto cruzado entre itens**. Logo,
  adicionar um item apura **apenas o item novo** e **nunca** mexe nos itens existentes. Não use
  `AtualizarAsync` (apaga todos os itens e zera `ValorLiquidado`) nem `RecalcularAsync` (zera todo
  `ValorApurado`) — ambos destroem dados do recurso.
- **Item não-precificável → rejeita** (semântica de criação). Se a guia é não-pacote e o motor
  retorna `SemTabela`/`Indeterminado`, o endpoint rejeita com `ValidationError` (igual a criar guia).
- **Guia pacote → suportada com valor manual.** Para guia `EhPacote`, o item exige `ValorApurado`
  preenchido (sem motor). Por isso o frontend precisa saber se a guia é pacote → `EhPacote` é
  exposto no `GuiaNoRecursoDto` (TASK-02) e repassado ao `app-item-guia-form` via `[ehPacote]`.

## Contexto compartilhado

- Bounded context: `Faturamento`. Serviço backend: `App/Faturamento/GuiaService.cs`. Endpoints:
  `App/Faturamento/Endpoints/GuiaEndpoints.cs`. Rota base: `/api/v1/admin/guias` (policy `TenantAccess`).
- Frontend admin-web (Angular 20, standalone + signals). Os tipos TS de guia/recurso são
  **escritos à mão** (não há regeneração de client para estes) em
  `apps/admin-web/src/app/admin/faturamento/{guia,recurso}.types.ts`.

## Tasks

### TASK-ADDITEM-01 — Backend: `GuiaService.AdicionarItemAsync` + endpoint POST itens

- [x] concluída

**Objetivo:** criar o método de serviço que adiciona um item à guia (append-only, apura só o item
novo) e expor o endpoint `POST /api/v1/admin/guias/{id:guid}/itens`.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/tests/Faturamento.Tests/Calculo/GuiaServiceCalculoTests.cs:13-87` — padrão de teste
  - `SeedBaseAsync` (Unimed) + `TabelaProcedimento.Create(tenant, operadora, proc, 200m)` para item
    apurável; e o teste de `SemTabela` rejeitado.
- `apps/backend/tests/Faturamento.Tests/GuiaPagamentoTests.cs:11-66` — `BuildTenant`, `SeedGuiaAsync`
  com operadora `TipoRuleSet.Nulo`, e como checar `ItensGuia` via `db.CreateTenantContext(tenantId)`.

**Criar/Editar:**

- `apps/backend/App/Faturamento/GuiaService.cs` (editar: novo método `AdicionarItemAsync`)
- `apps/backend/App/Faturamento/Endpoints/GuiaEndpoints.cs` (editar: nova rota + handler)
- `apps/backend/tests/Faturamento.Tests/` (novo arquivo de teste, ex. `Guia/AdicionarItemTests.cs`)

**Contratos que já existem (não precisa abrir — use como estão):**

```csharp
// GuiaService.cs:14  — comando de item (reutilize)
internal sealed record CriarItemGuiaCommand(
    Guid ProcedimentoId, PosicaoExecutor PosicaoExecutor,
    decimal PercentualOrdem, ViaAcesso ViaAcesso, Acomodacao Acomodacao,
    bool EhUrgencia, decimal? ValorApurado, int? TempoAnestesicoMin = null);

// ItemGuia.cs:38 — Create assina assim (ValorLiquidado nasce null)
ItemGuia.Create(guiaId, procedimentoId, posicao, percentualOrdem, via, acomodacao,
                ehUrgencia, valorApurado, tempoAnestesicoMin);

// Motor (namespace App.Faturamento.Motor — já em using no GuiaService)
new ApurarGuiaContext(tenantId, prestadorId, operadoraId, IReadOnlyList<ApurarItemInput>);
new ApurarItemInput(itemGuiaId, procedimentoId, posicao, percentualOrdem, via, acomodacao, ehUrgencia, tempoAnestesicoMin);
// ruleSet = _factory.Criar(operadora.TipoRuleSet);  resultados = await ruleSet.ApurarAsync(ctx, ct);
// resultado.Situacao == SituacaoApuracao.Calculado ; resultado.ValorApurado ; resultado.Passos (cada: .Regra .Fator .ValorResultante)
// Calculo.Create(tenantId, guiaId) ; PassoCalculo.Create(calculoId, itemGuiaId, seq, regra, fator, valorResultante)
// Erros: Result<GuiaDetalheDto>.Fail(new ValidationError("...")) / new NotFoundError("...")
```

**Padrão a seguir — implementação proposta do método (replique esta forma):**

```csharp
internal async Task<Result<GuiaDetalheDto>> AdicionarItemAsync(
    Guid guiaId, CriarItemGuiaCommand itemCmd, CancellationToken ct = default)
{
    if (itemCmd.PercentualOrdem < 0.01m || itemCmd.PercentualOrdem > 1.00m)
    {
        return Result<GuiaDetalheDto>.Fail(
            new ValidationError("PercentualOrdem deve estar entre 0.01 e 1.00."));
    }

    var guia = await _db.Guias.FirstOrDefaultAsync(g => g.Id == guiaId, ct);
    if (guia is null)
    {
        return Result<GuiaDetalheDto>.Fail(new NotFoundError("Guia não encontrada."));
    }

    var operadora = await _db.Operadoras.FirstOrDefaultAsync(o => o.Id == guia.OperadoraId, ct);
    if (operadora is null)
    {
        return Result<GuiaDetalheDto>.Fail(new NotFoundError("Operadora não encontrada."));
    }

    if (guia.EhPacote)
    {
        if (itemCmd.ValorApurado is null)
        {
            return Result<GuiaDetalheDto>.Fail(
                new ValidationError("Itens de guia pacote devem ter ValorApurado preenchido."));
        }
    }
    else
    {
        // rejeita item não-precificável (SemTabela/Indeterminado), igual à criação.
        // ValidarCalculoViavelAsync já existe (GuiaService.cs:661) e devolve null se viável.
        var erro = await ValidarCalculoViavelAsync(
            guia.PrestadorId, guia.OperadoraId, operadora, [itemCmd], _currentUser.TenantId!.Value, ct);
        if (erro is not null)
        {
            return Result<GuiaDetalheDto>.Fail(new ValidationError(erro));
        }
    }

    var item = ItemGuia.Create(
        guia.Id, itemCmd.ProcedimentoId, itemCmd.PosicaoExecutor,
        itemCmd.PercentualOrdem, itemCmd.ViaAcesso, itemCmd.Acomodacao,
        itemCmd.EhUrgencia, guia.EhPacote ? itemCmd.ValorApurado : null, itemCmd.TempoAnestesicoMin);
    _db.ItensGuia.Add(item);
    await _db.SaveChangesAsync(ct);

    if (!guia.EhPacote && operadora.TipoRuleSet != TipoRuleSet.Nulo)
    {
        var ruleSet = _factory.Criar(operadora.TipoRuleSet);
        var ctx = new ApurarGuiaContext(
            _currentUser.TenantId!.Value, guia.PrestadorId, guia.OperadoraId,
            [new ApurarItemInput(
                item.Id, item.ProcedimentoId, item.PosicaoExecutor,
                item.PercentualOrdem, item.ViaAcesso, item.Acomodacao,
                item.EhUrgencia, item.TempoAnestesicoMin)]);
        var resultado = (await ruleSet.ApurarAsync(ctx, ct))[0];

        if (resultado.Situacao == SituacaoApuracao.Calculado)
        {
            item.SetValorApurado(resultado.ValorApurado);

            var calculo = await _db.Calculos.FirstOrDefaultAsync(c => c.GuiaId == guiaId, ct);
            if (calculo is null)
            {
                calculo = Calculo.Create(_currentUser.TenantId!.Value, guiaId);
                _db.Calculos.Add(calculo);
            }

            var seq = await _db.PassosCalculo
                .Where(p => p.CalculoId == calculo.Id)
                .Select(p => (int?)p.Sequencia).MaxAsync(ct) ?? 0;
            foreach (var passo in resultado.Passos)
            {
                _db.PassosCalculo.Add(PassoCalculo.Create(
                    calculo.Id, item.Id, ++seq, passo.Regra, passo.Fator, passo.ValorResultante));
            }

            await _db.SaveChangesAsync(ct);
        }
    }

    return await ObterDetalheDtoInternalAsync(guiaId, ct);
}
```

**Endpoint — adicione a rota e o handler (espelhe `CriarGuiaAsync`, GuiaEndpoints.cs:85-108):**

```csharp
// dentro de MapGuiaEndpoints():
g.MapPost("{id:guid}/itens", AdicionarItemAsync);

// handler novo — reusa CriarItemGuiaRequest (GuiaEndpoints.cs:239) como body:
private static async Task<IResult> AdicionarItemAsync(
    Guid id, CriarItemGuiaRequest body, GuiaService service, CancellationToken ct)
{
    var cmd = new CriarItemGuiaCommand(
        body.ProcedimentoId, body.PosicaoExecutor, body.PercentualOrdem,
        body.ViaAcesso, body.Acomodacao, body.EhUrgencia, body.ValorApurado, body.TempoAnestesicoMin);
    var result = await service.AdicionarItemAsync(id, cmd, ct);
    if (result.IsFailure)
    {
        var statusCode = result.Error switch
        {
            NotFoundError => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
    }

    return Results.Ok(result.Value);
}
```

**Testes (red primeiro):**

- `Deve adicionar item apurável a guia Unimed e preencher ValorApurado` — seed via `SeedBaseAsync` +
  `TabelaProcedimento.Create(...200m)`; cria guia com 1 item; chama `AdicionarItemAsync`; assert
  novo item com `ValorApurado == 200m` e existe `PassoCalculo` "ValorBase" para o item novo.
- `Deve preservar ValorLiquidado e MotivoGlosa dos itens existentes ao adicionar item` — operadora
  `Nulo`; seed guia 1 item; `AtualizarPagamentoItemAsync(guia, item, 100m, "CB")`; adiciona 2º item;
  reabre via `db.CreateTenantContext` e assert que o 1º item ainda tem `ValorLiquidado==100m` e
  `MotivoGlosa=="CB"`.
- `Deve rejeitar item sem tabela em guia não-pacote` — Unimed sem `TabelaProcedimento`; espera
  `IsFailure` + `ValidationError`.
- `Deve rejeitar item pacote sem ValorApurado` — guia `EhPacote=true`; `CriarItemGuiaCommand` com
  `ValorApurado: null`; espera `ValidationError`.
- `Deve adicionar item pacote com ValorApurado manual sem invocar motor` — guia pacote; item com
  `ValorApurado: 500m`; assert item persistido com `500m` e sem `PassoCalculo` para ele.
- `Deve retornar NotFound para guia inexistente`.

**Aceite (checklist objetivo):**

- [x] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [x] `dotnet test apps/backend/Honorare.slnx` verde (novos testes inclusos)
- [x] Nenhuma chamada a `AtualizarAsync`/`RecalcularAsync` dentro de `AdicionarItemAsync`

**Commit:** `feat(faturamento): adiciona item à guia via endpoint granular (TASK-ADDITEM-01)`

---

### TASK-ADDITEM-02 — Backend: expor `EhPacote` em `GuiaNoRecursoDto`

- [x] concluída

**Objetivo:** o frontend precisa saber se a guia é pacote para mostrar o campo de valor manual no
modal. Adicionar `bool EhPacote` ao `GuiaNoRecursoDto` e preencher na projeção do detalhe do recurso.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md. **Não** depende da TASK-01.

**Ler (só isto):**

- `apps/backend/App/Faturamento/RecursoService.cs:32-38` (record `GuiaNoRecursoDto`) e
  `:196-253` (projeção `guiasRaw` → `guiaDtos`).

**Editar:**

- `apps/backend/App/Faturamento/RecursoService.cs`

**Mudanças (3 pontos no mesmo arquivo):**

```csharp
// 1) record (RecursoService.cs:32) — acrescente EhPacote:
internal sealed record GuiaNoRecursoDto(
    Guid Id, string NumeroGuia, DateOnly DataAtendimento,
    string? BeneficiarioNome, string? BeneficiarioCarteira,
    SituacaoGuia Situacao,
    string? Observacao,
    string LocalAtendimento,
    bool EhPacote,
    IReadOnlyList<ItemGuiaNoRecursoDto> Itens);

// 2) projeção anônima guiasRaw (~RecursoService.cs:202) — inclua g.EhPacote no select new { ... }

// 3) construção do guiaDtos (~RecursoService.cs:249) — passe g.EhPacote antes da lista de itens:
var guiaDtos = guiasRaw.Select(g => new GuiaNoRecursoDto(
    g.Id, g.NumeroGuia, g.DataAtendimento,
    g.BeneficiarioNome, g.BeneficiarioCarteira,
    g.Situacao, g.Observacao, g.LocalAtendimento, g.EhPacote,
    itensPorGuia.GetValueOrDefault(g.Id, []))).ToList();
```

**Testes (red primeiro):**

- Procure o teste existente do detalhe do recurso (ex. `tests/Faturamento.Tests/Recurso/`); adicione
  um caso: `ObterPorId deve retornar EhPacote da guia` — seed recurso com 1 guia pacote, assert
  `guias[0].EhPacote == true`. Se não houver teste de detalhe próximo, crie um seguindo o padrão
  de `GuiaPagamentoTests.cs:88-112` (cria recurso + `AdicionarGuiaAsync`).

**Aceite:**

- [x] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [x] `dotnet test apps/backend/Honorare.slnx` verde
- [x] `GuiaNoRecursoDto` serializa `ehPacote`

**Commit:** `feat(faturamento): expõe EhPacote no detalhe do recurso (TASK-ADDITEM-02)`

---

### TASK-ADDITEM-03 — Frontend: método `adicionarItem` no GuiaService + `ehPacote` no tipo

- [x] concluída

**Objetivo:** camada de wiring TS — método HTTP para o endpoint da TASK-01 e o campo `ehPacote` no
tipo de guia do recurso (TASK-02).

**Depende de (por contrato, não abra):** endpoint `POST /api/v1/admin/guias/{id}/itens` (TASK-01)
e campo `ehPacote` no DTO (TASK-02).

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/admin-web/src/app/admin/faturamento/guia.service.ts:78-103` — padrão de `criar`/`atualizar`
  e do tipo de retorno `GuiaDetalheItem`.

**Editar:**

- `apps/admin-web/src/app/admin/faturamento/guia.service.ts`
- `apps/admin-web/src/app/admin/faturamento/recurso.types.ts`

**Mudanças:**

```typescript
// guia.service.ts — novo método (CriarItemGuiaPayload já existe em guia.types.ts:85):
adicionarItem(guiaId: string, payload: CriarItemGuiaPayload): Observable<GuiaDetalheItem> {
  return this._http.post<GuiaDetalheItem>(`/api/v1/admin/guias/${guiaId}/itens`, payload);
}
// (adicione CriarItemGuiaPayload ao import de './guia.types')

// recurso.types.ts — acrescente em GuiaNoRecursoDto (interface ~linha 37):
//   ehPacote: boolean;
```

**Testes (red primeiro):** em `guia.service.spec.ts`, espelhe um teste existente de POST
(`criar`) usando `HttpTestingController`:

- `adicionarItem deve POSTar em /api/v1/admin/guias/:id/itens com o payload do item`.

**Aceite:**

- [x] `pnpm -F admin-web lint` (--max-warnings 0) limpo
- [x] `pnpm -F admin-web test:ci` verde
- [x] `adicionarItem` tipado `Observable<GuiaDetalheItem>`

**Commit:** `feat(admin-web): adicionarItem no GuiaService e ehPacote no tipo do recurso (TASK-ADDITEM-03)`

---

### TASK-ADDITEM-04 — Frontend: `AdicionarItemModalComponent`

- [x] concluída

**Objetivo:** criar o modal que envolve `app-item-guia-form`, chama `GuiaService.adicionarItem` ao
salvar, e emite `concluido`/`cancelado`. Segue o padrão de modal `importar-modal` (backdrop próprio,
sem CDK).

**Depende de (por contrato, não abra):**

```typescript
// GuiaService (TASK-03):
adicionarItem(guiaId: string, payload: CriarItemGuiaPayload): Observable<GuiaDetalheItem>;
// CriarItemGuiaPayload (guia.types.ts:85): { procedimentoId; posicaoExecutor; percentualOrdem;
//   viaAcesso; acomodacao; ehUrgencia; valorApurado: number|null; tempoAnestesicoMin?: number|null }
```

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/admin-web/src/app/admin/faturamento/guia-form/item-guia-form/item-guia-form.component.ts:78-263`
  — contrato do `app-item-guia-form`: inputs `[ehPacote]` (boolean), `[operadoraId]` (string),
  `[item]` (opcional); output `(itemChange)` emite `ItemGuiaDisplay | null` (null = cancelar). O
  form já tem seus próprios botões Cancelar/Salvar Item.
- `apps/admin-web/src/app/admin/catalog/procedimentos/importar-modal/importar-modal.component.html:1-6`
  — markup do backdrop (`@if (open) { <div class="...__backdrop"><div class="..."> ... } `).

**Criar:**

- `apps/admin-web/src/app/admin/faturamento/recurso-guias/adicionar-item-modal/adicionar-item-modal.component.ts`
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/adicionar-item-modal/adicionar-item-modal.component.scss`
- spec correspondente

**Contrato do componente (implemente assim):**

```typescript
@Component({
  selector: "app-adicionar-item-modal",
  imports: [ItemGuiaFormComponent],
  template: `
    @if (open()) {
      <div class="adicionar-item-modal__backdrop">
        <div class="adicionar-item-modal">
          <header class="adicionar-item-modal__header">
            <h2 class="adicionar-item-modal__title">Adicionar item</h2>
          </header>
          @if (erro()) {
            <p class="adicionar-item-modal__erro">{{ erro() }}</p>
          }
          <app-item-guia-form [ehPacote]="ehPacote()" [operadoraId]="operadoraId()" (itemChange)="onItemChange($event)" />
        </div>
      </div>
    }
  `,
  styleUrl: "./adicionar-item-modal.component.scss",
})
export class AdicionarItemModalComponent {
  // inputs (signal inputs):
  readonly open = input(false);
  readonly guiaId = input("");
  readonly operadoraId = input("");
  readonly ehPacote = input(false);
  // outputs:
  readonly concluido = output<void>();
  readonly cancelado = output<void>();

  private readonly _guiaService = inject(GuiaService);
  readonly erro = signal("");

  onItemChange(item: ItemGuiaDisplay | null): void {
    if (item === null) {
      // cancelar (botão do item-guia-form)
      this.cancelado.emit();
      return;
    }
    this.erro.set("");
    this._guiaService
      .adicionarItem(this.guiaId(), {
        procedimentoId: item.procedimentoId,
        posicaoExecutor: item.posicaoExecutor,
        percentualOrdem: item.percentualOrdem,
        viaAcesso: item.viaAcesso,
        acomodacao: item.acomodacao,
        ehUrgencia: item.ehUrgencia,
        valorApurado: item.valorApurado,
        tempoAnestesicoMin: item.tempoAnestesicoMin ?? null,
      })
      .subscribe({
        next: () => {
          this.concluido.emit();
        },
        error: (err: HttpErrorResponse) => {
          this.erro.set((err.error as { detail?: string } | null)?.detail ?? "Erro ao adicionar item. Verifique os dados e tente novamente.");
        },
      });
  }
}
```

Imports a resolver: `Component, inject, input, output, signal` de `@angular/core`;
`HttpErrorResponse` de `@angular/common/http`; `GuiaService` de `../../guia.service`;
`ItemGuiaFormComponent` de `../../guia-form/item-guia-form/item-guia-form.component`;
`ItemGuiaDisplay` de `../../guia.types`.

**SCSS (regras duras — ver `apps/admin-web/STYLES.md`):** sem hex/cores nomeadas; `@use 'styles/tokens' as *;`
para `space()` e mixins `@include text-*`; espaçamentos só com `space(n)` onde **n ∈ {1,2,3,4,6,8,12,16,24}**
(MEMORY: `space(5)` quebra). Backdrop: `position: fixed; inset: 0;` com `var(--color-*)` translúcido;
container centralizado. Espelhe `importar-modal.component.scss` se precisar de referência de proporções.

**Testes (red primeiro):**

- `Não renderiza conteúdo quando open=false`.
- `onItemChange(null) emite cancelado`.
- `onItemChange(item) chama adicionarItem e emite concluido no sucesso` (mock `GuiaService`).
- `Em erro do adicionarItem, seta a mensagem de erro` (mock retornando `throwError`).

**Aceite:**

- [x] `pnpm -F admin-web lint` e `pnpm -F admin-web stylelint` limpos (--max-warnings 0)
- [x] `pnpm -F admin-web test:ci` verde
- [x] modal não usa `| currency`/`| number`/`| date` (NG0701) nem `[value]` em `<select>` (MEMORY)

**Commit:** `feat(admin-web): modal para adicionar item à guia (TASK-ADDITEM-04)`

---

### TASK-ADDITEM-05 — Frontend: integrar o modal na tela `recurso-guias`

- [x] concluída

**Objetivo:** botão "+ Adicionar item" no card expandido da guia; abre o modal; ao concluir,
recarrega o recurso.

**Depende de (por contrato, não abra):**

```typescript
// <app-adicionar-item-modal> (TASK-04):
//   inputs:  [open]=boolean  [guiaId]=string  [operadoraId]=string  [ehPacote]=boolean
//   outputs: (concluido)=void  (cancelado)=void
// GuiaNoRecursoDto ganhou (TASK-03): ehPacote: boolean
```

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.ts:8-19`
  (decorator `@Component` — **não tem array `imports` hoje; é preciso adicioná-lo**),
  `:113-135` (bloco `guia-card__detalhe` onde fica a observação/itens — ponto de inserção do botão),
  `:379-436` (signals + `_carregar`), `:675-687` (`_carregar(id)` — reuse para recarregar).

**Editar:**

- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.ts`
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.scss` (estilo do botão)
- `recurso-guias.component.spec.ts`

**Mudanças:**

1. No `@Component`, adicionar `imports: [AdicionarItemModalComponent]` (importar o componente da TASK-04:
   `./adicionar-item-modal/adicionar-item-modal.component`).
2. Novos signals na classe:
   ```typescript
   readonly modalItemAberto = signal(false);
   readonly guiaParaItem = signal<GuiaNoRecursoDto | null>(null);
   ```
3. Métodos:
   ```typescript
   abrirModalItem(guia: GuiaNoRecursoDto): void {
     this.guiaParaItem.set(guia);
     this.modalItemAberto.set(true);
   }
   fecharModalItem(): void {
     this.modalItemAberto.set(false);
     this.guiaParaItem.set(null);
   }
   onItemAdicionado(): void {
     this.fecharModalItem();
     const id = this.recursoId();
     if (id) { this._carregar(id); }
   }
   ```
4. Botão dentro de `guia-card__detalhe` (perto do bloco de observação, ~linha 134), classe BEM
   `guia-card__adicionar-item`:
   ```html
   <button type="button" class="guia-card__adicionar-item" (click)="abrirModalItem(guia)">+ Adicionar item</button>
   ```
5. Modal renderizado **uma vez** no fim do template (antes de fechar `.recurso-guias`), dirigido por
   signals. `operadoraId` vem do signal de nível do recurso `operadoraId()` (já carregado em `_carregar`):
   ```html
   <app-adicionar-item-modal [open]="modalItemAberto()" [guiaId]="guiaParaItem()?.id ?? ''" [operadoraId]="operadoraId()" [ehPacote]="guiaParaItem()?.ehPacote ?? false" (concluido)="onItemAdicionado()" (cancelado)="fecharModalItem()" />
   ```

**Testes (red primeiro):** em `recurso-guias.component.spec.ts`:

- `abrirModalItem seta guiaParaItem e abre o modal`.
- `onItemAdicionado fecha o modal e recarrega o recurso` (espia `_recursoService.obterPorId`).

**Aceite:**

- [ ] `pnpm -F admin-web lint` e `pnpm -F admin-web stylelint` limpos
- [ ] `pnpm -F admin-web test:ci` verde
- [ ] abrir guia → expandir → "+ Adicionar item" abre o modal; salvar recarrega e mostra o item novo
- [ ] SCSS do botão usa só `space(n)` válidos e `var(--color-*)` (sem hex)

**Commit:** `feat(admin-web): botão e modal de adicionar item na tela do recurso (TASK-ADDITEM-05)`

---

## Checklist final

- [x] TASK-ADDITEM-01 — Backend: `AdicionarItemAsync` + endpoint
- [x] TASK-ADDITEM-02 — Backend: `EhPacote` no `GuiaNoRecursoDto`
- [x] TASK-ADDITEM-03 — Frontend: `adicionarItem` + tipo `ehPacote`
- [x] TASK-ADDITEM-04 — Frontend: `AdicionarItemModalComponent`
- [x] TASK-ADDITEM-05 — Frontend: integração na `recurso-guias`

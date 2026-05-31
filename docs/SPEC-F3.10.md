# SPEC F3.10 — Recurso: edição de detalhes por atendimento

**Pré-requisito:** F3.9 concluído. `RecursoGuiasComponent` com painel de filtros e botão "Adicionar todas" funcionando (RC-04).

**Pós-condição:**

1. `RecursoGuiasComponent` exibe, para cada guia vinculada ao recurso, a lista de itens com `ValorApurado` e `ValorLiquidado` ao expandir a linha.
2. O operador pode editar o campo `Observacao` de cada guia diretamente no painel de recurso — o texto aparece em vermelho no PDF e no portal do médico.
3. O operador pode sobrescrever o `ValorApurado` de cada item individualmente — o valor alterado é refletido no PDF como "VL CORRETO" e altera o cálculo de "RESTA PAGAR".
4. Dois novos endpoints `PATCH` suportam as edições pontuais sem substituir a guia inteira.

**Ordem de execução:** RC-05 → RC-06, RC-07 (paralelos) → RC-08

---

## Contexto de domínio

| Campo                     | Entidade   | Papel no recurso                                                                                                                          |
| ------------------------- | ---------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `Guia.Observacao`         | `Guia`     | Descreve a natureza da divergência. Exibido em vermelho no PDF do recurso e no portal do médico.                                          |
| `ItemGuia.ValorApurado`   | `ItemGuia` | O valor correto calculado pelo motor ("VL CORRETO" no PDF). Pode ser editado manualmente para casos onde o motor não cobre a regra exata. |
| `ItemGuia.ValorLiquidado` | `ItemGuia` | O que a operadora pagou ("PG UNIMED" no PDF). Preenchido via conciliação de demonstrativo — não editável aqui.                            |
| Glosa por item            | calculado  | `ValorApurado − ValorLiquidado` quando ambos estão preenchidos e `ValorApurado > ValorLiquidado`.                                         |

---

## RC-05 · Backend: expandir `GuiaNoRecursoDto` com observação e itens

**TDD — escrever tests primeiro.**

### Leia antes

- `apps/backend/App/Faturamento/RecursoService.cs` — `GuiaNoRecursoDto`, `ObterPorIdAsync`
- `apps/backend/App/Faturamento/ItemGuia.cs` — campos disponíveis
- `apps/backend/tests/Faturamento.Tests/Recurso/RecursoCrudTests.cs` — padrão de setup existente

### Arquivos modificados

| Arquivo                                               | Ação                                                          |
| ----------------------------------------------------- | ------------------------------------------------------------- |
| `tests/Faturamento.Tests/Recurso/RecursoCrudTests.cs` | Adicionar 2 casos (abaixo)                                    |
| `App/Faturamento/RecursoService.cs`                   | `GuiaNoRecursoDto`, `ItemGuiaNoRecursoDto`, `ObterPorIdAsync` |

### Novos tipos em `RecursoService.cs`

```csharp
// Substitui o atual GuiaNoRecursoDto (sem breaking change na API pública
// pois ObterPorIdAsync retorna RecursoDetalheDto)
internal sealed record ItemGuiaNoRecursoDto(
    Guid Id,
    string CodigoTuss,
    string DescricaoProcedimento,
    PosicaoExecutor PosicaoExecutor,
    decimal PercentualOrdem,
    ViaAcesso ViaAcesso,
    Acomodacao Acomodacao,
    bool EhUrgencia,
    decimal? ValorApurado,
    decimal? ValorLiquidado);

internal sealed record GuiaNoRecursoDto(
    Guid Id,
    string Senha,
    DateOnly DataAtendimento,
    string? BeneficiarioNome,
    string? BeneficiarioCarteira,
    SituacaoGuia Situacao,
    string? Observacao,                         // ← novo
    IReadOnlyList<ItemGuiaNoRecursoDto> Itens); // ← novo (substitui TotalItens)
```

> `TotalItens` foi removido: o frontend pode derivar `Itens.length`. Se outros callers usam `TotalItens`, migrar nesses pontos antes.

### Atualizar `ObterPorIdAsync`

Substituir a consulta de guias e a query separada de `itemCounts` por uma consulta que já carrega os itens:

```csharp
// Query de guias (mantém o left join de beneficiário)
var guiasRaw = await (
    from g in _db.Guias
    join b in _db.Beneficiarios on g.BeneficiarioId equals (Guid?)b.Id into bs
    from b in bs.DefaultIfEmpty()
    where g.RecursoId == id
    select new
    {
        g.Id, g.Senha, g.DataAtendimento, g.Situacao, g.Observacao,
        BeneficiarioNome = (string?)b.Nome,
        BeneficiarioCarteira = (string?)b.Carteira,
    }).ToListAsync(ct);

// Join com procedimentos para código TUSS e descrição
var guiaIds = guiasRaw.Select(g => g.Id).ToList();
var itens = await (
    from i in _db.ItensGuia
    join p in _db.Procedimentos on i.ProcedimentoId equals p.Id
    where guiaIds.Contains(i.GuiaId)
    select new
    {
        i.Id, i.GuiaId,
        CodigoTuss = p.CodigoTuss,
        DescricaoProcedimento = p.Descricao,
        i.PosicaoExecutor, i.PercentualOrdem,
        i.ViaAcesso, i.Acomodacao, i.EhUrgencia,
        i.ValorApurado, i.ValorLiquidado,
    }).ToListAsync(ct);

var itensPorGuia = itens.GroupBy(i => i.GuiaId)
    .ToDictionary(g => g.Key, g => g
        .Select(i => new ItemGuiaNoRecursoDto(
            i.Id, i.CodigoTuss, i.DescricaoProcedimento,
            i.PosicaoExecutor, i.PercentualOrdem,
            i.ViaAcesso, i.Acomodacao, i.EhUrgencia,
            i.ValorApurado, i.ValorLiquidado))
        .ToList());

var guiaDtos = guiasRaw.Select(g => new GuiaNoRecursoDto(
    g.Id, g.Senha, g.DataAtendimento,
    g.BeneficiarioNome, g.BeneficiarioCarteira, g.Situacao,
    g.Observacao,
    itensPorGuia.GetValueOrDefault(g.Id, []))).ToList();
```

### Casos de teste (adicionar a `RecursoCrudTests`)

```
ObterPorId_RetornaGuiasComItensAsync
  seed: recurso com 1 guia, 2 itens (ValorApurado 100, ValorLiquidado 80 no primeiro;
        ValorApurado null no segundo)
  query: ObterPorIdAsync(recurso.Id)
  assert:
    result.IsOk == true
    result.Value.Guias.Count == 1
    result.Value.Guias[0].Itens.Count == 2
    result.Value.Guias[0].Itens[0].ValorApurado == 100
    result.Value.Guias[0].Itens[0].ValorLiquidado == 80
    result.Value.Guias[0].Itens[1].ValorApurado == null

ObterPorId_RetornaGuiasComObservacaoAsync
  seed: recurso com 1 guia com Observacao = "Guia glosada indevidamente"
  query: ObterPorIdAsync(recurso.Id)
  assert:
    result.Value.Guias[0].Observacao == "Guia glosada indevidamente"
```

---

## RC-06 · Backend: PATCH observação da guia

**TDD — escrever tests primeiro.**

### Leia antes

- `apps/backend/App/Faturamento/GuiaService.cs` — padrão de commands e serviço
- `apps/backend/App/Faturamento/Guia.cs` — entidade e método `Atualizar`
- `apps/backend/App/Faturamento/Endpoints/GuiaEndpoints.cs` — mapeamento de endpoints

### Arquivos modificados

| Arquivo                                         | Ação                                                     |
| ----------------------------------------------- | -------------------------------------------------------- |
| `tests/Faturamento.Tests/Guia/GuiaCrudTests.cs` | Adicionar 2 casos                                        |
| `App/Faturamento/Guia.cs`                       | Adicionar método `AtualizarObservacao`                   |
| `App/Faturamento/GuiaService.cs`                | `AtualizarObservacaoCommand`, `AtualizarObservacaoAsync` |
| `App/Faturamento/Endpoints/GuiaEndpoints.cs`    | `PATCH /guias/{id}/observacao`                           |

### Entidade (`Guia.cs`)

```csharp
internal void AtualizarObservacao(string observacao)
{
    Observacao = observacao.Trim();
    AtualizadoEm = DateTimeOffset.UtcNow;
}
```

### Command e service (`GuiaService.cs`)

```csharp
internal sealed record AtualizarObservacaoCommand(string Observacao);
```

```csharp
internal async Task<Result<GuiaItem>> AtualizarObservacaoAsync(
    Guid id, AtualizarObservacaoCommand cmd, CancellationToken ct = default)
{
    var guia = await _db.Guias.FirstOrDefaultAsync(g => g.Id == id, ct);
    if (guia is null)
    {
        return Result<GuiaItem>.Fail(new NotFoundError("Guia não encontrada."));
    }

    guia.AtualizarObservacao(cmd.Observacao);
    await _db.SaveChangesAsync(ct);

    return Result<GuiaItem>.Ok(MapToItem(guia));
}
```

### Endpoint (`GuiaEndpoints.cs`)

```csharp
group.MapPatch("{id}/observacao", async (
    Guid id,
    AtualizarObservacaoRequest req,
    GuiaService service,
    CancellationToken ct) =>
{
    var result = await service.AtualizarObservacaoAsync(id, new(req.Observacao), ct);
    return result.Match(Results.Ok, ApiErrors.ToResult);
}).RequireAuthorization("TenantAccess");

internal sealed record AtualizarObservacaoRequest(string Observacao);
```

**Validação:** `Observacao` pode ser string vazia (limpar a observação). Tamanho máximo: 2000 caracteres (consistente com a constraint do `Guia.Observacao` existente). Retornar 422 se ultrapassar.

### Casos de teste

```
AtualizarObservacao_SalvaTextoAsync
  seed: 1 guia com Observacao = ""
  action: AtualizarObservacaoAsync(guia.Id, new("Procedimento não coberto pela tabela"))
  assert: result.IsOk == true
  assert: db.Guias.Find(guia.Id).Observacao == "Procedimento não coberto pela tabela"

AtualizarObservacao_RetornaNotFoundParaGuiaInexistenteAsync
  seed: nenhum
  action: AtualizarObservacaoAsync(Guid.NewGuid(), new("Texto"))
  assert: result.IsOk == false && result.Error is NotFoundError
```

---

## RC-07 · Backend: PATCH valor apurado de item

**TDD — escrever tests primeiro.**

### Leia antes

- `apps/backend/App/Faturamento/ItemGuia.cs` — `SetValorApurado` (já existe)
- `apps/backend/App/Faturamento/GuiaService.cs` — padrão de service
- `apps/backend/App/Faturamento/Endpoints/GuiaEndpoints.cs`

### Arquivos modificados

| Arquivo                                         | Ação                                                                 |
| ----------------------------------------------- | -------------------------------------------------------------------- |
| `tests/Faturamento.Tests/Guia/GuiaCrudTests.cs` | Adicionar 3 casos                                                    |
| `App/Faturamento/GuiaService.cs`                | `AtualizarValorApuradoItemCommand`, `AtualizarValorApuradoItemAsync` |
| `App/Faturamento/Endpoints/GuiaEndpoints.cs`    | `PATCH /guias/{id}/itens/{itemId}/valor-apurado`                     |

### Command e service (`GuiaService.cs`)

```csharp
internal sealed record AtualizarValorApuradoItemCommand(decimal? ValorApurado);
```

```csharp
internal async Task<Result<ItemGuiaItem>> AtualizarValorApuradoItemAsync(
    Guid guiaId, Guid itemId, AtualizarValorApuradoItemCommand cmd,
    CancellationToken ct = default)
{
    var item = await _db.ItensGuia
        .Where(i => i.Id == itemId && i.GuiaId == guiaId)
        .FirstOrDefaultAsync(ct);

    if (item is null)
    {
        return Result<ItemGuiaItem>.Fail(new NotFoundError("Item não encontrado."));
    }

    item.SetValorApurado(cmd.ValorApurado);
    await _db.SaveChangesAsync(ct);

    return Result<ItemGuiaItem>.Ok(MapItemToDto(item));
}
```

> `SetValorApurado` já existe em `ItemGuia.cs` — não cria método novo.

### Endpoint (`GuiaEndpoints.cs`)

```csharp
group.MapPatch("{id}/itens/{itemId}/valor-apurado", async (
    Guid id,
    Guid itemId,
    AtualizarValorApuradoItemRequest req,
    GuiaService service,
    CancellationToken ct) =>
{
    var result = await service.AtualizarValorApuradoItemAsync(
        id, itemId, new(req.ValorApurado), ct);
    return result.Match(Results.Ok, ApiErrors.ToResult);
}).RequireAuthorization("TenantAccess");

internal sealed record AtualizarValorApuradoItemRequest(decimal? ValorApurado);
```

**Validação:** `ValorApurado` pode ser `null` (limpar o valor — reverte para "não apurado"). Quando não-nulo, deve ser `> 0`. Retornar 422 se negativo ou zero.

### Casos de teste

```
AtualizarValorApurado_SalvaValorAsync
  seed: 1 guia com 1 item com ValorApurado = null
  action: AtualizarValorApuradoItemAsync(guia.Id, item.Id, new(150.75m))
  assert: result.IsOk == true
  assert: db.ItensGuia.Find(item.Id).ValorApurado == 150.75m

AtualizarValorApurado_LimpaValorAsync
  seed: 1 guia com 1 item com ValorApurado = 100m
  action: AtualizarValorApuradoItemAsync(guia.Id, item.Id, new(null))
  assert: result.IsOk == true
  assert: db.ItensGuia.Find(item.Id).ValorApurado == null

AtualizarValorApurado_RetornaNotFoundSeItemNaoPertenceAGuiaAsync
  seed: 2 guias (g1, g2) com 1 item cada (i1, i2)
  action: AtualizarValorApuradoItemAsync(g1.Id, i2.Id, new(100m))
  assert: result.IsOk == false && result.Error is NotFoundError
```

---

## RC-08 · Frontend: edição inline em `RecursoGuiasComponent`

### Leia antes

- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.ts`
- `apps/admin-web/src/app/admin/faturamento/recurso.types.ts`
- `apps/admin-web/src/app/admin/faturamento/guia.service.ts`
- `apps/admin-web/src/app/admin/faturamento/guia.types.ts`
- `apps/admin-web/STYLES.md` — tokens de cores e espaçamento antes de escrever qualquer SCSS

### Arquivos modificados

| Arquivo                                                                   | Ação                                                           |
| ------------------------------------------------------------------------- | -------------------------------------------------------------- |
| `src/app/admin/faturamento/recurso.types.ts`                              | Adicionar `ItemGuiaNoRecursoDto`, atualizar `GuiaNoRecursoDto` |
| `src/app/admin/faturamento/guia.service.ts`                               | `atualizarObservacao`, `atualizarValorApuradoItem`             |
| `src/app/admin/faturamento/guia.service.spec.ts`                          | Testes para os 2 novos métodos                                 |
| `src/app/admin/faturamento/recurso-guias/recurso-guias.component.ts`      | Lógica de expansão e edição                                    |
| `src/app/admin/faturamento/recurso-guias/recurso-guias.component.html`    | Template expandido                                             |
| `src/app/admin/faturamento/recurso-guias/recurso-guias.component.scss`    | Estilos de detalhe                                             |
| `src/app/admin/faturamento/recurso-guias/recurso-guias.component.spec.ts` | Testes de componente                                           |

### Tipos (`recurso.types.ts`)

```typescript
export interface ItemGuiaNoRecursoDto {
  id: string;
  codigoTuss: string;
  descricaoProcedimento: string;
  posicaoExecutor: PosicaoExecutor;
  percentualOrdem: number;
  viaAcesso: ViaAcesso;
  acomodacao: Acomodacao;
  ehUrgencia: boolean;
  valorApurado: number | null;
  valorLiquidado: number | null;
}

export interface GuiaNoRecursoDto {
  id: string;
  senha: string;
  dataAtendimento: string;
  beneficiarioNome: string | null;
  beneficiarioCarteira: string | null;
  situacao: SituacaoGuia;
  observacao: string | null; // ← novo
  itens: ItemGuiaNoRecursoDto[]; // ← novo (substituiu totalItens)
}
```

> Importar `PosicaoExecutor`, `ViaAcesso`, `Acomodacao`, `SituacaoGuia` de `guia.types.ts` (já existem).

### Serviço (`guia.service.ts`)

```typescript
atualizarObservacao(guiaId: string, observacao: string): Observable<GuiaItem> {
  return this._http.patch<GuiaItem>(
    `${this._base}/${guiaId}/observacao`,
    { observacao }
  );
}

atualizarValorApuradoItem(
  guiaId: string,
  itemId: string,
  valorApurado: number | null
): Observable<ItemGuiaItem> {
  return this._http.patch<ItemGuiaItem>(
    `${this._base}/${guiaId}/itens/${itemId}/valor-apurado`,
    { valorApurado }
  );
}
```

### Componente (`recurso-guias.component.ts`)

**Novos signals e state:**

```typescript
// Controla qual guia está expandida (null = nenhuma)
guiaExpandida = signal<string | null>(null);

// Observações em edição (guiaId → texto atual no input)
observacoesEmEdicao = signal<Record<string, string>>({});

// Valores apurados em edição (itemId → string do input)
valoresEmEdicao = signal<Record<string, string>>({});

// Feedback de salvamento por guia/item
salvandoObservacao = signal<Record<string, boolean>>({});
salvandoValorApurado = signal<Record<string, boolean>>({});
```

**Novos métodos:**

```typescript
alternarExpansao(guiaId: string): void {
  // toggle: fecha se já aberta, abre e inicializa estado de edição se fechada
  if (this.guiaExpandida() === guiaId) {
    this.guiaExpandida.set(null);
    return;
  }
  const guia = this.guias().find(g => g.id === guiaId);
  if (!guia) { return; }
  this.guiaExpandida.set(guiaId);
  // inicializar observação com valor atual
  this.observacoesEmEdicao.update(m => ({ ...m, [guiaId]: guia.observacao ?? '' }));
  // inicializar valores apurados com valores atuais
  guia.itens.forEach(item => {
    this.valoresEmEdicao.update(m => ({
      ...m,
      [item.id]: item.valorApurado != null ? String(item.valorApurado) : '',
    }));
  });
}

salvarObservacao(guiaId: string): void {
  const texto = this.observacoesEmEdicao()[guiaId] ?? '';
  this.salvandoObservacao.update(m => ({ ...m, [guiaId]: true }));
  this._guiaService.atualizarObservacao(guiaId, texto).subscribe({
    next: () => {
      // atualizar lista local sem recarregar tudo
      this.guias.update(gs =>
        gs.map(g => g.id === guiaId ? { ...g, observacao: texto } : g));
      this.salvandoObservacao.update(m => ({ ...m, [guiaId]: false }));
    },
    error: () => {
      this.erroValidacao.set('Erro ao salvar observação.');
      this.salvandoObservacao.update(m => ({ ...m, [guiaId]: false }));
    },
  });
}

salvarValorApurado(guiaId: string, itemId: string): void {
  const raw = this.valoresEmEdicao()[itemId] ?? '';
  const valor = raw === '' ? null : parseFloat(raw.replace(',', '.'));
  this.salvandoValorApurado.update(m => ({ ...m, [itemId]: true }));
  this._guiaService.atualizarValorApuradoItem(guiaId, itemId, valor).subscribe({
    next: () => {
      // atualizar item na lista local
      this.guias.update(gs =>
        gs.map(g => g.id === guiaId
          ? {
              ...g,
              itens: g.itens.map(i =>
                i.id === itemId ? { ...i, valorApurado: valor } : i),
            }
          : g));
      this.salvandoValorApurado.update(m => ({ ...m, [itemId]: false }));
    },
    error: () => {
      this.erroValidacao.set('Erro ao salvar valor apurado.');
      this.salvandoValorApurado.update(m => ({ ...m, [itemId]: false }));
    },
  });
}

formatarMoeda(value: number | null): string {
  if (value === null) { return '—'; }
  return new Intl.NumberFormat('pt-BR', {
    style: 'currency', currency: 'BRL',
    minimumFractionDigits: 2, maximumFractionDigits: 2,
  }).format(value);
}
```

> Não usar `CurrencyPipe` nem `DecimalPipe` — ver regra no CLAUDE.md (NG0701 com signals + async).

### Template — estrutura do painel de guias

O painel de guias vinculadas (abaixo do filtro de candidatas) deve ser estendido:

```html
<!-- Para cada guia vinculada -->
@for (guia of guias(); track guia.id) {
<div class="guia-card">
  <!-- Cabeçalho clicável para expandir -->
  <div class="guia-card__header" (click)="alternarExpansao(guia.id)">
    <span class="guia-card__senha">{{ guia.senha }}</span>
    <span class="guia-card__data">{{ guia.dataAtendimento }}</span>
    <span class="guia-card__beneficiario">{{ guia.beneficiarioNome ?? '—' }}</span>
    <span class="guia-card__itens">{{ guia.itens.length }} iten(s)</span>
    @if (guia.observacao) {
    <span class="guia-card__obs-badge">Obs.</span>
    }
    <button class="guia-card__remover" (click)="$event.stopPropagation(); removerGuia(guia.id)">Remover</button>
    <span class="guia-card__expand-icon"> {{ guiaExpandida() === guia.id ? '▲' : '▼' }} </span>
  </div>

  <!-- Detalhe expansível -->
  @if (guiaExpandida() === guia.id) {
  <div class="guia-card__detalhe">
    <!-- Campo observação -->
    <div class="guia-card__observacao">
      <label class="guia-card__obs-label">Observação</label>
      <textarea class="guia-card__obs-input" [value]="observacoesEmEdicao()[guia.id] ?? ''" (input)="observacoesEmEdicao.update(m => ({ ...m, [guia.id]: $any($event.target).value }))" rows="2" maxlength="2000" placeholder="Descreva a divergência (aparece no PDF e no portal do médico)"> </textarea>
      <button class="guia-card__obs-salvar" [disabled]="salvandoObservacao()[guia.id]" (click)="salvarObservacao(guia.id)">{{ salvandoObservacao()[guia.id] ? 'Salvando…' : 'Salvar observação' }}</button>
    </div>

    <!-- Tabela de itens -->
    <table class="guia-card__itens-table">
      <thead>
        <tr>
          <th>Código</th>
          <th>Procedimento</th>
          <th>Posição</th>
          <th>VL CORRETO</th>
          <th>PG OPERADORA</th>
          <th>GLOSA</th>
        </tr>
      </thead>
      <tbody>
        @for (item of guia.itens; track item.id) {
        <tr>
          <td>{{ item.codigoTuss }}</td>
          <td>{{ item.descricaoProcedimento }}</td>
          <td>{{ item.posicaoExecutor }}</td>
          <td class="guia-card__valor-apurado-cell">
            <input class="guia-card__valor-input" type="number" step="0.01" min="0" [value]="valoresEmEdicao()[item.id] ?? ''" (input)="valoresEmEdicao.update(m => ({ ...m, [item.id]: $any($event.target).value }))" (blur)="salvarValorApurado(guia.id, item.id)" (keydown.enter)="salvarValorApurado(guia.id, item.id)" [disabled]="salvandoValorApurado()[item.id]" placeholder="0,00" />
          </td>
          <td>{{ formatarMoeda(item.valorLiquidado) }}</td>
          <td [class.guia-card__glosa]="item.valorApurado !== null && item.valorLiquidado !== null && item.valorApurado > item.valorLiquidado">{{ (item.valorApurado !== null && item.valorLiquidado !== null) ? formatarMoeda(item.valorApurado - item.valorLiquidado) : '—' }}</td>
        </tr>
        }
      </tbody>
    </table>
  </div>
  }
</div>
}
```

### Testes de componente (spec)

Casos mínimos a cobrir:

```
deve_exibir_guias_com_numero_de_itensAsync
deve_expandir_guia_ao_clicarAsync
deve_fechar_guia_expandida_ao_clicar_novamente
deve_salvar_observacao_ao_clicar_botao
deve_salvar_valor_apurado_ao_sair_do_campo_blur
deve_atualizar_lista_local_apos_salvar_observacao
deve_atualizar_lista_local_apos_salvar_valor_apurado
deve_exibir_glosa_quando_valor_apurado_maior_que_liquidado
deve_exibir_traco_em_glosa_quando_faltam_valores
```

---

## Critérios de aceitação

- [x] `GET /api/v1/admin/recursos/{id}` retorna `guias[].itens` com `valorApurado`, `valorLiquidado`, `codigoTuss`, `posicaoExecutor`.
- [x] `GET /api/v1/admin/recursos/{id}` retorna `guias[].observacao`.
- [x] `PATCH /api/v1/admin/guias/{id}/observacao` persiste o texto e retorna a guia atualizada.
- [x] `PATCH /api/v1/admin/guias/{id}/itens/{itemId}/valor-apurado` persiste o valor e retorna o item atualizado.
- [x] `PATCH .../valor-apurado` com `itemId` de outra guia retorna 404.
- [x] No frontend, clicar no cabeçalho de uma guia expande a linha e exibe a tabela de itens.
- [x] Observação é salva ao clicar "Salvar observação"; a lista local é atualizada sem recarregar a página.
- [x] ValorApurado é salvo ao sair do campo (`blur`) ou ao pressionar Enter; a lista local é atualizada.
- [x] A coluna GLOSA exibe `ValorApurado − ValorLiquidado` quando ambos estão presentes e `ValorApurado > ValorLiquidado`.
- [ ] O PDF gerado após as edições reflete o novo `ValorApurado` como "VL CORRETO".
- [x] Cobertura de testes ≥ 80% nos arquivos modificados.

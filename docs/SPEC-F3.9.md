# SPEC F3.9 — Recurso: guias candidatas com filtros

**Pré-requisito:** F3.5 e F3.8 concluídos. `Guia.RecursoId`, `ItemGuia.ValorApurado`, `ItemGuia.ValorLiquidado` existentes.

**Pós-condição:** (1) `ListarGuias` filtra por operadora, senha, beneficiário, `semRecurso` e `somenteComGlosa`. (2) Novo endpoint `POST /recursos/{id}/guias/lote` adiciona todas as guias que batem com critérios de filtro server-side. (3) `RecursoGuiasComponent` exibe painel de filtros pré-fixado ao prestador/operadora do recurso com botão "Adicionar todas". (4) Bug de URL no `adicionarGuia` corrigido.

**Ordem de execução:** RC-01 → RC-02 (podem ser paralelos) → RC-03 → RC-04

---

## RC-01 · Backend: filtros adicionais no `ListarGuias`

**TDD — escrever test primeiro.**

### Leia antes

- `apps/backend/App/Faturamento/GuiaService.cs` — `ListarGuiasQuery`, `ListarAsync`
- `apps/backend/App/Faturamento/Endpoints/GuiaEndpoints.cs` — `ListarGuiasRequest`
- `apps/backend/tests/Faturamento.Tests/Guia/GuiaListTests.cs` — padrão de setup

### Arquivos modificados

| Arquivo                                         | Ação                               |
| ----------------------------------------------- | ---------------------------------- |
| `tests/Faturamento.Tests/Guia/GuiaListTests.cs` | Adicionar 6 casos (abaixo)         |
| `App/Faturamento/GuiaService.cs`                | `ListarGuiasQuery` + `ListarAsync` |
| `App/Faturamento/Endpoints/GuiaEndpoints.cs`    | `ListarGuiasRequest`               |

### Casos de teste (adicionar a `GuiaListTests`)

```
Listar_FiltraPorOperadoraIdAsync
  seed: 2 guias com operadoras distintas (opA, opB), mesmo prestador
  query: OperadoraId = opA
  assert: result.Total == 1 && result.Itens[0].OperadoraId == opA

Listar_FiltraPorSenhaAsync
  seed: guias com senhas "ABC123", "XYZ789", "ABCxxx"
  query: Senha = "ABC"
  assert: result.Total == 2 (ambas que contêm "ABC")

Listar_FiltraPorBeneficiarioAsync
  seed: 2 guias — beneficiário "Maria Silva" e "João Costa"
  query: Beneficiario = "Maria"
  assert: result.Total == 1 && result.Itens[0].BeneficiarioNome == "Maria Silva"

Listar_FiltraSemRecursoAsync
  seed: 3 guias — 1 vinculada a um Recurso, 2 sem recurso
  query: SemRecurso = true
  assert: result.Total == 2
  assert: result.Itens todos com RecursoId == null (confirmar via db se necessário)

Listar_FiltraSomenteComGlosaAsync
  seed:
    guia A — 1 item com ValorApurado=100, ValorLiquidado=80  (glosa)
    guia B — 1 item com ValorApurado=100, ValorLiquidado=100 (sem glosa)
    guia C — 1 item com ValorApurado=100, ValorLiquidado=null (não pago)
  query: SomenteComGlosa = true
  assert: result.Total == 1 && result.Itens[0].Id == guiaA.Id

Listar_CombinarOperadoraESemRecursoAsync
  seed: 4 guias (opA sem recurso, opA com recurso, opB sem recurso, opB com recurso)
  query: OperadoraId = opA, SemRecurso = true
  assert: result.Total == 1
```

### Implementação

**`ListarGuiasQuery` nova assinatura** (manter posição de params existentes, acrescentar ao final):

```csharp
internal sealed record ListarGuiasQuery(
    Guid? PrestadorId, Guid? OperadoraId,
    DateOnly? DataInicio, DateOnly? DataFim,
    SituacaoGuia? Situacao, string? Senha, string? Beneficiario,
    bool? SemRecurso, bool? SomenteComGlosa,
    int Pagina, int ItensPorPagina);
```

**Atualizar todos os call sites** de `ListarGuiasQuery(...)` no projeto (incluindo `GuiaEndpoints.cs` e testes).

**`ListarAsync` — reestruturar em duas fases:**

```csharp
// Fase 1: filtros pre-projeção (acesso a g.RecursoId sem join)
var baseQuery = _db.Guias.AsQueryable();

if (query.PrestadorId.HasValue)
    baseQuery = baseQuery.Where(g => g.PrestadorId == query.PrestadorId.Value);
if (query.OperadoraId.HasValue)
    baseQuery = baseQuery.Where(g => g.OperadoraId == query.OperadoraId.Value);
if (query.DataInicio.HasValue)
    baseQuery = baseQuery.Where(g => g.DataAtendimento >= query.DataInicio.Value);
if (query.DataFim.HasValue)
    baseQuery = baseQuery.Where(g => g.DataAtendimento <= query.DataFim.Value);
if (query.Situacao.HasValue)
    baseQuery = baseQuery.Where(g => g.Situacao == query.Situacao.Value);
if (query.SemRecurso == true)
    baseQuery = baseQuery.Where(g => g.RecursoId == null);
if (query.SomenteComGlosa == true)
    baseQuery = baseQuery.Where(g => _db.ItensGuia.Any(i =>
        i.GuiaId == g.Id &&
        i.ValorApurado.HasValue && i.ValorLiquidado.HasValue &&
        i.ValorApurado > i.ValorLiquidado));

// Fase 2: join para projeção (igual ao código atual, mas sobre baseQuery)
var q = from g in baseQuery
        join pr in _db.Prestadores on g.PrestadorId equals pr.Id
        // ... (igual ao atual)
        select new { ... };

// Filtros pós-projeção (requerem dados dos joins)
if (!string.IsNullOrWhiteSpace(query.Senha))
    q = q.Where(x => x.Senha.Contains(query.Senha));
if (!string.IsNullOrWhiteSpace(query.Beneficiario))
    q = q.Where(x => x.BeneficiarioNome != null &&
                      x.BeneficiarioNome.Contains(query.Beneficiario));
```

**`ListarGuiasRequest`** — acrescentar campos com default null:

```csharp
internal sealed record ListarGuiasRequest(
    Guid? PrestadorId = null, Guid? OperadoraId = null,
    DateOnly? DataInicio = null, DateOnly? DataFim = null,
    SituacaoGuia? Situacao = null, string? Senha = null, string? Beneficiario = null,
    bool? SemRecurso = null, bool? SomenteComGlosa = null,
    int Pagina = 1, int ItensPorPagina = 20);
```

Atualizar `ListarGuiasAsync` em `GuiaEndpoints` para passar os novos campos ao construir `ListarGuiasQuery`.

### Critérios de pronto (RC-01)

- [x] 6 novos casos passando em `GuiaListTests`
- [ ] `dotnet build` sem warnings
- [ ] `ListarGuiasQuery` call sites atualizados (build confirma)

---

## RC-02 · Backend: endpoint de lote + correção de bug

**TDD — escrever test primeiro.**

### Leia antes

- `apps/backend/App/Faturamento/RecursoService.cs` — `AdicionarGuiaAsync`, `RemoverGuiaAsync`
- `apps/backend/App/Faturamento/Endpoints/RecursoEndpoints.cs` — rotas atuais
- `apps/backend/tests/Faturamento.Tests/Recurso/RecursoCrudTests.cs` — padrão de setup

### Arquivos modificados

| Arquivo                                               | Ação                                                       |
| ----------------------------------------------------- | ---------------------------------------------------------- |
| `tests/Faturamento.Tests/Recurso/RecursoCrudTests.cs` | Adicionar 4 casos (abaixo)                                 |
| `App/Faturamento/RecursoService.cs`                   | `AdicionarGuiasEmLoteCommand`, `AdicionarGuiasEmLoteAsync` |
| `App/Faturamento/Endpoints/RecursoEndpoints.cs`       | Novo endpoint lote + corrigir `AdicionarGuiaAsync`         |

### Bug a corrigir

Rota atual: `g.MapPost("{id:guid}/guias/{guiaId:guid}", AdicionarGuiaAsync)` — correto.
Frontend atual: `POST /api/v1/admin/recursos/{id}/guias` com body `{ guiaId }` — **errado** (sem `{guiaId}` na path).
A correção fica no RC-03 (frontend). No backend, a rota já está correta — não alterar.

### Casos de teste

```
AdicionarEmLote_FiltroPorPeriodo_AdicionaTodasDoPeriodoAsync
  seed: recurso + 5 guias do prestador+operadora do recurso:
    3 com data entre 01/03-31/03 e SemRecurso
    1 fora do período
    1 já vinculada a outro recurso
  cmd: DataInicio=01/03, DataFim=31/03
  assert: result.IsSuccess && result.Value == 3
  assert: db.Guias.Count(g => g.RecursoId == recursoId) == 3

AdicionarEmLote_GuiaJaVinculadaAoMesmoRecurso_IgnoraSilenciosamenteAsync
  seed: 2 guias, 1 já vinculada ao recurso, 1 disponível
  cmd: sem filtros de data
  assert: result.IsSuccess && result.Value == 1  (só a nova)
  assert: db.Guias.Count(g => g.RecursoId == recursoId) == 2

AdicionarEmLote_RecursoNaoEncontrado_FalhaAsync
  cmd para recursoId inexistente
  assert: result.IsFailure && result.Error is NotFoundError

AdicionarEmLote_SomenteComGlosa_AdicionaApenasDivergentesAsync
  seed: 3 guias:
    guia A — item ValorApurado=100, ValorLiquidado=80
    guia B — item ValorApurado=100, ValorLiquidado=100
    guia C — item ValorApurado=100, ValorLiquidado=null
  cmd: SomenteComGlosa=true
  assert: result.Value == 1 && vinculada == guiaA
```

### Implementação

**`AdicionarGuiasEmLoteCommand`:**

```csharp
internal sealed record AdicionarGuiasEmLoteCommand(
    Guid PrestadorId, Guid OperadoraId,
    DateOnly? DataInicio, DateOnly? DataFim,
    SituacaoGuia? Situacao, string? Senha, string? Beneficiario,
    bool? SomenteComGlosa);
```

**`AdicionarGuiasEmLoteAsync`** — aplica os mesmos filtros da Fase 1 do RC-01, mais `g.RecursoId == null` fixo (ignora guias já vinculadas a qualquer recurso):

```csharp
internal async Task<Result<int>> AdicionarGuiasEmLoteAsync(
    Guid recursoId, AdicionarGuiasEmLoteCommand cmd, CancellationToken ct = default)
{
    if (!await _db.Recursos.AnyAsync(r => r.Id == recursoId, ct))
        return Result<int>.Fail(new NotFoundError("Recurso não encontrado."));

    var q = _db.Guias.Where(g =>
        g.PrestadorId == cmd.PrestadorId &&
        g.OperadoraId == cmd.OperadoraId &&
        g.RecursoId == null);

    if (cmd.DataInicio.HasValue) q = q.Where(g => g.DataAtendimento >= cmd.DataInicio.Value);
    if (cmd.DataFim.HasValue)    q = q.Where(g => g.DataAtendimento <= cmd.DataFim.Value);
    if (cmd.Situacao.HasValue)   q = q.Where(g => g.Situacao == cmd.Situacao.Value);
    if (!string.IsNullOrWhiteSpace(cmd.Senha))
        q = q.Where(g => g.Senha.Contains(cmd.Senha));
    if (cmd.SomenteComGlosa == true)
        q = q.Where(g => _db.ItensGuia.Any(i =>
            i.GuiaId == g.Id &&
            i.ValorApurado.HasValue && i.ValorLiquidado.HasValue &&
            i.ValorApurado > i.ValorLiquidado));

    // Beneficiario filter via subquery (sem join para manter IQueryable<Guia>)
    if (!string.IsNullOrWhiteSpace(cmd.Beneficiario))
        q = q.Where(g => g.BeneficiarioId.HasValue &&
            _db.Beneficiarios.Any(b =>
                b.Id == g.BeneficiarioId.Value &&
                b.Nome.Contains(cmd.Beneficiario)));

    var guias = await q.ToListAsync(ct);
    foreach (var guia in guias)
        guia.MarcarEmRecurso(recursoId);

    await _db.SaveChangesAsync(ct);
    return Result<int>.Ok(guias.Count);
}
```

**Novo endpoint em `RecursoEndpoints`:**

```csharp
g.MapPost("{id:guid}/guias/lote", AdicionarGuiasEmLoteAsync);

private static async Task<IResult> AdicionarGuiasEmLoteAsync(
    Guid id, AdicionarGuiasEmLoteRequest body, RecursoService service, CancellationToken ct)
{
    var cmd = new AdicionarGuiasEmLoteCommand(
        body.PrestadorId, body.OperadoraId,
        body.DataInicio, body.DataFim,
        body.Situacao, body.Senha, body.Beneficiario, body.SomenteComGlosa);
    var result = await service.AdicionarGuiasEmLoteAsync(id, cmd, ct);
    if (result.IsFailure)
        return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: result.Error!.Message);
    return Results.Ok(new { adicionadas = result.Value });
}

internal sealed record AdicionarGuiasEmLoteRequest(
    Guid PrestadorId, Guid OperadoraId,
    DateOnly? DataInicio = null, DateOnly? DataFim = null,
    SituacaoGuia? Situacao = null, string? Senha = null,
    string? Beneficiario = null, bool? SomenteComGlosa = null);
```

**Atenção — rota ambígua:** registrar `guias/lote` **antes** de `guias/{guiaId:guid}` no `MapRecursoEndpoints`, ou usar `.MapPost("...", ...)` com path literal completo. O `{guiaId:guid}` não vai capturar "lote" pois não é um GUID válido, mas a ordem pode importar dependendo do roteador.

### Critérios de pronto (RC-02)

- [x] 4 novos casos passando em `RecursoCrudTests`
- [x] `dotnet build` sem warnings
- [x] `GET /api/v1/admin/recursos/{id}/pdf` e outros endpoints existentes não quebrados

---

## RC-03 · Frontend: tipos e serviços

**TDD — escrever test primeiro.**

### Leia antes

- `apps/admin-web/src/app/admin/faturamento/guia.types.ts`
- `apps/admin-web/src/app/admin/faturamento/guia.service.ts`
- `apps/admin-web/src/app/admin/faturamento/recurso.service.ts`
- `apps/admin-web/src/app/admin/faturamento/recurso.types.ts`
- `apps/admin-web/src/app/admin/faturamento/recurso.service.spec.ts` — padrão com `HttpTestingController`

### Arquivos modificados

| Arquivo                               | Ação                                                     |
| ------------------------------------- | -------------------------------------------------------- |
| `faturamento/recurso.service.spec.ts` | Adicionar 3 casos                                        |
| `faturamento/guia.types.ts`           | Estender `ListarGuiasParams`                             |
| `faturamento/guia.service.ts`         | Mapear novos params no `listar()`                        |
| `faturamento/recurso.types.ts`        | Novo tipo `AdicionarGuiasLoteParams`                     |
| `faturamento/recurso.service.ts`      | Corrigir `adicionarGuia`; adicionar `adicionarGuiasLote` |

### Casos de teste (adicionar a `recurso.service.spec.ts`)

```
adicionarGuia_enviaUrlComGuiaIdNaPath
  service.adicionarGuia('rec-1', 'guia-99')
  assert: req.method == 'POST'
  assert: req.url == '/api/v1/admin/recursos/rec-1/guias/guia-99'
  (body pode ser {} ou vazio)

adicionarGuiasLote_enviaFiltrosNoBody
  service.adicionarGuiasLote('rec-1', {
    prestadorId: 'p1', operadoraId: 'op1',
    dataInicio: '2026-03-01', dataFim: '2026-03-31',
    somenteComGlosa: true
  })
  assert: req.method == 'POST'
  assert: req.url == '/api/v1/admin/recursos/rec-1/guias/lote'
  assert: req.body.prestadorId == 'p1'
  assert: req.body.somenteComGlosa == true

adicionarGuiasLote_retornaAdicionadas
  mock responde { adicionadas: 5 }
  assert: result.adicionadas == 5
```

### Implementação

**`guia.types.ts`** — estender `ListarGuiasParams`:

```typescript
export interface ListarGuiasParams {
  prestadorId?: string;
  operadoraId?: string; // novo
  dataInicio?: string;
  dataFim?: string;
  situacao?: SituacaoGuia;
  senha?: string;
  beneficiario?: string; // novo
  semRecurso?: boolean; // novo
  somenteComGlosa?: boolean; // novo
  pagina: number;
  itensPorPagina: number;
}
```

**`guia.service.ts`** — no método `listar()`, adicionar mapeamento dos novos campos (seguir padrão dos existentes com `if (params.x) httpParams = httpParams.set(...)`).

**`recurso.types.ts`** — novo tipo:

```typescript
export interface AdicionarGuiasLoteParams {
  prestadorId: string;
  operadoraId: string;
  dataInicio?: string;
  dataFim?: string;
  situacao?: SituacaoGuia;
  senha?: string;
  beneficiario?: string;
  somenteComGlosa?: boolean;
}
```

**`recurso.service.ts`** — duas mudanças:

```typescript
// CORREÇÃO: era POST .../guias com body { guiaId } — rota errada
adicionarGuia(recursoId: string, guiaId: string): Observable<void> {
  return this._http
    .post(`/api/v1/admin/recursos/${recursoId}/guias/${guiaId}`, {})
    .pipe(map(() => undefined));
}

// NOVO
adicionarGuiasLote(
  recursoId: string,
  filtros: AdicionarGuiasLoteParams,
): Observable<{ adicionadas: number }> {
  return this._http.post<{ adicionadas: number }>(
    `/api/v1/admin/recursos/${recursoId}/guias/lote`,
    filtros,
  );
}
```

### Critérios de pronto (RC-03)

- [ ] 3 novos casos passando em `recurso.service.spec.ts`
- [ ] `pnpm -F admin-web test:ci` verde
- [ ] `pnpm -F admin-web lint` sem warnings

---

## RC-04 · Frontend: refatorar `RecursoGuiasComponent`

**TDD — reescrever spec junto com o componente.**

### Leia antes

- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.ts` — implementação atual
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.spec.ts` — testes atuais
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.scss` — estilos atuais
- `apps/admin-web/STYLES.md` — tokens, mixins, `space()`
- `apps/admin-web/src/app/admin/faturamento/recurso.types.ts` (RC-03 concluído)
- `apps/admin-web/src/app/admin/faturamento/recurso.service.ts` (RC-03 concluído)
- `apps/admin-web/src/app/admin/faturamento/guia.service.ts` (RC-03 concluído)

### Arquivos modificados

| Arquivo                                         | Ação                             |
| ----------------------------------------------- | -------------------------------- |
| `recurso-guias/recurso-guias.component.ts`      | Reescrever                       |
| `recurso-guias/recurso-guias.component.scss`    | Estender com novos elementos BEM |
| `recurso-guias/recurso-guias.component.spec.ts` | Reescrever (casos abaixo)        |

### Casos de teste

```
exibeGuiasVinculadas
  detalhe com 2 guias vinculadas
  assert: 2 elementos .recurso-guias__linha-guia

botaoRemoverChamaServiceEAtualizaLista
  click .recurso-guias__btn-remover na linha de 'guia-1'
  assert: recursoService.removerGuia('rec-1', 'guia-1') chamado
  assert: guias() tem 0 itens após sucesso

filtrarChamaListarComPrestadorEOperadoraDoRecurso
  component carregado com recurso { prestadorId: 'p1', operadoraId: 'op1' }
  click .recurso-guias__btn-filtrar
  assert: guiaService.listar chamado com { prestadorId: 'p1', operadoraId: 'op1', semRecurso: true }

filtrosSaoPassadosParaListar
  filtroSenha.set('ABC'); filtroSomenteGlosa.set(true)
  click .recurso-guias__btn-filtrar
  assert: guiaService.listar chamado com { senha: 'ABC', somenteComGlosa: true }

tabelaCandidataExibeResultados
  guiaService.listar retorna 3 itens
  click filtrar
  fixture.detectChanges()
  assert: 3 elementos .recurso-guias__linha-candidata

estadoInicialNaoCarregaCandidatas
  ao inicializar (sem clicar filtrar)
  assert: guiaService.listar NÃO chamado
  assert: .recurso-guias__hint visível

adicionarUmaGuiaChamaServiceCorretamente
  candidatas com 1 item (id 'guia-x')
  click .recurso-guias__btn-adicionar na linha
  assert: recursoService.adicionarGuia('rec-1', 'guia-x') chamado

adicionarTodasChamaLoteComFiltrosAtuais
  filtroDataInicio.set('2026-03-01'); filtroDataFim.set('2026-03-31')
  guiaService.listar retorna 5 itens
  click filtrar; fixture.detectChanges()
  click .recurso-guias__btn-adicionar-todas
  assert: recursoService.adicionarGuiasLote chamado com {
    prestadorId: 'p1', operadoraId: 'op1',
    dataInicio: '2026-03-01', dataFim: '2026-03-31'
  }

erroRemoverExibeMensagem
  recursoService.removerGuia retorna throwError
  click remover
  assert: .recurso-guias__erro visível

botaoPdfChamaBaixarPdf
  click .recurso-guias__btn-pdf
  assert: recursoService.baixarPdf('rec-1') chamado
```

### Implementação — sinais e lógica

```typescript
// Fixos, populados ao carregar o recurso:
readonly prestadorId = signal('');
readonly operadoraId = signal('');

// Filtros controlados pelo usuário:
readonly filtroSenha = signal('');
readonly filtroBeneficiario = signal('');
readonly filtroDataInicio = signal('');
readonly filtroDataFim = signal('');
readonly filtroSituacao = signal<SituacaoGuia | ''>('');
readonly filtroSomenteGlosa = signal(false);

// Estado da busca:
readonly candidatas = signal<GuiaItem[]>([]);
readonly totalCandidatas = signal(0);
readonly carregandoCandidatas = signal(false);
readonly filtroAplicado = signal(false);   // controla exibição do hint
readonly erro = signal('');
```

**`filtrar()`** — chama `GuiaService.listar` com `prestadorId`, `operadoraId`, `semRecurso: true` e todos os filtros ativos. Ao sucesso: `candidatas.set(result.itens)`, `totalCandidatas.set(result.total)`, `filtroAplicado.set(true)`.

**`adicionarTodas()`** — chama `RecursoService.adicionarGuiasLote` com filtros atuais + `prestadorId`/`operadoraId`. Após sucesso: rechamar `_carregar(id)` e `filtrar()` para atualizar ambas as listas.

**`adicionarGuia(guia)`** — chama `RecursoService.adicionarGuia`, remove da lista `candidatas` e rechamar `_carregar(id)`.

**Sem `DatePipe` nem `CurrencyPipe` no template.** Usar método de instância:

```typescript
formatarData(iso: string): string {
  return new Intl.DateTimeFormat('pt-BR').format(new Date(iso + 'T00:00:00'));
}
```

**Situação `<select>`** — usar `[selected]` nas `<option>`, nunca `[value]` no `<select>`.

### Estrutura de template (BEM classes)

```
.recurso-guias
  .recurso-guias__header
    .recurso-guias__header-info  (operadora, prestador, número)
    .recurso-guias__btn-pdf

  section.recurso-guias__secao
    h3.recurso-guias__secao-titulo  "Guias vinculadas"
    table.recurso-guias__tabela
      thead > tr > th  (Senha | Data | Beneficiário | Situação | Itens | —)
      tbody > @for guia
        tr.recurso-guias__linha-guia
          ... > button.recurso-guias__btn-remover
    @empty > p.recurso-guias__vazio

  section.recurso-guias__secao
    h3.recurso-guias__secao-titulo  "Adicionar guias"
    .recurso-guias__filtros
      input.recurso-guias__filtro-input  [senha]
      input.recurso-guias__filtro-input  [beneficiario]
      input.recurso-guias__filtro-input[type=date]  [dataInicio]
      input.recurso-guias__filtro-input[type=date]  [dataFim]
      select.recurso-guias__filtro-select  [situacao] (Apresentada/Liquidada)
      label.recurso-guias__filtro-toggle
        input[type=checkbox]  [somenteGlosa]
        "Só com glosa"
      button.recurso-guias__btn-filtrar  "Filtrar"

    @if !filtroAplicado()
      p.recurso-guias__hint  "Aplique filtros para buscar guias disponíveis."

    @if filtroAplicado() && candidatas().length > 0
      .recurso-guias__acoes-candidatas
        button.recurso-guias__btn-adicionar-todas  "Adicionar todas ({{ totalCandidatas() }})"

    @if filtroAplicado()
      table.recurso-guias__tabela-candidatas
        thead > tr > th  (Senha | Data | Beneficiário | Situação | Itens | —)
        tbody > @for candidata
          tr.recurso-guias__linha-candidata
            ... > button.recurso-guias__btn-adicionar
        @empty > p.recurso-guias__vazio  "Nenhuma guia encontrada."

  @if erro()
    p.recurso-guias__erro
```

### Critérios de pronto (RC-04)

- [ ] 9 casos passando em `recurso-guias.component.spec.ts`
- [ ] `DatePipe` ausente no template (sem importação de `DatePipe`)
- [ ] Nenhum `[value]` em `<select>` — apenas `[selected]` em `<option>`
- [ ] `pnpm -F admin-web test:ci` verde (cobertura ≥ 80%)
- [ ] `pnpm -F admin-web lint` e `pnpm -F admin-web stylelint` sem warnings

---

## Critérios de pronto globais

- [x] RC-01: 6 novos casos em `GuiaListTests`; filtros `OperadoraId`, `Senha`, `Beneficiario`, `SemRecurso`, `SomenteComGlosa` funcionais
- [x] RC-02: 4 novos casos em `RecursoCrudTests`; endpoint `POST /recursos/{id}/guias/lote` funcional
- [ ] RC-03: 3 casos em `recurso.service.spec`; bug `adicionarGuia` corrigido (URL com `guiaId` na path)
- [ ] RC-04: 9 casos em `recurso-guias.component.spec`; painel de filtros funcional; "Adicionar todas" usa lote server-side
- [ ] `dotnet test` cobertura ≥ 80% em `Faturamento.Tests`
- [ ] `pnpm -F admin-web test:ci` cobertura ≥ 80%
- [ ] Nenhum `DatePipe`/`CurrencyPipe` no `RecursoGuiasComponent`

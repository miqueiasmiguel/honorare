# SPEC F3.4 — Demonstrativos e Conciliação Manual

**Pré-requisito:** F3.3 concluído.
**Pós-condição:** Admin registra demonstrativo da operadora, vincula cada linha a um `ItemGuia` (settando `ValorLiquidado`) e, quando todos os itens da guia estão conciliados, a guia avança para `Liquidada`.

---

## Modelo de dados

```
Demonstrativo                         (nova entidade, ITenantEntity)
  Id, TenantId, OperadoraId (FK→Catalog, Restrict)
  Competencia  string  "AAAA-MM"      ex: "2025-12"
  DataRecebimento  DateOnly
  Observacao  string?

ItemDemonstrativo                     (nova entidade, cascade delete em DemonstrativoId)
  Id, DemonstrativoId
  Senha        string                 código de pré-autorização da guia
  CodigoTuss   string
  Descricao    string?
  ValorApresentado  decimal
  ValorPago         decimal
  ValorGlosado      decimal           = ValorApresentado − ValorPago (enforced on Create)
  MotivoGlosa  string?
  ItemGuiaId   Guid?   FK→ItemGuia   null = não conciliado; Restrict on delete

ItemGuia (existente) += SetValorLiquidado(decimal?)   ← setter já faltava
Guia     (existente) += Liquidar() / ReverterParaApresentada()
```

**Regra de auto-liquidação:** após cada conciliação, se **todos** os `ItemGuia` da guia tiverem `ValorLiquidado IS NOT NULL` → `Guia.Liquidar()`.
**Reversão:** ao desconciliar qualquer item, se guia estava `Liquidada` → `Guia.ReverterParaApresentada()`.

---

## Endpoints

```
POST   /api/v1/admin/demonstrativos
GET    /api/v1/admin/demonstrativos          ?operadoraId&competencia&pagina&itensPorPagina
GET    /api/v1/admin/demonstrativos/{id}     → com itens
PUT    /api/v1/admin/demonstrativos/{id}     → header apenas
DELETE /api/v1/admin/demonstrativos/{id}     → 409 se qualquer item conciliado

POST   /api/v1/admin/demonstrativos/{id}/itens
DELETE /api/v1/admin/demonstrativos/{id}/itens/{itemId}   → 409 se conciliado

POST   /api/v1/admin/demonstrativos/{id}/itens/{itemId}/conciliar
       Body: { itemGuiaId: Guid }
DELETE /api/v1/admin/demonstrativos/{id}/itens/{itemId}/conciliar
```

---

## Arquivos-chave

```
App/Faturamento/
  Demonstrativo.cs                    ← novo
  ItemDemonstrativo.cs                ← novo
  Configurations/
    DemonstrativoConfiguration.cs     ← novo
    ItemDemonstrativoConfiguration.cs ← novo
  Migrations/AddDemonstrativo         ← novo (+ .editorconfig)
  DemonstrativoService.cs             ← novo
  Endpoints/DemonstrativoEndpoints.cs ← novo
  Guia.cs                             ← adicionar Liquidar() / ReverterParaApresentada()
  ItemGuia.cs                         ← adicionar SetValorLiquidado(decimal?)

tests/Faturamento.Tests/
  DemonstrativoSchemaTests.cs         ← DM-01
  DemonstrativoCrudTests.cs           ← DM-02
  ConciliacaoTests.cs                 ← DM-03

apps/admin-web/src/app/admin/faturamento/
  demonstrativo/                      ← DM-04 + DM-05
    demonstrativo.types.ts
    demonstrativo.service.ts
    demonstrativo-list/
    demonstrativo-form/
    conciliacao/
```

---

## TASK-DM-01 — Schema: Demonstrativo + ItemDemonstrativo ✅

**TDD: testes de schema → entidades → migration → build.**

### O que fazer

1. `Demonstrativo.cs`:

   ```csharp
   internal sealed class Demonstrativo : ITenantEntity
   {
       public Guid Id { get; private set; }
       public Guid TenantId { get; private set; }
       public Guid OperadoraId { get; private set; }
       public string Competencia { get; private set; } = string.Empty;
       public DateOnly DataRecebimento { get; private set; }
       public string? Observacao { get; private set; }
       private Demonstrativo() { }
       internal static Demonstrativo Create(Guid tenantId, Guid operadoraId,
           string competencia, DateOnly dataRecebimento, string? observacao) { ... }
       internal void Atualizar(Guid operadoraId, string competencia,
           DateOnly dataRecebimento, string? observacao) { ... }
   }
   ```

2. `ItemDemonstrativo.cs`:

   ```csharp
   internal sealed class ItemDemonstrativo
   {
       public Guid Id { get; private set; }
       public Guid DemonstrativoId { get; private set; }
       public string Senha { get; private set; } = string.Empty;
       public string CodigoTuss { get; private set; } = string.Empty;
       public string? Descricao { get; private set; }
       public decimal ValorApresentado { get; private set; }
       public decimal ValorPago { get; private set; }
       public decimal ValorGlosado { get; private set; }
       public string? MotivoGlosa { get; private set; }
       public Guid? ItemGuiaId { get; private set; }
       private ItemDemonstrativo() { }
       internal static ItemDemonstrativo Create(...) { ... }  // ValorGlosado = ValorApresentado − ValorPago
       internal void Conciliar(Guid itemGuiaId) => ItemGuiaId = itemGuiaId;
       internal void Desconciliar() => ItemGuiaId = null;
   }
   ```

3. EF config:
   - `Demonstrativo`: `HasQueryFilter` por TenantId; FK OperadoraId `HasOne<Operadora>().WithMany()` Restrict
   - `ItemDemonstrativo`: cascade delete em `DemonstrativoId`; FK `ItemGuiaId` `HasOne<ItemGuia>().WithMany()` Restrict; `ValorGlosado` computed column não — armazenado (enforced in Create)

4. `ItemGuia.cs` — adicionar:

   ```csharp
   internal void SetValorLiquidado(decimal? valor) => ValorLiquidado = valor;
   ```

5. `Guia.cs` — adicionar:

   ```csharp
   internal void Liquidar() => Situacao = SituacaoGuia.Liquidada;
   internal void ReverterParaApresentada() => Situacao = SituacaoGuia.Apresentada;
   ```

6. Migration `AddDemonstrativo` + `.editorconfig` na pasta.

### Testes (`DemonstrativoSchemaTests.cs`)

```
[Fact] Demonstrativo_Persistido
  Criar Demonstrativo via ctx → ler → campos preservados, sem itens

[Fact] ItemDemonstrativo_ValorGlosado_Calculado
  ValorApresentado=1000, ValorPago=700 → ValorGlosado==300

[Fact] ItemDemonstrativo_SemConciliacao_ItemGuiaIdNulo
  Criar item → ItemGuiaId == null

[Fact] ItemDemonstrativo_CascadeDelete
  Excluir Demonstrativo → itens removidos

[Fact] ItemDemonstrativo_Restrict_NaoExcluiGuiaComItem
  Conciliar item → tentar excluir ItemGuia → DbUpdateException
```

**Critério de pronto:** `dotnet test` passa; `dotnet build` limpo; migration aplicada.

---

## TASK-DM-02 — CRUD Demonstrativo ✅

**TDD: testes → service → endpoints → build.**

### O que fazer

`DemonstrativoService`:

```csharp
internal sealed record CriarDemonstrativoCommand(
    Guid OperadoraId, string Competencia, DateOnly DataRecebimento, string? Observacao);

internal sealed record AtualizarDemonstrativoCommand(
    Guid OperadoraId, string Competencia, DateOnly DataRecebimento, string? Observacao);

internal sealed record AdicionarItemCommand(
    string Senha, string CodigoTuss, string? Descricao,
    decimal ValorApresentado, decimal ValorPago, string? MotivoGlosa);

internal sealed record DemonstrativoDto(
    Guid Id, Guid OperadoraId, string OperadoraNome,
    string Competencia, DateOnly DataRecebimento, string? Observacao,
    int TotalItens, int ItensConciliados, DateTimeOffset CriadoEm);

internal sealed record ItemDemonstrativoDto(
    Guid Id, string Senha, string CodigoTuss, string? Descricao,
    decimal ValorApresentado, decimal ValorPago, decimal ValorGlosado,
    string? MotivoGlosa, Guid? ItemGuiaId, bool Conciliado);

internal sealed record DemonstrativoDetalheDto(DemonstrativoDto Header,
    IReadOnlyList<ItemDemonstrativoDto> Itens);
```

Métodos: `CriarAsync`, `ListarAsync`, `ObterPorIdAsync`, `AtualizarAsync`, `ExcluirAsync`, `AdicionarItemAsync`, `RemoverItemAsync`.

Guards:

- `ExcluirAsync`: verificar `Itens.Any(i => i.ItemGuiaId != null)` → lançar `InvalidOperationException` (409)
- `RemoverItemAsync`: verificar `ItemGuiaId != null` → lançar `InvalidOperationException` (409)

Mapear `InvalidOperationException` para HTTP 409 no exception handler global.

`DemonstrativoEndpoints.cs`: registrar rotas (padrão dos outros endpoints do projeto).

### Testes (`DemonstrativoCrudTests.cs`)

```
[Fact] Criar_Persistido
[Fact] Listar_FiltroPorOperadora
[Fact] Listar_FiltroPorCompetencia
[Fact] Listar_PaginacaoFunciona
[Fact] Atualizar_CamposAtualizados
[Fact] Excluir_SemItens_Removido
[Fact] Excluir_ComItemConciliado_Lanca409
[Fact] AdicionarItem_Persistido
[Fact] RemoverItem_NaoConciliado_Removido
[Fact] RemoverItem_Conciliado_Lanca409
```

**Critério de pronto:** testes passam; build limpo.

---

## TASK-DM-03 — Conciliação + auto-liquidação

**TDD: testes E2E com Postgres → produção.**

### O que fazer

Adicionar a `DemonstrativoService`:

```csharp
internal sealed record ConciliarItemCommand(Guid ItemGuiaId);

internal async Task ConciliarItemAsync(
    Guid demonstrativoId, Guid itemDemId, ConciliarItemCommand cmd)
{
    // 1. Carregar ItemDemonstrativo (validar pertence ao tenant)
    // 2. Carregar ItemGuia (validar pertence ao tenant, via Guia)
    // 3. itemDem.Conciliar(cmd.ItemGuiaId)
    // 4. itemGuia.SetValorLiquidado(itemDem.ValorPago)
    // 5. Carregar todos ItemGuia da Guia → todos com ValorLiquidado? → guia.Liquidar()
    // 6. SaveChanges
}

internal async Task DesconciliarItemAsync(Guid demonstrativoId, Guid itemDemId)
{
    // 1. Carregar ItemDemonstrativo + ItemGuia vinculado
    // 2. Carregar Guia do ItemGuia
    // 3. itemGuia.SetValorLiquidado(null)
    // 4. itemDem.Desconciliar()
    // 5. Se guia.Situacao == Liquidada → guia.ReverterParaApresentada()
    // 6. SaveChanges
}
```

Registrar rotas no `DemonstrativoEndpoints.cs`:

- `POST /{id}/itens/{itemId}/conciliar` → 204
- `DELETE /{id}/itens/{itemId}/conciliar` → 204

### Testes (`ConciliacaoTests.cs`) — PostgresContainerFixture

```
[Fact] Conciliar_SetaValorLiquidadoNoItem
  seed: demonstrativo com item (ValorPago=500), guia com 1 item
  → ConciliarItemAsync → ItemGuia.ValorLiquidado == 500m

[Fact] Conciliar_TodosItens_GuiaLiquidada
  guia com 2 itens → conciliar ambos → Guia.Situacao == Liquidada

[Fact] Conciliar_ItemParcial_GuiaContinuaApresentada
  guia com 2 itens → conciliar só 1 → Guia.Situacao == Apresentada

[Fact] Desconciliar_LimpaValorLiquidado
  conciliar → desconciliar → ItemGuia.ValorLiquidado == null

[Fact] Desconciliar_ReverteLiquidacao
  todos itens conciliados (Guia=Liquidada) → desconciliar 1 → Guia.Situacao == Apresentada

[Fact] Conciliar_ItemDeTenantDiferente_NaoEncontrado
  itemGuia de outro tenant → NotFound / InvalidOperation

[Fact] Conciliar_ItemJaConciliado_Substituivel
  conciliar com itemGuiaA → conciliar mesmo itemDem com itemGuiaB
  → ItemGuiaA.ValorLiquidado == null; ItemGuiaB.ValorLiquidado == itemDem.ValorPago
  (re-conciliação substitui a anterior)

[Fact] Conciliar_GlosaTotal_ValorPagoZero_ItemLiquidadoComZero
  ValorPago=0 (glosa total) → ItemGuia.ValorLiquidado == 0m (conciliado, não null)
```

**Nota sobre o teste de re-conciliação:** ao conciliar um `ItemDemonstrativo` já vinculado, o serviço deve primeiro desconciliar o anterior (limpar `ValorLiquidado` do `ItemGuia` anterior) antes de vincular ao novo.

**Critério de pronto:** todos os testes passam; build limpo.

---

## TASK-DM-04 — UI Angular: CRUD Demonstrativo

**TDD: testes Vitest → componentes → build.**

### O que fazer

1. `demonstrativo.types.ts`:

   ```ts
   export interface DemonstrativoForm {
     operadoraId: string;
     competencia: string;
     dataRecebimento: string;
     observacao: string | null;
   }
   export interface ItemDemonstrativoForm {
     senha: string;
     codigoTuss: string;
     descricao: string | null;
     valorApresentado: number;
     valorPago: number;
     motivoGlosa: string | null;
   }
   export interface DemonstrativoDto {
     id: string;
     operadoraId: string;
     operadoraNome: string;
     competencia: string;
     dataRecebimento: string;
     observacao: string | null;
     totalItens: number;
     itensConciliados: number;
     criadoEm: string;
   }
   export interface ItemDemonstrativoDto {
     id: string;
     senha: string;
     codigoTuss: string;
     descricao: string | null;
     valorApresentado: number;
     valorPago: number;
     valorGlosado: number;
     motivoGlosa: string | null;
     itemGuiaId: string | null;
     conciliado: boolean;
   }
   export interface DemonstrativoDetalheDto {
     header: DemonstrativoDto;
     itens: ItemDemonstrativoDto[];
   }
   ```

2. `DemonstrativoService` Angular: métodos `listar`, `obterPorId`, `criar`, `atualizar`, `excluir`, `adicionarItem`, `removerItem`. Todos com `error` handler.

3. `DemonstrativoListComponent`:
   - Tabela paginada; filtros por operadora e competência (signal + debounce 400 ms)
   - Badge "N/T conciliados"; botão "Conciliar" → `/admin/demonstrativos/:id/conciliar`; botão excluir com confirmação
   - Rota `/admin/demonstrativos`

4. `DemonstrativoFormComponent` (criar + editar):
   - Header fields (operadora, competencia, dataRecebimento, observacao)
   - Lista inline de itens: adicionar/remover (remover bloqueado se `conciliado`)
   - Cada item: senha, codigoTuss, descricao, valorApresentado, valorPago, motivoGlosa; `valorGlosado` calculado no template
   - Rota `/admin/demonstrativos/novo` e `/admin/demonstrativos/:id`

5. Sidebar: adicionar "Demonstrativos" em "Faturamento".

6. Regenerar cliente OpenAPI após implementar backend: `pnpm generate-api-client`.

### Testes

Arquivo: `demonstrativo-list.component.spec.ts` e `demonstrativo-form.component.spec.ts`

```
[it] lista exibe demonstrativos com badge de conciliação
[it] filtro por competencia dispara busca com debounce
[it] botão excluir desabilitado para itens com conciliados > 0
[it] form cria demonstrativo e navega para lista
[it] form carrega demonstrativo existente e preenche campos
[it] adicionar item inline exibe linha nova
[it] remover item não-conciliado remove da lista
[it] remover item conciliado mantém na lista (desabilitado)
[it] valorGlosado exibido = valorApresentado − valorPago
```

**Critério de pronto:** `pnpm -F admin-web test:ci` passa; `pnpm -F admin-web lint` sem warnings.

---

## TASK-DM-05 — UI Angular: Tela de Conciliação

**TDD: testes Vitest → componente → build.**

### O que fazer

Rota: `/admin/demonstrativos/:id/conciliar`

`ConciliacaoComponent`:

- **Painel esquerdo** — itens do demonstrativo:
  - Cada linha: senha, codigoTuss, valorApresentado, valorPago, valorGlosado, motivoGlosa
  - Badge "Conciliado" (verde) ou "Pendente" (amarelo)
  - Botão "Vincular" → abre busca; botão "Desvincular" (quando conciliado)

- **Busca de guias** (inline ou modal simples):
  - Campo de busca por senha da guia
  - Retorna itens da guia correspondente: prestador, data, codigoTuss, posicao, valorApurado
  - Botão "Vincular" em cada item → chama `POST .../conciliar`
  - Após vincular: atualiza estado local sem reload completo

- **Cabeçalho**: operadora, competência, progresso "X de Y itens conciliados"

- Erro em qualquer operação exibe mensagem inline (não bloqueia a tela).

### Testes

```
[it] exibe itens do demonstrativo
[it] item conciliado exibe badge verde e botão Desvincular
[it] item pendente exibe badge amarelo e botão Vincular
[it] progresso reflete contagem correta
[it] clicar Vincular emite evento com itemDemId
[it] clicar Desvincular chama desconciliar e atualiza estado
[it] erro na conciliação exibe mensagem inline
```

**Critério de pronto:** `pnpm -F admin-web test:ci` passa; `pnpm -F admin-web lint` sem warnings; fluxo funciona na tela.

---

## Resumo de entregáveis por task

| Task     | Backend | Frontend | Migration | Testes                          |
| -------- | ------- | -------- | --------- | ------------------------------- |
| DM-01 ✅ | ✓       | —        | ✓         | DemonstrativoSchemaTests        |
| DM-02    | ✓       | —        | —         | DemonstrativoCrudTests          |
| DM-03    | ✓       | —        | —         | ConciliacaoTests                |
| DM-04    | —       | ✓        | —         | demonstrativo-list/form.spec.ts |
| DM-05    | —       | ✓        | —         | conciliacao.component.spec.ts   |

**Após DM-05:** atualizar `PROXIMOS_PASSOS.md` marcando F3.4 como ✅.

# RC-09 — Remover módulo de demonstrativos e conciliação

## Decisão

Remove as entidades `Demonstrativo` / `ItemDemonstrativo` e todo o fluxo de conciliação.
O CSV da UNIMED passa a importar beneficiários/guias/itens e escrever `ValorLiquidado` +
`MotivoGlosa` diretamente em `ItemGuia` — sem staging.
Edição manual por item via endpoint PATCH.

**D-041** registrada na última sessão (ver Sessão 5 — Docs).

---

## Sessão 1 — Backend: remoção completa + importador reescrito + migration

**Objetivo:** ir de "módulo Demonstrativo existente" para `dotnet build` verde e migration gerada.
O build fica vermelho no meio — não encerre a sessão até o critério de saída.

### 1.1 Deletar entidades e configurações EF

```
App/Faturamento/Demonstrativo.cs
App/Faturamento/ItemDemonstrativo.cs
App/Faturamento/Configurations/DemonstrativoConfiguration.cs
App/Faturamento/Configurations/ItemDemonstrativoConfiguration.cs
```

### 1.2 Limpar AppDbContext.cs

Remover as duas linhas:

```csharp
public DbSet<Demonstrativo> Demonstrativos => Set<Demonstrativo>();
public DbSet<ItemDemonstrativo> ItensDemonstrativo => Set<ItemDemonstrativo>();
```

### 1.3 Deletar serviço e endpoints de demonstrativo

```
App/Faturamento/DemonstrativoService.cs
App/Faturamento/Endpoints/DemonstrativoEndpoints.cs
```

### 1.4 Limpar Program.cs

Remover:

```csharp
builder.Services.AddScoped<DemonstrativoService>();        // linha 47
builder.Services.AddScoped<ImportacaoDemonstrativoService>(); // linha 48
app.MapDemonstrativoEndpoints();                           // linha 187
```

Adicionar:

```csharp
builder.Services.AddScoped<ImportacaoGuiaCsvService>();
```

### 1.5 Adicionar `MotivoGlosa` em `ItemGuia.cs`

```csharp
public string? MotivoGlosa { get; private set; }
internal void SetMotivoGlosa(string? valor) => MotivoGlosa = valor?.Trim();
```

### 1.6 Mapear coluna em `ItemGuiaConfiguration.cs`

```csharp
builder.Property(i => i.MotivoGlosa)
    .HasColumnName("motivo_glosa")
    .HasMaxLength(200);
```

### 1.7 Renomear e reescrever o importador

Renomear arquivo: `ImportacaoDemonstrativoService.cs` → `ImportacaoGuiaCsvService.cs`
Renomear classe: `ImportacaoDemonstrativoService` → `ImportacaoGuiaCsvService`

**Preservar integralmente:**

- `ParsearCsv` (leitura, extração de `identificadorPagamento`, headers, mapeamento)
- Criação de `Beneficiario` se não existir
- Find-or-create de `Guia` por `(prestadorId, senha)`
- Find-or-update de `ItemGuia` por `(guiaId, procedimentoId, posicao)`
- Validação de funções desconhecidas e deflator faltante (D-038)
- `ExecutarCalculoAsync` e auto-liquidação

**Remover:**

- `db.Demonstrativos.AnyAsync(...)` — guarda de re-importação
- Criação de `Demonstrativo` e `ItemDemonstrativo`
- `db.ItensDemonstrativo.Add(itemDem)`

**Substituir** `itemDem.Conciliar(itemGuia.Id)` por:

```csharp
itemGuia.SetValorLiquidado(linha.Total);
itemGuia.SetMotivoGlosa(linha.CodGlosa);
```

Re-importar o mesmo CSV sobrescreve `ValorLiquidado` e `MotivoGlosa`. Comportamento intencional.

### 1.8 Expor `MotivoGlosa` nos DTOs de `GuiaService.cs`

No record `ItemGuiaDto` (~linha 46):

```csharp
string? MotivoGlosa
```

Nas projeções LINQ (~linhas 660 e 681):

```csharp
MotivoGlosa = i.MotivoGlosa,
```

### 1.9 Adicionar rotas em `GuiaEndpoints.cs`

```csharp
// importação CSV (migrado de DemonstrativoEndpoints)
g.MapPost("importar-csv", ImportarCsvAsync).DisableAntiforgery();

// edição manual de pagamento por item (handler implementado na Sessão 2)
g.MapPatch("{id:guid}/itens/{itemId:guid}/pagamento", AtualizarPagamentoItemAsync);
```

O handler `ImportarCsvAsync` é idêntico ao antigo, recebendo `ImportacaoGuiaCsvService`.
O handler `AtualizarPagamentoItemAsync` pode retornar `Results.StatusCode(501)` por ora.

### 1.10 Gerar migration

```bash
cd apps/backend/App
dotnet ef migrations add RemoveDemonstrativoAddMotivoGlosa \
  --output-dir Faturamento/Migrations \
  --namespace App.Faturamento.Migrations
```

Revisar o arquivo gerado — deve conter:

- `DropTable("item_demonstrativo")`
- `DropTable("demonstrativo")`
- `AddColumn<string>("motivo_glosa", table: "item_guia", nullable: true)`

Criar `App/Faturamento/Migrations/.editorconfig` se ainda não existir (ver referência em `App/Faturamento/Migrations/.editorconfig`):

```ini
[*.cs]
dotnet_diagnostic.IDE0005.severity = none
dotnet_diagnostic.IDE0161.severity = none
dotnet_diagnostic.CA1515.severity = none
dotnet_diagnostic.CA1861.severity = none
```

**Não aplicar ao banco agora** (D-022).

### Critério de saída

```bash
dotnet build apps/backend/Honorare.slnx   # zero warnings, zero errors
```

---

## Sessão 2 — Backend: TDD (adaptar testes de importação + implementar PATCH pagamento)

**Objetivo:** `dotnet test` verde com Faturamento ≥ 90% de cobertura.
Requer Sessão 1 concluída.

### 2.1 Adaptar testes de importação

Renomear arquivo: `ImportacaoDemonstrativoTests.cs` → `ImportacaoGuiaCsvTests.cs`

**Remover** todas as asserções sobre:

- `DemonstrativoId`
- `ItensDemonstrativo`
- contagem de `ItemDemonstrativo`

**Manter** cobertura de: CSV inválido, função desconhecida, deflator faltante, urgência (D-039),
criação de beneficiário, criação de guia, criação de item, auto-liquidação.

**Adicionar** novo caso (red → green):

```
Dado CSV com CodGlosa "CB" na linha
Quando importar
Então ItemGuia.MotivoGlosa == "CB"
```

### 2.2 Deletar arquivos de teste de demonstrativo

```
tests/Faturamento.Tests/Demonstrativo/DemonstrativoCrudTests.cs
tests/Faturamento.Tests/Demonstrativo/ConciliacaoTests.cs
tests/Faturamento.Tests/DemonstrativoSchemaTests.cs
```

### 2.3 Implementar PATCH pagamento (TDD)

Criar `tests/Faturamento.Tests/GuiaPagamentoTests.cs` com os casos abaixo (red → green → refactor):

```
PATCH { valorLiquidado: 100, motivoGlosa: "CB" }
  → ItemGuia.ValorLiquidado == 100
  → ItemGuia.MotivoGlosa == "CB"

Quando todos os itens da guia têm ValorLiquidado != null
  → guia.Situacao == Liquidada

PATCH { valorLiquidado: null }
  → guia.Situacao == Apresentada
```

Implementar em `GuiaService.cs`:

```csharp
Task<ItemGuiaDto> AtualizarPagamentoItemAsync(
    Guid guiaId, Guid itemId,
    decimal? valorLiquidado, string? motivoGlosa,
    CancellationToken ct)
```

Comportamento:

- Valida que a guia pertence ao tenant (query filter global já garante).
- Seta `ValorLiquidado` e `MotivoGlosa` no `ItemGuia`.
- Se `valorLiquidado == null` → limpa o campo; se guia estava `Liquidada` → reverte para `Apresentada`.
- Se todos os itens têm `ValorLiquidado != null` → chama `guia.Liquidar()`.

Substituir o stub 501 em `GuiaEndpoints.cs` pelo handler real.

Body esperado:

```json
{ "valorLiquidado": 150.0, "motivoGlosa": "CB" }
```

Rota: `PATCH /api/v1/admin/guias/{id}/itens/{itemId}/pagamento`

### Critério de saída

```bash
dotnet test apps/backend/Honorare.slnx   # verde, Faturamento ≥ 90%
```

---

## Sessão 3 — Frontend: remover módulo demonstrativo

**Objetivo:** eliminar todo rastro do módulo de demonstrativos do frontend.
Requer Sessão 1 concluída (tipos do backend já foram atualizados).

### 3.1 Deletar 16 arquivos

```
apps/admin-web/src/app/faturamento/demonstrativo.types.ts
apps/admin-web/src/app/faturamento/demonstrativo.service.ts
apps/admin-web/src/app/faturamento/demonstrativo-list/demonstrativo-list.component.ts
apps/admin-web/src/app/faturamento/demonstrativo-list/demonstrativo-list.component.scss
apps/admin-web/src/app/faturamento/demonstrativo-list/demonstrativo-list.component.spec.ts
apps/admin-web/src/app/faturamento/demonstrativo-form/demonstrativo-form.component.ts
apps/admin-web/src/app/faturamento/demonstrativo-form/demonstrativo-form.component.scss
apps/admin-web/src/app/faturamento/demonstrativo-form/demonstrativo-form.component.spec.ts
apps/admin-web/src/app/faturamento/conciliacao/conciliacao.component.ts
apps/admin-web/src/app/faturamento/conciliacao/conciliacao.component.scss
apps/admin-web/src/app/faturamento/conciliacao/conciliacao.component.spec.ts
apps/admin-web/src/app/faturamento/demonstrativos/importar-demonstrativo-modal/importar-demonstrativo-modal.component.ts
apps/admin-web/src/app/faturamento/demonstrativos/importar-demonstrativo-modal/importar-demonstrativo-modal.component.html
apps/admin-web/src/app/faturamento/demonstrativos/importar-demonstrativo-modal/importar-demonstrativo-modal.component.scss
apps/admin-web/src/app/faturamento/demonstrativos/importar-demonstrativo-modal/importar-demonstrativo-modal.component.spec.ts
```

(verificar se há um quarto arquivo na pasta `demonstrativos/` e deletar também)

### 3.2 `faturamento/faturamento.routes.ts`

Remover o export `demonstrativoRoutes` inteiro (rotas `/`, `/novo`, `/:id/conciliar`, `/:id`) e todos os imports de componentes demonstrativo.
Manter `faturamentoRoutes` e `recursoRoutes` intactos.

### 3.3 `admin/admin.routes.ts`

Remover o bloco:

```typescript
{
  path: 'demonstrativos',
  loadChildren: () =>
    import('./faturamento/faturamento.routes').then((m) => m.demonstrativoRoutes),
},
```

### 3.4 `admin/admin-shell.html`

Remover link de nav:

```html
<a class="admin-sidebar__link admin-sidebar__link--sub" routerLink="demonstrativos" routerLinkActive="active">Demonstrativos</a>
```

### 3.5 `dashboard/dashboard.html`

Subtítulo:

- De: `"Painel de administração — conciliação de pagamentos médicos."`
- Para: `"Painel de administração — controle de pagamentos médicos."`

### 3.6 `faturamento/guia.types.ts`

Adicionar `motivoGlosa: string | null` em `ItemGuiaItem` (~linha 22).

Remover `valorLiquidado?: number | null` de `ItemGuiaDisplay` (~linha 87) — campo duplicado, já existe em `ItemGuiaItem`.

Adicionar tipos migrados de `demonstrativo.types.ts`:

```typescript
export interface ResultadoImportacaoGuiaDto {
  identificadorPagamento: string;
  somenteValidar: boolean;
  guiasCriadas: number;
  guiasAtualizadas: number;
  itensCriados: number;
  itensAtualizados: number;
  itensIgnorados: number;
  beneficiariosCriados: number;
  guiasPrevistas: number;
  itensPrevistas: number;
  erros: ErroImportacaoDto[];
  alertas: AlertaImportacaoDto[];
}

export interface ErroImportacaoDto {
  linha: number;
  mensagem: string;
}
export interface AlertaImportacaoDto {
  linha: number;
  mensagem: string;
}
```

Não incluir `demonstrativoId` — não existe mais.

### 3.7 `faturamento/guia.service.ts`

Adicionar dois métodos:

```typescript
importarCsv(
  arquivo: File,
  prestadorId: string,
  operadoraId: string,
  somenteValidar: boolean,
): Observable<ResultadoImportacaoGuiaDto> {
  const form = new FormData();
  form.append('arquivo', arquivo);
  form.append('prestadorId', prestadorId);
  form.append('operadoraId', operadoraId);
  form.append('somenteValidar', String(somenteValidar));
  return this._http.post<ResultadoImportacaoGuiaDto>(
    '/api/v1/admin/guias/importar-csv', form);
}

atualizarPagamentoItem(
  guiaId: string,
  itemId: string,
  valorLiquidado: number | null,
  motivoGlosa: string | null,
): Observable<ItemGuiaItem> {
  return this._http.patch<ItemGuiaItem>(
    `/api/v1/admin/guias/${guiaId}/itens/${itemId}/pagamento`,
    { valorLiquidado, motivoGlosa });
}
```

### Critério de saída

```bash
pnpm -F admin-web lint       # zero warnings
pnpm -F admin-web test:ci    # verde, ≥ 80% cobertura
```

---

## Sessão 4 — Frontend: criar modal de importação CSV + edição inline de pagamento ✅

**Objetivo:** implementar as duas novas features de frontend.
Requer Sessão 3 concluída.

### 4.1 Criar `faturamento/guias/importar-csv-modal/` (4 arquivos)

Componente standalone com formulário:

- `prestadorId` (select — usar `[selected]` nas options, padrão do projeto)
- `operadoraId` (select — idem)
- `arquivo` (file input, `accept=".csv"`)
- `somenteValidar` (checkbox)

Carregar listas de prestadores e operadoras via `forkJoin` no `ngOnInit` (padrão do projeto — ver CLAUDE.md).

Ao submeter: chamar `GuiaService.importarCsv(...)`.

Exibir resultado:

- Guias criadas / atualizadas
- Itens criados / atualizados / ignorados
- Erros (lista com número da linha e mensagem)
- Alertas (idem)

Respeitar STYLES.md: `var(--color-*)`, `space(n)`, `@include text-*`.
Sem `dataRecebimento`, `competencia` ou `observacao` (existiam no modal antigo — não replicar).

Arquivo `.spec.ts` mínimo: renderiza formulário; ao submeter chama `importarCsv` com os valores corretos.

### 4.2 Adicionar botão "Importar CSV" em `guia-list`

Em `guia-list.component.ts` / `.html`: adicionar botão que abre o modal `importar-csv-modal`.
Seguir o padrão de abertura de modais já existente no componente.

### 4.3 Adicionar edição inline em `guia-form`

Na seção de itens da guia, adicionar por item:

- Campo `valorLiquidado` (number input, nullable) — usar `@include text-mono-value` (ver CLAUDE.md)
- Campo `motivoGlosa` (text input, nullable)

Ao sair do campo (blur): chamar `GuiaService.atualizarPagamentoItem(guiaId, itemId, valorLiquidado, motivoGlosa)`.

Exibir situação da guia atualizada após o PATCH (`Apresentada` / `Liquidada`).

Referência de padrão de inline edit: `RecursoGuiasComponent` (RC-08 implementou inline edit de `observacao` e `valorApurado`).

Atualizar spec de `guia-form`: inline edit chama `atualizarPagamentoItem` com os valores corretos.

### Critério de saída

```bash
pnpm -F admin-web lint
pnpm -F admin-web test:ci    # verde, ≥ 80% cobertura
```

Testar manualmente no browser: importar um CSV de teste; editar `valorLiquidado` de um item e confirmar que a situação da guia muda.

---

## Sessão 5 — Integração final + docs ✅

**Objetivo:** regenerar o cliente TypeScript, garantir cobertura global e atualizar a documentação.
Requer Sessões 1–4 concluídas.

### 5.1 Regenerar cliente TypeScript

```bash
pnpm generate-api-client
```

Verificar em `packages/api-contracts/` que o cliente gerado reflete:

- Nova rota `POST /guias/importar-csv`
- Nova rota `PATCH /guias/{id}/itens/{itemId}/pagamento`
- Campo `motivoGlosa` em `ItemGuiaItem`

Não editar o cliente gerado à mão.

### 5.2 Cobertura global

```bash
dotnet test apps/backend/Honorare.slnx   # Faturamento ≥ 90%
pnpm -F admin-web test:ci                # frontend ≥ 80%
```

Corrigir qualquer falha antes de avançar.

### 5.3 Criar D-041 em `docs/DECISOES.md`

> **D-041 — Módulo de Demonstrativo e Conciliação removido**
> O passo de matching `ItemDemonstrativo ↔ ItemGuia` não agrega valor quando a entrada é manual (D-003).
> `ValorLiquidado` e `MotivoGlosa` são escritos diretamente em `ItemGuia` — via importação CSV ou edição inline.
> Não reconstruir.

### 5.4 Atualizar `docs/DOMINIO.md`

- `Demonstrativo` → nota: "entidade removida em RC-09; substituído por edição direta em ItemGuia"
- `ItemDemonstrativo` → idem
- `Conciliação` → simplificar: "edição direta de ValorLiquidado por ItemGuia"
- `ItemGuia` → adicionar campo `MotivoGlosa`

### 5.5 Atualizar `docs/PROJETO.md`

Escopo item #4:

- De: "Conciliação com demonstrativos"
- Para: "Registro de valores pagos diretamente por item de guia (manual ou via importação CSV)"

### Critério de saída

Cliente gerado sem diff manual, testes verdes, docs atualizados.

---

## Resumo de arquivos por sessão

| Sessão | Ação   | Arquivo                                                                             |
| ------ | ------ | ----------------------------------------------------------------------------------- |
| 1      | DELETE | `Demonstrativo.cs`, `ItemDemonstrativo.cs`                                          |
| 1      | DELETE | `Configurations/DemonstrativoConfiguration.cs`, `ItemDemonstrativoConfiguration.cs` |
| 1      | DELETE | `DemonstrativoService.cs`, `Endpoints/DemonstrativoEndpoints.cs`                    |
| 1      | MODIFY | `AppDbContext.cs` — remover 2 DbSets                                                |
| 1      | MODIFY | `Program.cs` — trocar registros e endpoint                                          |
| 1      | RENAME | `ImportacaoDemonstrativoService` → `ImportacaoGuiaCsvService`                       |
| 1      | MODIFY | `ItemGuia.cs` — adicionar `MotivoGlosa` + `SetMotivoGlosa`                          |
| 1      | MODIFY | `ItemGuiaConfiguration.cs` — mapear `motivo_glosa`                                  |
| 1      | MODIFY | `GuiaService.cs` — adicionar `MotivoGlosa` em DTOs e projeções                      |
| 1      | MODIFY | `GuiaEndpoints.cs` — rota `importar-csv` + stub `pagamento`                         |
| 1      | ADD    | Migration `RemoveDemonstrativoAddMotivoGlosa`                                       |
| 2      | RENAME | `ImportacaoDemonstrativoTests` → `ImportacaoGuiaCsvTests`                           |
| 2      | DELETE | `DemonstrativoCrudTests.cs`, `ConciliacaoTests.cs`, `DemonstrativoSchemaTests.cs`   |
| 2      | ADD    | `GuiaPagamentoTests.cs`                                                             |
| 2      | MODIFY | `GuiaEndpoints.cs` — substituir stub por handler real                               |
| 2      | MODIFY | `GuiaService.cs` — método `AtualizarPagamentoItemAsync`                             |
| 3      | DELETE | 16 arquivos de demonstrativo (ver 3.1)                                              |
| 3      | MODIFY | `faturamento.routes.ts` — remover `demonstrativoRoutes`                             |
| 3      | MODIFY | `admin.routes.ts` — remover rota `demonstrativos`                                   |
| 3      | MODIFY | `admin-shell.html` — remover link nav                                               |
| 3      | MODIFY | `dashboard.html` — atualizar subtítulo                                              |
| 3      | MODIFY | `guia.types.ts` — `motivoGlosa`, tipos de importação                                |
| 3      | MODIFY | `guia.service.ts` — `importarCsv`, `atualizarPagamentoItem`                         |
| 4      | ADD    | `guias/importar-csv-modal/` (4 arquivos)                                            |
| 4      | MODIFY | `guia-list` — botão "Importar CSV"                                                  |
| 4      | MODIFY | `guia-form` — edição inline `valorLiquidado` + `motivoGlosa`                        |
| 5      | REGEN  | `pnpm generate-api-client`                                                          |
| 5      | MODIFY | `docs/DECISOES.md` — D-041                                                          |
| 5      | MODIFY | `docs/DOMINIO.md` — atualizar entidades removidas                                   |
| 5      | MODIFY | `docs/PROJETO.md` — atualizar escopo item #4                                        |

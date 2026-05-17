# SPEC F3.5 — Geração de Recurso (PDF)

**Pré-requisito:** F3.4 concluído.
**Pós-condição:** Admin cria um `Recurso`, seleciona guias com divergência, faz download do PDF pronto para envio à operadora. Guias incluídas ficam com `SituacaoGuia.EmRecurso`.

---

## Modelo de dados

```
Recurso                               (nova entidade, ITenantEntity)
  Id, TenantId
  OperadoraId   Guid   FK→Catalog Restrict
  PrestadorId   Guid   FK→Catalog Restrict
  Numero        string  "AAAAMM"  ex: "202602"   gerado de DataEmissao
  DataEmissao   DateOnly
  Observacao    string?
  CriadoEm      DateTimeOffset

Guia (existente) +=
  RecursoId  Guid?  FK→Recurso Restrict
  MarcarEmRecurso(Guid recursoId)  → Situacao = EmRecurso; RecursoId = recursoId
  RemoverDoRecurso()               → RecursoId = null; recalcula Situacao*
```

\* `RemoverDoRecurso`: se todos `ItemGuia` com `ValorLiquidado IS NOT NULL` → `Liquidada`; senão → `Apresentada`.

**Número automático:** `Numero = DataEmissao.ToString("yyyyMM")`. Não é unique — dois recursos no mesmo mês têm o mesmo código; é informativo, não chave.

---

## Endpoints

```
POST   /api/v1/admin/recursos
GET    /api/v1/admin/recursos           ?operadoraId&prestadorId&pagina&itensPorPagina
GET    /api/v1/admin/recursos/{id}      → com lista de guias vinculadas
PUT    /api/v1/admin/recursos/{id}      → header apenas (operadora, prestador, data, observacao)
DELETE /api/v1/admin/recursos/{id}      → 409 se possui guias vinculadas

POST   /api/v1/admin/recursos/{id}/guias
       Body: { guiaId: Guid }
       Guia deve pertencer ao tenant; situação muda para EmRecurso
       409 se guia já está em outro recurso

DELETE /api/v1/admin/recursos/{id}/guias/{guiaId}
       Remove guia do recurso; situação revertida (ver regra acima)

GET    /api/v1/admin/recursos/{id}/pdf
       Retorna application/pdf inline; nome sugerido: "RECURSO_{Numero}_{OperadoraNome}.pdf"
```

---

## PDF — estrutura

```
[Cabeçalho]
  Logo placeholder (espaço reservado) + Tenant.Name à direita

[Título — por recurso]
  {Prestador.Nome} - CRM {Prestador.RegistroProfissional} - RECURSO {OperadoraNome} {Numero}

[Por guia — repetir para cada guia]
  Linha resumo: Data {DataAtendimento} | Senha {Senha} | Carteira {Beneficiario.Carteira} |
                Paciente {Beneficiario.Nome} | Executor {PosicaoExecutorLabel}

  Tabela de itens:
    Colunas: Cód. TUSS | Descrição | % | PAGO | CORRETO
    Valores:
      %       = fator efetivo do cálculo*
      PAGO    = ItemGuia.ValorLiquidado (0 se null)
      CORRETO = ItemGuia.ValorApurado

  Linha de subtotal:
    Total PAGO = sum(ValorLiquidado ?? 0) por guia
    Total CORRETO = sum(ValorApurado ?? 0) por guia
    **RESTA PAGAR** = Total CORRETO − Total PAGO  (negrito, destaque)

  Observacao da guia (se preenchida) em vermelho, abaixo da tabela

[Totais finais — ao final do documento]
  Total geral PAGO | Total geral CORRETO | RESTA PAGAR total
```

\* `%` por item: produto dos `PassoCalculo.Fator` onde `Regra != "ValorBase"`. Se sem passos ou `EhPacote`, exibir `—`.

**Biblioteca:** QuestPDF (`QuestPDF` NuGet, community license). Registrar `QuestPDF.Infrastructure.Settings.License = LicenseType.Community` no `Program.cs`.

---

## Arquivos-chave

```
App/Faturamento/
  Recurso.cs                            ← novo
  Configurations/
    RecursoConfiguration.cs             ← novo
  Migrations/AddRecurso                 ← novo (+ .editorconfig)
  RecursoService.cs                     ← novo
  Pdf/RecursoPdfDocument.cs             ← novo (QuestPDF IDocument)
  Endpoints/RecursoEndpoints.cs         ← novo
  Guia.cs                               ← += RecursoId, MarcarEmRecurso, RemoverDoRecurso

tests/Faturamento.Tests/
  RecursoSchemaTests.cs                 ← RE-01
  RecursoCrudTests.cs                   ← RE-02
  RecursoPdfDataTests.cs                ← RE-03

apps/admin-web/src/app/admin/faturamento/
  recurso/
    recurso.types.ts
    recurso.service.ts
    recurso-list/
    recurso-form/                       ← RE-04
    recurso-guias/                      ← RE-05 (seleção de guias + download)
```

---

## TASK-RE-01 ✅ — Schema: Recurso + migration

**TDD: testes → entidades → migration → build.**

### O que fazer

1. `Recurso.cs`:

```csharp
internal sealed class Recurso : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OperadoraId { get; private set; }
    public Guid PrestadorId { get; private set; }
    public string Numero { get; private set; } = string.Empty;   // "AAAAMM"
    public DateOnly DataEmissao { get; private set; }
    public string? Observacao { get; private set; }
    public DateTimeOffset CriadoEm { get; private set; }

    private Recurso() { }

    internal static Recurso Create(Guid tenantId, Guid operadoraId, Guid prestadorId,
        DateOnly dataEmissao, string? observacao) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        OperadoraId = operadoraId,
        PrestadorId = prestadorId,
        Numero = dataEmissao.ToString("yyyyMM"),
        DataEmissao = dataEmissao,
        Observacao = observacao,
        CriadoEm = DateTimeOffset.UtcNow
    };

    internal void Atualizar(Guid operadoraId, Guid prestadorId,
        DateOnly dataEmissao, string? observacao)
    {
        OperadoraId = operadoraId;
        PrestadorId = prestadorId;
        DataEmissao = dataEmissao;
        Numero = dataEmissao.ToString("yyyyMM");
        Observacao = observacao;
    }
}
```

2. `RecursoConfiguration.cs`: `HasQueryFilter` por TenantId; FK OperadoraId e PrestadorId `Restrict`; tabela `recursos`.

3. `Guia.cs` — adicionar:

```csharp
public Guid? RecursoId { get; private set; }

internal void MarcarEmRecurso(Guid recursoId)
{
    RecursoId = recursoId;
    Situacao = SituacaoGuia.EmRecurso;
}

internal void RemoverDoRecurso(bool todosItensLiquidados)
{
    RecursoId = null;
    Situacao = todosItensLiquidados ? SituacaoGuia.Liquidada : SituacaoGuia.Apresentada;
}
```

4. `GuiaConfiguration.cs` — adicionar FK `RecursoId → Recurso` `Restrict`.

5. Migration `AddRecurso` + `.editorconfig` na pasta `Migrations/`.

### Testes (`RecursoSchemaTests.cs`) — PostgresContainerFixture

```
[Fact] Recurso_Persistido
  Criar Recurso → ler → campos preservados, Numero == "AAAAMM" correto

[Fact] Recurso_Numero_GeradoDaDataEmissao
  DataEmissao = 2026-02-15 → Numero == "202602"

[Fact] Guia_MarcarEmRecurso_MudaSituacao
  Guia Apresentada → MarcarEmRecurso → Situacao == EmRecurso, RecursoId preenchido

[Fact] Guia_RemoverDoRecurso_SemLiquidacao_VoltaApresentada
  Guia EmRecurso (itens sem ValorLiquidado) → RemoverDoRecurso(false) → Apresentada

[Fact] Guia_RemoverDoRecurso_Liquidada_VoltaLiquidada
  Guia EmRecurso (todos itens com ValorLiquidado) → RemoverDoRecurso(true) → Liquidada

[Fact] Recurso_Delete_Restrict_ComGuia
  Criar Recurso + vincular Guia → tentar excluir Recurso → DbUpdateException
```

**Critério de pronto:** `dotnet test` passa; `dotnet build` limpo; migration aplicada.

---

## TASK-RE-02 ✅ — CRUD Recurso (service + endpoints)

**TDD: testes → service → endpoints → build.**

### O que fazer

`RecursoService.cs`:

```csharp
internal sealed record CriarRecursoCommand(
    Guid OperadoraId, Guid PrestadorId, DateOnly DataEmissao, string? Observacao);

internal sealed record AtualizarRecursoCommand(
    Guid OperadoraId, Guid PrestadorId, DateOnly DataEmissao, string? Observacao);

internal sealed record RecursoDto(
    Guid Id, Guid OperadoraId, string OperadoraNome,
    Guid PrestadorId, string PrestadorNome, string? PrestadorRegistroProfissional,
    string Numero, DateOnly DataEmissao, string? Observacao,
    int TotalGuias, DateTimeOffset CriadoEm);

internal sealed record GuiaNoRecursoDto(
    Guid Id, string Senha, DateOnly DataAtendimento,
    string? BeneficiarioNome, string? BeneficiarioCarteira,
    SituacaoGuia Situacao, int TotalItens);

internal sealed record RecursoDetalheDto(RecursoDto Header, IReadOnlyList<GuiaNoRecursoDto> Guias);
```

Métodos:

- `CriarAsync`, `ListarAsync`, `ObterPorIdAsync`, `AtualizarAsync`, `ExcluirAsync`
- `AdicionarGuiaAsync(Guid recursoId, Guid guiaId)` — valida tenant, chama `guia.MarcarEmRecurso()`; 409 se `guia.RecursoId != null` (já em outro recurso)
- `RemoverGuiaAsync(Guid recursoId, Guid guiaId)` — verifica se todos os itens têm `ValorLiquidado`, chama `guia.RemoverDoRecurso(todosLiquidados)`

Guards:

- `ExcluirAsync`: `guias.Any()` → `InvalidOperationException` (409)

`RecursoEndpoints.cs`: registrar todas as rotas.

### Testes (`RecursoCrudTests.cs`) — PostgresContainerFixture

```
[Fact] Criar_Persistido
[Fact] Listar_FiltroPorOperadora
[Fact] Listar_FiltroPorPrestador
[Fact] Listar_PaginacaoFunciona
[Fact] Atualizar_CamposAtualizados_NumeroRecalculado
[Fact] Excluir_SemGuias_Removido
[Fact] Excluir_ComGuia_Lanca409
[Fact] AdicionarGuia_MudaSituacaoParaEmRecurso
[Fact] AdicionarGuia_JaEmOutroRecurso_Lanca409
[Fact] RemoverGuia_SemValorLiquidado_VoltaApresentada
[Fact] RemoverGuia_TodosLiquidados_VoltaLiquidada
```

**Critério de pronto:** testes passam; build limpo.

---

## TASK-RE-03 — Geração de PDF (QuestPDF)

**TDD: testes de dados → documento → endpoint → build.**

### O que fazer

1. Adicionar `QuestPDF` ao `App.csproj`:

```xml
<PackageReference Include="QuestPDF" Version="2025.*" />
```

2. `Program.cs` — antes de `builder.Build()`:

```csharp
QuestPDF.Infrastructure.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
```

3. `RecursoService.cs` — adicionar query de dados para o PDF:

```csharp
internal sealed record RecursoPdfData(
    string TenantName,
    string OperadoraNome,
    string PrestadorNome,
    string? PrestadorRegistroProfissional,
    string Numero,
    IReadOnlyList<GuiaPdfData> Guias);

internal sealed record GuiaPdfData(
    DateOnly DataAtendimento,
    string Senha,
    string? BeneficiarioNome,
    string? BeneficiarioCarteira,
    string PosicaoExecutorLabel,   // nome legível do PosicaoExecutor da primeira posição
    string? Observacao,
    IReadOnlyList<ItemPdfData> Itens);

internal sealed record ItemPdfData(
    string CodigoTuss,
    string Descricao,
    string FatorEfetivo,   // produto dos PassoCalculo.Fator exceto ValorBase, ou "—"
    decimal ValorPago,     // ValorLiquidado ?? 0
    decimal ValorApurado); // ValorApurado ?? 0

internal async Task<Result<RecursoPdfData>> ObterDadosPdfAsync(Guid id, CancellationToken ct)
{
    // JOIN: Recurso → Operadora, Prestador, Tenant
    //       Guias → Beneficiario (left join), ItemGuia → Procedimento, Calculo/PassoCalculo
    // Ordenar guias por DataAtendimento, itens por OrdemProcedimento
}
```

4. `Pdf/RecursoPdfDocument.cs` — implementa `IDocument`:

```csharp
internal sealed class RecursoPdfDocument(RecursoPdfData data) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;
    public void Compose(IDocumentContainer container) { ... }

    // Helpers:
    //   private void ComposeHeader(IContainer c)
    //   private void ComposeGuia(IContainer c, GuiaPdfData guia)
    //   private void ComposeItensTable(IContainer c, GuiaPdfData guia)
    //   private void ComposeTotaisFinais(IContainer c)
}
```

Layout: fonte padrão 9pt; tabela de itens com bordas finas; `RESTA PAGAR` em negrito; observação em vermelho (`#CC0000`).

5. `RecursoEndpoints.cs` — adicionar:

```csharp
g.MapGet("{id:guid}/pdf", GerarPdfAsync);

private static async Task<IResult> GerarPdfAsync(
    Guid id, RecursoService service, CancellationToken ct)
{
    var dados = await service.ObterDadosPdfAsync(id, ct);
    if (dados.IsFailure) return Results.NotFound();

    var doc = new RecursoPdfDocument(dados.Value!);
    var bytes = doc.GeneratePdf();
    var nome = $"RECURSO_{dados.Value!.Numero}_{dados.Value!.OperadoraNome}.pdf";
    return Results.File(bytes, "application/pdf", nome);
}
```

### Testes (`RecursoPdfDataTests.cs`) — PostgresContainerFixture

```
[Fact] ObterDadosPdf_RetornaTenantName
  Seed: recurso com tenant específico → TenantName == tenant.Name

[Fact] ObterDadosPdf_GuiaComItens_RetornaDadosCorretos
  1 guia, 2 itens → dados têm 1 guia com 2 itens, CodigoTuss correto

[Fact] ObterDadosPdf_ValorPago_NullVirazZero
  ItemGuia.ValorLiquidado = null → ItemPdfData.ValorPago == 0m

[Fact] ObterDadosPdf_FatorEfetivo_SemPassos_RetornaDash
  Guia sem cálculo (EhPacote) → FatorEfetivo == "—"

[Fact] ObterDadosPdf_FatorEfetivo_ComPassos_RetornaProduto
  Passos com fatores 0.7 e 0.5 → FatorEfetivo == "35%" (produto × 100)

[Fact] GerarPdf_NaoLancaExcecao
  Seed completo → GeneratePdf() retorna bytes.Length > 0

[Fact] ObterDadosPdf_RecursoDeOutroTenant_NotFound
```

**Critério de pronto:** testes passam; PDF gerado em dev contém as seções esperadas; build limpo.

---

## TASK-RE-04 — UI Angular: CRUD Recurso

**TDD: testes Vitest → componentes → build.**

### O que fazer

1. `recurso.types.ts`:

```ts
export interface RecursoForm {
  operadoraId: string;
  prestadorId: string;
  dataEmissao: string;
  observacao: string | null;
}
export interface RecursoDto {
  id: string;
  operadoraId: string;
  operadoraNome: string;
  prestadorId: string;
  prestadorNome: string;
  prestadorRegistroProfissional: string | null;
  numero: string;
  dataEmissao: string;
  observacao: string | null;
  totalGuias: number;
  criadoEm: string;
}
export interface GuiaNoRecursoDto {
  id: string;
  senha: string;
  dataAtendimento: string;
  beneficiarioNome: string | null;
  beneficiarioCarteira: string | null;
  situacao: string;
  totalItens: number;
}
export interface RecursoDetalheDto {
  header: RecursoDto;
  guias: GuiaNoRecursoDto[];
}
```

2. `RecursoService` Angular: `listar`, `obterPorId`, `criar`, `atualizar`, `excluir`. Todos com `error` handler.

3. `RecursoListComponent`:
   - Tabela paginada; filtros por operadora e prestador (signal + debounce 400 ms)
   - Badge com número de guias; botão "Gerenciar guias" → `/admin/recursos/:id/guias`; botão "PDF" → chama download; botão excluir
   - Rota `/admin/recursos`

4. `RecursoFormComponent` (criar + editar):
   - Campos: operadora (select), prestador (select), dataEmissao (date input), observacao (textarea)
   - Exibe `Numero` gerado (somente leitura, calculado no template: `dataEmissao | date:'yyyyMM'`)
   - Rota `/admin/recursos/novo` e `/admin/recursos/:id`

5. Sidebar: adicionar "Recursos" em "Faturamento".

6. Regenerar cliente OpenAPI: `pnpm generate-api-client`.

### Testes

```
[it] lista exibe recursos com badge de guias
[it] filtro por operadora dispara busca
[it] botão PDF dispara download via service
[it] form cria recurso e navega para lista
[it] form carrega recurso existente e preenche campos
[it] form exibe numero calculado da dataEmissao
```

**Critério de pronto:** `pnpm -F admin-web test:ci` passa; `pnpm -F admin-web lint` sem warnings.

---

## TASK-RE-05 — UI Angular: Seleção de guias + download PDF

**TDD: testes Vitest → componente → build.**

### O que fazer

Rota: `/admin/recursos/:id/guias`

`RecursoGuiasComponent`:

- **Cabeçalho:** operadora, prestador, número, botão "Baixar PDF" → `GET .../pdf` com `window.open` ou `<a download>`

- **Guias vinculadas** (lista):
  - Cada linha: senha, data, beneficiário, situação badge, total itens
  - Botão "Remover" por guia

- **Adicionar guias** (busca inline):
  - Campo de busca por senha
  - Endpoint `GET /api/v1/admin/guias?senha=...&situacao=Apresentada,Liquidada` — retorna guias disponíveis para o tenant (excluindo já EmRecurso)
  - Cada resultado: senha, data, prestador, beneficiário, total itens
  - Botão "Adicionar" → chama `POST .../guias` e atualiza lista

- Erro em qualquer operação exibe mensagem inline.

`RecursoService` Angular — adicionar: `adicionarGuia(recursoId, guiaId)`, `removerGuia(recursoId, guiaId)`, `baixarPdf(recursoId)`.

`baixarPdf`: faz `GET .../pdf` com `responseType: 'blob'`, cria URL temporária e dispara download.

### Testes

```
[it] exibe guias vinculadas ao recurso
[it] botão remover chama service e atualiza lista
[it] busca por senha retorna guias disponíveis
[it] botão adicionar chama service e adiciona à lista
[it] botão PDF chama baixarPdf do service
[it] erro ao adicionar exibe mensagem inline
[it] guia já EmRecurso não aparece nos resultados de busca
```

**Critério de pronto:** `pnpm -F admin-web test:ci` passa; `pnpm -F admin-web lint` sem warnings; fluxo completo funciona na tela.

---

## Resumo de entregáveis por task

| Task     | Backend | Frontend | Migration | Testes                          |
| -------- | ------- | -------- | --------- | ------------------------------- |
| RE-01 ✅ | ✓       | —        | ✓         | RecursoSchemaTests              |
| RE-02 ✅ | ✓       | —        | —         | RecursoCrudTests                |
| RE-03    | ✓ (PDF) | —        | —         | RecursoPdfDataTests             |
| RE-04    | —       | ✓        | —         | recurso-list/form.spec.ts       |
| RE-05    | —       | ✓        | —         | recurso-guias.component.spec.ts |

**Após RE-05:** atualizar `PROXIMOS_PASSOS.md` marcando F3.5 como ✅.

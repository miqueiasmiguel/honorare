# SPEC-F3.1 — Entrada manual de guia e controle de pagamentos

**Contexto:** F3.1 é a primeira feature do bounded context `Faturamento`. O admin entra guias de um médico (pagas e pendentes). A tela principal é o "Controle de Pagamentos" por prestador.

**Fronteiras explícitas:**

- NÃO invocar motor de cálculo (F3.2). `ItemGuia.ValorApurado` é nullable; só preenchido manualmente (quando `EhPacote = true`).
- NÃO criar entidade `Recurso` (F3.5).
- NÃO criar `IPricingRuleSet` nem interfaces especulativas (D-019).
- NÃO transicionar para `Liquidada` ou `EmRecurso` (F3.4 e F3.5). F3.1 cria apenas guias com `SituacaoGuia.Apresentada`.
- NÃO criar `Faturamento.csproj` separado — código vai em `App/Faturamento/` (mesmo padrão de `App/Catalog/`).

**Referências principais:**

- Padrão de entidade: `App/Catalog/Beneficiario.cs`
- Padrão de configuração EF: `App/Catalog/Configurations/BeneficiarioConfiguration.cs`
- Padrão de service: `App/Catalog/CatalogService.cs`
- Padrão de endpoints: `App/Catalog/Endpoints/CatalogEndpoints.cs`
- Padrão de testes backend: `tests/Catalog.Tests/Beneficiario/BeneficiarioCrudTests.cs`
- Padrão de testes Angular: `apps/admin-web/src/app/admin/catalog/beneficiarios/`
- Armadilhas Roslyn/Angular: ver memory (migration .editorconfig, fakeAsync, etc.)

---

## Modelo de dados

### Enums novos (em `App/Faturamento/`)

```csharp
// SituacaoGuia.cs
enum SituacaoGuia { Apresentada, Liquidada, EmRecurso }

// ViaAcesso.cs
enum ViaAcesso { Convencional, Videolaparoscopia, Endoscopica, Percutanea, NaoAplicavel }

// OrdemProcedimento.cs
enum OrdemProcedimento { Unico, Principal, SecundarioMesmaVia, SecundarioViaDiferente }

// Acomodacao.cs
enum Acomodacao { Enfermaria, Apartamento, Ambulatorial }
```

`PosicaoExecutor` já existe em `App/Catalog/PosicaoExecutor.cs` — apenas referenciar.

### Guia (ITenantEntity)

| Campo           | Tipo           | Restrições                      |
| --------------- | -------------- | ------------------------------- |
| Id              | Guid           | PK                              |
| TenantId        | Guid           | índice                          |
| PrestadorId     | Guid           | FK → Prestador (sem cascade)    |
| OperadoraId     | Guid           | FK → Operadora (sem cascade)    |
| BeneficiarioId  | Guid           | FK → Beneficiario (sem cascade) |
| Senha           | string(30)     | required                        |
| DataAtendimento | DateOnly       | required                        |
| Situacao        | SituacaoGuia   | default Apresentada             |
| EhPacote        | bool           | default false                   |
| Observacao      | string(2000)   | required, pode ser vazio        |
| CriadoEm        | DateTimeOffset |                                 |
| AtualizadoEm    | DateTimeOffset |                                 |

DB table: `guias`

### ItemGuia (sem ITenantEntity — isolamento via Guia)

| Campo             | Tipo              | Restrições                      |
| ----------------- | ----------------- | ------------------------------- |
| Id                | Guid              | PK                              |
| GuiaId            | Guid              | FK → Guia (cascade delete)      |
| ProcedimentoId    | Guid              | FK → Procedimento (sem cascade) |
| PosicaoExecutor   | PosicaoExecutor   | required                        |
| OrdemProcedimento | OrdemProcedimento | required                        |
| ViaAcesso         | ViaAcesso         | required                        |
| Acomodacao        | Acomodacao        | required                        |
| EhUrgencia        | bool              | default false                   |
| ValorApurado      | decimal?          | null; required quando EhPacote  |
| ValorLiquidado    | decimal?          | null até F3.4                   |
| CriadoEm          | DateTimeOffset    |                                 |

DB table: `itens_guia`

Todos os enums armazenados como string (`.HasConversion<string>()`).

---

## Tasks

### TASK-F31-01 — Entidades, configurações EF e migration `AddGuias`

**Status:** [x] concluída

**Arquivos a criar:**

- `App/Faturamento/SituacaoGuia.cs`
- `App/Faturamento/ViaAcesso.cs`
- `App/Faturamento/OrdemProcedimento.cs`
- `App/Faturamento/Acomodacao.cs`
- `App/Faturamento/Guia.cs`
- `App/Faturamento/ItemGuia.cs`
- `App/Faturamento/Configurations/GuiaConfiguration.cs`
- `App/Faturamento/Configurations/ItemGuiaConfiguration.cs`
- `App/Faturamento/Migrations/` (gerado via `dotnet ef`)
- `App/Faturamento/Migrations/.editorconfig` (suprimir IDE0005/IDE0161/CA1515/CA1861)
- `App/Data/AppDbContext.cs` — adicionar `DbSet<Guia>` e `DbSet<ItemGuia>`

**Padrão de referência:** `App/Catalog/Beneficiario.cs` + `App/Catalog/Configurations/BeneficiarioConfiguration.cs`

**Test cases (Red em `tests/Faturamento.Tests/Guia/GuiaSchemaTests.cs`):**

- Schema cria tabela `guias` com colunas corretas
- Schema cria tabela `itens_guia` com FK para `guias`
- `ItemGuia` é deletado em cascade quando `Guia` é deletada
- Global query filter isola `Guia` por `TenantId` (tenant A não vê guia do tenant B)

**Pronto quando:** `dotnet test --filter "GuiaSchemaTests"` passa (4 testes).

**Não fazer:** criar `GuiaService`, endpoints, seed data, soft-delete.

**Observações:**

- `Guia.Factory`: `internal static Guia Create(Guid tenantId, Guid prestadorId, Guid operadoraId, Guid beneficiarioId, string senha, DateOnly dataAtendimento, bool ehPacote, string observacao)`
- `ItemGuia.Factory`: `internal static ItemGuia Create(Guid guiaId, Guid procedimentoId, PosicaoExecutor posicao, OrdemProcedimento ordem, ViaAcesso via, Acomodacao acomodacao, bool ehUrgencia, decimal? valorApurado)`
- Migration via: `dotnet ef migrations add AddGuias --project App --context AppDbContext --output-dir Faturamento/Migrations`

---

### TASK-F31-02 — GuiaService: criar, listar, obter, atualizar, excluir

**Status:** [ ] pendente  
**Depende de:** TASK-F31-01

**Arquivos a criar/modificar:**

- `App/Faturamento/GuiaService.cs` (novo)
- `tests/Faturamento.Tests/Guia/GuiaCrudTests.cs` (novo)
- `tests/Faturamento.Tests/Guia/GuiaListTests.cs` (novo)

**Records/DTOs (topo de `GuiaService.cs`):**

```csharp
// Comandos
record CriarGuiaCommand(Guid PrestadorId, Guid OperadoraId, Guid BeneficiarioId,
    string Senha, DateOnly DataAtendimento, bool EhPacote, string Observacao,
    IReadOnlyList<CriarItemGuiaCommand> Itens);

record CriarItemGuiaCommand(Guid ProcedimentoId, PosicaoExecutor PosicaoExecutor,
    OrdemProcedimento OrdemProcedimento, ViaAcesso ViaAcesso, Acomodacao Acomodacao,
    bool EhUrgencia, decimal? ValorApurado);

record AtualizarGuiaCommand(Guid OperadoraId, Guid BeneficiarioId, string Senha,
    DateOnly DataAtendimento, bool EhPacote, string Observacao,
    IReadOnlyList<CriarItemGuiaCommand> Itens);

// Queries
record ListarGuiasQuery(Guid? PrestadorId, DateOnly? DataInicio, DateOnly? DataFim,
    SituacaoGuia? Situacao, int Pagina, int ItensPorPagina);

// DTOs
record GuiaDto(Guid Id, Guid PrestadorId, string PrestadorNome,
    Guid OperadoraId, string OperadoraNome, Guid BeneficiarioId,
    string BeneficiarioNome, string BeneficiarioCarteira, string Senha,
    DateOnly DataAtendimento, SituacaoGuia Situacao, bool EhPacote,
    string Observacao, int TotalItens, DateTimeOffset CriadoEm, DateTimeOffset AtualizadoEm);

record ItemGuiaDto(Guid Id, Guid ProcedimentoId, string CodigoTuss, string DescricaoProcedimento,
    PosicaoExecutor PosicaoExecutor, OrdemProcedimento OrdemProcedimento,
    ViaAcesso ViaAcesso, Acomodacao Acomodacao, bool EhUrgencia,
    decimal? ValorApurado, decimal? ValorLiquidado);

record GuiaDetalheDto(/* todos os campos de GuiaDto */ IReadOnlyList<ItemGuiaDto> Itens);

record ListarGuiasResult(IReadOnlyList<GuiaDto> Itens, int Total, int Pagina, int ItensPorPagina);
```

**Métodos do service:**

- `CriarAsync(CriarGuiaCommand, CancellationToken)` → `Task<Result<GuiaDetalheDto>>`
- `ListarAsync(ListarGuiasQuery, CancellationToken)` → `Task<ListarGuiasResult>`
- `ObterPorIdAsync(Guid, CancellationToken)` → `Task<Result<GuiaDetalheDto>>`
- `AtualizarAsync(Guid, AtualizarGuiaCommand, CancellationToken)` → `Task<Result<GuiaDetalheDto>>`
- `ExcluirAsync(Guid, CancellationToken)` → `Task<Result>`

**Test cases (Red primeiro):**

`GuiaCrudTests`:

- `Criar_ComDadosValidos_RetornaGuiaDetalheDtoAsync`
- `Criar_SemItens_RetornaValidationErrorAsync`
- `Criar_EhPacoteItemSemValorApurado_RetornaValidationErrorAsync`
- `Criar_PrestadorInexistente_RetornaNotFoundAsync`
- `Criar_OperadoraInexistente_RetornaNotFoundAsync`
- `Criar_BeneficiarioInexistente_RetornaNotFoundAsync`
- `Atualizar_GuiaInexistente_RetornaNotFoundAsync`
- `Atualizar_SubstituiTodosItensAsync`
- `Excluir_GuiaInexistente_RetornaNotFoundAsync`
- `Excluir_RemoveGuiaEItensAsync`
- `ObterPorId_TenantDiferente_RetornaNotFoundAsync` (isolamento)

`GuiaListTests`:

- `Listar_FiltraPorPrestadorIdAsync`
- `Listar_FiltraPorPeriodoAsync`
- `Listar_FiltraPorSituacaoAsync`
- `Listar_PaginacaoCorretaAsync`
- `Listar_SoRetornaGuiasDoPropriTenantAsync`

**Pronto quando:** `dotnet test --filter "GuiaCrudTests|GuiaListTests"` passa (16 testes).

**Não fazer:** endpoints REST, Angular, motor de cálculo, transição de situação.

**Observação:** `AtualizarAsync` deve deletar todos os `ItemGuia` existentes e inserir novos (replace completo, mesma estratégia de `CatalogService` para deflatores).

---

### TASK-F31-03 — GuiaEndpoints REST e testes de endpoint

**Status:** [ ] pendente  
**Depende de:** TASK-F31-02

**Arquivos a criar/modificar:**

- `App/Faturamento/Endpoints/GuiaEndpoints.cs` (novo)
- `App/Program.cs` — registrar `app.MapGuiaEndpoints()`
- `tests/Faturamento.Tests/Guia/GuiaEndpointTests.cs` (novo)

**Endpoints:**

```
GET    /api/v1/admin/guias               → ListarAsync (query params: prestadorId, dataInicio, dataFim, situacao, pagina, itensPorPagina)
GET    /api/v1/admin/guias/{id:guid}     → ObterPorIdAsync
POST   /api/v1/admin/guias               → CriarAsync (body: CriarGuiaCommand)
PUT    /api/v1/admin/guias/{id:guid}     → AtualizarAsync (body: AtualizarGuiaCommand)
DELETE /api/v1/admin/guias/{id:guid}     → ExcluirAsync
```

Todos com `.RequireAuthorization("TenantAccess")`.

**Padrão de referência:** `App/Catalog/Endpoints/CatalogEndpoints.cs` (handlers no mesmo arquivo, mesma convenção `Async` suffix, `[AsParameters]` para query).

**Test cases (Red primeiro em `GuiaEndpointTests`):**

- `GET /guias` retorna 200 com paginação
- `GET /guias/{id}` retorna 200 com itens
- `GET /guias/{id}` guia inexistente retorna 404
- `POST /guias` com body válido retorna 201 + Location
- `POST /guias` sem itens retorna 400
- `PUT /guias/{id}` atualiza e retorna 200
- `DELETE /guias/{id}` retorna 204
- `DELETE /guias/{id}` inexistente retorna 404
- Acesso sem JWT retorna 401

**Pronto quando:** `dotnet test --filter "GuiaEndpointTests"` passa (9 testes).

**Não fazer:** Angular, campos além do modelo definido.

---

### TASK-F31-04 — CatalogService: bloquear delete com Guia associada

**Status:** [ ] pendente  
**Depende de:** TASK-F31-01

**Arquivos a modificar:**

- `App/Catalog/CatalogService.cs` — implementar TODOs de delete guard
- `tests/Catalog.Tests/Operadora/OperadoraCrudTests.cs` — adicionar teste de conflito
- `tests/Catalog.Tests/Procedimento/ProcedimentoCrudTests.cs` — adicionar teste de conflito
- `tests/Catalog.Tests/Prestador/PrestadorCrudTests.cs` — adicionar teste de conflito

**Regras:**

- `ExcluirOperadoraAsync(id)`: se `_db.Set<Guia>().Any(g => g.OperadoraId == id)` → `Result.Failure(new ConflictError("Operadora possui guias associadas."))`
- `ExcluirProcedimentoAsync(id)`: se `_db.Set<ItemGuia>().Any(i => i.ProcedimentoId == id)` → `ConflictError`
- `ExcluirPrestadorAsync(id)`: se `_db.Set<Guia>().Any(g => g.PrestadorId == id)` → `ConflictError`

**Test cases (Red primeiro):**

- `ExcluirOperadora_ComGuiaAssociada_RetornaConflictErrorAsync`
- `ExcluirProcedimento_ComGuiaAssociada_RetornaConflictErrorAsync`
- `ExcluirPrestador_ComGuiaAssociada_RetornaConflictErrorAsync`

**Pronto quando:** 3 novos testes passam; testes existentes de delete ainda passam.

**Não fazer:** bloquear por outras entidades não mencionadas.

---

### TASK-F31-05 — Types TS e GuiaService Angular

**Status:** [ ] pendente  
**Depende de:** TASK-F31-03

**Arquivos a criar/modificar:**

- `apps/admin-web/src/app/admin/faturamento/guia.types.ts` (novo)
- `apps/admin-web/src/app/admin/faturamento/guia.service.ts` (novo)
- `apps/admin-web/src/app/admin/faturamento/guia.service.spec.ts` (novo)

**Padrão de referência:** `apps/admin-web/src/app/admin/catalog/catalog.types.ts` + `catalog.service.ts`

**Types principais:**

```typescript
type SituacaoGuia = "Apresentada" | "Liquidada" | "EmRecurso";
type ViaAcesso = "Convencional" | "Videolaparoscopia" | "Endoscopica" | "Percutanea" | "NaoAplicavel";
type OrdemProcedimento = "Unico" | "Principal" | "SecundarioMesmaVia" | "SecundarioViaDiferente";
type Acomodacao = "Enfermaria" | "Apartamento" | "Ambulatorial";
// PosicaoExecutor já existe em catalog.types.ts — importar de lá

interface ItemGuiaItem {
  id;
  procedimentoId;
  codigoTuss;
  descricaoProcedimento;
  posicaoExecutor;
  ordemProcedimento;
  viaAcesso;
  acomodacao;
  ehUrgencia;
  valorApurado: number | null;
  valorLiquidado: number | null;
}

interface GuiaItem {
  id;
  prestadorId;
  prestadorNome;
  operadoraId;
  operadoraNome;
  beneficiarioId;
  beneficiarioNome;
  beneficiarioCarteira;
  senha;
  dataAtendimento;
  situacao: SituacaoGuia;
  ehPacote;
  observacao;
  totalItens;
  criadoEm;
  atualizadoEm;
}

interface GuiaDetalheItem extends GuiaItem {
  itens: ItemGuiaItem[];
}

interface ListarGuiasParams {
  prestadorId?: string;
  dataInicio?: string;
  dataFim?: string;
  situacao?: SituacaoGuia;
  pagina: number;
  itensPorPagina: number;
}

interface ListarGuiasResult {
  itens: GuiaItem[];
  total;
  pagina;
  itensPorPagina;
}

interface CriarItemGuiaPayload {
  procedimentoId;
  posicaoExecutor;
  ordemProcedimento;
  viaAcesso;
  acomodacao;
  ehUrgencia;
  valorApurado: number | null;
}

interface CriarGuiaPayload {
  prestadorId;
  operadoraId;
  beneficiarioId;
  senha;
  dataAtendimento;
  ehPacote;
  observacao;
  itens: CriarItemGuiaPayload[];
}

interface AtualizarGuiaPayload extends Omit<CriarGuiaPayload, "prestadorId"> {}
```

**Métodos do service:**

- `listar(params)`, `obterPorId(id)`, `criar(payload)`, `atualizar(id, payload)`, `excluir(id)`

**Test cases (Red primeiro em `guia.service.spec.ts`):**

- `listar` chama `GET /api/v1/admin/guias` com params corretos
- `criar` chama `POST /api/v1/admin/guias`
- `atualizar` chama `PUT /api/v1/admin/guias/:id`
- `excluir` chama `DELETE /api/v1/admin/guias/:id`

**Pronto quando:** `pnpm -F admin-web test:ci` passa.

**Não fazer:** componentes Angular, rotas.

---

### TASK-F31-06 — ItemGuiaFormComponent

**Status:** [ ] pendente  
**Depende de:** TASK-F31-05

**Arquivos a criar:**

- `apps/admin-web/src/app/admin/faturamento/guia-form/item-guia-form/item-guia-form.component.ts`
- `apps/admin-web/src/app/admin/faturamento/guia-form/item-guia-form/item-guia-form.component.spec.ts`
- `apps/admin-web/src/app/admin/faturamento/guia-form/item-guia-form/item-guia-form.component.scss`

**Comportamento:**

- Input: `ehPacote: InputSignal<boolean>`, `item: InputSignal<CriarItemGuiaPayload | null>` (null = novo)
- Output: `itemChange: OutputEmitterRef<CriarItemGuiaPayload | null>` (null = cancelar/remover)
- Autocomplete de procedimento TUSS: campo texto debounce 300ms → `CatalogService.listarProcedimentos()` → seleciona → preenche `procedimentoId`
- Selects para: `posicaoExecutor`, `ordemProcedimento`, `viaAcesso`, `acomodacao`
- Checkbox `ehUrgencia`
- Campo `valorApurado`: visível e obrigatório somente quando `ehPacote() === true`

**Test cases (Red primeiro):**

- Renderiza campos básicos (posicao, ordem, via, acomodacao, urgencia)
- Campo `valorApurado` oculto quando `ehPacote = false`
- Campo `valorApurado` visível e required quando `ehPacote = true`
- Emite `itemChange` com valores preenchidos ao submeter

**Padrão de referência:** `apps/admin-web/src/app/admin/catalog/beneficiarios/beneficiario-autocomplete/` (padrão de autocomplete + signal inputs)

**Pronto quando:** `pnpm -F admin-web test:ci` passa.

**Não fazer:** lista de itens completa (isso vai no GuiaFormComponent), integração com formulário pai.

---

### TASK-F31-07 — GuiaFormComponent (criar / editar)

**Status:** [ ] pendente  
**Depende de:** TASK-F31-06

**Arquivos a criar:**

- `apps/admin-web/src/app/admin/faturamento/guia-form/guia-form.component.ts`
- `apps/admin-web/src/app/admin/faturamento/guia-form/guia-form.component.spec.ts`
- `apps/admin-web/src/app/admin/faturamento/guia-form/guia-form.component.scss`

**Comportamento:**

- Rota `/admin/guias/nova` → modo criar (sem `id`)
- Rota `/admin/guias/:id` → modo editar (carrega guia, preenche form)
- Campos: `prestadorId` (select, lista prestadores ativos), `operadoraId` (select, lista operadoras ativas), `BeneficiarioAutocompleteComponent` (já existe em `catalog/beneficiarios/`), `senha`, `dataAtendimento` (date input), checkbox `ehPacote`, `observacao` (textarea)
- Lista de itens: botão "Adicionar item" abre `ItemGuiaFormComponent` inline; cada item exibido em linha com botão remover; mínimo 1 item validado no submit
- Submit: chama `GuiaService.criar` ou `.atualizar`; em sucesso navega para `/admin/guias`
- Botão cancelar: navega para `/admin/guias`

**Test cases (Red primeiro):**

- Renderiza campos obrigatórios
- Submit sem itens mostra erro de validação (não chama service)
- Em modo editar, carrega dados da guia no form (mockar `GuiaService.obterPorId`)
- Submit com dados válidos chama `GuiaService.criar` e navega para `/admin/guias`

**Pronto quando:** `pnpm -F admin-web test:ci` passa.

**Não fazer:** upload de arquivo, campos fora do modelo definido, situação editável (sempre `Apresentada` na criação).

---

### TASK-F31-08 — GuiaListComponent (Controle de Pagamentos)

**Status:** [ ] pendente  
**Depende de:** TASK-F31-05

**Arquivos a criar:**

- `apps/admin-web/src/app/admin/faturamento/guia-list/guia-list.component.ts`
- `apps/admin-web/src/app/admin/faturamento/guia-list/guia-list.component.spec.ts`
- `apps/admin-web/src/app/admin/faturamento/guia-list/guia-list.component.scss`

**Comportamento:**

- Tabela paginada de guias do tenant
- Filtros: select `prestadorId`, date range `dataInicio/dataFim`, select `situacao`
- Color-coding por situação: `Apresentada` → amarelo (`--color-alerta`), `Liquidada` → verde (`--color-sucesso`), `EmRecurso` → vermelho (`--color-perigo`)
- Colunas: Data, Prestador, Operadora, Beneficiário, Carteira, Senha, Situação, Nº Itens, Ações
- Ações por linha: ícone editar → `/admin/guias/:id`, ícone excluir → confirmação → `GuiaService.excluir` → recarregar lista
- Botão "Nova Guia" → `/admin/guias/nova`
- Valores monetários: `@include text-mono-value` (tabular nums)

**Test cases (Red primeiro):**

- Renderiza tabela com guias (mockar `GuiaService.listar`)
- Linha `Apresentada` tem classe CSS correta
- Linha `Liquidada` tem classe CSS correta
- Linha `EmRecurso` tem classe CSS correta
- Clicar "Nova Guia" navega para `/admin/guias/nova`
- Filtro por situação recarrega lista com parâmetro correto

**Padrão de referência:** `apps/admin-web/src/app/admin/catalog/beneficiarios/beneficiario-list/beneficiario-list.component.ts`

**Pronto quando:** `pnpm -F admin-web test:ci` passa.

**Não fazer:** exportação CSV, colunas de valores financeiros (ValorApurado/ValorLiquidado aparecem em F3.4+).

---

### TASK-F31-09 — Rotas, sidebar e smoke test

**Status:** [ ] pendente  
**Depende de:** TASK-F31-07, TASK-F31-08

**Arquivos a criar/modificar:**

- `apps/admin-web/src/app/admin/faturamento/faturamento.routes.ts` (novo)
- `apps/admin-web/src/app/admin/admin.routes.ts` — adicionar `path: 'guias', loadChildren: faturamento.routes`
- `apps/admin-web/src/app/admin/admin-shell.ts` — adicionar entrada "Controle de Pagamentos" na sidebar

**Rotas:**

```typescript
{ path: '', component: GuiaListComponent },
{ path: 'nova', loadComponent: GuiaFormComponent },
{ path: ':id', loadComponent: GuiaFormComponent },
```

Acessível em `/admin/guias/`.

**Sidebar:** nova seção "Faturamento" com item "Controle de Pagamentos" → `/admin/guias`.

**Test cases (Red primeiro em `faturamento.routes.spec.ts`):**

- Rota `/guias` resolve para `GuiaListComponent`
- Rota `/guias/nova` resolve para `GuiaFormComponent`

**Pronto quando:** `pnpm -F admin-web test:ci` passa + smoke test manual: nav para `/admin/guias`, criar guia, editar, excluir.

**Não fazer:** proteger rotas por role separada (já protegido pelo `adminGuard` do shell).

---

## Checklist de entrega

- [x] TASK-F31-01 — schema + migration
- [ ] TASK-F31-02 — GuiaService (16 testes)
- [ ] TASK-F31-03 — GuiaEndpoints (9 testes)
- [ ] TASK-F31-04 — delete guards CatalogService (3 testes)
- [ ] TASK-F31-05 — types TS + GuiaService Angular (4 testes)
- [ ] TASK-F31-06 — ItemGuiaFormComponent (4 testes)
- [ ] TASK-F31-07 — GuiaFormComponent (4 testes)
- [ ] TASK-F31-08 — GuiaListComponent (6 testes)
- [ ] TASK-F31-09 — rotas + sidebar (2 testes + smoke manual)
- [ ] `dotnet test` passa (cobertura ≥ 80% em Faturamento.Tests)
- [ ] `pnpm -F admin-web test:ci` passa
- [ ] `pnpm -F admin-web lint` zero warnings
- [ ] Smoke manual: criar guia completa (com itens), editar, excluir, filtrar por prestador

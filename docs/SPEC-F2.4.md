# SPEC-F2.4 — Beneficiários (Criação Lazy)

> Spec otimizada para IA. Cada task é autossuficiente: inclui contexto, padrões de referência,
> contrato de interface e critério de pronto com TDD. Implemente as tasks em ordem — há
> dependências sequenciais.

---

## Contexto e Motivação

Beneficiário é o paciente/titular do plano. No fluxo da Guia (F3.1), o admin precisa informar
**carteira** (número da carteirinha) e **nome do paciente**. Em vez de exigir cadastro prévio,
o sistema adota **criação lazy**: ao salvar uma Guia, a carteira é usada para localizar ou criar
automaticamente um `Beneficiario`.

O modelo F2.4 entrega:

1. A entidade `Beneficiario` no contexto `Catalog` (per `CLAUDE.md`: Catalog handles beneficiaries).
2. CRUD administrativo (listar, obter, atualizar, excluir).
3. Endpoint `lookup-or-create` — o ponto central do padrão lazy — chamado pelo formulário de Guia.
4. Componente Angular `BeneficiarioAutocompleteComponent` reutilizável pelo formulário de Guia (F3.1).
5. Tela de listagem admin (`/admin/catalog/beneficiarios`).

**Corte de escopo:** Não há formulário de _criação manual_ de beneficiário. Eles nascem via
`lookup-or-create`. O admin pode editar nome e excluir (se sem guias). A criação manual fica
fora do MVP (beneficiários sem guia não têm utilidade).

---

## Modelo de Domínio

```
Beneficiario
  Id           : Guid          PK
  TenantId     : Guid          NOT NULL  (ITenantEntity → query filter automático)
  Carteira     : string(50)    NOT NULL  normalizada: trim + uppercase
  Nome         : string(150)   NOT NULL
  CriadoEm    : DateTimeOffset NOT NULL

UNIQUE INDEX (TenantId, Carteira)
INDEX        (TenantId, Nome)   -- para busca por nome
```

Regras de domínio:

- `Carteira` é a chave de negócio dentro de um tenant. Normalizar antes de persistir e buscar:
  `value.Trim().ToUpperInvariant()`.
- Um `Beneficiario` não pode ser excluído enquanto houver `Guia` referenciando-o (verificar em F3.1
  — por ora, adicionar TODO comentado na lógica de exclusão).
- Não há FK para `Operadora`: a carteira identifica o beneficiário de forma única dentro do tenant,
  independente da operadora.

---

## Arquitetura e Localização de Arquivos

```
apps/backend/App/Catalog/
  Beneficiario.cs                             ← entidade
  Configurations/
    BeneficiarioConfiguration.cs             ← IEntityTypeConfiguration
  Migrations/
    <timestamp>_AddBeneficiarios.cs          ← migration gerada
    <timestamp>_AddBeneficiarios.Designer.cs
    .editorconfig                             ← supressão de warnings (ver TASK-F24-01)
  CatalogService.cs                          ← adicionar seção Beneficiário (linhas ~922+)
  Endpoints/
    CatalogEndpoints.cs                       ← adicionar grupo /beneficiarios

apps/backend/tests/Catalog.Tests/
  Beneficiario/
    BeneficiarioCrudTests.cs
    BeneficiarioLookupTests.cs
    BeneficiarioEndpointTests.cs

apps/admin-web/src/app/admin/catalog/
  catalog.types.ts                            ← adicionar tipos BeneficiarioItem etc.
  catalog.service.ts                          ← adicionar métodos Beneficiario
  catalog.routes.ts                           ← adicionar rotas /beneficiarios
  beneficiarios/
    beneficiario-list/
      beneficiario-list.component.ts
      beneficiario-list.component.html
      beneficiario-list.component.scss
      beneficiario-list.component.spec.ts
    beneficiario-autocomplete/
      beneficiario-autocomplete.component.ts
      beneficiario-autocomplete.component.html
      beneficiario-autocomplete.component.scss
      beneficiario-autocomplete.component.spec.ts
```

---

## Padrões de Referência (não repetir aqui, consultar nos arquivos)

| Padrão                      | Arquivo de referência                                         |
| --------------------------- | ------------------------------------------------------------- |
| Entidade com factory method | `App/Catalog/Prestador.cs`                                    |
| IEntityTypeConfiguration    | `App/Catalog/Configurations/PrestadorConfiguration.cs`        |
| CatalogService seção CRUD   | `App/Catalog/CatalogService.cs` linhas 710–820 (Prestador)    |
| Endpoints MapGroup          | `App/Catalog/Endpoints/CatalogEndpoints.cs` linhas 33–39      |
| CRUD tests com fixture      | `tests/Catalog.Tests/Prestador/PrestadorCrudTests.cs`         |
| Endpoint tests              | `tests/Catalog.Tests/Operadora/OperadoraEndpointTests.cs`     |
| Angular signals + list      | `admin-web/src/app/admin/catalog/prestadores/prestador-list/` |
| Angular types + service     | `admin-web/src/app/admin/catalog/catalog.types.ts`            |
| Migration editorconfig      | `App/Catalog/Migrations/.editorconfig`                        |

---

## Contrato de API

### Grupo: `/api/v1/admin/beneficiarios` — política `TenantAccess`

| Método   | Rota                              | Descrição                      | Sucesso   | Erros    |
| -------- | --------------------------------- | ------------------------------ | --------- | -------- |
| `GET`    | `/beneficiarios`                  | Listar com paginação e filtros | 200       | —        |
| `GET`    | `/beneficiarios/{id:guid}`        | Obter por ID                   | 200       | 404      |
| `POST`   | `/beneficiarios/lookup-or-create` | Busca por carteira ou cria     | 200 / 201 | 400      |
| `PUT`    | `/beneficiarios/{id:guid}`        | Atualizar nome                 | 200       | 400, 404 |
| `DELETE` | `/beneficiarios/{id:guid}`        | Excluir                        | 204       | 404, 409 |

### Schemas

**`GET /beneficiarios`** — query params:

```
carteira?: string
nome?:     string
pagina:    int (default 1)
itensPorPagina: int (default 20, max 100)
```

Resposta `200`:

```json
{
  "itens": [{ "id": "guid", "carteira": "0001234567", "nome": "JOÃO SILVA", "criadoEm": "..." }],
  "total": 42,
  "pagina": 1,
  "itensPorPagina": 20
}
```

**`POST /beneficiarios/lookup-or-create`** — body:

```json
{ "carteira": "0001234567", "nome": "João Silva" }
```

Resposta `200` (encontrado):

```json
{ "id": "guid", "carteira": "0001234567", "nome": "JOÃO SILVA", "criadoEm": "...", "criado": false }
```

Resposta `201` (criado — header `Location: /api/v1/admin/beneficiarios/{id}`):

```json
{ "id": "guid", "carteira": "0001234567", "nome": "JOÃO SILVA", "criadoEm": "...", "criado": true }
```

Resposta `400`:

```json
{ "errors": { "carteira": ["Carteira é obrigatória"], "nome": ["Nome é obrigatório"] } }
```

**`PUT /beneficiarios/{id}`** — body:

```json
{ "nome": "João da Silva" }
```

Resposta `200`: mesmo schema de BeneficiarioDto (sem campo `criado`).

---

## Tasks

---

### TASK-F24-01 ✅ — Entidade `Beneficiario`, configuração EF e migration

**Objetivo:** Criar a entidade de domínio, configuração EF Core e a migration correspondente.

#### 1. Entidade `apps/backend/App/Catalog/Beneficiario.cs`

Seguir exatamente o padrão de `Prestador.cs`. Campos e regras:

- Construtor `private Beneficiario()` (EF Core).
- Factory method estático `internal static Beneficiario Create(Guid tenantId, string carteira, string nome)`:
  - Normaliza carteira: `carteira.Trim().ToUpperInvariant()`
  - Normaliza nome: `nome.Trim()`
  - `CriadoEm = DateTimeOffset.UtcNow`
- Método `internal void Atualizar(string nome)`:
  - Normaliza e atribui `Nome`
- Implementa `ITenantEntity` via propriedade `TenantId`.

#### 2. Configuração `App/Catalog/Configurations/BeneficiarioConfiguration.cs`

```csharp
internal sealed class BeneficiarioConfiguration : IEntityTypeConfiguration<Beneficiario>
{
    public void Configure(EntityTypeBuilder<Beneficiario> builder)
    {
        builder.ToTable("beneficiarios");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.TenantId).IsRequired();
        builder.Property(b => b.Carteira).HasMaxLength(50).IsRequired();
        builder.Property(b => b.Nome).HasMaxLength(150).IsRequired();
        builder.Property(b => b.CriadoEm).IsRequired();

        builder.HasIndex(b => new { b.TenantId, b.Carteira }).IsUnique();
        builder.HasIndex(b => new { b.TenantId, b.Nome });
    }
}
```

#### 3. `AppDbContext` — adicionar `DbSet`

Em `App/Data/AppDbContext.cs`, adicionar:

```csharp
public DbSet<Beneficiario> Beneficiarios => Set<Beneficiario>();
```

#### 4. Migration

Gerar via `dotnet ef migrations add AddBeneficiarios --project App --context AppDbContext --output-dir Catalog/Migrations`.

A migration deve criar tabela `beneficiarios` com os campos acima. Verificar se `Up()` e `Down()` estão corretos.

Criar `App/Catalog/Migrations/.editorconfig` se não existir, com o mesmo conteúdo das migrations já existentes (supprime IDE0005, IDE0161, CA1515, CA1861).

#### 5. Build

`dotnet build apps/backend/Honorare.slnx` deve passar sem warnings.

**Critério de pronto:** build limpo, migration gerada com `Up()`/`Down()` corretos, DbSet adicionado.

---

### TASK-F24-02 ✅ — `CatalogService`: CRUD de Beneficiário (TDD)

**Objetivo:** Implementar os métodos CRUD no `CatalogService` guiados por testes.

#### Escrever os testes primeiro: `tests/Catalog.Tests/Beneficiario/BeneficiarioCrudTests.cs`

Usar `PostgresContainerFixture` e `BuildTenant()` conforme padrão de `PrestadorCrudTests.cs` (ou `OperadoraCrudTests.cs`).

Testes obrigatórios (escrever todos antes de implementar):

```
Criar_CarteiraENomeValidos_RetornaBeneficiarioDto
Criar_CarteiraNormalizada_PersistidaUppercase
  → carteira " 001abc " deve ser persistida "001ABC"
Criar_NomeTrimado_PersistidoSemEspacos
Criar_CarteiraVazia_RetornaValidationError
Criar_NomeVazio_RetornaValidationError
Criar_CarteiraDuplicadaMesmoTenant_RetornaConflictError
Criar_CarteiraDuplicadaTenantDiferente_Permitido
  → mesma carteira em tenants distintos deve ser permitida
Obter_IdExistente_RetornaBeneficiarioDto
Obter_IdInexistente_RetornaNotFoundError
Obter_IdDeOutroTenant_RetornaNotFoundError
  → global query filter deve impedir acesso cross-tenant
Atualizar_NomeValido_RetornaDtoAtualizado
Atualizar_NomeVazio_RetornaValidationError
Atualizar_IdInexistente_RetornaNotFoundError
Listar_SemFiltros_RetornaTodosDoTenant
Listar_FiltroCarteira_RetornaApenasMatches
  → busca case-insensitive (ILIKE)
Listar_FiltroNome_RetornaApenasMatches
Listar_IsolacaoDeTenant_NaoRetornaDadosDeTenantDiferente
Excluir_IdExistente_RemoveBeneficiario
Excluir_IdInexistente_RetornaNotFoundError
```

#### Implementar em `CatalogService.cs`

Adicionar seção Beneficiário após a seção Prestador (linha ~921), seguindo o padrão idêntico ao de Prestador. DTOs e commands internos ao arquivo (Records selados no final):

```csharp
// --- Records internos (adicionar junto aos outros no final do arquivo) ---
internal sealed record ListarBeneficiariosQuery(
    string? Carteira, string? Nome, int Pagina, int ItensPorPagina);

internal sealed record ListarBeneficiariosResult(
    IReadOnlyList<BeneficiarioDto> Itens, int Total, int Pagina, int ItensPorPagina);

internal sealed record BeneficiarioDto(
    Guid Id, string Carteira, string Nome, DateTimeOffset CriadoEm);

internal sealed record CriarBeneficiarioCommand(string Carteira, string Nome);
internal sealed record AtualizarBeneficiarioCommand(string Nome);
```

Métodos a implementar:

- `ListarBeneficiariosAsync(ListarBeneficiariosQuery query, CancellationToken ct)`
- `ObterBeneficiarioPorIdAsync(Guid id, CancellationToken ct)`
- `CriarBeneficiarioAsync(CriarBeneficiarioCommand cmd, CancellationToken ct)`
- `AtualizarBeneficiarioAsync(Guid id, AtualizarBeneficiarioCommand cmd, CancellationToken ct)`
- `ExcluirBeneficiarioAsync(Guid id, CancellationToken ct)`

Regras de validação em `CriarBeneficiarioAsync`:

- Carteira: required, max 50 chars após normalização.
- Nome: required, max 150 chars após normalização.
- Unicidade (TenantId, Carteira normalizada): retorna `ConflictError` se já existe.

Bloquear exclusão com TODO:

```csharp
// TODO F3.1: verificar Guias associadas antes de excluir (retornar 409 se houver)
```

**Critério de pronto:** todos os testes passam, cobertura ≥ 80%, `dotnet test` limpo.

---

### TASK-F24-03 ✅ — `CatalogService`: `LookupOrCreateAsync` (TDD)

**Objetivo:** Método que encapsula o padrão lazy — busca por carteira normalizada; cria se não
encontrar. É a operação central desta feature.

#### Escrever os testes primeiro: `tests/Catalog.Tests/Beneficiario/BeneficiarioLookupTests.cs`

```
LookupOrCreate_CarteiraExistente_RetornaBeneficiarioExistenteSemCriar
  → chamar duas vezes com mesma carteira → segundo retorno tem criado=false
  → contagem de beneficiários não aumenta na segunda chamada
LookupOrCreate_CarteiraNova_CriaBeneficiarioRetornaCriado
  → criado=true, Id persistido no banco
LookupOrCreate_CarteiraComEspacos_NormalizaEEncontraExistente
  → " 001ABC " deve encontrar registro persistido como "001ABC"
LookupOrCreate_CarteiraMinuscula_NormalizaEEncontraExistente
  → "001abc" deve encontrar registro persistido como "001ABC"
LookupOrCreate_CarteiraVazia_RetornaValidationError
LookupOrCreate_NomeVazioComCarteiraNova_RetornaValidationError
  → nome só é obrigatório se a carteira for nova (criação); se encontrar existente, nome é ignorado
LookupOrCreate_IsolacaoDeTenant_NaoCruzaTenants
  → mesma carteira em tenant A não é encontrada em tenant B
```

#### Implementar em `CatalogService.cs`

Record de retorno:

```csharp
internal sealed record LookupOrCreateResult(BeneficiarioDto Beneficiario, bool Criado);
```

Assinatura:

```csharp
internal async Task<Result<LookupOrCreateResult>> LookupOrCreateAsync(
    string carteira, string nome, CancellationToken ct)
```

Lógica:

1. Validar: carteira not-empty.
2. Normalizar carteira: `carteira.Trim().ToUpperInvariant()`.
3. Buscar no DbContext: `FirstOrDefaultAsync(b => b.Carteira == normalizada)`.
4. Se encontrado: retornar `Result.Ok(new LookupOrCreateResult(beneficiario.ToDto(), false))`.
5. Se não encontrado: validar nome not-empty; chamar `Beneficiario.Create(...)` e persistir; retornar
   `Result.Ok(new LookupOrCreateResult(novo.ToDto(), true))`.

**Critério de pronto:** todos os testes passam, comportamento idempotente verificado.

---

### TASK-F24-04 ✅ — Endpoints REST + testes de endpoint (TDD)

**Objetivo:** Mapear os endpoints no `CatalogEndpoints.cs` e cobri-los com testes de integração HTTP.

#### Escrever os testes primeiro: `tests/Catalog.Tests/Beneficiario/BeneficiarioEndpointTests.cs`

Seguir exatamente o padrão de `OperadoraEndpointTests.cs` (WebApplicationFactory + TestAuthHandler).

Testes obrigatórios:

```
GET_Beneficiarios_SemAutenticacao_Retorna401
GET_Beneficiarios_Autenticado_Retorna200ComListaAsync
POST_LookupOrCreate_CarteiraExistente_Retorna200ComCriadoFalseAsync
POST_LookupOrCreate_CarteiraNova_Retorna201ComCriadoTrueELocationHeaderAsync
POST_LookupOrCreate_CarteiraNova_LocationHeaderAponta_ParaRecursoAsync
  → Location deve ser /api/v1/admin/beneficiarios/{id}
POST_LookupOrCreate_BodyVazio_Retorna400Async
GET_Beneficiarios_PorId_Existente_Retorna200Async
GET_Beneficiarios_PorId_Inexistente_Retorna404Async
PUT_Beneficiarios_NomeValido_Retorna200Async
PUT_Beneficiarios_NomeVazio_Retorna400Async
PUT_Beneficiarios_IdInexistente_Retorna404Async
DELETE_Beneficiarios_Existente_Retorna204Async
DELETE_Beneficiarios_Inexistente_Retorna404Async
```

#### Implementar em `CatalogEndpoints.cs`

Adicionar grupo no `MapCatalogEndpoints`:

```csharp
// Beneficiários
var beneficiarios = app.MapGroup("/api/v1/admin/beneficiarios")
    .RequireAuthorization("TenantAccess");

beneficiarios.MapGet("", ListarBeneficiariosAsync);
beneficiarios.MapGet("{id:guid}", ObterBeneficiarioPorIdAsync);
beneficiarios.MapPost("lookup-or-create", LookupOrCreateAsync);
beneficiarios.MapPut("{id:guid}", AtualizarBeneficiarioAsync);
beneficiarios.MapDelete("{id:guid}", ExcluirBeneficiarioAsync);
```

Handlers `private static`:

- `ListarBeneficiariosAsync`: `[AsParameters] ListarBeneficiariosRequest req` → `Results.Ok(result.Value)`.
- `LookupOrCreateAsync`: body `LookupOrCreateRequest` → se `criado=true` retorna `Results.Created($"/api/v1/admin/beneficiarios/{id}", dto)`, senão `Results.Ok(dto)`.
- Demais handlers: padrão já estabelecido (400/404/409 via `switch (result.Error)`).

Records de request (adicionar ao final do arquivo, seção interna):

```csharp
private sealed record ListarBeneficiariosRequest(
    string? Carteira, string? Nome,
    int Pagina = 1, int ItensPorPagina = 20);

private sealed record LookupOrCreateRequest(string Carteira, string Nome);
private sealed record AtualizarBeneficiarioRequest(string Nome);
```

**Critério de pronto:** todos os testes passam, `dotnet build` limpo, `pnpm generate-api-client` regenera o cliente TypeScript sem erros.

---

### TASK-F24-05 ✅ — Angular: tipos e `CatalogService`

**Objetivo:** Adicionar os tipos TypeScript e os métodos HTTP ao `CatalogService` do `admin-web`.

**Pré-requisito:** Rodar `pnpm generate-api-client` após TASK-F24-04 para gerar o cliente OpenAPI.

#### Adicionar em `catalog.types.ts`

```typescript
// ── Beneficiário ────────────────────────────────────────────────────────────

export interface BeneficiarioItem {
  id: string;
  carteira: string;
  nome: string;
  criadoEm: string;
}

export interface ListarBeneficiariosParams {
  carteira?: string;
  nome?: string;
  pagina: number;
  itensPorPagina: number;
}

export interface ListarBeneficiariosResult {
  itens: BeneficiarioItem[];
  total: number;
  pagina: number;
  itensPorPagina: number;
}

export interface LookupOrCreateResult extends BeneficiarioItem {
  criado: boolean;
}

export interface AtualizarBeneficiarioPayload {
  nome: string;
}
```

#### Adicionar em `catalog.service.ts`

```typescript
// ── Beneficiários ────────────────────────────────────────────────────────────

listarBeneficiarios(
  params: ListarBeneficiariosParams,
): Observable<ListarBeneficiariosResult> { ... }

obterBeneficiario(id: string): Observable<BeneficiarioItem> { ... }

lookupOrCreateBeneficiario(
  carteira: string,
  nome: string,
): Observable<LookupOrCreateResult> { ... }

atualizarBeneficiario(
  id: string,
  payload: AtualizarBeneficiarioPayload,
): Observable<BeneficiarioItem> { ... }

excluirBeneficiario(id: string): Observable<void> { ... }
```

#### Testes em `catalog.service.spec.ts`

Usar `HttpTestingController` (padrão já usado nos outros specs do projeto).

```
listarBeneficiarios_chamaCaminhoCorreto
lookupOrCreateBeneficiario_enviaCamposNormalizados
atualizarBeneficiario_enviaPUT
excluirBeneficiario_enviaDELETE
```

**Critério de pronto:** `pnpm -F admin-web test:ci` passa, lint limpo.

---

### TASK-F24-06 — Angular: `BeneficiarioAutocompleteComponent`

**Objetivo:** Componente reutilizável que o formulário de Guia (F3.1) usará. Recebe uma carteira,
busca/cria o beneficiário e emite o resultado.

#### Comportamento

```
[carteira digitada] →  debounce 400ms  →  lookup-or-create  →  emite BeneficiarioItem
```

- Enquanto aguarda, exibe indicador de carregamento no campo.
- Se encontrado (criado=false): exibe nome com badge "Encontrado".
- Se criado (criado=true): exibe nome com badge "Novo".
- Erro de rede: exibe mensagem inline; não lança exceção.
- Se carteira for apagada: limpa o estado e emite `null`.

#### Interface do componente

```typescript
@Component({
  selector: 'app-beneficiario-autocomplete',
  ...
})
export class BeneficiarioAutocompleteComponent {
  // Inputs
  readonly label = input<string>('Carteira do Beneficiário');
  readonly disabled = input<boolean>(false);

  // Output
  readonly beneficiarioChange = output<BeneficiarioItem | null>();

  // Estado interno (Signals)
  readonly carteira = signal('');
  readonly nomeSelecionado = signal('');
  readonly estado = signal<'idle' | 'buscando' | 'encontrado' | 'novo' | 'erro'>('idle');

  // Exposto para o form de Guia acessar o último resultado
  readonly beneficiarioAtual = signal<BeneficiarioItem | null>(null);
}
```

O nome (do beneficiário encontrado/criado) é exibido como read-only dentro do próprio componente,
abaixo do campo de carteira. O form de Guia NÃO precisa de campo separado de nome — o nome é
preenchido automaticamente.

Para o campo nome na criação de beneficiário novo: exibir campo de nome editável inline quando
o estado for `novo` + nenhum registro encontrado. O componente pede confirmação do nome antes
de emitir o resultado.

#### Fluxo detalhado de `novo`:

1. Carteira digitada, debounce dispara.
2. GET `/api/v1/admin/beneficiarios?carteira=XXX&pagina=1&itensPorPagina=1` — verificar se existe.
   - Se existe: emitir diretamente sem perguntar nome.
   - Se não existe: exibir campo "Nome do Paciente" editável.
3. Quando nome preenchido + botão "Confirmar" (ou Enter): chamar `lookup-or-create`.
4. Emitir resultado via `beneficiarioChange`.

#### Testes obrigatórios (`beneficiario-autocomplete.component.spec.ts`)

```
exibeIndicadorDeBuscaEnquantoAguarda
aoEncontrarBeneficiarioExistente_exibeBadgeEncontradoENomeAsync
aoNaoEncontrarBeneficiario_exibeCampoDeNomeAsync
aoConfirmarNomeNovo_emiteBeneficiarioComCriadoTrueAsync
aoApagarCarteira_emiteNullAsync
erroDeRede_exibeMensagemInlineSemLancarExcecaoAsync
disabled_true_bloqueiaCampoAsync
```

**Padrões Angular a seguir:**

- Signals para todo estado interno.
- `takeUntilDestroyed()` para cancelar subscriptions.
- `vi.useFakeTimers()` para testar debounce (não `fakeAsync` — ver memory `feedback_angular_fakeAsync_vitest.md`).
- Sem `fakeAsync`, `tick` ou `jasmine.clock`.
- `?.textContent ?? ''` nunca `!` em DOM queries (ver memory `feedback_angular_dom_query_pattern.md`).
- SCSS deve usar tokens (`var(--color-*)`, `space()`, `@include text-*`).

**Critério de pronto:** testes passam, lint/stylelint limpos, componente exportado em `catalog.routes.ts` como lazy-loaded.

---

### TASK-F24-07 — Angular: `BeneficiarioListComponent` (admin view)

**Objetivo:** Tela de listagem para o admin consultar e editar beneficiários existentes.

**Rota:** `/admin/catalog/beneficiarios`

#### Funcionalidades

- Lista paginada com colunas: Carteira, Nome, Criado em, Ações.
- Filtros: campo texto "Carteira ou Nome" (busca nos dois campos via dois requests ou um request unificado). Para MVP, usar filtro por nome; o filtro por carteira é separado.
- Ação "Editar": modal inline ou linha editável — apenas o campo `Nome` é editável.
- Ação "Excluir": `window.confirm` → `DELETE` → recarga (padrão de `prestador-list`).
- Sem botão "Novo Beneficiário" (criação só por lookup-or-create).

#### Edição inline (simplificação de UX)

Em vez de formulário separado, ao clicar "Editar" a linha vira um input de texto com botões
"Salvar" / "Cancelar". Chama `PUT /beneficiarios/{id}`. Signals para controlar qual linha está
em edição (`editandoId = signal<string | null>(null)`).

#### Testes obrigatórios (`beneficiario-list.component.spec.ts`)

```
exibeListaDeBeneficiariosCarregadosAsync
exibeMensagemQuandoListaEstiverVaziaAsync
filtroAltera_disparaNovaConsultaAsync
clicarEditarExibeInputDeNomeAsync
salvarEdicaoChama_PUT_ERecarregaListaAsync
clicarExcluirComConfirmacaoChama_DELETE_ERecarregaListaAsync
```

#### Roteamento

Adicionar em `catalog.routes.ts`:

```typescript
{
  path: 'beneficiarios',
  loadComponent: () =>
    import('./beneficiarios/beneficiario-list/beneficiario-list.component')
      .then((m) => m.BeneficiarioListComponent),
},
```

Adicionar item na sidebar do `admin-web` na seção "Cadastros":

```
Beneficiários  → /admin/catalog/beneficiarios
```

**Critério de pronto:** rota acessível, testes passam, lint/stylelint limpos, sidebar atualizada.

---

## Critério de Pronto Global (F2.4)

- [ ] `dotnet build apps/backend/Honorare.slnx` → zero warnings/errors.
- [ ] `dotnet test apps/backend/Honorare.slnx` → todos passam, cobertura ≥ 80% em `Catalog`.
- [ ] `pnpm -F admin-web test:ci` → todos passam, cobertura ≥ 80%.
- [ ] `pnpm -F admin-web lint` → zero warnings.
- [ ] `pnpm -F admin-web stylelint` → zero warnings.
- [ ] Componente `BeneficiarioAutocompleteComponent` exportado e reutilizável por F3.1.
- [ ] Rota `/admin/catalog/beneficiarios` acessível e funcional.
- [ ] Sidebar atualizada com link "Beneficiários".

## Dependências e Bloqueios

- **Depende de:** F2.3 (infra existente: fixture Testcontainers, padrão CatalogService, EF migrations).
- **Desbloqueia:** F3.1 (formulário de Guia importa `BeneficiarioAutocompleteComponent` diretamente).
- **Não bloqueia:** F3.2 (motor de cálculo — independente de beneficiário).

## Cortes de Escopo Se Necessário

Se o prazo apertar, cortar nesta ordem:

1. `BeneficiarioListComponent` (TASK-F24-07) — admin gerencia beneficiários via Guias.
2. Campo de nome editável inline — simplificar para rota `/beneficiarios/:id` com form separado.
3. Badge "Encontrado"/"Novo" no autocomplete — simplificar para apenas o nome exibido.

**Não cortar:** entidade, `lookup-or-create`, `BeneficiarioAutocompleteComponent` — são o núcleo do padrão lazy e pré-requisito de F3.1.

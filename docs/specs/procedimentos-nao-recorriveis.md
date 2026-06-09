# SPEC: Procedimentos não recorríveis

> **Como executar:** uma task por sessão. Rode `/tdd-task docs/specs/procedimentos-nao-recorriveis.md <TASK-ID>`,
> deixe a sessão implementar+commitar, limpe o contexto (`/clear`), rode a próxima.
> CLAUDE.md e MEMORY.md já entram no contexto de cada sessão — as tasks NÃO os repetem.
> **O checkbox `[ ]`/`[x]` é a única memória entre sessões:** uma sessão fria se orienta
> só por ele. Comece pela primeira task ainda `[ ]`; nunca refaça uma já marcada `[x]`.

## Objetivo

Impedir que guias de consulta (e outros procedimentos que o cliente configurar) entrem
num recurso de glosa quando ele usa o "Adicionar todas" (lote). Cada tenant mantém uma
**lista de códigos TUSS "não recorríveis"**. O lote pula guias que contenham um item com
esses códigos; a lista de candidatas continua mostrando essas guias, porém **marcadas**, e
o operador ainda pode adicioná-las **manualmente** (escape hatch). A lista é gerenciada na
tela de Configurações, escolhendo procedimentos via busca no catálogo.

## Contexto compartilhado (válido para todas as tasks)

- **Fato de domínio (verificado nos dados reais):** uma consulta é SEMPRE uma guia isolada
  — nenhuma guia mistura consulta com outro procedimento. Por isso a regra é no nível da
  **guia inteira** (a guia tem ALGUM item com código na lista → é não recorrível). **Não há
  tratamento item-level.**
- **Nome do conceito:** "não recorrível" / `naoRecorrivel` / `CodigosNaoRecorriveis`.
- **Sem seed:** a lista começa vazia; o cliente cadastra pela tela.
- `Tenant` é uma entidade **global** (não `ITenantEntity`); serviços acessam o tenant atual
  por `_db.Tenants.FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId!.Value, ct)`.
- Backend: warnings = errors; sem CQRS/MediatR/Repository/AutoMapper; `AppDbContext` único.
- O front (admin-web) usa serviços `HttpClient` escritos à mão (não o client gerado) para
  Tenant/Guia/Recurso — então os tipos TS são mantidos à mão. **Não** é preciso rodar
  `pnpm generate-api-client` nesta feature.

## Tasks

### TASK-NREC-01 — Campo `CodigosNaoRecorriveis` no Tenant + migration

- [x] concluída

**Objetivo:** adicionar a lista de TUSS não recorríveis ao agregado `Tenant`, mapeada como
`text[]` no Postgres, com migration.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Identity/Tenant.cs` (arquivo inteiro, ~60 linhas — entidade alvo)
- `apps/backend/App/Identity/Configurations/TenantConfiguration.cs` (~18 linhas)
- `apps/backend/tests/Identity.Tests/Entities/TenantTests.cs:1-30` — formato do teste de entidade

**Criar/Editar:**

- `apps/backend/App/Identity/Tenant.cs` (editar: campo + métodos)
- `apps/backend/App/Identity/Configurations/TenantConfiguration.cs` (editar: mapear coluna)
- `apps/backend/tests/Identity.Tests/Entities/TenantTests.cs` (editar: novos testes)
- Migration nova (gerada por CLI — ver abaixo)

**Padrão a seguir (entidade — adicionar ao `Tenant.cs`):**

```csharp
// propriedade — List<string> mapeia para text[] no Npgsql automaticamente
public List<string> CodigosNaoRecorriveis { get; private set; } = [];

// método: normaliza (trim, remove vazios, dedup, ordena estável). NÃO valida formato
// (dígitos) aqui — isso é responsabilidade do service, como RenameAsync já faz.
public void DefinirCodigosNaoRecorriveis(IEnumerable<string> codigos)
{
    CodigosNaoRecorriveis = codigos
        .Select(c => c.Trim())
        .Where(c => c.Length > 0)
        .Distinct()
        .OrderBy(c => c, StringComparer.Ordinal)
        .ToList();
}
```

**Padrão a seguir (config — adicionar em `TenantConfiguration.Configure`):**

```csharp
builder.Property(t => t.CodigosNaoRecorriveis).HasColumnType("text[]");
```

**Gerar a migration** (a coluna do Tenant vive em `Identity/Migrations`, como a do logo —
`namespace App.Identity.Migrations`; essa pasta JÁ tem `.editorconfig`, não criar outro):

```bash
cd apps/backend/App
dotnet ef migrations add AddTenantCodigosNaoRecorriveis \
  --output-dir Identity/Migrations --namespace App.Identity.Migrations
```

Confira que o `AddColumn` gerado usa `type: "text[]"`, `nullable: false` e
`defaultValue: new List<string>()` (ou `defaultValueSql: "'{}'::text[]"`). Ajuste o
`defaultValue` se o EF gerar `nullable: false` sem default (tabela pode ter linhas).

**Testes (red primeiro):**

- `DefinirCodigosNaoRecorriveis_DeveArmazenarLista`
- `DefinirCodigosNaoRecorriveis_DeveRemoverDuplicatasEEspacos` (ex.: `[" 10101012 ", "10101012"]` → `["10101012"]`)
- `DefinirCodigosNaoRecorriveis_DeveIgnorarVazios` (ex.: `["", "  "]` → lista vazia)
- `Create_DeveIniciarComListaVazia`

**Aceite (checklist objetivo):**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde
- [ ] migration aparece em `apps/backend/App/Identity/Migrations/` com `text[]`
- [ ] snapshot `apps/backend/App/Migrations/AppDbContextModelSnapshot.cs` atualizado com a coluna

**Commit:** `feat(identity): lista de códigos não recorríveis no tenant (TASK-NREC-01)`

---

### TASK-NREC-02 — Expor e atualizar a lista via TenantSettings (service + endpoint)

- [x] concluída

**Objetivo:** `GET /api/v1/admin/tenant` passa a devolver `codigosNaoRecorriveis`; novo
`PUT /api/v1/admin/tenant/codigos-nao-recorriveis` salva a lista (validando: só dígitos).

**Depende de:** TASK-NREC-01. Contratos já existentes (não precisa abrir o `Tenant.cs`):

```csharp
public List<string> CodigosNaoRecorriveis { get; private set; }      // no Tenant
public void DefinirCodigosNaoRecorriveis(IEnumerable<string> codigos); // normaliza/dedup
```

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Identity/TenantSettingsService.cs:1-65` — record `TenantSettings`, `GetSettingsAsync`, `RenameAsync` (padrão de validação via `Result`/`ValidationError`)
- `apps/backend/App/Identity/Endpoints/TenantSettingsEndpoints.cs` (arquivo inteiro, ~96 linhas)
- `apps/backend/tests/Faturamento.Tests/Identity/TenantSettingsServiceTests.cs:1-68` — padrão de teste de service (Postgres fixture, `FakeTenantUser`, `SeedTenantAsync`)

**Criar/Editar:**

- `apps/backend/App/Identity/TenantSettingsService.cs` (editar)
- `apps/backend/App/Identity/Endpoints/TenantSettingsEndpoints.cs` (editar)
- `apps/backend/tests/Faturamento.Tests/Identity/TenantSettingsServiceTests.cs` (editar)

**Mudanças no service:**

1. Record passa a expor a lista:

```csharp
internal sealed record TenantSettings(
    Guid Id, string Name, bool HasLogo, IReadOnlyList<string> CodigosNaoRecorriveis);
```

2. **Atualizar TODAS as construções de `new TenantSettings(...)`** no arquivo (há 3+: em
   `GetSettingsAsync`, `RenameAsync`, `UploadLogoAsync`) para incluir
   `tenant.CodigosNaoRecorriveis`.

3. Novo método (validação de dígitos espelha `RecursoService.ValidarNumero`):

```csharp
internal async Task<Result<TenantSettings>> AtualizarCodigosNaoRecorriveisAsync(
    IReadOnlyList<string> codigos, CancellationToken ct = default)
{
    var normalizados = codigos.Select(c => (c ?? string.Empty).Trim())
        .Where(c => c.Length > 0).ToList();
    if (normalizados.Any(c => !c.All(char.IsAsciiDigit)))
    {
        return Result<TenantSettings>.Fail(
            new ValidationError("Código TUSS deve conter apenas dígitos."));
    }

    var tenant = await _db.Tenants.FindAsync([_currentUser.TenantId!.Value], ct);
    if (tenant is null)
    {
        return Result<TenantSettings>.Fail(new NotFoundError("Tenant não encontrado."));
    }

    tenant.DefinirCodigosNaoRecorriveis(normalizados);
    await _db.SaveChangesAsync(ct);
    return Result<TenantSettings>.Ok(new TenantSettings(
        tenant.Id, tenant.Name, tenant.LogoKey is not null, tenant.CodigosNaoRecorriveis));
}
```

**Mudanças no endpoint** (grupo já é `/api/v1/admin/tenant` com `TenantAccess`):

```csharp
g.MapPut("/codigos-nao-recorriveis", AtualizarCodigosNaoRecorriveisAsync);
// ...
private static async Task<IResult> AtualizarCodigosNaoRecorriveisAsync(
    AtualizarCodigosNaoRecorriveisRequest body, TenantSettingsService svc, CancellationToken ct)
{
    var result = await svc.AtualizarCodigosNaoRecorriveisAsync(body.Codigos, ct);
    if (result.IsFailure)
    {
        var statusCode = result.Error switch
        {
            NotFoundError => StatusCodes.Status404NotFound,
            ValidationError => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
    }
    return Results.Ok(result.Value);
}
// no fim do arquivo, junto de RenameTenantRequest:
internal sealed record AtualizarCodigosNaoRecorriveisRequest(IReadOnlyList<string> Codigos);
```

**Testes (red primeiro):**

- `AtualizarCodigosNaoRecorriveisAsync_DeveSalvarListaNoTenant`
- `AtualizarCodigosNaoRecorriveisAsync_DeveRejeitarCodigoNaoNumerico` (ex.: `["abc"]` → `ValidationError`)
- `GetSettingsAsync_DeveRetornarCodigosNaoRecorriveis` (após salvar, lê de volta)

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde
- [ ] `GET /api/v1/admin/tenant` inclui `codigosNaoRecorriveis` no JSON

**Commit:** `feat(identity): endpoint para gerenciar códigos não recorríveis (TASK-NREC-02)`

---

### TASK-NREC-03 — Flag `naoRecorrivel` na listagem de guias

- [x] concluída

**Objetivo:** `GuiaService.ListarAsync` passa a marcar cada guia com `NaoRecorrivel = true`
quando ela tem algum item cujo `Procedimento.CodigoTuss` está na lista do tenant. **Não
filtra** — só marca (a UI exibe selo e mantém a guia adicionável manualmente).

**Depende de:** TASK-NREC-01 (campo `tenant.CodigosNaoRecorriveis : List<string>`).

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Faturamento/GuiaService.cs:46-51` — record `GuiaDto`
- `apps/backend/App/Faturamento/GuiaService.cs:257-386` — `ListarAsync` inteiro (já carrega
  `counts` por uma query separada após obter `ids` — replique essa forma para os códigos)
- Padrão de teste de service: `apps/backend/tests/Faturamento.Tests/Identity/TenantSettingsServiceTests.cs:1-48` (fixture Postgres, `FakeTenantUser`). Procure um teste existente de `GuiaService` com `grep -rl "new GuiaService" apps/backend/tests` para imitar o setup de prestador/operadora/guia/item.

**Criar/Editar:**

- `apps/backend/App/Faturamento/GuiaService.cs` (editar: record + `ListarAsync`)
- arquivo de teste de `GuiaService.ListarAsync` (editar/criar)

**Mudança no record** (campo novo no FIM; atualizar as construções `new GuiaDto(...)`):

```csharp
internal sealed record GuiaDto(
    Guid Id, Guid PrestadorId, string PrestadorNome,
    Guid OperadoraId, string OperadoraNome, Guid? BeneficiarioId,
    string? BeneficiarioNome, string? BeneficiarioCarteira, string NumeroGuia,
    DateOnly DataAtendimento, SituacaoGuia Situacao, bool EhPacote,
    string Observacao, string LocalAtendimento, int TotalItens,
    DateTimeOffset CriadoEm, DateTimeOffset AtualizadoEm,
    bool NaoRecorrivel);
```

**Cálculo do flag** — em `ListarAsync`, logo após o bloco `counts` (linha ~374), antes de
montar `itens`:

```csharp
var tenant = await _db.Tenants
    .FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId!.Value, ct);
var codigos = tenant?.CodigosNaoRecorriveis ?? [];
var naoRecorriveis = codigos.Count == 0
    ? new HashSet<Guid>()
    : (await (from i in _db.ItensGuia
              join p in _db.Procedimentos on i.ProcedimentoId equals p.Id
              where ids.Contains(i.GuiaId) && codigos.Contains(p.CodigoTuss)
              select i.GuiaId).Distinct().ToListAsync(ct)).ToHashSet();
```

E na projeção `new GuiaDto(...)` acrescente o último argumento:
`naoRecorriveis.Contains(x.Id)`.

> `grep -rn "new GuiaDto(" apps/backend --include=*.cs` (produção **e** testes) para
> garantir que toda construção do record foi atualizada com o novo argumento posicional.

**Testes (red primeiro):**

- `ListarAsync_DeveMarcarNaoRecorrivel_QuandoGuiaTemItemComCodigoNaLista`
- `ListarAsync_NaoDeveMarcar_QuandoCodigoNaoEstaNaLista`
- `ListarAsync_NaoDeveExcluirGuia_MesmoSendoNaoRecorrivel` (guia não recorrível continua no resultado)

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde (cobertura Faturamento ≥ 90%)
- [ ] guia não recorrível CONTINUA aparecendo na listagem, apenas com `NaoRecorrivel=true`

**Commit:** `feat(faturamento): marca guias não recorríveis na listagem (TASK-NREC-03)`

---

### TASK-NREC-04 — Lote "Adicionar todas" pula guias não recorríveis

- [ ] pendente

**Objetivo:** `RecursoService.AdicionarGuiasEmLoteAsync` deixa de vincular guias que tenham
algum item com código TUSS na lista do tenant. **Único ponto que filtra de fato.**
`AdicionarGuiaAsync` (individual) **permanece intocado** — é o escape hatch.

**Depende de:** TASK-NREC-01 (campo `tenant.CodigosNaoRecorriveis : List<string>`).

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Faturamento/RecursoService.cs:362-419` — `AdicionarGuiasEmLoteAsync` inteiro
- Teste existente do lote: rode `grep -rln "AdicionarGuiasEmLote" apps/backend/tests` e abra o arquivo achado para imitar o setup.

**Criar/Editar:**

- `apps/backend/App/Faturamento/RecursoService.cs` (editar: `AdicionarGuiasEmLoteAsync`)
- arquivo de teste do lote (editar)

**Mudança** — depois de aplicar todos os filtros do `cmd` e ANTES de `await q.ToListAsync(ct)`
(linha ~411):

```csharp
var tenant = await _db.Tenants
    .FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId!.Value, ct);
var codigos = tenant?.CodigosNaoRecorriveis ?? [];
if (codigos.Count > 0)
{
    q = q.Where(g => !_db.ItensGuia.Any(i =>
        i.GuiaId == g.Id &&
        _db.Procedimentos.Any(p => p.Id == i.ProcedimentoId && codigos.Contains(p.CodigoTuss))));
}
```

> `RecursoService` já injeta `AppDbContext _db` e `ICurrentUser _currentUser` (ver ctor na
> linha 82) — não precisa mudar dependências.

**Testes (red primeiro):**

- `AdicionarGuiasEmLoteAsync_DevePularGuiaComCodigoNaoRecorrivel` (cria 1 guia consulta + 1 guia normal; lote vincula só a normal; retorna count=1)
- `AdicionarGuiasEmLoteAsync_DeveVincularTodas_QuandoListaVazia` (sem códigos configurados → comportamento atual)
- `AdicionarGuiaAsync_DeveVincularGuiaNaoRecorrivel_QuandoIndividual` (escape hatch: add individual ignora a lista)

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde (cobertura Faturamento ≥ 90%)
- [ ] add individual de guia não recorrível continua funcionando

**Commit:** `feat(faturamento): lote de recurso ignora guias não recorríveis (TASK-NREC-04)`

---

### TASK-NREC-05 — Tela de Configurações: gerenciar códigos não recorríveis

- [ ] pendente

**Objetivo:** seção nova na página de Configurações para adicionar/remover procedimentos
não recorríveis, escolhendo via busca no catálogo (autocomplete) e salvando via PUT.

**Depende de:** TASK-NREC-02. Contrato do backend:

- `GET /api/v1/admin/tenant` → `{ id, name, hasLogo, codigosNaoRecorriveis: string[] }`
- `PUT /api/v1/admin/tenant/codigos-nao-recorriveis` body `{ codigos: string[] }` → mesmo DTO
- `GET /api/v1/admin/procedimentos?busca=<txt>&pagina=1&itensPorPagina=10` →
  `{ itens: ProcedimentoItem[], total, ... }`, `ProcedimentoItem = { id, codigoTuss, descricao, ... }`

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md (atenção: `[selected]` vs `[value]`,
`Intl.*` em vez de pipes, `space()` só com steps válidos, sem hex/cor nomeada — **ler
`apps/admin-web/STYLES.md` antes do SCSS**).

**Ler (só isto):**

- `apps/admin-web/src/app/admin/configuracoes/tenant.types.ts` (5 linhas)
- `apps/admin-web/src/app/admin/configuracoes/tenant.service.ts` (~31 linhas)
- `apps/admin-web/src/app/admin/configuracoes/configuracoes-page.ts` (~135 linhas — padrão signals/subscribe)
- `apps/admin-web/src/app/admin/configuracoes/configuracoes-page.spec.ts:1-45` — padrão de teste (spy de service com `of(...)`)
- `apps/admin-web/src/app/admin/catalog/catalog.service.ts:74-90` — `listarProcedimentos`
- `apps/admin-web/src/app/admin/catalog/catalog.types.ts:37-62` — `ProcedimentoItem`, params/result

**Criar/Editar:**

- `apps/admin-web/src/app/admin/configuracoes/tenant.types.ts` (editar: campo no DTO)
- `apps/admin-web/src/app/admin/configuracoes/tenant.service.ts` (editar: método PUT)
- `apps/admin-web/src/app/admin/configuracoes/configuracoes-page.ts` (editar: estado + handlers)
- `apps/admin-web/src/app/admin/configuracoes/configuracoes-page.html` (editar: seção nova)
- `apps/admin-web/src/app/admin/configuracoes/configuracoes-page.scss` (editar: estilos da seção — usar tokens/`space()`)
- `apps/admin-web/src/app/admin/configuracoes/configuracoes-page.spec.ts` (editar: novos testes)

**Tipos/serviço:**

```typescript
// tenant.types.ts
export interface TenantSettings {
  id: string;
  name: string;
  hasLogo: boolean;
  codigosNaoRecorriveis: string[];
}

// tenant.service.ts (injeta HttpClient como _http)
atualizarCodigosNaoRecorriveis(codigos: string[]): Observable<TenantSettings> {
  return this._http.put<TenantSettings>(
    '/api/v1/admin/tenant/codigos-nao-recorriveis', { codigos });
}
```

**Comportamento da seção (no componente):**

- Estado: `readonly naoRecorriveis = signal<{ codigoTuss: string; descricao: string }[]>([]);`
  populado no `ngOnInit` a partir de `settings.codigosNaoRecorriveis`. Para resolver a
  descrição de cada código salvo, faça **uma** chamada
  `catalogService.listarProcedimentos({ pagina: 1, itensPorPagina: 200 })` e monte um mapa
  `codigoTuss → descricao` (código sem match exibe só o código).
- Autocomplete: input de busca → `catalogService.listarProcedimentos({ busca, pagina: 1, itensPorPagina: 10 })`;
  resultados num dropdown; clicar adiciona `{ codigoTuss, descricao }` ao signal (evite
  duplicar `codigoTuss`).
- Remover: botão por item remove do signal.
- Salvar: botão chama
  `tenantService.atualizarCodigosNaoRecorriveis(naoRecorriveis().map(x => x.codigoTuss))`.
- **Todo `.subscribe` precisa de handler `error`** (regra do projeto) — setar `erroValidacao`.
- Injetar `CatalogService` via `inject(CatalogService)`.

**Testes (red primeiro):**

- `deve carregar códigos não recorríveis e resolver descrições no init`
- `deve adicionar um procedimento selecionado na busca à lista`
- `deve remover um procedimento da lista`
- `deve salvar enviando apenas os códigos TUSS` (verifica arg do spy `atualizarCodigosNaoRecorriveis`)

> Atualize o objeto `SETTINGS` do spec existente para incluir `codigosNaoRecorriveis: []`,
> e o spy `tenantServiceSpy` para incluir
> `atualizarCodigosNaoRecorriveis: vi.fn().mockReturnValue(of(...))`. Adicione um spy de
> `CatalogService` com `listarProcedimentos: vi.fn().mockReturnValue(of({ itens: [...], total, pagina:1, itensPorPagina:10 }))`.

**Aceite:**

- [ ] `pnpm -F admin-web lint` e `stylelint` sem warnings
- [ ] `pnpm -F admin-web test:ci` verde (cobertura ≥ 80%)
- [ ] `pnpm -F admin-web build` ok

**Commit:** `feat(admin-web): gerenciar procedimentos não recorríveis nas configurações (TASK-NREC-05)`

---

### TASK-NREC-06 — Selo "Não recorrível" na seleção de guias do recurso

- [ ] pendente

**Objetivo:** na tabela de candidatas do `recurso-guias`, marcar visualmente as guias não
recorríveis (o backend já as devolve com `naoRecorrivel=true` e já as exclui do lote).

**Depende de:** TASK-NREC-03 (a listagem de guias devolve `naoRecorrivel: boolean`).

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md (atenção: sem hex/cor nomeada,
`space()` com steps válidos — **ler `apps/admin-web/STYLES.md` antes do SCSS**).

**Ler (só isto):**

- `apps/admin-web/src/app/admin/faturamento/guia.types.ts:38-56` — interface `GuiaItem`
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.ts:340-368` — `<tr>` da candidata (onde entra o selo)
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.spec.ts` (abrir para ver o setup do spy e adicionar teste)

**Criar/Editar:**

- `apps/admin-web/src/app/admin/faturamento/guia.types.ts` (editar: campo no `GuiaItem`)
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.ts` (editar: selo no template)
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.scss` (editar: estilo do selo — tokens/`space()`)
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.spec.ts` (editar: teste)

**Tipo:**

```typescript
// adicionar ao final da interface GuiaItem em guia.types.ts.
// OPCIONAL de propósito: `GuiaItem` é tipo compartilhado e há 4 specs que constroem
// literais dele (guia-form, guia-list, guia.service, recurso-guias). O backend SEMPRE
// envia o campo, então `?` não custa nada em runtime e evita quebrar a compilação
// strict desses fixtures.
export interface GuiaItem {
  // ...campos existentes...
  naoRecorrivel?: boolean;
}
```

**Template** — na `<td>` da coluna "Guia" (ou "Situação"), exibir um selo quando marcado:

```html
@if (candidata.naoRecorrivel) {
<span class="recurso-guias__badge-nao-recorrivel">Não recorrível</span>
}
```

> Não é preciso mexer no `adicionarGuia` (individual) — adicionar manualmente uma guia não
> recorrível deve continuar permitido (escape hatch).

**Testes (red primeiro):**

- `deve exibir selo "Não recorrível" quando candidata.naoRecorrivel é true`
- `não deve exibir selo quando naoRecorrivel é false`

> Atualize os fixtures de `GuiaItem` no spec para incluir `naoRecorrivel: false` (e um
> caso `true`).

**Aceite:**

- [ ] `pnpm -F admin-web lint` e `stylelint` sem warnings
- [ ] `pnpm -F admin-web test:ci` verde
- [ ] `pnpm -F admin-web build` ok

**Commit:** `feat(admin-web): selo de guia não recorrível na seleção do recurso (TASK-NREC-06)`

---

## Checklist final

- [x] TASK-NREC-01 — campo no Tenant + migration
- [x] TASK-NREC-02 — service + endpoint
- [x] TASK-NREC-03 — flag na listagem de guias
- [ ] TASK-NREC-04 — lote pula não recorríveis
- [ ] TASK-NREC-05 — UI de configuração (autocomplete catálogo)
- [ ] TASK-NREC-06 — selo na seleção de guias do recurso

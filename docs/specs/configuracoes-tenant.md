# SPEC: Configurações do Tenant (renomear + logo no PDF do recurso)

> **Como executar:** uma task por sessão. Rode `/tdd-task docs/specs/configuracoes-tenant.md <TASK-ID>`,
> deixe a sessão implementar+commitar, limpe o contexto (`/clear`), rode a próxima.
> CLAUDE.md e MEMORY.md já entram no contexto de cada sessão — as tasks NÃO os repetem.
> **O checkbox `[ ]`/`[x]` é a única memória entre sessões:** uma sessão fria se orienta
> só por ele. Comece pela primeira task ainda `[ ]`; nunca refaça uma já marcada `[x]`.

## Objetivo

Criar a seção de **Configurações do Tenant** no admin-web onde o `TenantAdmin` pode (1) renomear o
tenant e (2) enviar uma logo (PNG/JPEG). A logo é armazenada por uma abstração de storage **desacoplada**
(`IFileStorage`, implementação inicial em disco local; futuro S3/Supabase sem tocar domínio) e passa a ser
renderizada no cabeçalho do PDF do recurso. O `Tenant` guarda apenas uma **chave opaca** (`LogoKey`),
nunca os bytes.

## Contexto compartilhado (válido para todas as tasks)

- **Decisão de arquitetura já tomada:** `IFileStorage` é a **terceira exceção sancionada** à regra
  "no speculative interfaces" do CLAUDE.md — mesma justificativa do `IGatewayPagamento` (necessidade
  futura concreta declarada: trocar disco local por S3/Supabase). A TASK-01 registra isso em `docs/DECISOES.md`.
- **Autorização:** todos os endpoints novos ficam sob `/api/v1/admin/tenant` com policy `TenantAccess`
  (TenantAdmin self-service; SaaS admin acessa por herança). O tenant alvo vem **sempre** de
  `ICurrentUser.TenantId` — nunca de parâmetro de rota.
- **Formatos de logo:** PNG e JPEG apenas. Validar por **assinatura de bytes (magic number)**, não por
  extensão. PNG = `89 50 4E 47 0D 0A 1A 0A`; JPEG = `FF D8 FF`. Tamanho máximo: **2 MB**.
- **Chave da logo (tenant-scoped, isolamento LGPD):** `tenants/{tenantId}/logo.png` ou `.jpg` conforme o
  tipo validado. O `content-type` servido é derivado da extensão da chave.
- **Backend:** bounded-context flat, sem Clean Architecture. Service → `AppDbContext` direto. `Result`/`Result<T>`
  para erros (`ValidationError`, `NotFoundError`, etc. em `App/Errors.cs`). `internal sealed`.
- **Frontend:** services usam `HttpClient` cru + tipos escritos à mão (NÃO o client gerado). Não é necessário
  rodar `pnpm generate-api-client` — o admin-web não consome o client gerado.

## Tasks

### TASK-TCFG-01 — Abstração de storage `IFileStorage` + `LocalFileStorage` + DI + infra

- [x] concluída

**Objetivo:** criar a abstração de storage desacoplada, a implementação em disco local, registrar no DI,
configurar `appsettings` + volume no docker-compose, e registrar a decisão em `docs/DECISOES.md`.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Program.cs:37-48` — bloco de registros `AddScoped` onde entra o storage.

**Criar:**

- `apps/backend/App/Storage/IFileStorage.cs` (novo) — interface + record do objeto retornado.
- `apps/backend/App/Storage/LocalFileStorage.cs` (novo) — implementação em disco.
- `apps/backend/App/Storage/StorageOptions.cs` (novo) — options bindadas de `appsettings`.
- `apps/backend/tests/Faturamento.Tests/Storage/LocalFileStorageTests.cs` (novo) — testes unitários.

**Editar:**

- `apps/backend/App/Program.cs` — registrar options + `IFileStorage`→`LocalFileStorage` (singleton).
- `apps/backend/App/appsettings.json` — bloco `Storage`.
- `infra/docker-compose.yml` — volume nomeado `honorare_storage` montado no serviço `backend` + env var do base path.
- `docs/DECISOES.md` — nova decisão (D-0xx) sancionando `IFileStorage` como 3ª exceção.

**Contrato a criar (forma exata):**

```csharp
// App/Storage/IFileStorage.cs
namespace App.Storage;

internal interface IFileStorage
{
    Task SaveAsync(string key, byte[] content, string contentType, CancellationToken ct = default);
    Task<FileStorageObject?> GetAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}

internal sealed record FileStorageObject(byte[] Content, string ContentType);
```

```csharp
// App/Storage/StorageOptions.cs
namespace App.Storage;

internal sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string BasePath { get; set; } = string.Empty;
}
```

**`LocalFileStorage` — requisitos:**

- Construtor recebe `StorageOptions` (via `IOptions<StorageOptions>`). Base path do disco.
- `SaveAsync`: cria diretórios pais, grava bytes. `GetAsync`: lê bytes; retorna `null` se não existir;
  `ContentType` derivado da extensão da chave (`.png`→`image/png`, `.jpg`/`.jpeg`→`image/jpeg`).
- `DeleteAsync`: remove o arquivo se existir (idempotente — não falha se ausente).
- **Segurança contra path traversal:** combine `BasePath` + key, resolva o caminho absoluto
  (`Path.GetFullPath`) e **rejeite** (lance) se o resultado não estiver sob `BasePath`. A key usa `/`
  como separador lógico; normalize para o separador do SO.

**`appsettings.json` — adicionar:**

```json
"Storage": {
  "BasePath": "/var/lib/honorare/storage"
}
```

**`docker-compose.yml` — no serviço `backend`:** adicionar `volumes: - honorare_storage:/var/lib/honorare/storage`
e env `Storage__BasePath: "/var/lib/honorare/storage"`; declarar `honorare_storage:` no bloco `volumes:` do final.

**`Program.cs` — registrar (perto da linha 48):**

```csharp
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
```

(adicionar `using App.Storage;` no topo.)

**Testes (red primeiro) — usar um diretório temporário (`Path.GetTempPath()` + `Guid`) como BasePath, limpar no fim:**

- `Deve gravar e recuperar bytes com o mesmo conteúdo (round-trip)`
- `Deve retornar null em GetAsync quando a chave não existe`
- `Deve derivar content-type image/png para chave .png e image/jpeg para .jpg`
- `Deve remover o arquivo em DeleteAsync e ser idempotente quando ausente`
- `Deve rejeitar key com path traversal (ex.: "../../etc/passwd")`

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde
- [ ] `docs/DECISOES.md` contém a nova decisão sobre `IFileStorage`
- [ ] `docker-compose.yml` tem o volume `honorare_storage` montado no `backend`

**Commit:** `feat(storage): abstração IFileStorage + LocalFileStorage desacoplado (TASK-TCFG-01)`

---

### TASK-TCFG-02 — `Tenant`: rename + LogoKey + migration

- [x] concluída

**Objetivo:** adicionar comportamento de domínio (`Rename`, `SetLogoKey`, `ClearLogoKey`) e a coluna
`LogoKey` ao `Tenant`, com a migration EF correspondente.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Identity/Tenant.cs:1-25` — aggregate atual (colado abaixo, pode não precisar abrir).
- `apps/backend/App/Identity/Configurations/TenantConfiguration.cs:1-16` — config EF (colada abaixo).
- `apps/backend/tests/Identity.Tests/Entities/TenantTests.cs` — formato dos testes da entidade.

**Editar:**

- `apps/backend/App/Identity/Tenant.cs`
- `apps/backend/App/Identity/Configurations/TenantConfiguration.cs`
- (migration gerada por CLI)

**Estado atual do `Tenant` (replicar o estilo — props private set, métodos de comando):**

```csharp
internal sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TenantStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Tenant() { }
    public static Tenant Create(string name) => new() { Id = Guid.NewGuid(), Name = name, Status = TenantStatus.Ativo, CreatedAt = DateTimeOffset.UtcNow };
    public void Activate() => Status = TenantStatus.Ativo;
    public void Suspend() => Status = TenantStatus.Suspenso;
    public void Cancel() => Status = TenantStatus.Cancelado;
}
```

**Adicionar ao `Tenant`:**

- `public string? LogoKey { get; private set; }`
- `public void Rename(string name)` — `name.Trim()`; lançar `ArgumentException` se vazio/whitespace
  (a validação amigável fica no service; o domínio só protege invariante).
- `public void SetLogoKey(string key)` e `public void ClearLogoKey()` (seta `LogoKey = null`).

**Config EF atual (`TenantConfiguration`):** já mapeia `Name` (HasMaxLength(256), IsRequired). **Adicionar:**

```csharp
builder.Property(t => t.LogoKey).HasMaxLength(512);
```

**Migration (rodar de DENTRO de `apps/backend/App`):**

```bash
cd apps/backend/App
dotnet ef migrations add AddTenantLogoKey --output-dir Identity/Migrations --namespace App.Identity.Migrations
```

Nota: `apps/backend/App/Identity/Migrations/.editorconfig` JÁ existe (suprime IDE0005/IDE0161/CA1515/CA1861)
— **não** crie outro. A migration deve ser apenas `AddColumn` de `LogoKey` (nullable, max 512).

**Testes (red primeiro), em `TenantTests.cs`:**

- `Rename deve atualizar Name fazendo trim`
- `Rename deve lançar quando nome é vazio ou whitespace`
- `SetLogoKey deve definir LogoKey e ClearLogoKey deve voltar a null`

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings (migration inclusa)
- [ ] `dotnet test apps/backend/Honorare.slnx` verde
- [ ] migration gerada em `Identity/Migrations` só adiciona a coluna `LogoKey`

**Commit:** `feat(identity): Tenant.Rename + LogoKey com migration (TASK-TCFG-02)`

---

### TASK-TCFG-03 — `TenantSettingsService` + endpoints: obter settings + renomear

- [x] concluída

**Objetivo:** criar o service de configurações do tenant (escopado por `ICurrentUser.TenantId`) com
`GetSettingsAsync` e `RenameAsync`, e expor os endpoints `GET`/`PATCH` em `/api/v1/admin/tenant`.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Depende de:** TASK-TCFG-02 (`Tenant.Rename(string)`, `Tenant.LogoKey`, `ClearLogoKey()` já existem).

**Ler (só isto):**

- `apps/backend/App/Identity/AdminService.cs:8-12,65-101` — padrão de service escopado + `UpdateProfileAsync` (validação + Result).
- `apps/backend/App/Identity/Endpoints/AdminEndpoints.cs:1-13,55-72` — padrão de MapGroup + handler + request record.
- `apps/backend/tests/Faturamento.Tests/Identity/AdminServiceTests.cs:1-55` — padrão de teste de integração (FakeCurrentUser, BuildContext, SeedTenant).

**Criar:**

- `apps/backend/App/Identity/TenantSettingsService.cs` (novo)
- `apps/backend/App/Identity/Endpoints/TenantSettingsEndpoints.cs` (novo)
- `apps/backend/tests/Faturamento.Tests/Identity/TenantSettingsServiceTests.cs` (novo)

**Editar:**

- `apps/backend/App/Program.cs` — `builder.Services.AddScoped<TenantSettingsService>();` (perto da linha 41)
  e `app.MapTenantSettingsEndpoints();` (perto da linha 200).

**Padrão de service escopado a seguir (de AdminService — replicar a forma):**

```csharp
internal sealed class AdminService(AppDbContext db, ICurrentUser currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly ICurrentUser _currentUser = currentUser;

    internal async Task<Result<ProfileSummary>> UpdateProfileAsync(string nome, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result<ProfileSummary>.Fail(new ValidationError("Nome é obrigatório."));
        // ... busca, muta, SaveChanges, devolve Result.Ok(dto)
    }
}
```

**`TenantSettingsService` — implementar:**

- record `TenantSettings(Guid Id, string Name, bool HasLogo)` (`HasLogo = tenant.LogoKey is not null`).
- `GetSettingsAsync(ct)`: busca o tenant por `_currentUser.TenantId!.Value` (use `_db.Tenants.FindAsync`);
  `NotFoundError` se nulo; devolve `Result<TenantSettings>`.
- `RenameAsync(string name, ct)`: valida `string.IsNullOrWhiteSpace(name)` → `ValidationError("Nome é obrigatório.")`;
  valida `name.Trim().Length > 256` → `ValidationError`; busca tenant; chama `tenant.Rename(name)`; `SaveChangesAsync`;
  devolve `Result<TenantSettings>`.

**Endpoints (`/api/v1/admin/tenant`, policy `TenantAccess`) — seguir AdminEndpoints:**

```csharp
internal static void MapTenantSettingsEndpoints(this WebApplication app)
{
    var g = app.MapGroup("/api/v1/admin/tenant").RequireAuthorization("TenantAccess");
    g.MapGet("/", GetSettingsAsync);
    g.MapPatch("/", RenameAsync);
}

internal sealed record RenameTenantRequest(string Name);
```

Handlers: `GetSettingsAsync` → `Results.Ok` ou `Results.Problem(404)`; `RenameAsync(RenameTenantRequest body, ...)`
→ `Results.Ok(value)` ou `Results.Problem(400)` em `ValidationError` / `404` em `NotFoundError`.

**Testes (red primeiro):**

- `GetSettingsAsync deve retornar nome e HasLogo=false do tenant atual`
- `RenameAsync deve atualizar o nome do tenant atual`
- `RenameAsync deve falhar com ValidationError quando nome é vazio`
- `RenameAsync deve falhar com NotFoundError quando o tenant não existe`

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde
- [ ] endpoints registrados em Program.cs

**Commit:** `feat(identity): TenantSettingsService + endpoints obter/renomear (TASK-TCFG-03)`

---

### TASK-TCFG-04 — Upload, download e remoção da logo

- [x] concluída

**Objetivo:** adicionar ao `TenantSettingsService` o upload da logo (validação por magic bytes + tamanho,
gravação via `IFileStorage`, atualização de `LogoKey`), a obtenção dos bytes e a remoção; expor os endpoints
`POST` (multipart), `GET .../logo` (stream) e `DELETE .../logo`.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Depende de:**

- TASK-TCFG-01 — `IFileStorage` existe (contrato abaixo, não precisa abrir):

```csharp
internal interface IFileStorage
{
    Task SaveAsync(string key, byte[] content, string contentType, CancellationToken ct = default);
    Task<FileStorageObject?> GetAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
internal sealed record FileStorageObject(byte[] Content, string ContentType);
```

- TASK-TCFG-02 — `Tenant.SetLogoKey(string)`, `Tenant.ClearLogoKey()`, `Tenant.LogoKey` existem.
- TASK-TCFG-03 — `TenantSettingsService(AppDbContext db, ICurrentUser currentUser)` existe e está registrado;
  `TenantSettingsEndpoints.MapTenantSettingsEndpoints` existe e está mapeado. O construtor do service vai
  **passar a receber também `IFileStorage`** — ajuste a injeção.

**Ler (só isto):**

- `apps/backend/App/Identity/TenantSettingsService.cs` — service criado na task anterior (editar).
- `apps/backend/App/Identity/Endpoints/TenantSettingsEndpoints.cs` — endpoints criados na task anterior (editar).

**Editar:**

- `apps/backend/App/Identity/TenantSettingsService.cs` — injetar `IFileStorage`; adicionar métodos.
- `apps/backend/App/Identity/Endpoints/TenantSettingsEndpoints.cs` — 3 endpoints novos.
- `apps/backend/tests/Faturamento.Tests/Identity/TenantSettingsServiceTests.cs` — novos testes.

**Validação de imagem (magic bytes) — implementar como helper estático no service:**

```csharp
// PNG: 89 50 4E 47 0D 0A 1A 0A ; JPEG: FF D8 FF
private static string? DetectImageContentType(byte[] bytes)
{
    if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        return "image/png";
    if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        return "image/jpeg";
    return null;
}
private const long MaxLogoBytes = 2 * 1024 * 1024; // 2 MB
```

**Métodos a adicionar no `TenantSettingsService`:**

- `UploadLogoAsync(byte[] content, CancellationToken ct)`:
  - `content.Length == 0` → `ValidationError("Arquivo vazio.")`
  - `content.Length > MaxLogoBytes` → `ValidationError("Logo excede 2 MB.")`
  - `DetectImageContentType(content)` nulo → `ValidationError("Formato inválido. Use PNG ou JPEG.")`
  - busca tenant (NotFound se ausente); extensão `image/png`→`.png`, `image/jpeg`→`.jpg`;
    `key = $"tenants/{tenantId}/logo{ext}"`; **se já houver `LogoKey` com extensão diferente, apague a antiga**
    via `_storage.DeleteAsync(antiga)` (evita órfão ao trocar png↔jpg).
  - `await _storage.SaveAsync(key, content, contentType, ct)`; `tenant.SetLogoKey(key)`; `SaveChangesAsync`;
    devolve `Result<TenantSettings>` (HasLogo=true).
- `GetLogoAsync(CancellationToken ct)`: busca tenant; se `LogoKey is null` → `Result<FileStorageObject>.Fail(NotFoundError)`;
  senão `_storage.GetAsync(LogoKey)` (NotFound se nulo); devolve `Result<FileStorageObject>`.
- `DeleteLogoAsync(CancellationToken ct)`: busca tenant; se `LogoKey is not null` → `_storage.DeleteAsync(LogoKey)`
  - `tenant.ClearLogoKey()` + `SaveChangesAsync`; devolve `Result` (Ok mesmo se já não havia logo — idempotente).

**Endpoints a adicionar:**

```csharp
g.MapPost("/logo", UploadLogoAsync).DisableAntiforgery();
g.MapGet("/logo", GetLogoAsync);
g.MapDelete("/logo", DeleteLogoAsync);
```

- `UploadLogoAsync(IFormFile file, TenantSettingsService svc, CancellationToken ct)`: ler bytes do
  `file.OpenReadStream()` para `byte[]` (MemoryStream); chamar `svc.UploadLogoAsync(bytes, ct)`;
  `Results.Ok(value)` ou `Problem(400/404)`. **`.DisableAntiforgery()` é obrigatório** em endpoint multipart
  minimal API senão dá 400.
- `GetLogoAsync` → em sucesso `Results.File(obj.Content, obj.ContentType)`; senão `Problem(404)`.
- `DeleteLogoAsync` → `Results.NoContent()`.

**Testes (red primeiro) — usar um `IFileStorage` fake/in-memory (Dictionary) no teste:**

- `UploadLogoAsync deve aceitar PNG válido, gravar no storage e setar LogoKey`
- `UploadLogoAsync deve rejeitar bytes que não são PNG/JPEG (ValidationError)`
- `UploadLogoAsync deve rejeitar arquivo acima de 2 MB`
- `GetLogoAsync deve retornar os bytes gravados`
- `GetLogoAsync deve falhar com NotFound quando o tenant não tem logo`
- `DeleteLogoAsync deve limpar LogoKey e remover do storage`

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde
- [ ] cobertura Identity ≥ 80%

**Commit:** `feat(identity): upload/download/remoção da logo do tenant (TASK-TCFG-04)`

---

### TASK-TCFG-05 — Renderizar a logo no PDF do recurso

- [x] concluída

**Objetivo:** carregar os bytes da logo do tenant via `IFileStorage` em `RecursoService.ObterDadosPdfAsync`,
adicionar campo nullable em `RecursoPdfData`, e renderizar a logo condicionalmente no cabeçalho do PDF.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Depende de:**

- TASK-TCFG-01 — `IFileStorage` existe (contrato abaixo):

```csharp
internal interface IFileStorage { Task<FileStorageObject?> GetAsync(string key, CancellationToken ct = default); /* ... */ }
internal sealed record FileStorageObject(byte[] Content, string ContentType);
```

- TASK-TCFG-02 — `Tenant.LogoKey` (string?) existe.

**Ler (só isto):**

- `apps/backend/App/Faturamento/RecursoService.cs:52-58,418-525` — record `RecursoPdfData` + `ObterDadosPdfAsync`
  (já busca o tenant na linha 427: `var tenant = await _db.Tenants.FirstOrDefaultAsync(...)`).
- `apps/backend/App/Faturamento/Pdf/RecursoPdfDocument.cs:42-59` — `ComposeHeader` (colado abaixo).
- `apps/backend/tests/Faturamento.Tests/Recurso/RecursoPdfDataTests.cs` — formato dos testes do PDF data.

**Editar:**

- `apps/backend/App/Faturamento/RecursoService.cs` — injetar `IFileStorage`; novo campo no record; popular bytes.
- `apps/backend/App/Faturamento/Pdf/RecursoPdfDocument.cs` — renderizar logo no header.
- `apps/backend/tests/Faturamento.Tests/Recurso/RecursoPdfDataTests.cs` — teste do novo campo.

**`RecursoService` — verificar o construtor atual** (provavelmente `RecursoService(AppDbContext db, ...)`):
adicionar `IFileStorage` como parâmetro. O `RecursoService` JÁ está registrado em `Program.cs:48` e o
`IFileStorage` em `Program.cs` (TASK-01), então a injeção resolve sozinha.

**`RecursoPdfData` (estado atual, linha 52):**

```csharp
internal sealed record RecursoPdfData(
    string TenantName,
    string OperadoraNome,
    string PrestadorNome,
    string? PrestadorRegistroProfissional,
    string Numero,
    IReadOnlyList<GuiaPdfData> Guias);
```

**Adicionar** parâmetro `byte[]? TenantLogo` ao record (ao final, antes ou depois de `Guias` — ajuste o
chamador na linha ~518). Em `ObterDadosPdfAsync`, após obter `tenant` (linha 427):

```csharp
byte[]? logoBytes = null;
if (tenant?.LogoKey is not null)
{
    var obj = await _storage.GetAsync(tenant.LogoKey, ct);
    logoBytes = obj?.Content;
}
```

e passar `logoBytes` ao construir `RecursoPdfData`.

**`ComposeHeader` atual (renderizar logo antes do nome):**

```csharp
private void ComposeHeader(IContainer c)
{
    c.Column(col =>
    {
        col.Item().Text(t => { t.DefaultTextStyle(s => s.Bold().FontSize(12)); t.Span(data.TenantName); });
        col.Item().Text($"Recurso Nº {data.Numero}");
        // ...
    });
}
```

**Modificar:** se `data.TenantLogo is not null`, renderizar a imagem no topo do `Column`, com altura limitada,
antes do nome do tenant. QuestPDF (raster, PNG/JPEG): `col.Item().Height(50).Image(data.TenantLogo).FitHeight();`
(`using QuestPDF.Infrastructure;` se necessário para `Image`). Guard com `if (data.TenantLogo is not null) { ... }`
(IDE0011: sempre usar chaves).

**Testes (red primeiro):**

- `ObterDadosPdfAsync deve popular TenantLogo com os bytes quando o tenant tem LogoKey`
- `ObterDadosPdfAsync deve deixar TenantLogo null quando o tenant não tem logo`
  (use um `IFileStorage` fake retornando bytes conhecidos; semear tenant com `SetLogoKey`).

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde
- [ ] cobertura Faturamento ≥ 90%

**Commit:** `feat(faturamento): logo do tenant no cabeçalho do PDF do recurso (TASK-TCFG-05)`

---

### TASK-TCFG-06 — Frontend: página de Configurações (renomear + upload de logo)

- [ ] pendente

**Objetivo:** criar a seção "Configurações" no admin-web: service, tipos, página com formulário de renomear
e upload/preview/remoção de logo, rota e link na sidebar.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md (inclui armadilhas Angular 20: `[selected]` vs `[value]`,
NG0701 com pipes locale → usar `Intl`, error handler em TODO subscribe, escala `space()`).

**Depende de (contrato backend já existente):**

- `GET  /api/v1/admin/tenant` → `{ id: string, name: string, hasLogo: boolean }`
- `PATCH /api/v1/admin/tenant` body `{ name: string }` → mesmo shape
- `POST /api/v1/admin/tenant/logo` multipart, campo `file` → mesmo shape
- `GET  /api/v1/admin/tenant/logo` → imagem (blob) ou 404
- `DELETE /api/v1/admin/tenant/logo` → 204

**Ler (só isto):**

- `apps/admin-web/src/app/admin/admin.service.ts:1-25` — padrão de service `HttpClient` cru (colado abaixo).
- `apps/admin-web/src/app/admin/profile/profile-page.ts:1-53` — padrão de página com signal + form + subscribe.
- `apps/admin-web/src/app/admin/faturamento/recurso.service.ts:71-77` — padrão de download de blob (colado abaixo).
- `apps/admin-web/src/app/admin/admin.routes.ts:1-35` — rotas (adicionar `configuracoes`).
- `apps/admin-web/src/app/admin/admin-shell.html` — sidebar (adicionar link após "Meu perfil").
- `apps/admin-web/STYLES.md` — antes de escrever SCSS (tokens, `space()`, mixins `text-*`).

**Criar:**

- `apps/admin-web/src/app/admin/configuracoes/tenant.service.ts` (novo)
- `apps/admin-web/src/app/admin/configuracoes/tenant.types.ts` (novo)
- `apps/admin-web/src/app/admin/configuracoes/configuracoes-page.ts` (novo)
- `apps/admin-web/src/app/admin/configuracoes/configuracoes-page.html` (novo)
- `apps/admin-web/src/app/admin/configuracoes/configuracoes-page.scss` (novo)
- `apps/admin-web/src/app/admin/configuracoes/configuracoes-page.spec.ts` (novo)
- `apps/admin-web/src/app/admin/configuracoes/tenant.service.spec.ts` (novo)

**Editar:**

- `apps/admin-web/src/app/admin/admin.routes.ts` — rota `configuracoes` (loadComponent).
- `apps/admin-web/src/app/admin/admin-shell.html` — link `routerLink="configuracoes"` "Configurações".

**Padrão de service (HttpClient cru — replicar):**

```typescript
@Injectable({ providedIn: "root" })
export class AdminService {
  private readonly http = inject(HttpClient);
  getProfile(): Observable<ProfileSummary> {
    return this.http.get<ProfileSummary>("/api/v1/admin/profile");
  }
  updateProfile(payload: UpdateProfilePayload): Observable<ProfileSummary> {
    return this.http.patch<ProfileSummary>("/api/v1/admin/profile", payload);
  }
}
```

**`TenantService` a implementar:**

- `getSettings(): Observable<TenantSettings>` → `GET /api/v1/admin/tenant`
- `rename(name: string): Observable<TenantSettings>` → `PATCH /api/v1/admin/tenant` body `{ name }`
- `uploadLogo(file: File): Observable<TenantSettings>` → montar `FormData`, `fd.append('file', file)`,
  `POST /api/v1/admin/tenant/logo` (NÃO setar Content-Type manualmente — o browser põe o boundary).
- `getLogoUrl()` ou `downloadLogo(): Observable<Blob>` → `GET /api/v1/admin/tenant/logo` com `{ responseType: 'blob' }`
  (padrão de blob abaixo); o componente faz `URL.createObjectURL(blob)` para preview.
- `deleteLogo(): Observable<unknown>` → `DELETE /api/v1/admin/tenant/logo`

```typescript
// padrão blob (recurso.service.ts:71)
this._http.get("/api/v1/admin/tenant/logo", { responseType: "blob" }).subscribe({
  next: (blob) => {
    /* URL.createObjectURL */
  },
  error: () => undefined,
});
```

**`tenant.types.ts`:**

```typescript
export interface TenantSettings {
  id: string;
  name: string;
  hasLogo: boolean;
}
```

**Página (`ConfiguracoesPage`) — seguir ProfilePage (signals + ReactiveFormsModule):**

- `ngOnInit`: `getSettings()` → popular form `nome` e signal `hasLogo`; se `hasLogo`, baixar blob e setar `logoUrl` signal.
- Form de renomear: igual ProfilePage (Validators.required + maxLength(256)), `saving`/`saved` signals,
  **error handler em todo subscribe** limpando `saving`.
- Upload: `<input type="file" accept="image/png,image/jpeg">`; no `change`, pegar `File`, validar tipo/tamanho
  no cliente (feedback rápido) e chamar `uploadLogo`; no sucesso, rebaixar/atualizar o preview.
- Remover logo: botão → `deleteLogo()` → limpar `logoUrl` + `hasLogo`.
- **NÃO usar pipes `currency`/`number`/`date`** (NG0701) — não há valores monetários aqui, mas siga a regra.
- SCSS: `@use 'styles/tokens' as *;` apenas `var(--color-*)`, `space(n)` da escala válida (1,2,3,4,6,8,12,16,24),
  mixins `@include text-*`. Sem hex, sem cores nomeadas, sem `!important`.

**Testes (red primeiro):**

- service: `getSettings`, `rename`, `uploadLogo` (FormData), `deleteLogo` chamam a URL/método certos (HttpTestingController).
- componente: carrega settings no init; submit chama `rename`; seleção de arquivo chama `uploadLogo`;
  todo subscribe tem error handler.

**Aceite:**

- [ ] `pnpm -F admin-web lint` (--max-warnings 0)
- [ ] `pnpm -F admin-web stylelint`
- [ ] `pnpm -F admin-web prettier:check`
- [ ] `pnpm -F admin-web test:ci` verde, cobertura ≥ 80%
- [ ] link "Configurações" aparece na sidebar e a rota carrega a página

**Commit:** `feat(admin-web): seção de configurações do tenant (renomear + logo) (TASK-TCFG-06)`

---

## Checklist final

- [x] TASK-TCFG-01 — IFileStorage + LocalFileStorage + infra
- [x] TASK-TCFG-02 — Tenant rename + LogoKey + migration
- [x] TASK-TCFG-03 — TenantSettingsService + endpoints (obter/renomear)
- [x] TASK-TCFG-04 — upload/download/remoção da logo
- [x] TASK-TCFG-05 — logo no PDF do recurso
- [ ] TASK-TCFG-06 — frontend: página de configurações

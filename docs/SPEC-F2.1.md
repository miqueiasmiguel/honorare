# SPEC — F2.1: Gerenciamento de usuários (TenantAdmin)

**Feature:** Telas e endpoints que permitem ao `TenantAdmin` gerenciar usuários do seu próprio tenant e editar o seu perfil.  
**Branch de referência:** `master`  
**Estimativa:** 4–5 dias de implementação sequencial.

---

## 1. Contexto e escopo

### O que já existe (não reimplementar)

| Artefato                       | Caminho                                          | Notas                                                                                                              |
| ------------------------------ | ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------ |
| Endpoints SaaS (SaasAdmin)     | `App/Identity/Endpoints/SaasEndpoints.cs`        | `GET/POST /api/v1/saas/tenants/{id}/users` e `PATCH .../status` — policy `SaasOnly`                                |
| `SaasService`                  | `App/Identity/SaasService.cs`                    | Listagem/criação/toggle de usuários com `tenantId` explícito                                                       |
| `ApplicationUser`              | `App/Identity/ApplicationUser.cs`                | Campos: `GoogleId`, `TenantId`, `MedicoId`, `IsActive`, `CreatedAt`                                                |
| `UserSummary` record           | `App/Identity/SaasService.cs` linhas 22–23       | `(Guid Id, string Email, string Role, bool IsActive, DateTimeOffset CreatedAt, Guid? MedicoId)`                    |
| `ICurrentUser` / `CurrentUser` | `App/Identity/ICurrentUser.cs`, `CurrentUser.cs` | Expõe `UserId`, `TenantId`, `MedicoId`, `IsSaasAdmin` a partir dos claims JWT                                      |
| Global query filter            | `App/Data/AppDbContext.cs`                       | Filtra `ITenantEntity` por `TenantId` — **`ApplicationUser` NÃO implementa `ITenantEntity`; filtro não se aplica** |
| Shell SaaS + rotas             | `apps/admin-web/src/app/saas/`                   | `SaasShell`, `TenantList`, `TenantDetail`, `saasGuard`, `saas.routes.ts`                                           |
| `AuthService` (Angular)        | `apps/admin-web/src/app/auth/auth.service.ts`    | Signal `role()` lê claim `role` do JWT                                                                             |

### O que será criado nesta feature

**Backend:**

- Campo `Nome` em `ApplicationUser` + migration EF Core
- Record `ProfileSummary`
- `Nome` adicionado ao record `UserSummary` existente
- Classe `AdminService` (`App/Identity/AdminService.cs`)
- Endpoints `AdminEndpoints.cs` (`/api/v1/admin/`, policy `TenantAccess`)
- Registro em `Program.cs`
- Suite xUnit `AdminServiceTests.cs`

**Frontend:**

- `apps/admin-web/src/app/admin/admin.types.ts`
- `apps/admin-web/src/app/admin/admin.service.ts`
- `apps/admin-web/src/app/admin/admin.guard.ts`
- `apps/admin-web/src/app/admin/admin-shell.ts` + `.html` + `.scss`
- `apps/admin-web/src/app/admin/admin.routes.ts`
- `apps/admin-web/src/app/admin/users/user-list.ts` + `.html` + `.scss`
- `apps/admin-web/src/app/admin/profile/profile-page.ts` + `.html` + `.scss`
- Atualização de `app.routes.ts`
- Atualização de `apps/admin-web/src/app/auth/callback/callback.ts`
- Suite Vitest para `UserList` e `ProfilePage`

### Fora do escopo

- Criação de novos usuários pelo TenantAdmin (responsabilidade do SaasAdmin, já implementado em `TenantDetail`)
- Edição do perfil de outros usuários (somente toggle de status)
- Convite por e-mail
- Portal do médico (`medico-pwa`) — F4.1

---

## 2. Decisões de design (fixas — não questionar)

1. **`ApplicationUser` não é `ITenantEntity`.** O global query filter não filtra usuários. O `AdminService` deve filtrar por `TenantId` explicitamente em toda query em `_db.Users`.

2. **Role é derivado dinamicamente, não é coluna no banco.** `AuthService.DeriveRole(user)` já implementa: `TenantId is null → SaasAdmin`, `MedicoId is not null → Medico`, `else → TenantAdmin`. Usar este método estático sempre.

3. **TenantAdmin não pode desativar a si mesmo.** `UpdateUserStatusAsync` deve rejeitar com `ForbiddenError` quando `userId == _currentUser.UserId && !isActive`.

4. **`Nome` é nullable.** Usuários criados antes desta migration têm `Nome = null`. O frontend deve tratar `null` como string vazia.

5. **`Nome` max 100 caracteres.** Validação no backend; o frontend deve aplicar `maxlength="100"` no campo HTML.

6. **`UserSummary` é o record compartilhado.** Adicionar `Nome` nele (não criar novo record). Isso atualiza automaticamente a resposta de `GET /api/v1/saas/tenants/{id}/users` — efeito intencional.

7. **Sem interface `IAdminService`.** Seguir a regra do projeto: não criar interfaces especulativas.

8. **Redirecionamento pós-login por role.** O componente `Callback` deve navegar para `/saas/tenants` (SaasAdmin) ou `/admin/users` (TenantAdmin) após armazenar os tokens. A rota `''` passa a usar um `homeRedirectGuard` que aplica a mesma lógica para quem já está autenticado.

---

## 3. Mapa de dependências entre tasks

```
TASK-ADMIN-01 (ApplicationUser.Nome + migration)
    └─► TASK-ADMIN-02 (AdminService backend)
            └─► TASK-ADMIN-03 (AdminEndpoints + Program.cs)
                    ├─► TASK-ADMIN-04 (testes xUnit AdminService)
                    └─► TASK-ADMIN-05 (regenerar cliente OpenAPI)
                                └─► TASK-ADMIN-06 (admin.types.ts + AdminService Angular)
                                        ├─► TASK-ADMIN-07 (adminGuard + homeRedirectGuard)
                                        │       └─► TASK-ADMIN-08 (AdminShell + rotas + app.routes.ts + Callback)
                                        ├─► TASK-ADMIN-09 (UserList + testes Vitest)
                                        └─► TASK-ADMIN-10 (ProfilePage + testes Vitest)
```

Executar as tasks **estritamente nesta ordem**. Cada task tem um critério de conclusão verificável antes de avançar.

---

## 4. Tasks

---

### TASK-ADMIN-01 ✅ — Adicionar `Nome` a `ApplicationUser` e criar migration EF Core

**Arquivos a modificar:**

- `apps/backend/App/Identity/ApplicationUser.cs`

**Arquivos a criar:**

- Migration gerada pelo `dotnet ef migrations add`

#### 4.1.1 Alterações em `ApplicationUser.cs`

Adicionar a propriedade e o método abaixo **imediatamente após** a linha `public DateTimeOffset CreatedAt { get; private set; }`:

```csharp
public string? Nome { get; private set; }
```

Adicionar o método após `public void Activate() => IsActive = true;`:

```csharp
public void UpdateNome(string nome) => Nome = nome.Trim();
```

Não alterar o construtor estático `Create` — `Nome` começa como `null`, que é o valor correto para usuários pré-existentes.

#### 4.1.2 Criar migration

Executar no diretório `apps/backend/`:

```bash
dotnet ef migrations add AddNomeToApplicationUser --project App
```

Verificar que a migration gerada contém:

- `migrationBuilder.AddColumn<string>(name: "Nome", table: "AspNetUsers", nullable: true, maxLength: 100)` no `Up`
- `migrationBuilder.DropColumn(name: "Nome", table: "AspNetUsers")` no `Down`

Se `maxLength` não estiver presente na migration gerada, adicionar a configuração de fluent API:

**Arquivo a modificar:** `App/Identity/Configurations/ApplicationUserConfiguration.cs`

```csharp
builder.Property(u => u.Nome).HasMaxLength(100);
```

Então recriar a migration:

```bash
dotnet ef migrations remove --project App
dotnet ef migrations add AddNomeToApplicationUser --project App
```

#### 4.1.3 Atualizar `UserSummary` em `SaasService.cs`

Localizar o record na linha 22 e atualizar:

```csharp
// antes:
internal sealed record UserSummary(
    Guid Id, string Email, string Role, bool IsActive, DateTimeOffset CreatedAt, Guid? MedicoId);

// depois:
internal sealed record UserSummary(
    Guid Id, string Email, string? Nome, string Role, bool IsActive, DateTimeOffset CreatedAt, Guid? MedicoId);
```

Atualizar as duas projeções em `SaasService.ListTenantUsersAsync` e `SaasService.CreateUserAsync` para incluir `u.Nome` e `user.Nome` respectivamente nos construtores do record.

**Critério de conclusão:** `dotnet build apps/backend/Honorare.slnx` passa sem warnings.

---

### TASK-ADMIN-02 ✅ — Criar `AdminService`

**Arquivo a criar:** `apps/backend/App/Identity/AdminService.cs`

#### 4.2.1 Records auxiliares

Adicionar no topo do arquivo (após `using`s):

```csharp
internal sealed record ProfileSummary(Guid Id, string Email, string? Nome, string Role);
```

#### 4.2.2 Classe `AdminService`

```csharp
using App.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Identity;

internal sealed class AdminService(AppDbContext db, ICurrentUser currentUser)
{
    private readonly AppDbContext _db = db;
    private readonly ICurrentUser _currentUser = currentUser;

    // Retorna todos os usuários do tenant do usuário autenticado.
    // ApplicationUser não implementa ITenantEntity; filtrar por TenantId explicitamente.
    internal async Task<IReadOnlyList<UserSummary>> GetUsersAsync(CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId!.Value;

        var users = await _db.Users
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(ct);

        return users
            .Select(u => new UserSummary(
                u.Id,
                u.Email!,
                u.Nome,
                AuthService.DeriveRole(u),
                u.IsActive,
                u.CreatedAt,
                u.MedicoId))
            .ToList();
    }

    // Ativa ou desativa um usuário do tenant.
    // Rejeita auto-desativação para prevenir lockout.
    internal async Task<Result> UpdateUserStatusAsync(
        Guid userId, bool isActive, CancellationToken ct = default)
    {
        if (!isActive && userId == _currentUser.UserId)
        {
            return Result.Fail(new ForbiddenError("Você não pode desativar sua própria conta."));
        }

        var tenantId = _currentUser.TenantId!.Value;
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);

        if (user is null)
        {
            return Result.Fail(new NotFoundError("Usuário não encontrado neste tenant."));
        }

        if (isActive) { user.Activate(); } else { user.Deactivate(); }
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // Retorna o perfil do usuário autenticado.
    internal async Task<Result<ProfileSummary>> GetProfileAsync(CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([_currentUser.UserId], ct);
        if (user is null)
        {
            return Result<ProfileSummary>.Fail(new NotFoundError("Usuário não encontrado."));
        }

        var role = AuthService.DeriveRole(user);
        return Result<ProfileSummary>.Ok(new ProfileSummary(user.Id, user.Email!, user.Nome, role));
    }

    // Atualiza o Nome do usuário autenticado.
    // Rejeita nome vazio ou com mais de 100 caracteres.
    internal async Task<Result<ProfileSummary>> UpdateProfileAsync(
        string nome, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<ProfileSummary>.Fail(new ValidationError("Nome é obrigatório."));
        }

        if (nome.Trim().Length > 100)
        {
            return Result<ProfileSummary>.Fail(new ValidationError("Nome deve ter no máximo 100 caracteres."));
        }

        var user = await _db.Users.FindAsync([_currentUser.UserId], ct);
        if (user is null)
        {
            return Result<ProfileSummary>.Fail(new NotFoundError("Usuário não encontrado."));
        }

        user.UpdateNome(nome);
        await _db.SaveChangesAsync(ct);

        var role = AuthService.DeriveRole(user);
        return Result<ProfileSummary>.Ok(new ProfileSummary(user.Id, user.Email!, user.Nome, role));
    }
}
```

**Critério de conclusão:** `dotnet build apps/backend/Honorare.slnx` passa sem warnings.

---

### TASK-ADMIN-03 ✅ — Criar `AdminEndpoints` e registrar em `Program.cs`

**Arquivo a criar:** `apps/backend/App/Identity/Endpoints/AdminEndpoints.cs`

```csharp
namespace App.Identity.Endpoints;

internal static class AdminEndpoints
{
    internal static void MapAdminEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/v1/admin").RequireAuthorization("TenantAccess");

        g.MapGet("/users", GetUsersAsync);
        g.MapPatch("/users/{userId}/status", UpdateUserStatusAsync);
        g.MapGet("/profile", GetProfileAsync);
        g.MapPatch("/profile", UpdateProfileAsync);
    }

    private static async Task<IResult> GetUsersAsync(
        AdminService adminService, CancellationToken ct)
    {
        var users = await adminService.GetUsersAsync(ct);
        return Results.Ok(users);
    }

    private static async Task<IResult> UpdateUserStatusAsync(
        Guid userId, UpdateAdminUserStatusRequest body,
        AdminService adminService, CancellationToken ct)
    {
        var result = await adminService.UpdateUserStatusAsync(userId, body.IsActive, ct);
        if (result.IsFailure)
        {
            var statusCode = result.Error switch
            {
                ForbiddenError => StatusCodes.Status403Forbidden,
                NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest
            };
            return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> GetProfileAsync(
        AdminService adminService, CancellationToken ct)
    {
        var result = await adminService.GetProfileAsync(ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest body, AdminService adminService, CancellationToken ct)
    {
        var result = await adminService.UpdateProfileAsync(body.Nome, ct);
        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: result.Error!.Message);
        }

        return Results.Ok(result.Value);
    }
}

internal sealed record UpdateAdminUserStatusRequest(bool IsActive);
internal sealed record UpdateProfileRequest(string Nome);
```

#### 4.3.1 Registrar em `Program.cs`

Localizar a chamada `app.MapSaasEndpoints();` e adicionar logo abaixo:

```csharp
app.MapAdminEndpoints();
```

Registrar `AdminService` no contêiner DI. Localizar `builder.Services.AddScoped<SaasService>();` e adicionar:

```csharp
builder.Services.AddScoped<AdminService>();
```

**Critério de conclusão:** `dotnet run --project apps/backend/App` inicia sem erros. `GET /api/v1/admin/users` com token de TenantAdmin retorna `200 []`.

---

### TASK-ADMIN-04 ✅ — Testes xUnit para `AdminService`

**Arquivo a criar:** `apps/backend/tests/Faturamento.Tests/Identity/AdminServiceTests.cs`

> **Nota:** O projeto de testes chama-se `Faturamento.Tests` por convenção do repositório. Testes de Identity vivem nele até haver projeto dedicado.

A suite usa `PostgresContainerFixture` (Testcontainers) — não usar mocks de banco.

```csharp
using App.Data;
using App.Identity;
using Microsoft.AspNetCore.Identity;

namespace Faturamento.Tests.Identity;

[Collection(nameof(PostgresCollection))]
public class AdminServiceTests(PostgresContainerFixture db)
{
    // Helper: cria um CurrentUser fake para os testes.
    private static ICurrentUser FakeCurrentUser(Guid userId, Guid tenantId) =>
        new FakeCurrentUser(userId, tenantId);

    // Helper: cria contexto de banco com o usuário fake injetado.
    private AppDbContext BuildContext(ICurrentUser user) =>
        new(db.BuildOptions<AppDbContext>(), user);

    // Helper: cria um tenant e dois usuários (admin + médico) no banco.
    private static async Task<(Tenant tenant, ApplicationUser admin, ApplicationUser medico)>
        SeedTenantAsync(AppDbContext ctx)
    {
        var tenant = Tenant.Create("Tenant Teste");
        ctx.Tenants.Add(tenant);

        var admin = ApplicationUser.Create("admin@test.com", tenant.Id, medicoId: null);
        var medico = ApplicationUser.Create("medico@test.com", tenant.Id, medicoId: Guid.NewGuid());

        var hasher = new PasswordHasher<ApplicationUser>();
        ctx.Users.Add(admin);
        ctx.Users.Add(medico);
        await ctx.SaveChangesAsync();

        return (tenant, admin, medico);
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsOnlyUsersOfCurrentTenant()
    {
        await using var ctx = BuildContext(FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenantA, adminA, _) = await SeedTenantAsync(ctx);

        var tenantB = Tenant.Create("Outro Tenant");
        ctx.Tenants.Add(tenantB);
        var userB = ApplicationUser.Create("outro@test.com", tenantB.Id, null);
        ctx.Users.Add(userB);
        await ctx.SaveChangesAsync();

        var currentUser = FakeCurrentUser(adminA.Id, tenantA.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var users = await service.GetUsersAsync();

        Assert.Equal(2, users.Count); // admin + medico do tenantA
        Assert.DoesNotContain(users, u => u.Email == "outro@test.com");
    }

    [Fact]
    public async Task GetUsersAsync_DerivedRoleIsCorrect()
    {
        await using var ctx = BuildContext(FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, medico) = await SeedTenantAsync(ctx);

        var currentUser = FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var users = await service.GetUsersAsync();

        Assert.Contains(users, u => u.Email == "admin@test.com" && u.Role == "TenantAdmin");
        Assert.Contains(users, u => u.Email == "medico@test.com" && u.Role == "Medico");
    }

    [Fact]
    public async Task UpdateUserStatusAsync_DeactivatesMedicoSuccessfully()
    {
        await using var ctx = BuildContext(FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, medico) = await SeedTenantAsync(ctx);

        var currentUser = FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateUserStatusAsync(medico.Id, isActive: false);

        Assert.True(result.IsSuccess);
        await using var ctx3 = BuildContext(currentUser);
        var updated = await ctx3.Users.FindAsync([medico.Id]);
        Assert.False(updated!.IsActive);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_SelfDeactivation_ReturnsForbidden()
    {
        await using var ctx = BuildContext(FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, _) = await SeedTenantAsync(ctx);

        var currentUser = FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateUserStatusAsync(admin.Id, isActive: false);

        Assert.True(result.IsFailure);
        Assert.IsType<ForbiddenError>(result.Error);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_UserFromOtherTenant_ReturnsNotFound()
    {
        await using var ctx = BuildContext(FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenantA, adminA, _) = await SeedTenantAsync(ctx);

        var tenantB = Tenant.Create("Tenant B");
        ctx.Tenants.Add(tenantB);
        var userB = ApplicationUser.Create("b@test.com", tenantB.Id, null);
        ctx.Users.Add(userB);
        await ctx.SaveChangesAsync();

        var currentUser = FakeCurrentUser(adminA.Id, tenantA.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateUserStatusAsync(userB.Id, isActive: false);

        Assert.True(result.IsFailure);
        Assert.IsType<NotFoundError>(result.Error);
    }

    [Fact]
    public async Task GetProfileAsync_ReturnsCurrentUserProfile()
    {
        await using var ctx = BuildContext(FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, _) = await SeedTenantAsync(ctx);

        var currentUser = FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.GetProfileAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(admin.Id, result.Value!.Id);
        Assert.Equal("admin@test.com", result.Value.Email);
        Assert.Equal("TenantAdmin", result.Value.Role);
        Assert.Null(result.Value.Nome);
    }

    [Fact]
    public async Task UpdateProfileAsync_PersistsNome()
    {
        await using var ctx = BuildContext(FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, _) = await SeedTenantAsync(ctx);

        var currentUser = FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateProfileAsync("  Dr. Exemplo  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Dr. Exemplo", result.Value!.Nome); // trim aplicado

        await using var ctx3 = BuildContext(currentUser);
        var refreshed = await ctx3.Users.FindAsync([admin.Id]);
        Assert.Equal("Dr. Exemplo", refreshed!.Nome);
    }

    [Fact]
    public async Task UpdateProfileAsync_NomeVazio_ReturnsValidationError()
    {
        await using var ctx = BuildContext(FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, _) = await SeedTenantAsync(ctx);

        var currentUser = FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateProfileAsync("   ");

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }

    [Fact]
    public async Task UpdateProfileAsync_NomeMuitoLongo_ReturnsValidationError()
    {
        await using var ctx = BuildContext(FakeCurrentUser(Guid.NewGuid(), Guid.NewGuid()));
        var (tenant, admin, _) = await SeedTenantAsync(ctx);

        var currentUser = FakeCurrentUser(admin.Id, tenant.Id);
        await using var ctx2 = BuildContext(currentUser);
        var service = new AdminService(ctx2, currentUser);

        var result = await service.UpdateProfileAsync(new string('a', 101));

        Assert.True(result.IsFailure);
        Assert.IsType<ValidationError>(result.Error);
    }
}

// Test double para ICurrentUser — não usar mocks de framework.
file sealed class FakeCurrentUser(Guid userId, Guid tenantId) : ICurrentUser
{
    public bool IsAuthenticated => true;
    public Guid UserId => userId;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => false;
}
```

**Critério de conclusão:** `dotnet test apps/backend/Honorare.slnx` passa com coverage ≥ 90% no contexto de `AdminService`.

---

### TASK-ADMIN-05 — Regenerar cliente OpenAPI

Executar:

```bash
pnpm generate-api-client
```

Verificar que `packages/api-contracts/` foi atualizado e contém:

- Tipos gerados para `UserSummary` com campo `nome?: string | null`
- Tipos para `ProfileSummary`
- Operações para os 4 novos endpoints `/api/v1/admin/`

**Critério de conclusão:** `pnpm -F admin-web build` (type check) passa após a regeneração.

---

### TASK-ADMIN-06 — Types e `AdminService` Angular

#### 4.6.1 Types

**Arquivo a criar:** `apps/admin-web/src/app/admin/admin.types.ts`

```typescript
import type { UserRole } from "../saas/saas.types";

export interface AdminUserSummary {
  id: string;
  email: string;
  nome: string | null;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
  medicoId: string | null;
}

export interface ProfileSummary {
  id: string;
  email: string;
  nome: string | null;
  role: string;
}

export interface UpdateProfilePayload {
  nome: string;
}
```

#### 4.6.2 Serviço

**Arquivo a criar:** `apps/admin-web/src/app/admin/admin.service.ts`

```typescript
import { inject, Injectable } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import type { AdminUserSummary, ProfileSummary, UpdateProfilePayload } from "./admin.types";

@Injectable({ providedIn: "root" })
export class AdminService {
  private readonly http = inject(HttpClient);

  listUsers(): Observable<AdminUserSummary[]> {
    return this.http.get<AdminUserSummary[]>("/api/v1/admin/users");
  }

  updateUserStatus(userId: string, isActive: boolean): Observable<unknown> {
    return this.http.patch(`/api/v1/admin/users/${userId}/status`, { isActive });
  }

  getProfile(): Observable<ProfileSummary> {
    return this.http.get<ProfileSummary>("/api/v1/admin/profile");
  }

  updateProfile(payload: UpdateProfilePayload): Observable<ProfileSummary> {
    return this.http.patch<ProfileSummary>("/api/v1/admin/profile", payload);
  }
}
```

**Critério de conclusão:** `pnpm -F admin-web lint` passa sem warnings.

---

### TASK-ADMIN-07 — Guards: `adminGuard` e `homeRedirectGuard`

#### 4.7.1 `adminGuard`

**Arquivo a criar:** `apps/admin-web/src/app/admin/admin.guard.ts`

```typescript
import { inject } from "@angular/core";
import { type CanActivateFn, Router } from "@angular/router";
import { AuthService } from "../auth/auth.service";

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.role() === "TenantAdmin") return true;
  return router.createUrlTree(["/"]);
};
```

#### 4.7.2 `homeRedirectGuard`

**Arquivo a criar:** `apps/admin-web/src/app/auth/home-redirect.guard.ts`

```typescript
import { inject } from "@angular/core";
import { type CanActivateFn, Router } from "@angular/router";
import { AuthService } from "./auth.service";

// Redireciona o usuário autenticado para a seção correta com base no role.
// Executado após authGuard (que garante autenticação).
export const homeRedirectGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const role = auth.role();
  if (role === "SaasAdmin") return router.createUrlTree(["/saas/tenants"]);
  if (role === "TenantAdmin") return router.createUrlTree(["/admin/users"]);
  return router.createUrlTree(["/auth/login"]);
};
```

**Critério de conclusão:** `pnpm -F admin-web lint` passa sem warnings.

---

### TASK-ADMIN-08 — `AdminShell`, rotas e atualização de `app.routes.ts` e `Callback`

#### 4.8.1 `AdminShell`

**Arquivo a criar:** `apps/admin-web/src/app/admin/admin-shell.ts`

```typescript
import { Component, inject } from "@angular/core";
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { AuthService } from "../auth/auth.service";

@Component({
  selector: "app-admin-shell",
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: "./admin-shell.html",
  styleUrl: "./admin-shell.scss",
})
export class AdminShell {
  private readonly _auth = inject(AuthService);
  private readonly _router = inject(Router);

  logout(): void {
    this._auth.logout();
    void this._router.navigate(["/auth/login"]);
  }
}
```

**Arquivo a criar:** `apps/admin-web/src/app/admin/admin-shell.html`

```html
<nav class="shell__nav">
  <span class="shell__brand">Honorare</span>
  <ul class="shell__links">
    <li>
      <a routerLink="users" routerLinkActive="shell__link--active" class="shell__link"> Usuários </a>
    </li>
    <li>
      <a routerLink="profile" routerLinkActive="shell__link--active" class="shell__link"> Meu perfil </a>
    </li>
  </ul>
  <button type="button" class="shell__logout" (click)="logout()">Sair</button>
</nav>
<main class="shell__content">
  <router-outlet />
</main>
```

**Arquivo a criar:** `apps/admin-web/src/app/admin/admin-shell.scss`

```scss
@use "styles/tokens" as *;

.shell__nav {
  display: flex;
  align-items: center;
  gap: space(6);
  padding: space(3) space(8);
  background: var(--color-superficie);
  border-bottom: 1px solid var(--color-contorno);
}

.shell__brand {
  @include text-label;
  font-weight: 600;
  color: var(--color-tinta);
  margin-right: auto;
}

.shell__links {
  display: flex;
  gap: space(4);
  list-style: none;
  margin: 0;
  padding: 0;
}

.shell__link {
  @include text-body;
  color: var(--color-tinta-suave);
  text-decoration: none;

  &--active {
    color: var(--color-tinta);
    font-weight: 600;
  }
}

.shell__logout {
  @include text-label;
  color: var(--color-tinta-suave);
  background: none;
  border: none;
  cursor: pointer;
  padding: space(1) space(2);
}

.shell__content {
  padding: space(8);
}
```

#### 4.8.2 Rotas do módulo admin

**Arquivo a criar:** `apps/admin-web/src/app/admin/admin.routes.ts`

```typescript
import { Routes } from "@angular/router";
import { AdminShell } from "./admin-shell";
import { adminGuard } from "./admin.guard";

export const adminRoutes: Routes = [
  {
    path: "",
    component: AdminShell,
    canActivate: [adminGuard],
    children: [
      {
        path: "users",
        loadComponent: () => import("./users/user-list").then((m) => m.UserList),
      },
      {
        path: "profile",
        loadComponent: () => import("./profile/profile-page").then((m) => m.ProfilePage),
      },
      { path: "", redirectTo: "users", pathMatch: "full" },
    ],
  },
];
```

#### 4.8.3 Atualizar `app.routes.ts`

**Arquivo a modificar:** `apps/admin-web/src/app/app.routes.ts`

Substituir o conteúdo completo:

```typescript
import { Routes } from "@angular/router";
import { authGuard } from "./auth/auth.guard";
import { homeRedirectGuard } from "./auth/home-redirect.guard";

export const routes: Routes = [
  {
    path: "auth/login",
    loadComponent: () => import("./auth/login/login").then((m) => m.Login),
  },
  {
    path: "auth/callback",
    loadComponent: () => import("./auth/callback/callback").then((m) => m.Callback),
  },
  {
    path: "saas",
    canActivate: [authGuard],
    loadChildren: () => import("./saas/saas.routes").then((m) => m.saasRoutes),
  },
  {
    path: "admin",
    canActivate: [authGuard],
    loadChildren: () => import("./admin/admin.routes").then((m) => m.adminRoutes),
  },
  {
    // Rota raiz: redireciona para /saas/tenants ou /admin/users com base no role.
    path: "",
    canActivate: [authGuard, homeRedirectGuard],
    children: [],
  },
  {
    path: "**",
    redirectTo: "",
  },
];
```

#### 4.8.4 Atualizar `Callback`

**Arquivo a modificar:** `apps/admin-web/src/app/auth/callback/callback.ts`

Localizar a chamada de navegação pós-armazenamento de tokens. O `Callback` atualmente navega para `/` após `storeTokens()`. Substituir a navegação para usar role-based redirect:

```typescript
// Substituir:
void this._router.navigate(["/"]);

// Por:
const role = this._auth.role();
const destination = role === "SaasAdmin" ? "/saas/tenants" : "/admin/users";
void this._router.navigate([destination]);
```

**Critério de conclusão:**

- `pnpm -F admin-web lint` passa.
- `pnpm -F admin-web build` compila sem erros.
- Navegando para `/` com token de TenantAdmin redireciona para `/admin/users`.
- Navegando para `/` com token de SaasAdmin redireciona para `/saas/tenants`.

---

### TASK-ADMIN-09 — Componente `UserList` e testes Vitest

#### 4.9.1 Componente

**Arquivo a criar:** `apps/admin-web/src/app/admin/users/user-list.ts`

```typescript
import { Component, inject, OnInit, signal } from "@angular/core";
import { AdminService } from "../admin.service";
import type { AdminUserSummary } from "../admin.types";
import type { UserRole } from "../../saas/saas.types";

@Component({
  selector: "app-user-list",
  templateUrl: "./user-list.html",
  styleUrl: "./user-list.scss",
})
export class UserList implements OnInit {
  private readonly adminService = inject(AdminService);

  readonly users = signal<AdminUserSummary[]>([]);

  ngOnInit(): void {
    this.loadUsers();
  }

  toggleStatus(user: AdminUserSummary): void {
    this.adminService.updateUserStatus(user.id, !user.isActive).subscribe({
      next: () => this.loadUsers(),
      error: () => undefined,
    });
  }

  roleBadgeClass(role: UserRole): string {
    return role === "Medico" ? "badge badge--medico" : "badge badge--admin";
  }

  statusBadgeClass(isActive: boolean): string {
    return isActive ? "badge badge--ativo" : "badge badge--inativo";
  }

  displayNome(user: AdminUserSummary): string {
    return user.nome ?? user.email;
  }

  formatDate(isoDate: string): string {
    const d = new Date(isoDate);
    const day = String(d.getUTCDate()).padStart(2, "0");
    const month = String(d.getUTCMonth() + 1).padStart(2, "0");
    const year = String(d.getUTCFullYear());
    return `${day}/${month}/${year}`;
  }

  private loadUsers(): void {
    this.adminService.listUsers().subscribe({
      next: (u) => this.users.set(u),
      error: () => undefined,
    });
  }
}
```

**Arquivo a criar:** `apps/admin-web/src/app/admin/users/user-list.html`

```html
<section class="user-list">
  <header class="user-list__header">
    <h1 class="user-list__title">Usuários</h1>
  </header>

  <table class="user-list__table">
    <thead>
      <tr>
        <th>Nome / E-mail</th>
        <th>Perfil</th>
        <th>Status</th>
        <th>Desde</th>
        <th>Ação</th>
      </tr>
    </thead>
    <tbody>
      @for (user of users(); track user.id) {
      <tr class="user-list__row">
        <td class="user-list__cell">
          <span class="user-list__nome">{{ displayNome(user) }}</span>
          @if (user.nome) {
          <span class="user-list__email">{{ user.email }}</span>
          }
        </td>
        <td class="user-list__cell">
          <span [class]="roleBadgeClass(user.role)">{{ user.role }}</span>
        </td>
        <td class="user-list__cell">
          <span [class]="statusBadgeClass(user.isActive)"> {{ user.isActive ? 'Ativo' : 'Inativo' }} </span>
        </td>
        <td class="user-list__cell">{{ formatDate(user.createdAt) }}</td>
        <td class="user-list__cell">
          <button type="button" class="btn btn--ghost btn--sm" (click)="toggleStatus(user)">{{ user.isActive ? 'Desativar' : 'Ativar' }}</button>
        </td>
      </tr>
      } @empty {
      <tr>
        <td colspan="5" class="user-list__empty">Nenhum usuário encontrado.</td>
      </tr>
      }
    </tbody>
  </table>
</section>
```

**Arquivo a criar:** `apps/admin-web/src/app/admin/users/user-list.scss`

```scss
@use "styles/tokens" as *;

.user-list {
  &__header {
    margin-bottom: space(6);
  }

  &__title {
    @include text-heading-2;
    color: var(--color-tinta);
  }

  &__table {
    width: 100%;
    border-collapse: collapse;
  }

  &__row:nth-child(even) {
    background: var(--color-superficie);
  }

  &__cell {
    @include text-body;
    padding: space(3) space(4);
    vertical-align: middle;
    border-bottom: 1px solid var(--color-contorno);
  }

  &__nome {
    display: block;
    color: var(--color-tinta);
    font-weight: 500;
  }

  &__email {
    @include text-label;
    color: var(--color-tinta-suave);
  }

  &__empty {
    @include text-body;
    color: var(--color-tinta-suave);
    text-align: center;
    padding: space(8);
  }
}
```

#### 4.9.2 Testes Vitest

**Arquivo a criar:** `apps/admin-web/src/app/admin/users/user-list.spec.ts`

```typescript
import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { UserList } from "./user-list";
import { AdminService } from "../admin.service";
import type { AdminUserSummary } from "../admin.types";

const mockUsers: AdminUserSummary[] = [
  {
    id: "1",
    email: "admin@test.com",
    nome: "Dr. Admin",
    role: "TenantAdmin",
    isActive: true,
    createdAt: "2025-01-15T00:00:00Z",
    medicoId: null,
  },
  {
    id: "2",
    email: "medico@test.com",
    nome: null,
    role: "Medico",
    isActive: false,
    createdAt: "2025-02-20T00:00:00Z",
    medicoId: "med-1",
  },
];

function setup(users: AdminUserSummary[] = mockUsers) {
  const adminServiceSpy = {
    listUsers: vi.fn().mockReturnValue(of(users)),
    updateUserStatus: vi.fn().mockReturnValue(of(undefined)),
  };

  TestBed.configureTestingModule({
    imports: [UserList],
    providers: [{ provide: AdminService, useValue: adminServiceSpy }],
  });

  const fixture = TestBed.createComponent(UserList);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, adminService: adminServiceSpy };
}

describe("UserList", () => {
  it("carrega usuários na inicialização", () => {
    const { component, adminService } = setup();
    expect(adminService.listUsers).toHaveBeenCalledOnce();
    expect(component.users()).toHaveLength(2);
  });

  it("exibe nome quando disponível", () => {
    const { fixture } = setup();
    const nomes = fixture.nativeElement.querySelectorAll<HTMLElement>(".user-list__nome");
    expect(nomes[0]?.textContent?.trim() ?? "").toBe("Dr. Admin");
  });

  it("exibe email quando nome é null", () => {
    const { fixture } = setup();
    const nomes = fixture.nativeElement.querySelectorAll<HTMLElement>(".user-list__nome");
    expect(nomes[1]?.textContent?.trim() ?? "").toBe("medico@test.com");
  });

  it("exibe mensagem de lista vazia quando não há usuários", () => {
    const { fixture } = setup([]);
    const empty = fixture.nativeElement.querySelector<HTMLElement>(".user-list__empty");
    expect(empty?.textContent?.trim() ?? "").toBe("Nenhum usuário encontrado.");
  });

  it("toggleStatus chama updateUserStatus com valor invertido e recarrega lista", () => {
    const { component, adminService } = setup();
    const user = component.users()[0];
    component.toggleStatus(user);
    expect(adminService.updateUserStatus).toHaveBeenCalledWith("1", false);
    expect(adminService.listUsers).toHaveBeenCalledTimes(2);
  });

  it("formatDate formata data no padrão DD/MM/AAAA", () => {
    const { component } = setup();
    expect(component.formatDate("2025-01-15T00:00:00Z")).toBe("15/01/2025");
  });

  it("displayNome retorna nome quando presente", () => {
    const { component } = setup();
    expect(component.displayNome(mockUsers[0])).toBe("Dr. Admin");
  });

  it("displayNome retorna email quando nome é null", () => {
    const { component } = setup();
    expect(component.displayNome(mockUsers[1])).toBe("medico@test.com");
  });
});
```

**Critério de conclusão:** `pnpm -F admin-web test:ci` passa com todos os testes de `user-list.spec.ts` verdes.

---

### TASK-ADMIN-10 — Componente `ProfilePage` e testes Vitest

#### 4.10.1 Componente

**Arquivo a criar:** `apps/admin-web/src/app/admin/profile/profile-page.ts`

```typescript
import { Component, inject, OnInit, signal } from "@angular/core";
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { AdminService } from "../admin.service";
import type { ProfileSummary } from "../admin.types";

@Component({
  selector: "app-profile-page",
  imports: [ReactiveFormsModule],
  templateUrl: "./profile-page.html",
  styleUrl: "./profile-page.scss",
})
export class ProfilePage implements OnInit {
  private readonly adminService = inject(AdminService);

  readonly profile = signal<ProfileSummary | null>(null);
  readonly saving = signal(false);
  readonly saved = signal(false);

  readonly form = new FormGroup({
    nome: new FormControl("", {
      nonNullable: true,
      validators: [(c) => Validators.required(c), (c) => Validators.maxLength(100)(c)],
    }),
  });

  ngOnInit(): void {
    this.adminService.getProfile().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.form.controls.nome.setValue(p.nome ?? "");
      },
      error: () => undefined,
    });
  }

  submit(): void {
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true);
    this.saved.set(false);
    this.adminService.updateProfile({ nome: this.form.controls.nome.value }).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.saving.set(false);
        this.saved.set(true);
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }
}
```

**Arquivo a criar:** `apps/admin-web/src/app/admin/profile/profile-page.html`

```html
<section class="profile">
  <h1 class="profile__title">Meu perfil</h1>

  @if (profile(); as p) {
  <dl class="profile__info">
    <dt class="profile__label">E-mail</dt>
    <dd class="profile__value">{{ p.email }}</dd>
    <dt class="profile__label">Perfil</dt>
    <dd class="profile__value">{{ p.role }}</dd>
  </dl>
  }

  <form class="profile__form" (ngSubmit)="submit()" [formGroup]="form">
    <label class="profile__field-label" for="nome">Nome de exibição</label>
    <input id="nome" type="text" class="profile__input" formControlName="nome" maxlength="100" autocomplete="name" />
    @if (form.controls.nome.touched && form.controls.nome.hasError('required')) {
    <span class="profile__error">Nome é obrigatório.</span>
    } @if (form.controls.nome.touched && form.controls.nome.hasError('maxlength')) {
    <span class="profile__error">Nome deve ter no máximo 100 caracteres.</span>
    }

    <div class="profile__actions">
      <button type="submit" class="btn btn--primary" [disabled]="saving()">{{ saving() ? 'Salvando…' : 'Salvar' }}</button>
      @if (saved()) {
      <span class="profile__success">Perfil salvo.</span>
      }
    </div>
  </form>
</section>
```

**Arquivo a criar:** `apps/admin-web/src/app/admin/profile/profile-page.scss`

```scss
@use "styles/tokens" as *;

.profile {
  max-width: 480px;

  &__title {
    @include text-heading-2;
    color: var(--color-tinta);
    margin-bottom: space(6);
  }

  &__info {
    display: grid;
    grid-template-columns: max-content 1fr;
    gap: space(2) space(6);
    margin-bottom: space(8);
  }

  &__label {
    @include text-label;
    color: var(--color-tinta-suave);
  }

  &__value {
    @include text-body;
    color: var(--color-tinta);
  }

  &__form {
    display: flex;
    flex-direction: column;
    gap: space(3);
  }

  &__field-label {
    @include text-label;
    color: var(--color-tinta);
  }

  &__input {
    @include text-body;
    padding: space(2) space(3);
    border: 1px solid var(--color-contorno);
    border-radius: 4px;
    color: var(--color-tinta);
    background: var(--color-fundo);
  }

  &__error {
    @include text-label;
    color: var(--color-erro);
  }

  &__success {
    @include text-label;
    color: var(--color-sucesso);
  }

  &__actions {
    display: flex;
    align-items: center;
    gap: space(4);
    margin-top: space(2);
  }
}
```

#### 4.10.2 Testes Vitest

**Arquivo a criar:** `apps/admin-web/src/app/admin/profile/profile-page.spec.ts`

```typescript
import { TestBed } from "@angular/core/testing";
import { of } from "rxjs";
import { ProfilePage } from "./profile-page";
import { AdminService } from "../admin.service";
import type { ProfileSummary } from "../admin.types";

const mockProfile: ProfileSummary = {
  id: "user-1",
  email: "admin@test.com",
  nome: "Dr. Admin",
  role: "TenantAdmin",
};

function setup(profile: ProfileSummary = mockProfile) {
  const adminServiceSpy = {
    getProfile: vi.fn().mockReturnValue(of(profile)),
    updateProfile: vi.fn().mockReturnValue(of({ ...profile, nome: "Novo Nome" })),
  };

  TestBed.configureTestingModule({
    imports: [ProfilePage],
    providers: [{ provide: AdminService, useValue: adminServiceSpy }],
  });

  const fixture = TestBed.createComponent(ProfilePage);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, adminService: adminServiceSpy };
}

describe("ProfilePage", () => {
  it("carrega perfil na inicialização e preenche formulário", () => {
    const { component } = setup();
    expect(component.profile()).toMatchObject({ email: "admin@test.com" });
    expect(component.form.controls.nome.value).toBe("Dr. Admin");
  });

  it("preenche nome vazio quando perfil tem nome null", () => {
    const { component } = setup({ ...mockProfile, nome: null });
    expect(component.form.controls.nome.value).toBe("");
  });

  it("exibe e-mail como somente leitura", () => {
    const { fixture } = setup();
    const dd = fixture.nativeElement.querySelectorAll<HTMLElement>(".profile__value");
    expect(dd[0]?.textContent?.trim() ?? "").toBe("admin@test.com");
  });

  it("submit chama updateProfile com o nome do formulário", () => {
    const { component, adminService } = setup();
    component.form.controls.nome.setValue("Novo Nome");
    component.submit();
    expect(adminService.updateProfile).toHaveBeenCalledWith({ nome: "Novo Nome" });
  });

  it("submit não é chamado quando formulário é inválido", () => {
    const { component, adminService } = setup();
    component.form.controls.nome.setValue("");
    component.submit();
    expect(adminService.updateProfile).not.toHaveBeenCalled();
  });

  it("saved é true após submit bem-sucedido", () => {
    const { component } = setup();
    component.form.controls.nome.setValue("Novo Nome");
    component.submit();
    expect(component.saved()).toBe(true);
  });

  it("atualiza sinal profile após submit bem-sucedido", () => {
    const { component } = setup();
    component.form.controls.nome.setValue("Novo Nome");
    component.submit();
    expect(component.profile()?.nome).toBe("Novo Nome");
  });
});
```

**Critério de conclusão:** `pnpm -F admin-web test:ci` passa com todos os testes de `profile-page.spec.ts` verdes.

---

## 5. Verificação final (executar nesta ordem)

```bash
# Backend: build limpo + testes + coverage
dotnet build apps/backend/Honorare.slnx
dotnet test apps/backend/Honorare.slnx

# Frontend: lint + testes + build
pnpm -F admin-web lint
pnpm -F admin-web stylelint
pnpm -F admin-web test:ci
pnpm -F admin-web build
```

Todos os comandos devem passar sem erros ou warnings.

---

## 6. Checklist de revisão antes de marcar F2.1 como concluída

- [ ] Migration `AddNomeToApplicationUser` presente e `dotnet ef database update` aplica sem erros
- [ ] `GET /api/v1/admin/users` (token TenantAdmin) → 200 com lista de usuários do tenant
- [ ] `GET /api/v1/admin/users` (token SaasAdmin) → 403 (SaasAdmin não tem acesso a `/admin/`)
- [ ] `PATCH /api/v1/admin/users/{userId}/status` com userId == currentUser → 403
- [ ] `PATCH /api/v1/admin/users/{userId}/status` com userId de outro tenant → 404
- [ ] `PATCH /api/v1/admin/profile` com nome de 101 chars → 400
- [ ] Login com TenantAdmin → redireciona para `/admin/users`
- [ ] Login com SaasAdmin → redireciona para `/saas/tenants`
- [ ] Acesso a `/admin/users` com token SaasAdmin → redirecionado para `/` (homeRedirectGuard → `/saas/tenants`)
- [ ] `pnpm -F admin-web test:ci` coverage ≥ 80%
- [ ] `dotnet test` coverage ≥ 80% geral; coverage de `AdminService` ≥ 90%

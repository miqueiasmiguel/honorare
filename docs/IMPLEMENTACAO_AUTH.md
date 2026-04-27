# Honorare — Implementação de Autenticação e Autorização

> **Arquivo de contexto para IA.** Contém decisões, restrições, modelo de dados e tasks ordenadas para implementar o sistema de auth do Honorare. Leia integralmente antes de gerar qualquer código.

---

## Contexto e decisões

### Personas e isolamento

| Persona                               | Role (claim)  | Isolamento de dados                           |
| ------------------------------------- | ------------- | --------------------------------------------- |
| Admin geral do SaaS (dono do produto) | `SaasAdmin`   | Nenhum — acessa qualquer tenant               |
| Admin da empresa de faturamento       | `TenantAdmin` | `TenantId` via global query filter            |
| Médico (usuário do PWA)               | `Medico`      | `TenantId` + `MedicoId` explícito nas queries |

### Método de autenticação

**Google OAuth 2.0 como único método no MVP.** Não há senha. Não há magic link. Não há MFA. Não há convite por email.

Racional:

- Público B2B brasileiro tem altíssima penetração de Gmail/Google Workspace
- Elimina toda superfície de ataque de senha
- ASP.NET Core Identity tem provider Google built-in
- Zero infraestrutura nova (OAuth app no Google Console é gratuito)
- O `PasswordHash` do `IdentityUser` fica nulo — nenhuma senha é persistida

Diferimentos conscientes (não implementar no MVP):

- Magic link → fase 2, se aparecer usuário sem Google
- Passkeys (WebAuthn) → fase 3, quando PWA tiver adoção real
- MFA → pós-MVP
- Social login adicional (Apple, Microsoft) → pós-MVP
- Convite por email → SaaS admin cria usuários diretamente via painel

### Claims do JWT

```json
{
  "sub": "user-guid",
  "role": "SaasAdmin | TenantAdmin | Medico",
  "tenant_id": "guid", // AUSENTE para SaasAdmin
  "medico_id": "guid", // PRESENTE apenas para Medico
  "email": "...",
  "jti": "token-guid",
  "exp": 0
}
```

### Políticas de autorização (ASP.NET Core)

```
SaasOnly     → role == SaasAdmin
TenantAccess → role == TenantAdmin OR SaasAdmin
MedicoAccess → role == Medico
```

### Segmentação de rotas

```
/api/v1/saas/**   → [Authorize("SaasOnly")]
/api/v1/admin/**  → [Authorize("TenantAccess")]
/api/v1/medico/** → [Authorize("MedicoAccess")]
```

### Isolamento por TenantId — como funciona

`ICurrentUser` é um serviço scoped (por request) que lê claims do `IHttpContextAccessor`. Ele é injetado no `AppDbContext` e usado no global query filter:

```csharp
public interface ICurrentUser
{
    Guid? TenantId { get; }   // null quando SaasAdmin
    Guid? MedicoId { get; }   // null quando não é Medico
    bool IsSaasAdmin { get; }
}
```

O global query filter em toda entidade com `TenantId`:

```csharp
builder.HasQueryFilter(e =>
    _currentUser.IsSaasAdmin || e.TenantId == _currentUser.TenantId);
```

**Isolamento por MedicoId não usa global filter** — é `Where(e => e.MedicoId == _currentUser.MedicoId)` explícito nas queries dos endpoints `/api/v1/medico/**`. Segundo global filter interagiria mal com queries do admin.

### Regra LGPD para SaasAdmin acessando dados de tenant

Toda rota `/api/v1/saas/**` que acessa dados de um tenant específico deve receber `tenantId` como parâmetro de rota e o controller deve validar que o tenant existe. Isso torna auditável "o admin acessou dados do tenant X". Não implementar acesso implícito a dados de tenant a partir do contexto do SaasAdmin.

### Restrições do projeto (do CLAUDE.md e DECISOES.md)

- **Sem CQRS, MediatR, Repository, AutoMapper** (D-018)
- **Sem interfaces especulativas** — nenhuma interface além das já definidas (D-019)
- **`AppDbContext` único** (D-011)
- **TreatWarningsAsErrors** — todo código deve compilar sem warnings
- **Nullable enable** — sem `null` sem anotação explícita
- **Convenções de nomenclatura Roslyn**: campos privados `_camelCase`, métodos async `PascalCaseAsync`, constantes `PascalCase`
- Bounded context `Identity` é folha da hierarquia — nenhum outro contexto pode depender de contextos acima dele

---

## Modelo de dados

### Entidades no bounded context `Identity`

```csharp
// Estende IdentityUser do ASP.NET Core Identity
// PasswordHash SEMPRE nulo — auth é exclusivamente via Google OAuth
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? GoogleId { get; private set; }
    public Guid? TenantId { get; private set; }   // null = SaasAdmin
    public Guid? MedicoId { get; private set; }   // null = não é Medico
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public TenantStatus Status { get; private set; }  // Ativo, Suspenso, Cancelado
    public DateTimeOffset CreatedAt { get; private set; }
}

public enum TenantStatus { Ativo, Suspenso, Cancelado }

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }   // hash SHA-256, nunca o valor raw
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public string? ReplacedByTokenId { get; private set; }
}
```

---

## Tasks de implementação

As tasks estão ordenadas por dependência. Nunca pular uma task antes de concluir a anterior dentro do mesmo grupo.

---

### TASK-AUTH-01 — Instalar pacotes NuGet (Implementado ✅)

**Escopo:** `apps/backend/App/App.csproj` e projetos relevantes.

Pacotes a adicionar:

```
Microsoft.AspNetCore.Authentication.Google
Microsoft.AspNetCore.Authentication.JwtBearer
System.IdentityModel.Tokens.Jwt
Microsoft.AspNetCore.Identity.EntityFrameworkCore
```

Verificar se já existem antes de adicionar. Usar NuGet Central Package Management (`Directory.Packages.props`) — adicionar versão lá, referenciar sem versão nos `.csproj`.

**Critério de pronto:** `dotnet build` limpo sem warnings.

---

### TASK-AUTH-02 — Entidade `ApplicationUser`, `Tenant`, `RefreshToken` (Implementado ✅)

**Escopo:** `apps/backend/Identity/`

- Criar `ApplicationUser : IdentityUser<Guid>` com propriedades do modelo de dados acima
- Criar `Tenant` com `TenantStatus` enum
- Criar `RefreshToken`
- Todas as propriedades com setters privados; expor factory methods ou construtors com parâmetros obrigatórios
- Sem construtores sem parâmetros públicos (EF Core aceita privado)
- Configurações EF Core em `Identity/Configurations/` (um arquivo por entidade, implementando `IEntityTypeConfiguration<T>`)
- Registrar as configurações no `AppDbContext` via `modelBuilder.ApplyConfigurationsFromAssembly`

**Restrições:**

- `ApplicationUser.PasswordHash` deve ser ignorado no modelo ou sempre nulo — incluir comentário explicando o motivo
- Índice único em `ApplicationUser.GoogleId`
- Índice único em `RefreshToken.TokenHash`
- `Tenant` e `ApplicationUser` NÃO têm global query filter por `TenantId` — são entidades do próprio Identity que o SaasAdmin gerencia globalmente

**Critério de pronto:** `dotnet build` limpo. Migration gerada com `dotnet ef migrations add InitIdentity` (não aplicar — só gerar).

---

### TASK-AUTH-03 — Serviço `ICurrentUser` (Implementado ✅)

**Escopo:** `apps/backend/Identity/`

```csharp
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid? TenantId { get; }
    Guid? MedicoId { get; }
    bool IsSaasAdmin { get; }
    bool IsAuthenticated { get; }
}
```

Implementação `CurrentUser` lê claims do `IHttpContextAccessor`. Registrar como `Scoped` em `Program.cs`.

Este serviço é o único mecanismo pelo qual o global query filter do `AppDbContext` decide se aplica ou ignora o filtro de `TenantId`. Nenhum outro mecanismo deve existir para esse bypass.

**Critério de pronto:** unit test cobrindo: SaasAdmin retorna `TenantId == null` e `IsSaasAdmin == true`; TenantAdmin retorna o `TenantId` correto; Medico retorna `TenantId` e `MedicoId` corretos.

---

### TASK-AUTH-04 — Global query filter no `AppDbContext` (Implementado ✅)

**Escopo:** `apps/backend/App/AppDbContext.cs`

- Injetar `ICurrentUser` via construtor do `AppDbContext`
- Para toda entidade que implementa `ITenantEntity` (interface com `Guid TenantId`), aplicar:
  ```csharp
  builder.HasQueryFilter(e =>
      _currentUser.IsSaasAdmin || e.TenantId == _currentUser.TenantId);
  ```
- `Tenant`, `ApplicationUser`, `RefreshToken` NÃO implementam `ITenantEntity`

**Critério de pronto:** teste de integração com `PostgresContainerFixture` criando dois tenants, dados em ambos, e verificando que `TenantAdmin` do tenant A não vê dados do tenant B.

---

### TASK-AUTH-05 — Google OAuth callback e emissão de JWT (Implementado ✅)

**Escopo:** `apps/backend/Identity/` + `apps/backend/App/Program.cs`

Fluxo:

1. Frontend redireciona para `/api/v1/auth/google` (backend inicia OAuth)
2. Google redireciona para `/api/v1/auth/google/callback`
3. Backend recebe `GoogleId` + `email` do Google
4. Busca `ApplicationUser` por `GoogleId` — se não existe e não existe por email, retorna 401 (usuário não cadastrado; SaaS admin pré-cadastra)
5. Se `IsActive == false` ou `Tenant.Status != Ativo`, retorna 403
6. Emite JWT (access token, 15 min) + refresh token (7 dias)
7. Refresh token é persistido como hash SHA-256 em `RefreshToken`
8. Retorna `{ accessToken, refreshToken, expiresIn }` como JSON

Configuração em `Program.cs`:

```csharp
builder.Services
    .AddAuthentication()
    .AddGoogle(o => {
        o.ClientId = builder.Configuration["Google:ClientId"]!;
        o.ClientSecret = builder.Configuration["Google:ClientSecret"]!;
    })
    .AddJwtBearer(/* configuração padrão */);
```

Variáveis de ambiente necessárias (adicionar ao `.env` e ao `appsettings.json`):

```
Google__ClientId
Google__ClientSecret
Jwt__Secret          # chave simétrica ≥ 256 bits
Jwt__Issuer
Jwt__Audience
```

**Restrições:**

- Nunca logar o refresh token raw
- O `jti` do JWT deve ser um `Guid.NewGuid()` para permitir revogação futura
- `TenantId` e `MedicoId` só entram no JWT se não forem nulos

**Critério de pronto:** fluxo completo testável via Swagger/httpie. JWT decodificável com claims corretos para cada role.

**Notas de implementação:**

- `AuthService.ProcessGoogleCallbackAsync` encapsula toda a lógica de negócio (lookup, validações, emissão de tokens)
- `GET /api/v1/auth/google` inicia o challenge; `GET /api/v1/auth/google/finalize` processa o callback e retorna JSON ou redireciona via `?returnUrl=`
- `returnUrl` suporta `/admin/…` (admin-web) e `/app/…` (medico-pwa), habilitando os dois apps Angular
- TASK-AUTH-10 incluída: GoogleId associado automaticamente no primeiro login por email
- Políticas `SaasOnly`, `TenantAccess`, `MedicoAccess` registradas no mesmo PR (antecipa TASK-AUTH-08)
- Segredo JWT mínimo de 32 chars; `jti` é `Guid.NewGuid()` em cada token
- 13 testes de integração cobrindo todos os cenários de erro e claims por role

---

### TASK-AUTH-06 — Endpoint de refresh token (Implementado ✅)

**Escopo:** `apps/backend/Identity/`

`POST /api/v1/auth/refresh`

```json
{ "refreshToken": "..." }
```

Fluxo:

1. Hashear o token recebido (SHA-256)
2. Buscar `RefreshToken` pelo hash
3. Validar: não revogado, não expirado, usuário ativo, tenant ativo
4. Revogar o token atual (`IsRevoked = true`)
5. Emitir novo par (access token + refresh token)
6. Persistir novo refresh token

Retornar 401 para qualquer falha de validação (não detalhar o motivo — evitar oracle de tokens).

**Critério de pronto:** teste unitário cobrindo token válido, token revogado, token expirado, usuário inativo.

**Notas de implementação:**

- `AuthService.RefreshTokenAsync` encapsula toda a lógica; extração de `HashToken(string raw)` como helper privado estático elimina duplicação com `GenerateRefreshTokenPair`
- `RefreshToken.Revoke(newToken.Id.ToString())` rastreia a cadeia de rotação via `ReplacedByTokenId`
- Todos os cenários de falha retornam `UnauthorizedError` (sem discriminar o motivo — evita oracle)
- `POST /api/v1/auth/refresh` mapeado como `.AllowAnonymous()` — não exige JWT
- `RefreshRequest` é um `internal sealed record` declarado no nível do namespace em `AuthEndpoints.cs`
- 8 testes de integração em `Identity.Tests/Auth/RefreshTokenTests.cs` cobrindo: token válido, rotação, claims corretos, token revogado, token expirado, usuário inativo, token desconhecido, e `ExpiresIn` = 900

---

### TASK-AUTH-07 — Endpoint de logout (Implementado ✅)

**Escopo:** `apps/backend/Identity/`

`POST /api/v1/auth/logout` — requer autenticação JWT.

Revogar todos os `RefreshToken` ativos do usuário. Retornar 204.

O access token não pode ser revogado (stateless) — o frontend deve descartar o token localmente. TTL curto de 15 min já limita a janela de exposição.

**Notas de implementação:**

- `AuthService.LogoutAsync(Guid userId)` consulta somente tokens não revogados (`Where(!t.IsRevoked)`) — tokens já revogados são ignorados sem erro
- `POST /api/v1/auth/logout` mapeado com `.RequireAuthorization()` — lê `userId` via `ICurrentUser` injetado no handler
- Após logout, `RefreshTokenAsync` com o token antigo retorna `UnauthorizedError` (token revogado)
- 4 testes de integração em `Identity.Tests/Auth/LogoutTests.cs` cobrindo: revogação de todos os tokens, sem tokens (nenhum erro), refresh após logout retorna 401, e mix de tokens já revogados + ativos

---

### TASK-AUTH-08 — Políticas de autorização e middleware de tenant suspenso (Implementado ✅)

**Escopo:** `apps/backend/App/Program.cs`

Registrar políticas:

```csharp
builder.Services.AddAuthorization(o => {
    o.AddPolicy("SaasOnly",     p => p.RequireRole("SaasAdmin"));
    o.AddPolicy("TenantAccess", p => p.RequireRole("TenantAdmin", "SaasAdmin"));
    o.AddPolicy("MedicoAccess", p => p.RequireRole("Medico"));
});
```

Middleware de tenant suspenso:

- Após autenticação JWT, verificar `Tenant.Status` do `TenantId` do usuário
- Se `Suspenso` ou `Cancelado`, retornar 403 com body `{ "error": "tenant_suspended" }`
- SaasAdmin não passa por este middleware

**Critério de pronto:** request de usuário com tenant suspenso retorna 403 antes de chegar ao controller.

**Notas de implementação:**

- Políticas já estavam implementadas desde TASK-AUTH-05; esta task adicionou apenas o middleware
- `TenantStatusMiddleware` em `App/Identity/TenantStatusMiddleware.cs` — lê `ctx.User` diretamente (não via `ICurrentUser`) para ser testável sem o pipeline HTTP completo
- Middleware registrado com `app.UseMiddleware<TenantStatusMiddleware>()` entre `UseAuthentication()` e `UseAuthorization()` — SaasAdmin e anônimos passam sem consulta ao DB
- `AppDbContext` é injetado via parâmetro do `InvokeAsync` (padrão ASP.NET Core para serviços scoped em middleware singleton)
- Testado diretamente instanciando `TenantStatusMiddleware` e passando `DefaultHttpContext` + `AppDbContext` real — sem necessidade de `WebApplicationFactory`
- 6 testes em `Identity.Tests/Middleware/TenantStatusMiddlewareTests.cs`: tenant ativo, suspenso, cancelado, SaasAdmin, anônimo, e verificação do corpo da resposta JSON

---

### TASK-AUTH-09 — Painel SaaS: CRUD de Tenants e usuários (Implementado ✅)

**Escopo:** `apps/backend/Identity/` (endpoints sob `/api/v1/saas/`)

Endpoints mínimos do painel SaaS:

| Método  | Rota                                                    | Descrição                             |
| ------- | ------------------------------------------------------- | ------------------------------------- |
| `GET`   | `/api/v1/saas/tenants`                                  | Listar todos os tenants com status    |
| `POST`  | `/api/v1/saas/tenants`                                  | Criar novo tenant                     |
| `PATCH` | `/api/v1/saas/tenants/{tenantId}/status`                | Ativar / suspender / cancelar         |
| `GET`   | `/api/v1/saas/tenants/{tenantId}/users`                 | Listar usuários do tenant             |
| `POST`  | `/api/v1/saas/tenants/{tenantId}/users`                 | Criar usuário (TenantAdmin ou Medico) |
| `PATCH` | `/api/v1/saas/tenants/{tenantId}/users/{userId}/status` | Ativar / desativar usuário            |

Criação de usuário recebe: `{ email, role, medicoId? }`. O `GoogleId` é preenchido no primeiro login do usuário (associado por email).

**Restrição LGPD:** toda rota com `{tenantId}` deve validar que o tenant existe, mesmo para SaasAdmin. Isso garante auditabilidade.

**Critério de pronto:** SaaS admin consegue criar tenant, criar usuário TenantAdmin neste tenant, e o usuário consegue autenticar via Google e receber JWT com o `TenantId` correto.

**Notas de implementação:**

- `SaasService` em `App/Identity/SaasService.cs` — acesso direto ao `AppDbContext`, sem `UserManager`
- `SaasEndpoints` em `App/Identity/Endpoints/SaasEndpoints.cs` — `MapGroup("/api/v1/saas").RequireAuthorization("SaasOnly")`
- `Tenant.Activate()` adicionado à entidade (necessário para PATCH de status → Ativo)
- Records de retorno: `TenantSummary` e `UserSummary` declarados em `SaasService.cs`
- `DeriveRole` reutilizado de `AuthService` (mesmo assembly, `internal static`)
- `ListTenantUsers` materializa antes de projetar role (EF Core não traduz `DeriveRole` para SQL)
- Roles válidos para criação de usuário: `TenantAdmin`, `Medico` — `SaasAdmin` rejeitado com `ValidationError`
- Erro de container compartilhado no teste: `Assert.Empty` em `ListTenantsAsync` falha pois o banco não está vazio; testes de lista verificam subconjunto por ID
- 25 testes de integração em `Identity.Tests/Saas/SaasServiceTests.cs`; 1 teste de critério de pronto (`CreateUser_ThenAuthenticateViaGoogle_JwtContainsCorrectTenantId`)

---

### TASK-AUTH-10 — Associação GoogleId no primeiro login

**Escopo:** `apps/backend/Identity/` (callback do OAuth, TASK-AUTH-05)

No callback do Google, se `ApplicationUser` é encontrado por email mas `GoogleId` está nulo (usuário pré-cadastrado pelo SaaS admin), associar o `GoogleId` recebido do Google ao registro e salvar.

Isso permite que o SaaS admin cadastre usuários por email antes deles fazerem o primeiro login.

---

### TASK-AUTH-11 — Frontend: interceptor Angular + fluxo de login

**Escopo:** `apps/admin-web/` e `apps/medico-pwa/`

Para cada Angular app:

1. `AuthService`: armazena access token em memória (não localStorage), refresh token em `httpOnly cookie` se possível ou localStorage como fallback
2. `AuthInterceptor` (HTTP interceptor): adiciona `Authorization: Bearer <token>` em toda request para `/api/`
3. Lógica de refresh automático: se response 401, tenta refresh antes de redirecionar para login
4. Guard `AuthGuard` para rotas protegidas
5. Tela de login com botão "Entrar com Google" que redireciona para `/api/v1/auth/google`
6. Callback handler que recebe o token da resposta e armazena

**Restrição:** access token NUNCA em localStorage. Usar variável em memória no `AuthService` (singleton). Refresh token pode ser localStorage se cookie httpOnly não for viável no subpath PWA.

**Critério de pronto:** admin loga em `/admin/`, médico loga em `/app/`, ambos veem telas distintas. Token refresha automaticamente sem deslogar o usuário.

---

### TASK-AUTH-12 — Testes de integração ponta-a-ponta

**Escopo:** `apps/backend/tests/`

Cenários obrigatórios usando `PostgresContainerFixture`:

1. Google callback com usuário não cadastrado → 401
2. Google callback com tenant suspenso → 403
3. Google callback com usuário inativo → 403
4. Google callback válido (TenantAdmin) → JWT com `tenant_id` correto, sem `medico_id`
5. Google callback válido (Medico) → JWT com `tenant_id` e `medico_id`
6. Google callback válido (SaasAdmin) → JWT sem `tenant_id`
7. Refresh token válido → novo par emitido, token anterior revogado
8. Refresh token revogado → 401
9. Request de TenantAdmin não vê dados de outro tenant
10. Request de Medico não vê guias de outro médico no mesmo tenant
11. SaasAdmin acessa dados de qualquer tenant via `{tenantId}` na rota

**Cobertura mínima do bounded context Identity: 80%.**

---

## Arquivos a criar/modificar (checklist)

```
apps/backend/
├── Directory.Packages.props              # MODIFICAR — adicionar versões dos pacotes
├── App/
│   ├── App.csproj                        # MODIFICAR — referenciar novos pacotes
│   ├── Program.cs                        # MODIFICAR — configurar auth, policies, middleware
│   └── AppDbContext.cs                   # MODIFICAR — injetar ICurrentUser, global filter
├── Identity/
│   ├── ApplicationUser.cs                # CRIAR
│   ├── Tenant.cs                         # CRIAR
│   ├── RefreshToken.cs                   # CRIAR
│   ├── TenantStatus.cs                   # CRIAR
│   ├── ICurrentUser.cs                   # CRIAR
│   ├── CurrentUser.cs                    # CRIAR
│   ├── ITenantEntity.cs                  # CRIAR
│   ├── AuthService.cs                    # CRIAR (lógica JWT, refresh, revogação)
│   ├── Endpoints/
│   │   ├── AuthEndpoints.cs              # CRIAR (google, callback, refresh, logout)
│   │   └── SaasEndpoints.cs              # CRIAR (CRUD tenants e usuários)
│   └── Configurations/
│       ├── ApplicationUserConfiguration.cs  # CRIAR
│       ├── TenantConfiguration.cs           # CRIAR
│       └── RefreshTokenConfiguration.cs     # CRIAR
└── tests/
    └── Identity.Tests/
        ├── AuthServiceTests.cs           # CRIAR
        └── TenantIsolationTests.cs       # CRIAR

apps/admin-web/src/
├── app/auth/
│   ├── auth.service.ts                   # CRIAR
│   ├── auth.interceptor.ts               # CRIAR
│   ├── auth.guard.ts                     # CRIAR
│   └── login/login.component.ts          # CRIAR

apps/medico-pwa/src/
└── app/auth/                             # CRIAR — estrutura idêntica ao admin-web
```

---

## Variáveis de ambiente necessárias

Adicionar ao `.env` (local) e configurar em CI/deploy:

```env
Google__ClientId=...
Google__ClientSecret=...
Jwt__Secret=...         # min 32 chars, gerar com: openssl rand -base64 32
Jwt__Issuer=https://honorare.com.br
Jwt__Audience=honorare-api
Jwt__AccessTokenMinutes=15
Jwt__RefreshTokenDays=7
```

---

## O que NÃO implementar (cortes explícitos do MVP)

- Convite por email com token de aceite
- Magic link
- Passkeys / WebAuthn
- MFA (TOTP, SMS)
- Social login além de Google
- Recuperação de conta (o SaaS admin redefine manualmente)
- RBAC granular por recurso (roles são suficientes)
- Audit log de eventos de auth (entra na Fase 6 — F6.1)
- Rate limiting em endpoints de auth (pós-MVP)
- Rotação automática de JWT secret

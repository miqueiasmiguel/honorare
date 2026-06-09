# SPEC — Impersonação de Tenant pelo SaasAdmin

**Objetivo:** permitir que o `SaasAdmin` "entre" em um tenant específico para criar, alterar e visualizar dados como se fosse um `TenantAdmin` daquele cliente. Primeiro uso concreto: importar tabelas de procedimentos para um cliente.

**Abordagem (Opção B):** token de impersonação. Um endpoint emite um JWT curto com `role=SaasAdmin` **+** claim `tenant_id` do tenant alvo **+** claim marcadora `act_as_saas=true`. Como `CurrentUser.TenantId` já lê `tenant_id`, todos os ~13 pontos `_currentUser.TenantId!.Value` passam a funcionar sem alteração. A única mudança estrutural é o filtro global de query.

**Decisões fixas (não reabrir):**

- `role` permanece `SaasAdmin` durante a impersonação. Justificativa: o **único** branch comportamental de `IsSaasAdmin` no backend é o filtro global (`AppDbContext.cs:60`) — confirmado por `grep -rn "IsSaasAdmin" apps/backend/App`. Sem outros branches god-mode, manter `SaasAdmin` é contido e dá de graça: `/saas/**` e o endpoint de exit (ambos `SaasOnly`) seguem alcançáveis durante a impersonação; acesso a tenant suspenso continua livre (o `TenantStatusMiddleware` pula `SaasAdmin`); `sub` permanece o usuário SaaS real (auditoria + `LogoutAsync` corretos).
- `sub` do token = UserId do SaasAdmin real (nunca um usuário do tenant).
- `tenant_id` no claim, **não** um `EffectiveTenantId` separado.
- Sobrevivência ao refresh: coluna `RefreshToken.ActingTenantId` (nullable).
- Exit é **server-side** (`POST /saas/impersonation/exit`): revoga o refresh token de impersonação e fecha o log de auditoria. O front restaura o `_rt` SaaS depois.

**Claims do token de impersonação:**

```json
{ "sub": "<saas-admin-userid>", "role": "SaasAdmin", "tenant_id": "<tenant-alvo>", "act_as_saas": "true", "email": "...", "jti": "...", "exp": 0 }
```

**Ordem de execução:** IMP-01 → IMP-02 → IMP-03 → IMP-04 (backend) → IMP-05 → IMP-06 (frontend). Uma task por sessão. TDD obrigatório: escrever o teste que falha (RED) antes do código de produção.

---

## IMP-01 · Backend: filtro global + `IsImpersonating`

Reescrever o filtro de tenant para isolar o SaasAdmin quando ele tem um `tenant_id` ativo. É a única mudança no predicado de isolamento — base de tudo.

### Leia antes

- `apps/backend/App/Data/AppDbContext.cs` — `ApplyTenantFilterForEntity` (linha 56-61)
- `apps/backend/App/Identity/ICurrentUser.cs`, `CurrentUser.cs`
- `apps/backend/App/Data/AppDbContextFactory.cs` — stub design-time de `ICurrentUser`
- Fixtures (stubs de `ICurrentUser`): `tests/{Identity,Catalog,Faturamento}.Tests/Fixtures/PostgresContainerFixture.cs`

### Arquivos modificados

| Arquivo                                            | Ação                                                                      |
| -------------------------------------------------- | ------------------------------------------------------------------------- |
| `App/Identity/ICurrentUser.cs`                     | `+ bool IsImpersonating { get; }`                                         |
| `App/Identity/CurrentUser.cs`                      | `IsImpersonating => IsSaasAdmin && TenantId is not null;`                 |
| `App/Data/AppDbContext.cs`                         | reescrever o predicado do filtro                                          |
| `App/Data/AppDbContextFactory.cs`                  | adicionar `IsImpersonating => false` ao stub                              |
| `tests/*/Fixtures/PostgresContainerFixture.cs` (3) | `IsImpersonating` nos stubs + novo `CreateImpersonationContext(tenantId)` |

### Novo predicado do filtro

```csharp
.HasQueryFilter(e =>
    (_currentUser.IsSaasAdmin && _currentUser.TenantId == null) // SaaS global → vê tudo
    || e.TenantId == _currentUser.TenantId);                    // tenant ou impersonação → isolado
```

### Stub de impersonação nas fixtures

```csharp
internal AppDbContext CreateImpersonationContext(Guid tenantId) =>
    new(/* options */, new ImpersonatingCurrentUser(tenantId));

private sealed class ImpersonatingCurrentUser(Guid tenantId) : ICurrentUser
{
    public Guid UserId => Guid.Empty;
    public Guid? TenantId => tenantId;
    public Guid? MedicoId => null;
    public bool IsSaasAdmin => true;
    public bool IsImpersonating => true;
    public bool IsAuthenticated => true;
}
```

### Testes (RED primeiro) — `Catalog.Tests`

- `SaasGlobal_VeRegistrosDeTodosOsTenants` — gravar linha no tenant A e no tenant B via `CreateContext()` (saas, com `TenantId` explícito); ler via `CreateContext()` → enxerga as duas.
- `Impersonacao_VeApenasRegistrosDoTenantAtivo` — `CreateImpersonationContext(A)` → enxerga só as do A.
- `Impersonacao_NaoVeRegistrosDeOutroTenant` — `CreateImpersonationContext(A)` não retorna linha do B.

---

## IMP-02 · Backend: token de impersonação + endpoint + migration

### Leia antes

- `App/Identity/AuthService.cs` — `CreateAccessToken`, `GenerateRefreshTokenPair`, `AuthTokens`
- `App/Identity/RefreshToken.cs` — `Create`
- `App/Identity/Endpoints/SaasEndpoints.cs` — grupo `SaasOnly`
- `App/Identity/SaasService.cs` — padrão de validação "tenant existe"
- `App/Identity/Migrations/.editorconfig` (existe ✓)

### Arquivos modificados

| Arquivo                                                    | Ação                                                                 |
| ---------------------------------------------------------- | -------------------------------------------------------------------- |
| `App/Identity/RefreshToken.cs`                             | `+ Guid? ActingTenantId`; `Create(..., Guid? actingTenantId = null)` |
| `App/Identity/Configurations/RefreshTokenConfiguration.cs` | mapear `ActingTenantId` (nullable)                                   |
| `App/Identity/AuthService.cs`                              | refatorar `CreateAccessToken`; novo `CreateImpersonationTokensAsync` |
| `App/Identity/Endpoints/SaasEndpoints.cs`                  | `MapPost("/tenants/{tenantId}/impersonate", ...)`                    |
| `App/Identity/Migrations/*`                                | `dotnet ef migrations add AddActingTenantIdToRefreshToken`           |

### Assinaturas

```csharp
// tenantOverride preenche tenant_id e emite act_as_saas=true
private string CreateAccessToken(ApplicationUser user, string role,
    Guid? tenantOverride = null);

internal async Task<Result<AuthTokens>> CreateImpersonationTokensAsync(
    Guid saasUserId, Guid tenantId, CancellationToken ct = default);
```

Regras de `CreateImpersonationTokensAsync`:

1. Carregar `user` por `saasUserId`. Falha `Forbidden` se `user is null`, inativo, ou `user.TenantId is not null` (só SaasAdmin impersona).
2. Validar que o tenant existe (qualquer status — suspenso é permitido). `NotFound` se não existir.
3. Mint: `CreateAccessToken(user, "SaasAdmin", tenantOverride: tenantId)` + refresh token com `ActingTenantId = tenantId`.

Endpoint: lê o id do usuário atual via `ICurrentUser.UserId`; `{tenantId}` da rota. `404` → tenant inexistente.

> A gravação do `ImpersonationLog` (auditoria) entra na IMP-04 — aqui apenas os tokens.

### Comando da migration

```bash
cd apps/backend/App
dotnet ef migrations add AddActingTenantIdToRefreshToken \
  --output-dir Identity/Migrations --namespace App.Identity.Migrations
```

### Testes (RED primeiro) — `Identity.Tests`

- `Impersonar_RetornaTokenComTenantEMarker` — decodificar o access token → `tenant_id == alvo` e `act_as_saas == "true"`, `sub == saasUserId`, `role == "SaasAdmin"`.
- `Impersonar_PersisteRefreshComActingTenantId`.
- `Impersonar_TenantInexistente_Falha` (NotFound).
- `Impersonar_UsuarioNaoSaas_Falha` (Forbidden) — usuário com `TenantId` preenchido.
- `Impersonar_TenantSuspenso_PermiteToken` (status ≠ Ativo não bloqueia).

---

## IMP-03 · Backend: refresh preserva impersonação + re-check de privilégio

Sem isso, ao expirar o access token (15 min) o refresh devolve um token SaaS comum e a impersonação se perde silenciosamente.

### Leia antes

- `App/Identity/AuthService.cs` — `RefreshTokenAsync` (linhas 55-97)

### Arquivos modificados

| Arquivo                       | Ação                                        |
| ----------------------------- | ------------------------------------------- |
| `App/Identity/AuthService.cs` | ramo de impersonação em `RefreshTokenAsync` |

### Lógica

Após carregar `token` e `user`, **se** `token.ActingTenantId is not null`:

1. Re-checar privilégio: `user.TenantId is null` e `user.IsActive` — senão `Unauthorized` (um refresh row com `ActingTenantId` vazado não pode escalar privilégio).
2. Mint do access token com `CreateAccessToken(user, "SaasAdmin", tenantOverride: token.ActingTenantId)`.
3. Propagar `ActingTenantId` para o novo refresh token rotacionado.

Caso `ActingTenantId is null`: fluxo atual inalterado.

### Testes (RED primeiro) — `Identity.Tests`

- `Refresh_DeImpersonacao_MantemTenantEMarker` — token rotacionado ainda traz `tenant_id` + `act_as_saas`.
- `Refresh_RotacionaPreservandoActingTenantId` — novo refresh row mantém `ActingTenantId`.
- `Refresh_ImpersonacaoDeUsuarioRebaixado_Falha` — `user.TenantId` passou a não-null → `Unauthorized`.

---

## IMP-04 · Backend: exit + auditoria + e2e import

Fecha a demanda no servidor: auditoria LGPD (início/fim) e validação ponta-a-ponta da importação de tabela sob impersonação.

### Leia antes

- `App/Identity/Endpoints/SaasEndpoints.cs`, `AuthService.cs` (`LogoutAsync`, `CreateImpersonationTokensAsync`)
- `App/Data/AppDbContext.cs` (registrar `DbSet`), `Program.cs` (pipeline/middleware)
- `App/Catalog/CatalogService.cs` — `ImportarTabelaCsvAsync` / `ImportarProcedimentosCsvAsync`

### Arquivos modificados

| Arquivo                                                               | Ação                                                                    |
| --------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `App/Identity/ImpersonationLog.cs` (novo)                             | entidade de auditoria (não `ITenantEntity` — global)                    |
| `App/Identity/Configurations/ImpersonationLogConfiguration.cs` (novo) | mapeamento                                                              |
| `App/Data/AppDbContext.cs`                                            | `DbSet<ImpersonationLog>`                                               |
| `App/Identity/AuthService.cs`                                         | gravar log em `CreateImpersonationTokensAsync`; `EndImpersonationAsync` |
| `App/Identity/Endpoints/SaasEndpoints.cs`                             | `MapPost("/impersonation/exit", ...)`                                   |
| `App/Identity/ImpersonationSpanMiddleware.cs` (novo)                  | tag OTEL `saas.acting_tenant` quando `act_as_saas` presente             |
| `App/Program.cs`                                                      | registrar o middleware (após auth)                                      |
| `App/Identity/Migrations/*`                                           | `dotnet ef migrations add AddImpersonationLog`                          |

### `ImpersonationLog`

Campos: `Id`, `SaasUserId`, `TenantId`, `StartedAt`, `EndedAt` (nullable). Gravado em `CreateImpersonationTokensAsync` (StartedAt).

### Exit (`EndImpersonationAsync(saasUserId, ct)`)

1. Revogar todos os refresh tokens ativos do usuário com `ActingTenantId != null`.
2. Fechar (`EndedAt = now`) os `ImpersonationLog` abertos do usuário.

Endpoint `POST /api/v1/saas/impersonation/exit` (`SaasOnly`) — usa `ICurrentUser.UserId`.

### Span OTEL

Middleware após `UseAuthentication`: se claim `act_as_saas == "true"`, `Activity.Current?.SetTag("saas.acting_tenant", <tenant_id>)`. Segue a convenção `Honorare.<Context>`.

### Testes (RED primeiro)

- `Identity.Tests`: `Impersonar_GravaLogComStartedAt`; `Exit_RevogaRefreshDeImpersonacao_EFechaLog`.
- `Catalog.Tests` (e2e — **fecha a demanda**): `ImportarTabela_SobImpersonacao_CarimbaTenantAlvo` — construir `CatalogService` com `ImpersonatingCurrentUser(A)`, importar CSV, assert linhas persistidas com `TenantId == A` e visíveis só no contexto de A.

---

## IMP-05 · Frontend: `AuthService` enter/exit impersonação

### Leia antes

- `apps/admin-web/src/app/auth/auth.service.ts` — `storeTokens`, `refresh`, `role` (decode do JWT)

### Arquivos modificados

| Arquivo                                            | Ação                                                                                   |
| -------------------------------------------------- | -------------------------------------------------------------------------------------- |
| `apps/admin-web/src/app/auth/auth.service.ts`      | `enterImpersonation`, `exitImpersonation`, sinais `isImpersonating`/`actingTenantName` |
| `apps/admin-web/src/app/auth/auth.service.spec.ts` | testes                                                                                 |

### Comportamento

Constantes: `RefreshTokenKey = '_rt'`, `SaasRefreshKey = '_rt_saas'`, `ImpNameKey = '_imp_name'`.

`AuthService` usa `HttpBackend` (sem interceptor) → adicionar header `Authorization: Bearer <accessToken atual>` manualmente nas duas chamadas.

```ts
enterImpersonation(tenantId: string, tenantName: string): Observable<boolean>
// POST /api/v1/saas/tenants/{tenantId}/impersonate
// sucesso: localStorage[_rt_saas] = localStorage[_rt];
//          storeTokens(resp); localStorage[_imp_name] = tenantName;

exitImpersonation(): Observable<boolean>
// POST /api/v1/saas/impersonation/exit (best-effort)
// finally: _rt = _rt_saas; remove _rt_saas, _imp_name; return refresh()
```

Sinais:

- `isImpersonating` — `computed`: decodifica o token (mesmo helper do `role()`) → `true` se `role==='SaasAdmin'` **e** existe claim `tenant_id`.
- `actingTenantName` — lê `localStorage[_imp_name]` (sobrevive a reload; o token só carrega o GUID).

`initSession()` inalterado — o refresh restaura a impersonação automaticamente (backend honra `ActingTenantId`).

### Testes (Vitest, RED primeiro)

- `enterImpersonation guarda o _rt SaaS e troca os tokens`.
- `exitImpersonation restaura o _rt SaaS e limpa _imp_name`.
- `isImpersonating é true quando o token tem role SaasAdmin + tenant_id`.

---

## IMP-06 · Frontend: botão "Entrar no cliente" + banner + rotas [x] concluída

### Leia antes

- `apps/admin-web/src/app/saas/tenants/tenant-detail.ts` (+ template)
- `apps/admin-web/src/app/saas/saas-shell.ts`, shell/layout de `/admin`
- `apps/admin-web/src/app/app.routes.ts` — guard de `/admin` (confirmar que aceita `role=SaasAdmin`)

### Arquivos modificados

| Arquivo                                    | Ação                                           |
| ------------------------------------------ | ---------------------------------------------- |
| `saas/tenants/tenant-detail.ts`            | botão "Gerenciar dados deste cliente"          |
| `admin/.../impersonation-banner.ts` (novo) | banner persistente                             |
| shell de `/admin` (template)               | renderizar o banner quando `isImpersonating()` |
| respectivos `*.spec.ts`                    | testes de componente                           |

### Comportamento

- **tenant-detail:** botão → `auth.enterImpersonation(id, nome).subscribe({ next: () => router.navigate(['/admin']), error: ... })`. Todo `subscribe` tem handler de `error` (convenção do projeto).
- **banner:** visível em `/admin/**` quando `auth.isImpersonating()`. Texto: `Operando como {{ auth.actingTenantName() }} · Sair`. "Sair" → `auth.exitImpersonation().subscribe({ next: () => router.navigate(['/saas']), error: ... })`.
- Estilo: seguir `apps/admin-web/STYLES.md` (tokens `var(--color-*)`, `space(n)`, mixins `@include text-*` — nada de hex/px crus).

### Testes (Vitest, RED primeiro)

- `banner aparece quando isImpersonating é true`.
- `clicar Sair chama exitImpersonation e navega para /saas`.
- `botão do tenant-detail chama enterImpersonation com id e nome`.

---

## Resumo das migrations

| Migration                         | Task   |
| --------------------------------- | ------ |
| `AddActingTenantIdToRefreshToken` | IMP-02 |
| `AddImpersonationLog`             | IMP-04 |

Ambas exigem o `.editorconfig` em `Identity/Migrations/` (já existe).

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Honorare is a SaaS platform for medical payment reconciliation — a financial ledger ("conta-corrente") between physicians and health plan operators (convênios), starting with UNIMED. The product is aimed at billing companies that manage physician payments, not the doctors directly.

**Current status:** Phase 1 scaffolding complete. Backend (.NET 10), admin-web (Angular SPA), medico-pwa (Angular PWA), infra (Docker Compose + Nginx + observability stack), and the full engineering harness (linting, testing, CI, pre-commit hooks) are in place. Domain feature development begins next.

## Commands

```bash
# ── Setup ──────────────────────────────────────────────────────────────────────
pnpm install                        # Install all JS/TS deps + activate Husky hooks

# ── Dev ────────────────────────────────────────────────────────────────────────
pnpm dev:up                         # Start Docker Compose (Postgres + backend + Nginx + observability)
pnpm -F admin-web dev               # Dev server: admin Angular SPA  (http://localhost:4200/admin/)
pnpm -F medico-pwa dev              # Dev server: doctor Angular PWA  (http://localhost:4201/app/)
pnpm generate-api-client            # Regenerate TS client from OpenAPI spec

# ── Backend (.NET) ─────────────────────────────────────────────────────────────
# NOTE: solution file is Honorare.slnx (not .sln)
dotnet build apps/backend/Honorare.slnx           # Build (warnings = errors)
dotnet run --project apps/backend/App             # Run in development
dotnet test apps/backend/Honorare.slnx            # Run all xUnit tests + coverage

# ── EF Core migrations ─────────────────────────────────────────────────────────
# dotnet-ef is a global tool at ~/.dotnet/tools/ — requires DOTNET_ROOT + PATH.
# Add to ~/.zshrc (asdf manages the runtime at a non-standard path):
#
#   export DOTNET_ROOT="$(asdf where dotnet 2>/dev/null)"
#   export PATH="$HOME/.dotnet/tools:$PATH"
#
# Run migrations from INSIDE the App project directory (--output-dir is relative to it):
#
#   cd apps/backend/App
#   dotnet ef migrations add <Name> --output-dir Catalog/Migrations --namespace App.Catalog.Migrations
#   dotnet ef migrations add <Name> --output-dir Faturamento/Migrations --namespace App.Faturamento.Migrations
#   dotnet ef migrations add <Name> --output-dir Identity/Migrations --namespace App.Identity.Migrations

# ── Frontend — lint ────────────────────────────────────────────────────────────
pnpm -F admin-web lint              # ESLint (--max-warnings 0)
pnpm -F admin-web lint:fix          # ESLint autofix
pnpm -F admin-web stylelint         # StyleLint SCSS (--max-warnings 0)
pnpm -F admin-web prettier:check    # Prettier check (no write)
pnpm -F admin-web prettier:fix      # Prettier autoformat

# ── Frontend — test ────────────────────────────────────────────────────────────
pnpm -F admin-web test              # Vitest watch mode
pnpm -F admin-web test:ci           # Vitest single-run + coverage (used by CI)
```

CI/CD uses four independent GitHub Actions workflows:

| Workflow            | Trigger path                                    | Steps                                                   |
| ------------------- | ----------------------------------------------- | ------------------------------------------------------- |
| `backend-ci.yml`    | `apps/backend/**`                               | restore → build → test → coverage threshold             |
| `admin-web-ci.yml`  | `apps/admin-web/**, packages/api-contracts/**`  | prettier → eslint → stylelint → test → coverage → build |
| `medico-pwa-ci.yml` | `apps/medico-pwa/**, packages/api-contracts/**` | same as admin-web                                       |
| `codeql.yml`        | `main`/`master` push + weekly                   | CodeQL static analysis (C# + TypeScript)                |

## Architecture

### Monorepo Layout

```
honorare/
├── .editorconfig               # Root — covers all file types (root = true)
├── .commitlintrc.json          # Conventional commits enforcement
├── .lintstagedrc.js            # Per-workspace lint-staged rules
├── .husky/
│   ├── pre-commit              # → pnpm lint-staged
│   └── commit-msg              # → pnpm commitlint --edit "$1"
├── .vscode/
│   ├── settings.json           # formatOnSave, ESLint flat config, per-lang formatters
│   └── extensions.json         # Recommended extensions for the team
├── .github/
│   ├── dependabot.yml          # Weekly updates: npm (3 scopes), NuGet, Actions
│   └── workflows/
│       ├── backend-ci.yml
│       ├── admin-web-ci.yml
│       ├── medico-pwa-ci.yml
│       └── codeql.yml          # Security analysis (C# + TypeScript)
├── apps/
│   ├── backend/
│   │   ├── .editorconfig       # Roslyn analyzer severity rules + C# naming conventions
│   │   ├── Directory.Build.props  # TreatWarningsAsErrors, EnforceCodeStyleInBuild,
│   │   │                          # AnalysisLevel=latest-All, NuGetAudit
│   │   ├── App/                # Main .NET 10 Web API project
│   │   └── tests/
│   │       └── Faturamento.Tests/
│   │           └── Fixtures/
│   │               └── PostgresContainerFixture.cs  # Testcontainers xUnit fixture
│   ├── admin-web/              # Angular SPA — billing company admins
│   │   ├── .editorconfig       # Angular/TS overrides (inherits root, no root=true)
│   │   ├── eslint.config.js    # Flat config: typescript-eslint strict + angular-eslint
│   │   ├── .stylelintrc.json   # BEM, no color-named, no !important
│   │   ├── vitest.config.ts    # Vitest + @angular/build vite plugin + v8 coverage
│   │   └── src/
│   │       └── test-setup.ts   # Zone.js + BrowserDynamicTestingModule init
│   └── medico-pwa/             # Angular PWA — individual physicians (same structure)
├── packages/
│   └── api-contracts/          # TypeScript client auto-generated from OpenAPI
├── infra/
│   ├── docker-compose.yml
│   ├── nginx/                  # Reverse proxy: /admin/, /app/, /api/v1/
│   ├── grafana/                # Pre-provisioned datasources (Prometheus, Jaeger, Loki)
│   ├── otel/                   # OpenTelemetry Collector config
│   └── prometheus/
├── tools/
│   └── generate-api-client.sh
└── docs/                       # All domain/architecture documentation
```

### Backend Bounded Contexts

The backend follows a **bounded-context structure without Clean Architecture layers**. Contexts are flat (no `Domain/Application/Infrastructure/` subdirectories unless a context exceeds ~10 files).

**Dependency direction is strictly unidirectional:**

```
Reporting → Faturamento → Catalog → Identity
```

| Context       | Responsibility                                                        |
| ------------- | --------------------------------------------------------------------- |
| `Identity`    | Auth (Google OAuth 2.0), tenants, users, roles, tenant suspension     |
| `Catalog`     | Operators, procedures, pricing tables, providers, beneficiaries       |
| `Faturamento` | Invoices (guias), UNIMED calculation engine, statement reconciliation |
| `Reporting`   | Aggregated queries for admin dashboard and doctor portal              |

### Cross-Context Foreign Keys (EF Core)

When an entity in a downstream context (e.g., `Faturamento`) references an entity from an upstream context (e.g., `Catalog`), use **bare FK properties with no navigation properties**. Configure in EF as:

```csharp
builder.HasOne<Prestador>().WithMany()
    .HasForeignKey(g => g.PrestadorId)
    .OnDelete(DeleteBehavior.Restrict);
```

Rules:

- `Restrict` for references to catalog entities (prevent orphan deletion)
- `Cascade` only for owned child rows within the same context (e.g., `ItemGuia → Guia`)
- Never add a navigation property pointing back to an upstream context

### Multi-Tenancy

Every operational entity carries `TenantId`. A global EF Core query filter enforces this — never bypass it. Each billing company is a tenant; doctors are end-users within a tenant. LGPD compliance depends on this isolation.

### API & Client Generation

The backend exposes a versioned OpenAPI spec at `/api/v1/`. The TypeScript client in `packages/api-contracts/` is **generated**, not hand-written. Run `pnpm tools generate-api-client` after any backend contract change before touching frontend code.

### Nginx Routing

A single domain serves three paths via Nginx:

- `/api/v1/` → .NET backend (port 5000)
- `/admin/` → admin-web Angular SPA
- `/app/` → medico-pwa Angular PWA

Angular apps must be built with the correct `--base-href` for their subpath.

## Observability

The backend is instrumented with **OpenTelemetry** (traces, metrics, structured logs) and exports via OTLP to an OpenTelemetry Collector. The collector fans out to:

| Signal  | Backend    | UI                                |
| ------- | ---------- | --------------------------------- |
| Traces  | Jaeger     | `http://localhost:16686`          |
| Metrics | Prometheus | `http://localhost:9090`           |
| Logs    | Loki       | Grafana → `http://localhost:3000` |

### How it works

- `Otlp:Endpoint` in `appsettings.json` points to the collector (`http://otel-collector:4317` in Docker, `http://localhost:4317` for local dev without Docker).
- ASP.NET Core requests, outbound HTTP, EF Core queries, and .NET runtime metrics are instrumented automatically.
- `ILogger` output is bridged to OTEL — no separate logging sink needed. Every log record carries the active `TraceId`, enabling trace ↔ log correlation in Grafana.
- Grafana datasources (Prometheus, Jaeger, Loki) are pre-provisioned via `infra/grafana/provisioning/`.

### Adding custom traces/metrics

Use the standard .NET `ActivitySource` and `Meter` APIs — do not take a library dependency on the OTel SDK in domain code:

```csharp
// in a service that needs a custom span
private static readonly ActivitySource Activity = new("Honorare.Faturamento");

using var span = Activity.StartActivity("ApurarGuia");
span?.SetTag("guia.id", guiaId);
```

Register the source name in `Program.cs` inside `.WithTracing(t => t.AddSource("Honorare.Faturamento"))`.

### Exception handler

`Program.cs` configures a global exception handler (`UseExceptionHandler`) that must do three things on every unhandled exception:

1. **Log** via `ILogger` so the full stacktrace appears in Loki.
2. **Record on the active span** via `Activity.Current?.AddException(ex)` (use `AddException`, not the deprecated `RecordException`) so Jaeger shows the exception event.
3. **Return a sanitized response** — never expose internal details to the client.

Status code mapping:

| Exception type                                              | HTTP status                   | Rationale                                                        |
| ----------------------------------------------------------- | ----------------------------- | ---------------------------------------------------------------- |
| `BadHttpRequestException { InnerException: JsonException }` | **422** Unprocessable Entity  | JSON syntactically valid but field values are semantically wrong |
| Other `BadHttpRequestException`                             | **400** Bad Request           | Genuinely malformed HTTP request                                 |
| `InvalidOperationException`                                 | **409** Conflict              | Business rule violation (e.g., delete entity with active links)  |
| Anything else                                               | **500** Internal Server Error | Server-side fault                                                |

### Rules

- Never disable telemetry in production. The `Otlp:Endpoint` can be set to a no-op address if a collector is unavailable, but instrumentation stays active.
- Custom span/metric names must follow the `Honorare.<Context>` prefix convention.
- The `TenantId` must be added as a span attribute on every request that touches tenant data — this is essential for debugging multi-tenant issues without violating data isolation.
- When a 500 occurs and the exception has no Jaeger event, check that the exception handler is calling `Activity.Current?.AddException(ex)` — a missing call leaves the span with `error: true` but no detail.

## Engineering Harness

The goal is zero tolerance for quality regressions: every linter warning is a build error, and CI enforces the same rules that run locally. "It passes on my machine" is not acceptable.

### Warnings are errors everywhere

| Stack             | Mechanism                                                                         | File                                 |
| ----------------- | --------------------------------------------------------------------------------- | ------------------------------------ |
| .NET              | `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel=latest-All`    | `apps/backend/Directory.Build.props` |
| TypeScript        | `strict`, `noImplicitOverride`, `noImplicitReturns`, `noFallthroughCasesInSwitch` | `apps/*/tsconfig.json`               |
| Angular templates | `strictTemplates`, `strictInjectionParameters`, `typeCheckHostBindings`           | `apps/*/tsconfig.json`               |
| ESLint            | `--max-warnings 0` in `lint` script                                               | `apps/*/package.json`                |
| StyleLint         | `--max-warnings 0` in `stylelint` script                                          | `apps/*/package.json`                |

### `.editorconfig` structure

A single root `.editorconfig` (`root = true`) covers every file type with explicit rules (max_line_length, charset, indent, eol per extension). App-level configs under `apps/admin-web/` and `apps/medico-pwa/` inherit from it — they do **not** set `root = true`. The backend has a dedicated `apps/backend/.editorconfig` with Roslyn analyzer severity rules and C# code-style preferences.

Every EF Core `Migrations/` folder requires its own `.editorconfig` suppressing the four rules that conflict with auto-generated migration code:

```ini
[*.cs]
dotnet_diagnostic.IDE0005.severity = none
dotnet_diagnostic.IDE0161.severity = none
dotnet_diagnostic.CA1515.severity = none
dotnet_diagnostic.CA1861.severity = none
```

Without this file the build fails (`TreatWarningsAsErrors`) on every new migration. See `App/Faturamento/Migrations/.editorconfig` as the reference.

### .NET: `Directory.Build.props`

`apps/backend/Directory.Build.props` applies to every `.csproj` under that directory. Never override these properties in individual project files — use `#pragma warning disable` with an explanatory comment for one-off exceptions.

| Property                  | Value        | Effect                                                        |
| ------------------------- | ------------ | ------------------------------------------------------------- |
| `TreatWarningsAsErrors`   | `true`       | All compiler + analyzer warnings fail `dotnet build`          |
| `EnforceCodeStyleInBuild` | `true`       | IDE code-style rules (IDE0xxx) enforced during `dotnet build` |
| `AnalysisLevel`           | `latest-All` | Every Roslyn analyzer rule shipped with the SDK is active     |
| `Nullable`                | `enable`     | Null-safety enforced across all projects                      |
| `Deterministic`           | `true`       | Reproducible build artifacts                                  |
| `NuGetAudit`              | `true`       | `dotnet restore` fails on packages with CVE ≥ moderate        |

### Roslyn naming conventions (enforced as errors)

| Symbol                  | Convention        | Example         |
| ----------------------- | ----------------- | --------------- |
| Private instance fields | `_camelCase`      | `_tenantId`     |
| Async methods           | `PascalCaseAsync` | `GetGuiasAsync` |
| Constants               | `PascalCase`      | `MaxRetryCount` |

### Key IDE diagnostics

| Rule      | Severity | What it catches                                                                 |
| --------- | -------- | ------------------------------------------------------------------------------- |
| `IDE0005` | error    | Unnecessary `using` directives                                                  |
| `IDE0011` | error    | Missing braces on `if`/`else`/`for` etc. — always use `{ }` even for one-liners |
| `IDE0051` | error    | Unused private members                                                          |
| `IDE0052` | error    | Unread private members                                                          |
| `IDE0055` | error    | Formatting violations                                                           |
| `IDE0059` | warning  | Unnecessary value assignments                                                   |
| `IDE0060` | warning  | Unused parameters                                                               |
| `CA2007`  | none     | `ConfigureAwait` — not required in ASP.NET Core                                 |

### TypeScript / Angular linting

Each Angular app has `apps/*/eslint.config.js` (ESLint v9 flat config) with:

- `typescript-eslint` `strictTypeChecked` + `stylisticTypeChecked` — type-aware rules
- `angular-eslint` `tsRecommended` + `templateRecommended` + `templateAccessibility`
- `eslint-config-prettier` at the end (disables formatting rules that conflict with Prettier)
- Test files (`*.spec.ts`) have `no-explicit-any` and `explicit-function-return-type` relaxed

SCSS is linted by StyleLint (`apps/*/.stylelintrc.json`) using `stylelint-config-standard-scss` with BEM selector enforcement, `color-named: never`, and no `!important`.

### Pre-commit hooks (Husky + lint-staged)

`pnpm install` activates Husky via the `prepare` script. Two hooks run on every commit:

| Hook         | File                | What it runs                                                            |
| ------------ | ------------------- | ----------------------------------------------------------------------- |
| `pre-commit` | `.husky/pre-commit` | `pnpm lint-staged` — ESLint + StyleLint + Prettier on staged files only |
| `commit-msg` | `.husky/commit-msg` | `pnpm commitlint` — enforces Conventional Commits format                |

Commit format: `type(scope): subject` where type ∈ `feat fix chore docs style refactor perf test ci revert`. Header ≤ 100 chars.

### Security

- **NuGetAudit** — `dotnet restore` fails if any NuGet package (direct or transitive) has a CVE at moderate severity or above. Configured in `Directory.Build.props`.
- **CodeQL** — `.github/workflows/codeql.yml` runs static security analysis on C# and TypeScript on every PR to main and weekly. Uses the `security-and-quality` query pack.
- **Dependabot** — `.github/dependabot.yml` opens weekly PRs for npm (root, admin-web, medico-pwa), NuGet, and GitHub Actions. Angular, ESLint, OpenTelemetry, and EF Core packages are grouped to reduce PR noise.

## Testing Philosophy

This project follows **Test-Driven Development (TDD)**. Write the failing test first, then write the minimum production code to make it pass, then refactor.

**The minimum acceptable test coverage is 80% across all projects.** The CI pipeline enforces this threshold — builds fail if coverage drops below it.

### Rules

- **Red → Green → Refactor.** Never write production code without a failing test that demands it.
- **Test behavior, not implementation.** Tests assert observable outcomes (return values, side effects, exceptions); they do not assert internal method calls unless testing an integration boundary.
- **One concept per test.** A test name should read as a sentence describing the scenario and expected outcome.
- **UNIMED calculation tests are ground truth.** The `Faturamento/Tests/` suite targets real paid invoices. Any change to calculation logic requires a new or updated test case from an actual document.
- **No test should depend on another.** Each test sets up and tears down its own state.

### Coverage Targets by Layer

| Layer                              | Minimum Coverage                                     |
| ---------------------------------- | ---------------------------------------------------- |
| `Faturamento` (calculation engine) | 90% — financial correctness is non-negotiable        |
| `Identity`, `Catalog`              | 80%                                                  |
| `Reporting`                        | 80%                                                  |
| Angular components                 | 80% (V8 coverage via Vitest + `@vitest/coverage-v8`) |

### What counts toward coverage

- Unit tests for domain logic (services, calculation engine, validators)
- Integration tests that hit a real Postgres container via `PostgresContainerFixture` (Testcontainers)
- Component tests for Angular (not e2e)

Infrastructure glue code (EF migrations, DI wiring, `Program.cs`) is excluded from the coverage threshold.

### Angular testing stack

Tests use **Vitest** (not Karma — deprecated). Each app has `vitest.config.ts` pointing to `src/test-setup.ts` which initialises Zone.js and `BrowserDynamicTestingModule`. Coverage is collected via `@vitest/coverage-v8` and reported in `json-summary` format for CI threshold enforcement. Run with `pnpm -F admin-web test` (watch) or `pnpm -F admin-web test:ci` (CI, single-run).

### Backend integration tests

Use `PostgresContainerFixture` for tests that need a real database. It starts a Postgres container via Testcontainers, so `dotnet test` works locally without Docker Compose:

```csharp
[Collection(nameof(PostgresCollection))]
public class MinhaIntegrationTest(PostgresContainerFixture db)
{
    [Fact]
    public async Task Exemplo()
    {
        // SaaS admin context — no tenant filter:
        await using var ctx = db.CreateContext();

        // Tenant-scoped context — global query filter active:
        var tenantId = Guid.NewGuid();
        await using var tenantCtx = db.CreateTenantContext(tenantId);
        // …
    }
}
```

The fixture container is **shared across all tests in the collection** — never assume an empty database. Always use a fresh `Guid.NewGuid()` as `tenantId` to isolate each test's data.

## Authentication & Authorization

The full auth stack is implemented (TASK-AUTH-01 through TASK-AUTH-11). Do not re-implement any of this.

### Method

Google OAuth 2.0 only. No passwords, no magic links, no MFA. `ApplicationUser.PasswordHash` is always null.

### Flow

1. Frontend redirects to `GET /api/v1/auth/google` — backend initiates the OAuth challenge
2. Google redirects to `GET /api/v1/auth/google/finalize?returnUrl=...`
3. Backend looks up the user by `GoogleId` (or by email on first login — auto-associates the `GoogleId`)
4. Issues a JWT access token (15 min) + refresh token (7 days, stored as SHA-256 hash)
5. Returns `{ accessToken, refreshToken, expiresIn }` or redirects via `?returnUrl=`

### JWT Claims

```json
{ "sub": "user-guid", "role": "SaasAdmin|TenantAdmin|Medico", "tenant_id": "guid (absent for SaasAdmin)", "medico_id": "guid (Medico only)", "email": "...", "jti": "guid", "exp": 0 }
```

### Authorization Policies

| Policy         | Roles                      |
| -------------- | -------------------------- |
| `SaasOnly`     | `SaasAdmin`                |
| `TenantAccess` | `TenantAdmin`, `SaasAdmin` |
| `MedicoAccess` | `Medico`                   |

Route prefixes: `/api/v1/saas/**` → `SaasOnly`, `/api/v1/admin/**` → `TenantAccess`, `/api/v1/medico/**` → `MedicoAccess`.

### Tenant Isolation

`ICurrentUser` (scoped service) reads claims from `IHttpContextAccessor`. Injected into `AppDbContext` for the global query filter on every entity implementing `ITenantEntity`:

```csharp
builder.HasQueryFilter(e => _currentUser.IsSaasAdmin || e.TenantId == _currentUser.TenantId);
```

`Tenant`, `ApplicationUser`, `RefreshToken` do **not** implement `ITenantEntity` — they are managed globally by SaaS admin.

`TenantStatusMiddleware` (between `UseAuthentication` and `UseAuthorization`) blocks requests from users whose tenant is `Suspenso` or `Cancelado` with HTTP 403 `{ "error": "tenant_suspended" }`.

### Refresh & Logout

- `POST /api/v1/auth/refresh` — rotates the refresh token (old one revoked, new pair issued). Anonymous endpoint.
- `POST /api/v1/auth/logout` — revokes all active refresh tokens for the user. Requires JWT.

### SaaS Admin Endpoints (`/api/v1/saas/`)

`SaasService` provides CRUD for tenants and users. Every route with `{tenantId}` validates that the tenant exists (LGPD auditability).

| Method  | Route                                                   | Description                             |
| ------- | ------------------------------------------------------- | --------------------------------------- |
| `GET`   | `/api/v1/saas/tenants`                                  | List all tenants                        |
| `POST`  | `/api/v1/saas/tenants`                                  | Create tenant                           |
| `PATCH` | `/api/v1/saas/tenants/{tenantId}/status`                | Activate / suspend / cancel             |
| `GET`   | `/api/v1/saas/tenants/{tenantId}/users`                 | List users of a tenant                  |
| `POST`  | `/api/v1/saas/tenants/{tenantId}/users`                 | Create user (`TenantAdmin` or `Medico`) |
| `PATCH` | `/api/v1/saas/tenants/{tenantId}/users/{userId}/status` | Activate / deactivate user              |

### Frontend Auth (Angular)

Both `admin-web` and `medico-pwa` share the same pattern:

- `AuthService` — access token in memory (Angular signal), refresh token in `localStorage["_rt"]`
- `authInterceptor` — adds `Authorization: Bearer` to all requests outside `/api/v1/auth/`; retries on 401 after refresh
- `authGuard` — protects routes; attempts silent refresh before redirecting to `/auth/login`
- `Login` — single "Entrar com Google" button → `GET /api/v1/auth/google?returnUrl=...`
- `Callback` — reads `accessToken/refreshToken/expiresIn` from query params, strips them from browser history, stores tokens, navigates to `/`
- `app.config.ts` — `provideAppInitializer` restores session on startup via silent refresh

### Environment Variables Required

```env
Google__ClientId=...
Google__ClientSecret=...
Jwt__Secret=...          # min 32 chars
Jwt__Issuer=https://honorare.com.br
Jwt__Audience=honorare-api
Jwt__AccessTokenMinutes=15
Jwt__RefreshTokenDays=7
```

### What is NOT implemented (by design)

Magic links, passkeys, MFA, additional social providers, email invites, RBAC beyond roles, auth audit log, rate limiting on auth endpoints.

---

## Frontend Conventions (Angular)

### Observable error handling

Every `.subscribe()` call **must** include an `error` handler. Omitting it causes silent failures — the HTTP request fails, the loading state never clears, and the user sees nothing. This applies to all HTTP calls in services and components.

```typescript
this._service.criar(payload).subscribe({
  next: () => {
    /* success path */
  },
  error: () => {
    this.erroValidacao.set("Mensagem amigável ao operador.");
  },
});
```

### Nullable GUID fields

When a backend field is `Guid?`, the corresponding TypeScript type must be `string | null`. When building the request payload, never send `""` (empty string) — the .NET JSON deserializer cannot convert it to `Guid?` and throws a 422 error. Use `field || null`:

```typescript
// signal initialized as signal('')
beneficiarioId: this.beneficiarioId() || null,  // sends null, not ""
```

When reading the field back from a response DTO, use `?? ''` to convert `null` back to empty string for the signal:

```typescript
this.beneficiarioId.set(guia.beneficiarioId ?? "");
```

### Optional FK left join (EF Core)

When a FK becomes `Guid?` on an entity, every LINQ query joining that table must become a **left join** or rows without the FK will be excluded:

```csharp
join b in _db.Beneficiarios on g.BeneficiarioId equals (Guid?)b.Id into bs
from b in bs.DefaultIfEmpty()
// nullable projection:
BeneficiarioNome = (string?)b.Nome,
```

---

## Frontend Styling (admin-web)

Before writing any SCSS for `admin-web`, read [`apps/admin-web/STYLES.md`](apps/admin-web/STYLES.md). It is the single source of truth for styles.

**Hard rules:**

- Never use raw hex values (`#1f1b16`) — always use `var(--color-*)`.
- Never use named colors (`red`, `green`).
- Never define `px` values outside the spacing scale. Use `space(n)` from the SCSS function.
- Typography is always applied via `@include text-*` mixins — never set `font-size`/`font-weight`/`line-height` manually.
- Numbers/monetary values always use `@include text-mono-value` (activates tabular nums).
- StyleLint enforces hex banning, `!important`, named colors, BEM, and nesting depth automatically.

**Importing tokens in a component:**

```scss
@use "styles/tokens" as *; // gives access to all mixins + space()

.my-block {
  @include text-body;
  color: var(--color-tinta);
  padding: space(4) space(6);
}
```

CSS custom properties (`var(--...)`) work without any `@use` — they're global.

---

## Key Architectural Constraints

These are firm decisions (see `docs/DECISOES.md` for full rationale):

- **No CQRS, MediatR, Repository pattern, or AutoMapper.** A direct service → `AppDbContext` flow is preferred.
- **Single `AppDbContext`** with EF Core configurations organized per bounded context.
- **No speculative interfaces** — the only interfaces are `IPricingRuleSet` (multiple operators planned) and `IGatewayPagamento` (future payment gateway integration).
- **Calculation engine must trace every step.** Every invoice calculation stores a complete audit trail — this is what enables physicians to dispute underpayments.
- **UNIMED rules validated against real invoices.** The test suite in `Faturamento/Tests/` targets 15–20 real paid UNIMED invoices as ground truth. Do not modify calculation logic without a corresponding test case from a real document.

## Domain Language

Business concepts use Portuguese; infrastructure and tooling use English.

| Portuguese           | English meaning                                                                   |
| -------------------- | --------------------------------------------------------------------------------- |
| Guia                 | Invoice / claim                                                                   |
| Demonstrativo        | Payment statement from operator                                                   |
| Conta-corrente       | Running account / ledger                                                          |
| Convênio / Operadora | Health plan / insurance operator                                                  |
| Prestador            | Healthcare provider (the physician or clinic)                                     |
| Beneficiário         | Patient / plan member                                                             |
| Faturamento          | Billing                                                                           |
| Glosa                | Claim denial / rejection by the operator                                          |
| Tabela               | Pricing table (e.g., CBHPM, AMB, or operator-specific)                            |
| Porte                | Procedure complexity tier                                                         |
| Deflator             | Discount multiplier applied to auxiliary procedures                               |
| Apuração             | Calculation/determination of the correct fee per pricing rules                    |
| Senha                | Pre-authorization code issued by the operator for a procedure                     |
| Apresentada          | Guia status: submitted to operator, awaiting payment                              |
| Liquidada            | Guia status: payment fully settled by operator                                    |
| Em Recurso           | Guia status: included in a formal dispute sent to operator                        |
| ValorApurado         | System-determined correct fee (labeled "VL CORRETO" in the recurso PDF)           |
| ValorLiquidado       | Amount actually paid by the operator (labeled "PG UNIMED" in the recurso PDF)     |
| Recurso              | Formal dispute document sent to operator; also the entity grouping disputed guias |

## Domain: UNIMED Calculation Rules

The core of the product. A procedure's expected payment is:

```
valor_item = valor_base × deflator × Π(multiplicadores_aplicáveis)
```

Applicable multipliers include: accommodation type (enfermaria vs. apartamento), urgency surcharge, laparoscopy surcharge, number of auxiliary surgeons (each with its own deflator), and anesthesia porte. See `docs/DOMINIO.md` for the full rule set.

## Documentation

All product and architecture decisions live in `docs/`:

| File                      | Contents                                              |
| ------------------------- | ----------------------------------------------------- |
| `PROJETO.md`              | Product vision, scope, MVP timeline                   |
| `ARQUITETURA.md`          | Full tech stack decisions and bounded context details |
| `DOMINIO.md`              | Glossary, UNIMED pricing rules, special cases         |
| `DECISOES.md`             | 23 architectural and product decisions with rationale |
| `PROXIMOS_PASSOS.md`      | Phased backlog (Phase 0–6) with effort estimates      |
| `honorare-brand-guide.md` | Design system: palette, typography, voice             |

When implementing a feature, read the relevant `docs/` section before writing code.

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Honorare is a SaaS platform for medical payment reconciliation — a financial ledger ("conta-corrente") between physicians and health plan operators (convênios), starting with UNIMED. The product is aimed at billing companies that manage physician payments, not the doctors directly.

**Current status:** Pre-development (Phase 0). Only documentation exists in `docs/`. No source code has been created yet.

## Commands

These are the intended commands once the codebase is scaffolded (Phase 1+):

```bash
# JavaScript / TypeScript (via pnpm workspaces)
pnpm install                   # Install all JS/TS dependencies
pnpm dev:up                    # Start Docker Compose (Postgres + backend + Nginx)
pnpm -F admin-web dev          # Dev server for admin Angular SPA
pnpm -F medico-pwa dev         # Dev server for doctor Angular PWA
pnpm tools generate-api-client # Regenerate TypeScript client from OpenAPI spec

# .NET Backend
dotnet build                   # Build the backend solution
dotnet run --project apps/backend/App  # Run backend in development
dotnet test                    # Run all xUnit tests
```

CI/CD uses three independent GitHub Actions workflows filtering by path:
- `backend-ci.yml` → `apps/backend/**`
- `admin-web-ci.yml` → `apps/admin-web/**, packages/api-contracts/**`
- `medico-pwa-ci.yml` → `apps/medico-pwa/**, packages/api-contracts/**`

## Architecture

### Planned Monorepo Layout

```
honorare/
├── apps/
│   ├── backend/            # .NET 10 API solution
│   ├── admin-web/          # Angular SPA for billing company admins
│   └── medico-pwa/         # Angular PWA for individual physicians
├── packages/
│   └── api-contracts/      # TypeScript client auto-generated from OpenAPI
├── infra/
│   ├── docker-compose.yml
│   ├── nginx/              # Reverse proxy: /admin/, /app/, /api/v1/
│   └── postgres/
├── tools/
│   └── generate-api-client.sh
└── docs/                   # All domain/architecture documentation
```

### Backend Bounded Contexts

The backend follows a **bounded-context structure without Clean Architecture layers**. Contexts are flat (no `Domain/Application/Infrastructure/` subdirectories unless a context exceeds ~10 files).

**Dependency direction is strictly unidirectional:**

```
Reporting → Faturamento → Catalog → Identity
```

| Context | Responsibility |
|---|---|
| `Identity` | Auth, tenants, users, invitations |
| `Catalog` | Operators, procedures, pricing tables, providers, beneficiaries |
| `Faturamento` | Invoices (guias), UNIMED calculation engine, statement reconciliation |
| `Reporting` | Aggregated queries for admin dashboard and doctor portal |

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

## Key Architectural Constraints

These are firm decisions (see `docs/DECISOES.md` for full rationale):

- **No CQRS, MediatR, Repository pattern, or AutoMapper.** A direct service → `AppDbContext` flow is preferred.
- **Single `AppDbContext`** with EF Core configurations organized per bounded context.
- **No speculative interfaces** — the only interfaces are `IPricingRuleSet` (multiple operators planned) and `IGatewayPagamento` (future payment gateway integration).
- **Calculation engine must trace every step.** Every invoice calculation stores a complete audit trail — this is what enables physicians to dispute underpayments.
- **UNIMED rules validated against real invoices.** The test suite in `Faturamento/Tests/` targets 15–20 real paid UNIMED invoices as ground truth. Do not modify calculation logic without a corresponding test case from a real document.

## Domain Language

Business concepts use Portuguese; infrastructure and tooling use English.

| Portuguese | English meaning |
|---|---|
| Guia | Invoice / claim |
| Demonstrativo | Payment statement from operator |
| Conta-corrente | Running account / ledger |
| Convênio / Operadora | Health plan / insurance operator |
| Prestador | Healthcare provider (the physician or clinic) |
| Beneficiário | Patient / plan member |
| Faturamento | Billing |
| Glosa | Claim denial / rejection by the operator |
| Tabela | Pricing table (e.g., CBHPM, AMB, or operator-specific) |
| Porte | Procedure complexity tier |
| Deflator | Discount multiplier applied to auxiliary procedures |
| Apuração | Calculation/determination of the correct fee per pricing rules |
| Senha | Pre-authorization code issued by the operator for a procedure |
| Apresentada | Guia status: submitted to operator, awaiting payment |
| Liquidada | Guia status: payment fully settled by operator |
| Em Recurso | Guia status: included in a formal dispute sent to operator |
| ValorApurado | System-determined correct fee (labeled "VL CORRETO" in the recurso PDF) |
| ValorLiquidado | Amount actually paid by the operator (labeled "PG UNIMED" in the recurso PDF) |
| Recurso | Formal dispute document sent to operator; also the entity grouping disputed guias |

## Domain: UNIMED Calculation Rules

The core of the product. A procedure's expected payment is:

```
valor_item = valor_base × deflator × Π(multiplicadores_aplicáveis)
```

Applicable multipliers include: accommodation type (enfermaria vs. apartamento), urgency surcharge, laparoscopy surcharge, number of auxiliary surgeons (each with its own deflator), and anesthesia porte. See `docs/DOMINIO.md` for the full rule set.

## Documentation

All product and architecture decisions live in `docs/`:

| File | Contents |
|---|---|
| `PROJETO.md` | Product vision, scope, MVP timeline |
| `ARQUITETURA.md` | Full tech stack decisions and bounded context details |
| `DOMINIO.md` | Glossary, UNIMED pricing rules, special cases |
| `DECISOES.md` | 23 architectural and product decisions with rationale |
| `PROXIMOS_PASSOS.md` | Phased backlog (Phase 0–6) with effort estimates |
| `honorare-brand-guide.md` | Design system: palette, typography, voice |

When implementing a feature, read the relevant `docs/` section before writing code.

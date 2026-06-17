<div align="center">

# Honorare

**The running account between a physician and their health-plan operator.**

Honorare predicts what each medical procedure _should_ pay under UNIMED's pricing rules,
reconciles it against what was _actually_ paid, and surfaces the discrepancies worth disputing —
producing a formatted appeal document (_recurso_) that turns underpayments back into revenue.

Think _Conta Azul / Stripe / Wise_ for medical billing — not hospital software.

[![Backend CI](https://img.shields.io/badge/backend--ci-passing-2ea44f)](#cicd)
[![Frontend CI](https://img.shields.io/badge/frontend--ci-passing-2ea44f)](#cicd)
[![CodeQL](https://img.shields.io/badge/CodeQL-security--and--quality-blue)](#security)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![Angular](https://img.shields.io/badge/Angular-20-DD0031)
![Coverage](<https://img.shields.io/badge/min%20coverage-80%25%20(90%25%20engine)-2ea44f>)
![Tests](https://img.shields.io/badge/tests-587%20cases-2ea44f)

</div>

---

## The problem

A surgeon who works with health-plan operators has no clear view of:

- **how much** they should be paid per procedure — the rules are genuinely complex (porte, apartment-rate doubling, access route, urgency surcharge, surgical role, auxiliary surgeons, anesthesia tiers);
- **what's been paid** versus what's still open;
- **when the operator denied a claim incorrectly** (a _glosa_) — money left on the table.

Today the billing company that manages these payments does it by hand, in spreadsheets, for 100–500 claims a month. Honorare replaces the spreadsheet with a **traceable calculation engine** and an **auditable ledger** — every cent the engine computes can be explained step by step, which is exactly what makes a dispute defensible against the operator.

---

## What it does

| Capability                      | Detail                                                                                                                                                                          |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **UNIMED calculation engine**   | Computes the correct fee for every procedure from real pricing rules. Each calculation persists a full audit trail (`PassoCalculo`) — no black boxes.                           |
| **Reconciliation ledger**       | Records amounts actually paid per claim item (manual or CSV import) and flags divergences against the computed value.                                                           |
| **Recurso (appeal) generation** | The product's core deliverable: a formatted PDF per physician/period listing disputed claims, the correct value, what was paid, the amount owed, and a free-text justification. |
| **Glosa auditing**              | Identifies denied/underpaid claims that are worth contesting.                                                                                                                   |
| **Physician portal (PWA)**      | Doctors log in to see pending claims, statuses, and the notes attached to each divergence.                                                                                      |
| **Multi-tenant SaaS**           | Billing companies are tenants; physicians are end-users scoped within a tenant. Strict data isolation for LGPD compliance.                                                      |

---

## Architecture at a glance

A **pragmatic, deliberately un-over-engineered** monorepo. No CQRS, no MediatR, no Repository pattern, no AutoMapper — a direct `service → DbContext` flow, by explicit architectural decision (documented with rationale in [`docs/DECISOES.md`](docs/DECISOES.md)).

```
honorare/
├── apps/
│   ├── backend/      .NET 10 Web API — bounded contexts, OpenTelemetry, EF Core
│   ├── admin-web/    Angular 20 SPA  — billing-company admins
│   └── medico-pwa/   Angular 20 PWA  — physicians
├── packages/
│   └── api-contracts/  TypeScript client auto-generated from the OpenAPI spec
├── infra/            Docker Compose + Nginx + full observability stack
└── docs/             Product vision, domain rules, and 23 recorded decisions
```

### Backend bounded contexts

Dependencies flow in one strict direction — downstream contexts never leak navigation properties back upstream:

```
Reporting → Faturamento → Catalog → Identity
```

| Context       | Responsibility                                                                          |
| ------------- | --------------------------------------------------------------------------------------- |
| `Identity`    | Google OAuth 2.0 auth, tenants, users, roles, tenant suspension, impersonation audit    |
| `Catalog`     | Operators, procedures, pricing tables, providers, beneficiaries                         |
| `Faturamento` | Claims (_guias_), the UNIMED calculation engine, statement reconciliation, recurso PDFs |
| `Reporting`   | Aggregated queries for the admin dashboard and physician portal                         |

### The calculation engine

The heart of the product. The expected payment for a procedure item is:

```
valor_item = valor_base × deflator × Π(applicable multipliers)
```

Multipliers cover accommodation type, urgency surcharge, laparoscopy, auxiliary-surgeon count, and anesthesia porte. Pricing logic is **pluggable** behind `IPricingRuleSet` — UNIMED is the first concrete rule set; operators without a negotiated table fall back to a `NullRuleSet` (status + notes only, no computed value). Adding a second operator means writing one rule set, not rewriting the engine.

> **Ground-truth testing:** the engine is validated against _real, already-paid_ UNIMED invoices. Calculation logic cannot change without a corresponding test case from an actual document — financial correctness is non-negotiable, and the engine carries a **90% minimum coverage** bar (vs. 80% elsewhere).

---

## Tech stack

| Layer             | Choices                                                                                                |
| ----------------- | ------------------------------------------------------------------------------------------------------ |
| **Backend**       | .NET 10, ASP.NET Core Minimal APIs, EF Core (single `AppDbContext`, per-context configs), PostgreSQL   |
| **Frontend**      | Angular 20 (standalone components + signals), strict templates, Vitest                                 |
| **Auth**          | Google OAuth 2.0 only — no passwords. JWT access tokens + rotating refresh tokens, role-based policies |
| **Observability** | OpenTelemetry (traces, metrics, structured logs) → OTel Collector → Jaeger, Prometheus, Loki, Grafana  |
| **Infra**         | Docker Compose, Nginx reverse proxy (single domain → `/api`, `/admin`, `/app`)                         |
| **Contracts**     | OpenAPI spec → auto-generated TypeScript client (never hand-written)                                   |
| **Tooling**       | pnpm workspaces, Husky + lint-staged, Conventional Commits, asdf-pinned SDK                            |

---

## Engineering standards

This repo is built to a **zero-tolerance-for-warnings** bar — the same rules run locally and in CI. "It passes on my machine" is not a defense.

- **Warnings are errors, everywhere.** .NET: `TreatWarningsAsErrors` + `AnalysisLevel=latest-All` + `EnforceCodeStyleInBuild`, nullable enabled, NuGet CVE auditing. TypeScript: `strict` + type-aware ESLint (`strictTypeChecked`). Angular: `strictTemplates`. ESLint/StyleLint run with `--max-warnings 0`.
- **Test-Driven Development.** Red → green → refactor. **587 backend test cases** across unit and Testcontainers-backed Postgres integration tests; 80% minimum coverage enforced in CI (90% for the calculation engine).
- **Multi-tenancy enforced at the data layer.** A global EF Core query filter scopes every operational entity by `TenantId` — it is never bypassed in application code.
- **Full request observability.** Automatic instrumentation of ASP.NET requests, outbound HTTP, and EF queries; logs carry the active `TraceId` for trace↔log correlation; a global exception handler records exceptions on the active span and returns sanitized responses with precise status-code mapping (422/400/409/500).
- **Pre-commit gates.** Husky runs lint-staged + commitlint on every commit; CI re-runs the full suite.
- **Decisions are written down.** 23 architecture/product decisions live in [`docs/DECISOES.md`](docs/DECISOES.md), each with its rationale — including the deliberate choices _not_ to adopt common patterns.

### CI/CD

Four independent GitHub Actions workflows, path-filtered so each app only rebuilds when it changes:

| Workflow                         | Pipeline                                                                               |
| -------------------------------- | -------------------------------------------------------------------------------------- |
| `backend-ci`                     | restore → build → test → coverage threshold                                            |
| `admin-web-ci` / `medico-pwa-ci` | prettier → eslint → stylelint → test → coverage → build                                |
| `codeql`                         | C# + TypeScript static security analysis (`security-and-quality` pack), weekly + on PR |

---

## Getting started

> Requires Node ≥ 22, pnpm ≥ 10, Docker, and .NET 10 (pinned via `.tool-versions`).

```bash
pnpm install            # install JS/TS deps + activate Husky hooks
pnpm dev:up             # Postgres + backend + Nginx + observability via Docker Compose

pnpm -F admin-web dev   # admin SPA  → http://localhost:4200/admin/
pnpm -F medico-pwa start# doctor PWA → http://localhost:4200/app/
```

Backend tasks go through `make` (it derives the SDK version from `.tool-versions` and works in any shell):

```bash
make build              # compile the backend
make test               # all xUnit tests + coverage
make test-backend-unit  # unit tests only (skips Testcontainers)
```

Observability UIs once `dev:up` is running:

| Signal            | UI                                 |
| ----------------- | ---------------------------------- |
| Traces            | Jaeger — http://localhost:16686    |
| Metrics           | Prometheus — http://localhost:9090 |
| Logs / dashboards | Grafana — http://localhost:3000    |

---

## Documentation

Product and engineering decisions are documented, not tribal knowledge:

| File                                         | Contents                                             |
| -------------------------------------------- | ---------------------------------------------------- |
| [`docs/PROJETO.md`](docs/PROJETO.md)         | Product vision, scope, MVP timeline                  |
| [`docs/ARQUITETURA.md`](docs/ARQUITETURA.md) | Tech-stack decisions and bounded-context details     |
| [`docs/DOMINIO.md`](docs/DOMINIO.md)         | Glossary and the full UNIMED pricing rule set        |
| [`docs/DECISOES.md`](docs/DECISOES.md)       | 23 architectural & product decisions, with rationale |

---

<div align="center">
<sub>Business concepts are modeled in Portuguese (the domain language); infrastructure and tooling in English — a conscious ubiquitous-language choice.</sub>
</div>

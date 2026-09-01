# ADR-0001: Test-Driven Development as an AI Development Harness

- **Status:** Accepted
- **Date:** 2026-09-01
- **Deciders:** Project owner / solo maintainer
- **Related:** [ADR-0002](./0002-opentelemetry-for-production-diagnosability.md), `docs/DECISOES.md` (D-010, D-013, D-038), `CLAUDE.md` § Testing Philosophy

## Context

Honorare is built almost entirely by a solo maintainer working with AI coding agents
(Claude Code). That working model has two properties that dominate every other
engineering consideration on this project:

1. **The agent produces plausible code, not necessarily correct code.** LLM output
   compiles, reads well, uses the right idioms, and can still get a business rule
   subtly wrong. In a domain where `valor_item = valor_base × deflator × Π(multiplicadores)`
   decides how much money a physician is owed, a wrong multiplier is not a bug that
   surfaces as a crash — it silently produces a number that looks reasonable and is
   wrong. Nobody notices until an operator disputes a payment.

2. **The agent has no durable memory.** Each session starts from a cold context.
   Whatever the previous session understood about a rule is gone unless it was written
   down somewhere the next session is forced to read. Prose documentation decays and is
   not verified by anything; the agent can contradict `docs/` and the build stays green.

Conventional arguments for testing (regression safety, refactoring confidence) apply
here too, but they are secondary. The primary problem is that there is **no second pair
of human eyes and no QA step**. Code review by the same person who prompted the agent
catches far less than it does across two engineers, because the reviewer is anchored to
the intent they just described.

We also have an unusually good source of ground truth: 15–20 real, already-paid UNIMED
invoices. For each one, the correct output is known exactly. This is a domain where
tests can assert real answers rather than the implementation's own opinion of itself.

## Decision

**We practice strict TDD, and we treat the test suite as the primary control surface
for AI-assisted development, not merely as a safety net.**

Concretely:

### 1. Red → Green → Refactor is mandatory

No production code is written without a failing test that demands it. The
`/tdd-task` workflow (`.claude/commands/tdd-task.md`) encodes this as the only
sanctioned way to implement a unit of work: read exactly one task from a spec, write
the tests, watch them fail, write the minimum code to pass, commit, mark the task done,
and end the session. One task per isolated context window.

This gives the agent a **machine-checkable stop condition**. Without it, "done" is
whatever the model decides looks finished. With it, "done" is a green run of tests that
were written before the implementation existed and therefore could not be shaped to fit
whatever the model happened to produce.

### 2. Tests are the specification the agent cannot argue with

`docs/` describes rules in prose; the test suite *encodes* them. When a future session
proposes a change to the calculation engine, the failing tests are what stop it. This is
why `CLAUDE.md` states that UNIMED calculation tests are ground truth and that changing
calculation logic requires a corresponding test case from a real document — the rule
exists to prevent an agent from "fixing" a test to match code it just wrote.

### 3. Coverage floors are enforced in CI, not recommended

| Scope                                | Floor | Rationale                                 |
| ------------------------------------ | ----- | ----------------------------------------- |
| `Faturamento` (calculation engine)   | 90%   | Financial correctness is non-negotiable   |
| `Identity`, `Catalog`, `Reporting`   | 80%   | Standard floor                            |
| Angular components (both apps)       | 80%   | Standard floor                            |

`.gitea/workflows/ci-cd.yml` fails the pipeline below 80% line coverage for the backend
(ReportGenerator over Cobertura output) and for each Angular app (Vitest
`coverage-summary.json`). Infrastructure glue — EF migrations, DI wiring, `Program.cs` —
is excluded from the threshold.

### 4. The harness extends beyond tests

Coverage alone does not constrain an agent enough. The same principle — *make the
machine reject bad output instead of relying on human review* — is applied across the
stack: `TreatWarningsAsErrors` + `AnalysisLevel=latest-All` on .NET, `strict` TypeScript
with `strictTemplates`, ESLint and StyleLint at `--max-warnings 0`, and Husky pre-commit
hooks. Every one of these turns a judgement call into a build failure.

### 5. Integration tests hit a real database

`PostgresContainerFixture` (Testcontainers) starts a real Postgres so that EF Core query
filters, the multi-tenant global filter, and migrations are exercised for real rather
than against an in-memory provider that behaves differently. The container is shared
across the collection, so every test isolates itself with a fresh
`tenantId = Guid.NewGuid()`.

## Consequences

### Positive

- **The agent gets objective feedback instead of approval.** The loop is
  write test → run → observe failure → implement → run → observe pass. No step of that
  depends on a human judging whether the code "looks right".
- **Regressions across sessions are caught mechanically.** A session with no memory of
  D-039 (`ACRESCIMO = 0,00%` is not urgency) cannot silently undo it; a test fails.
- **Refactoring is cheap**, which matters because AI-assisted development produces a lot
  of code that deserves restructuring shortly after it is written.
- **Real-invoice tests make correctness auditable to the client.** When an operator
  disputes a value, we can point at the test case built from the paid invoice.
- Current state: 590 `[Fact]`/`[Theory]` cases across `Identity.Tests` (125),
  `Catalog.Tests` (177) and `Faturamento.Tests` (288), plus 57 frontend spec files.

### Negative

- **Test code is the larger half of the work.** Each feature costs materially more
  up-front than writing the implementation alone.
- **Coverage floors can be gamed.** Line coverage is a proxy; a suite can hit 90% and
  still assert nothing meaningful. The floor is a tripwire, not a quality metric — it
  catches *untested* code, not *badly tested* code. Reviewing test quality remains a
  human responsibility.
- **Testcontainers requires a working Docker daemon**, which makes the full test run
  slower and unavailable in constrained environments. Mitigated by
  `make test-backend-unit`, which skips container-backed tests.
- **Tests written by the same agent that writes the code can encode the same
  misunderstanding.** TDD ordering reduces this (the test exists before the code), and
  the real-invoice ground truth removes it entirely for the calculation engine, but it
  is not eliminated everywhere.
- Warnings-as-errors occasionally blocks work for reasons unrelated to correctness
  (auto-generated EF migrations need a `.editorconfig` suppressing IDE0005, IDE0161,
  CA1515, CA1861). Accepted as the cost of a build that never accumulates noise.

### Neutral

- The 80% floor is a *minimum*, not a target. Contexts routinely exceed it.
- End-to-end/browser tests are deliberately out of scope. Component tests plus
  container-backed integration tests are the boundary.

## Alternatives considered

**Write tests after the implementation ("test-later").** Rejected: tests written after
the fact are shaped by the code that exists and tend to assert what it already does. For
AI-generated code this is close to worthless — it locks in whatever the model produced,
including the misunderstanding.

**Rely on human code review instead of coverage gates.** Rejected: there is one
developer, and reviewing code you just prompted into existence is not independent
review. The gate has to be mechanical to mean anything.

**Test only the calculation engine, ship the rest untested.** Rejected: the failures
that actually reached production were not in the engine. They were in tenant filtering,
nullable FK joins, and Angular reactive-context bugs (see `CLAUDE.md` § Frontend
Conventions) — exactly the areas that would have been left uncovered.

**Formal verification / property-based testing of the pricing rules.** Not rejected in
principle, but out of scope for the MVP: the rules come from a document that is itself
being reverse-engineered, so example-based tests derived from paid invoices are the more
truthful specification right now.

## Compliance

- CI enforces the 80% line-coverage floor per project; the pipeline fails below it.
- `make test` runs the full suite; `make test-backend-unit` skips Testcontainers.
- New calculation behaviour requires a test case sourced from a real document.
- Reviewers should check whether tests were committed *before* implementation in the
  task's commit history, not merely that tests exist.

## Revisit when

- The team grows past one developer and independent human review becomes available.
- The AI-assisted workflow is abandoned in favour of fully manual development.
- Line coverage demonstrably stops correlating with defects found in production.

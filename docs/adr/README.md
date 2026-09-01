# Architecture Decision Records

ADRs record decisions whose *rationale* matters more than the decision itself — the
context that made the choice correct, the alternatives that were rejected, and the
conditions under which it should be revisited.

Written in English, unlike the rest of `docs/`, which is Portuguese. Business concepts
keep their Portuguese names (guia, apuração, operadora) per the project's domain-language
convention.

## Relationship to `DECISOES.md`

`docs/DECISOES.md` is the **complete, terse list** of every binding decision (D-001…),
one short paragraph each. An ADR is written only when a decision needs more room than
that: real context, trade-offs, and the alternatives considered. Most decisions never
need one.

`DECISOES.md` remains the binding index. ADRs expand on entries there; they do not
replace them.

## Index

| ADR                                                         | Title                                     | Status   |
| ----------------------------------------------------------- | ----------------------------------------- | -------- |
| [0001](./0001-tdd-as-an-ai-development-harness.md)          | TDD as an AI Development Harness          | Accepted |
| [0002](./0002-opentelemetry-for-production-diagnosability.md) | OpenTelemetry for Production Diagnosability | Accepted |

## Format

Numbered `NNNN-kebab-case-title.md`, sequential, never reused. Sections: Status, Context,
Decision, Consequences (positive / negative / neutral), Alternatives considered,
Compliance, Revisit when.

A superseded ADR is never deleted or edited into agreement with the present — its status
becomes `Superseded by ADR-NNNN` and the file stays as it was.

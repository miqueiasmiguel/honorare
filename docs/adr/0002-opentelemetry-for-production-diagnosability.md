# ADR-0002: OpenTelemetry for Production Diagnosability

- **Status:** Accepted
- **Date:** 2026-09-01
- **Deciders:** Project owner / solo maintainer
- **Related:** [ADR-0001](./0001-tdd-as-an-ai-development-harness.md), `docs/DECISOES.md` (D-010), `CLAUDE.md` § Observability

## Context

Production failures in Honorare were effectively undiagnosable.

The concrete failure mode was always the same: an operator hit an error, reported it as
"deu erro" (possibly with a screenshot of a generic message), and the bug could not be
reproduced locally. The reasons compound:

- **Production data cannot be copied to a development machine.** The inputs that trigger
  these bugs are real UNIMED invoices, real beneficiary records and tenant-specific
  pricing tables. This is personal health-adjacent financial data; LGPD compliance and
  the multi-tenant isolation model (D-010) are the whole point of the architecture.
  "Just restore a dump locally" is not available.
- **The failing input is tenant- and catalog-specific.** A guia that fails apuração does
  so because of that tenant's procedure table, that operadora's rule set and that
  specific combination of multipliers. Reconstructing it by hand from a user's
  description is guesswork.
- **The API deliberately hides the detail.** The global exception handler returns a
  sanitized `application/problem+json` body — never internal detail — which is correct
  for a multi-tenant SaaS and simultaneously means that unless the server captures the
  exception itself, the stack trace is destroyed at the boundary.
- **Console logs are ephemeral and uncorrelated.** Container stdout survives until the
  next deploy, has no request correlation, and answers "was there an error?" but never
  "which tenant, which guia, which SQL, and what happened before it".

What was actually needed, in order: **the stack trace**, the request that produced it,
the tenant it belonged to, and the sequence of database calls around it. Nothing less
closed the loop.

There is a second, less acute need. The calculation engine is the product: when apuração
produces a wrong or slow result, the question is *which step* — and the engine already
stores a full audit trail per calculation, so runtime spans around those steps make the
audit trail navigable rather than merely archived.

## Decision

**Instrument the backend with OpenTelemetry — traces, metrics and logs — exporting via
OTLP to an OpenTelemetry Collector that fans out to Jaeger, Prometheus and Loki, with
Grafana as the single query surface.**

### Pipeline

```
.NET backend ──OTLP/gRPC──▶ OTel Collector ──┬─▶ Jaeger      (traces)
  (Otlp:Endpoint)                            ├─▶ Prometheus  (metrics)
                                             └─▶ Loki        (logs)
                                                    ▲
                                              Grafana (pre-provisioned datasources)
```

`infra/otel/collector.yml` defines the three pipelines; `infra/grafana/provisioning/`
ships the datasources so a fresh `make up` gives a working Grafana with no manual setup.

### Instrumentation

Registered in `Program.cs`: ASP.NET Core requests (with `RecordException = true`),
outbound `HttpClient`, EF Core commands, and .NET runtime metrics. `ILogger` output is
bridged to OTel via `builder.Logging.AddOpenTelemetry(...)`, so **every log record carries
the active `TraceId`** — this is what makes trace ↔ log correlation work in Grafana and
is the single most valuable property of the whole setup.

### The exception handler contract

On every unhandled exception, `UseExceptionHandler` must do three things:

1. **Log** via `ILogger` — full stack trace reaches Loki.
2. **Record on the active span** via `Activity.Current?.AddException(ex)` plus
   `SetStatus(ActivityStatusCode.Error, …)` — Jaeger shows the exception event.
   (`AddException`, not the deprecated `RecordException`.)
3. **Return a sanitized response** — the client learns nothing internal.

Status mapping: `BadHttpRequestException { InnerException: JsonException }` → 422,
other `BadHttpRequestException` → 400, `InvalidOperationException` → 409 (business rule
violation), anything else → 500.

### Tenant attribution

`TenantId` is attached as a span attribute on requests touching tenant data, and
`ImpersonationSpanMiddleware` tags `saas.acting_tenant` when a SaaS admin is acting
inside a tenant. This makes "which tenant hit this" answerable **without** putting
tenant data into the telemetry payload — the isolation guarantee is not weakened to gain
observability.

### Domain code stays vendor-neutral

Custom spans and metrics use the BCL primitives `ActivitySource` and `Meter` — domain
code never takes a dependency on the OTel SDK. Source names follow
`Honorare.<Context>` and are registered in `Program.cs` via `.AddSource(...)`:

```csharp
private static readonly ActivitySource Activity = new("Honorare.Faturamento");

using var span = Activity.StartActivity("ApurarGuia");
span?.SetTag("guia.id", guiaId);
```

### Non-negotiable

Telemetry is never disabled in production. If a collector is unavailable,
`Otlp:Endpoint` may point at a no-op address, but instrumentation stays active.

## Consequences

### Positive

- **The original problem is solved.** Production stack traces are captured, searchable
  in Loki, and linked to the trace in Jaeger that shows the request, its EF Core queries
  and its timings. Reproducing locally is no longer a prerequisite for fixing a bug.
- **One correlation key.** `TraceId` joins a user-visible error to its log line, its
  span tree and its SQL. This is what turns three separate tools into one workflow.
- **Vendor-neutral by construction.** OTLP is the wire format and the SDK is standard;
  swapping Jaeger for Tempo, or the whole stack for a hosted backend, is a collector
  config change, not a code change.
- **Free baseline instrumentation.** HTTP, EF Core and runtime metrics arrive without
  any domain code being written.
- **Debuggable without touching production data**, which keeps the LGPD posture intact
  — the thing that made this problem hard in the first place is not compromised by the
  solution.
- Custom spans around apuração make the calculation engine's audit trail navigable at
  runtime, not just after the fact.

### Negative

- **Operational surface grows.** Collector, Jaeger, Prometheus, Loki and Grafana are
  five more services to run, upgrade and keep healthy in `infra/docker-compose.yml`.
- **Resource cost.** The collector is memory-limited to 256 MiB and batches on a 10 s
  timeout, but the stack is not free on a small host.
- **Storage is currently unbounded in practice.** No retention policy is configured;
  this will need attention before trace and log volume becomes a real cost.
- **Telemetry is a data-leak surface.** Span attributes and log messages are exported
  outside the tenant boundary. The rule "identifiers yes, tenant data no" has to be
  enforced by review — nothing prevents someone from tagging a span with a beneficiary
  name.
- **Silent failure mode.** If the exception handler stops calling
  `Activity.Current?.AddException(ex)`, spans still show `error: true` but carry no
  detail, and nothing fails loudly. This is documented in `CLAUDE.md` as the first thing
  to check when a 500 has no Jaeger event.
- Batching means telemetry is near-real-time, not real-time; a crash can lose the last
  unflushed batch.

### Neutral

- Local development without Docker points `Otlp:Endpoint` at `http://localhost:4317`;
  inside Compose it is `http://otel-collector:4317`.
- No sampling is configured — every trace is exported. Fine at current volume,
  revisit when it is not.

## Alternatives considered

**Structured file logs (Serilog) only.** Rejected: it answers "what was logged" but not
"what happened during this request". No span tree, no EF Core timings, no correlation
between a user's error and the SQL behind it — and correlation was the missing piece.

**A hosted APM (Sentry / Datadog / Application Insights).** Rejected for the MVP.
Sentry would have solved the stack-trace half quickly, but these are paid, vendor-locked,
and require shipping production telemetry to a third party — an added LGPD assessment for
a single-client product. OpenTelemetry keeps the option open: pointing the collector at a
hosted backend later is a configuration change.

**Application Insights specifically**, as the path of least resistance for .NET.
Rejected: it binds the codebase to Azure while the product is not deployed on Azure.

**Do nothing / add ad-hoc logging when a bug appears.** Rejected — this *was* the status
quo, and it is what produced the ADR. Reactive logging requires reproducing the bug to
know what to log, which is exactly what was impossible.

## Compliance

- Custom span and metric names use the `Honorare.<Context>` prefix and are registered in
  `Program.cs`.
- Requests touching tenant data carry `TenantId` as a span attribute.
- Domain code uses `ActivitySource`/`Meter`, never the OTel SDK directly.
- Never put beneficiary, physician or invoice content into span attributes or log
  messages — identifiers only.

## Revisit when

- Trace/log volume makes self-hosted storage costly enough to justify sampling or a
  retention policy.
- The project moves to managed infrastructure where a hosted backend is cheaper than
  operating the collector fan-out.
- A second service is added and cross-service trace propagation needs explicit design.

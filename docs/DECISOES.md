# Honorare — Decisões

Lista consolidada das decisões importantes tomadas. Cada uma tem justificativa curta e gatilho para revisitar.

## Produto e escopo

### D-001: Nome do produto é Honorare

Verificação INPI/domínio é responsabilidade do dono e ainda está pendente. Em código, pacotes e infra, usar `honorare` (lowercase) e `Honorare` (PascalCase).

**Revisitar:** se INPI ou domínio comprometer.

### D-002: Foco MVP é UNIMED

Estrutura é pluggable (`IPricingRuleSet`) para outras operadoras, mas implementação inicial apenas UNIMED. Cada Unimed Singular é uma `Operadora` separada no modelo.

**Revisitar:** quando primeiro cliente pedir outra operadora.

### D-003: Entrada manual no MVP, OCR na fase 2

Cliente atual usa planilhas. MVP replica o fluxo manual. OCR via LLM multimodal (já validado pelo dono) entra na fase 2.

**Revisitar:** após estabilização do MVP, com 100+ guias/mês causando gargalo de digitação.

### D-004: PWA em vez de Flutter

Cliente queria app nativo "porque concorrente tem". Decisão: PWA primeiro (instala como app, ícone na tela inicial). Flutter como fase 2 se uso real justificar e cliente quiser estar nas lojas.

**Trade-off:** sem distribuição via App Store / Google Play; sem biometria iOS no MVP.

**Revisitar:** após 6 meses de uso real do PWA, com dados sobre taxa de adoção.

### D-005: Cobrança SaaS fora do sistema no MVP

Cliente único. Cobrança da assinatura por boleto/PIX manual com contrato direto. Bloqueio por inadimplência via campo `Tenant.Status` editado manualmente pelo admin.

**Não implementar:** gateway, webhooks, planos, NFS-e automática, trial.

**Revisitar:** quando houver 3+ clientes pagando ou operação manual virar gargalo.

## Arquitetura

### D-006: Stack — .NET 10 + Angular + PWA + Postgres

Backend .NET 10 com EF Core. Frontend Angular para admin web e PWA do médico. PostgreSQL como banco. pnpm para JS/TS.

**Revisitar:** se houver decisão estratégica de mudar stack (improvável no MVP).

### D-007: Monorepo único

Repositório único com `apps/`, `packages/`, `infra/`, `tools/`, `docs/`. pnpm workspaces para JS; backend gerenciado por dotnet CLI fora dos workspaces.

**Não usar Nx/Turborepo no MVP** — complexidade não compensa.

**Revisitar:** quando houver 5+ apps/packages e dor real de gerenciar.

### D-008: Bounded contexts — Identity, Catalog, Faturamento, Reporting

Quatro bounded contexts no backend, dentro de uma única solution. App é o composition root.

Dependência hierárquica: `Reporting → Faturamento → Catalog → Identity`. Nunca o contrário.

**Revisitar:** se aparecer responsabilidade que não cabe em nenhum dos quatro.

### D-009: Faturamento (português) reservando Billing para SaaS

Bounded context atual de cobrança contra operadoras se chama `Faturamento`. O nome `Billing` está reservado para futuro contexto de assinatura SaaS.

Mistura de inglês (`Identity`, `Catalog`, `Reporting`) e português (`Faturamento`) é decisão consciente — `Faturamento` ressoa com vocabulário do setor.

**Detalhes em ADR-005.**

### D-010: Multi-tenant desde o dia 1

Toda entidade operacional tem `TenantId`. Global query filter no EF Core. Mesmo com cliente único, estrutura multi-tenant é não-negociável (LGPD + futuro próximo).

**Não revisitar.** Decisão fundamental.

### D-011: DbContext único

`AppDbContext` único com configurações por bounded context em pastas separadas. Não usar DbContext por contexto.

**Revisitar:** se transações entre contextos virarem dor (improvável neste tamanho).

### D-012: Schema novo, sem migrar dados legados

Sistema atual está em testes (não produção). Liberdade para reescrever schema do zero. Dados antigos não serão migrados.

### D-013: Sem versionamento de procedimentos no MVP

Tabela de procedimentos pode ser substituída por inteiro ao reimportar. Snapshot de valores usados é gravado em cada `Calculo` para garantir explicabilidade histórica mesmo sem versionamento formal.

**Revisitar:** se cliente contestar valores históricos após mudança de tabela.

### D-014: URLs por subpath — `/admin/`, `/app/`, `/api/v1/`

Mesmo domínio com Nginx roteando subpaths. Não subdomínios.

**Trade-offs:** PWA em subpath é mais frágil (service worker scope, base href, iOS); cookies de auth compartilháveis; um certificado SSL.

**Revisitar:** se PWA der problemas estruturais com subpath.

### D-015: API versionada como `/api/v1/` desde o início

Backend e clientes (admin Angular, PWA Angular, futuro Flutter) não fazem deploy juntos. Backward-compat exige versionamento desde já.

### D-016: OpenAPI como fonte de verdade

Backend gera OpenAPI; cliente TypeScript é gerado automaticamente em `packages/api-contracts/`. Os dois Angular consomem via `workspace:*`. **Nenhum cliente HTTP escrito à mão.**

### D-017: Dois Angular separados (admin-web, medico-pwa)

Não um Angular único com lazy loading. Justificativa: PWA configuration, bundle size, contextos mentais distintos, deploy independente.

**Compartilham:** apenas `@honorare/api-contracts`.

## Princípios de design

### D-018: YAGNI radical

Não adicionar padrões sem dor concreta presente:
- Sem CQRS, MediatR
- Sem Repository (EF Core já é)
- Sem AutoMapper (mapeamento manual)
- Sem Clean Architecture com camadas físicas

### D-019: Sem interfaces especulativas

Uma classe = uma implementação. Interface só com múltiplas implementações reais.

**Exceções legítimas:**
- `IPricingRuleSet` (UNIMED é primeira de várias operadoras previstas)
- `IGatewayPagamento` (futuro, quando `Billing` for criado)

### D-020: Estrutura flat por contexto

Subpastas dentro de bounded context só quando justificadas pelo número de arquivos. Não criar `Domain/`, `Application/`, `Infrastructure/`.

## Validação

### D-021: 15-20 casos reais UNIMED como padrão-ouro

Motor de cálculo é validado contra guias reais já pagas do cliente. Esses casos vivem em `Faturamento.Tests/Calculo/CasosReais/` e são a fonte de verdade — não a documentação de regras.

**Pendência crítica:** conseguir esses casos antes de implementar o motor.

## Tooling

### D-022: Claude Code com escopo apertado por tarefa

Cada prompt: escopo limitado, critério de pronto explícito, "não melhore de passagem". Investigação antes de ação. Migrations sempre revisadas, nunca aplicadas automaticamente.

### D-023: CLAUDE.md em três níveis

- Raiz: regras gerais do monorepo
- Por app: regras específicas (Angular, .NET, PWA)
- Por bounded context (backend): regras específicas de domínio

Knowledge do Project no Claude.ai contém apenas os documentos destilados (`PROJETO.md`, `ARQUITETURA.md`, `DOMINIO.md`, `DECISOES.md`, `PROXIMOS_PASSOS.md`).

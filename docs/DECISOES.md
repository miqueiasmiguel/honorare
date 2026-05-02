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

### D-024: Google OAuth 2.0 como único método de autenticação no MVP

Sem senha, sem magic link, sem MFA, sem convite por email. Usuários são pré-cadastrados pelo SaaS admin (email + role); o `GoogleId` é associado no primeiro login. `PasswordHash` é sempre nulo.

**Justificativa:** elimina toda a superfície de ataque de senha; público B2B brasileiro tem alta penetração de Google Workspace/Gmail; provider Google é built-in no ASP.NET Core Identity sem infraestrutura adicional.

**Revisitar:** quando aparecer usuário sem conta Google (adicionar magic link como fallback) ou quando volume justificar MFA.

**Não implementar no MVP:** magic link, passkeys (WebAuthn), social login adicional (Apple/Microsoft), recuperação de conta self-service, convite por email.

### D-025: Três roles fixas — SaasAdmin, TenantAdmin, Medico

Sem RBAC granular por recurso. Roles são suficientes para o MVP com cliente único.

| Role          | Acessa             | Isolamento                         |
| ------------- | ------------------ | ---------------------------------- |
| `SaasAdmin`   | Painel SaaS global | Nenhum                             |
| `TenantAdmin` | Painel do tenant   | `TenantId` via global query filter |
| `Medico`      | PWA do médico      | `TenantId` + `MedicoId` explícito  |

Claims do JWT: `sub`, `role`, `tenant_id` (ausente para SaasAdmin), `medico_id` (presente só para Medico).

**Revisitar:** quando houver múltiplos perfis dentro de um tenant (ex: admin só de leitura, admin de escrita).

### D-026: `ICurrentUser` como único ponto de bypass do global query filter

O `AppDbContext` injeta `ICurrentUser` (serviço scoped). O global query filter por `TenantId` verifica `ICurrentUser.IsSaasAdmin` antes de filtrar. Nenhum outro mecanismo pode bypassar o filtro.

Isolamento por `MedicoId` não usa global filter — é `Where` explícito nos endpoints do médico para evitar interferência com queries administrativas.

**Regra LGPD:** toda rota do SaaS admin que acessa dados de tenant específico recebe `tenantId` como parâmetro de rota e valida sua existência — garante auditabilidade sem violar o isolamento.

**Revisitar:** nunca — essa abstração é simples e funciona para qualquer expansão futura de roles.

### D-027: `ApplicationUser` não implementa `ITenantEntity`

`ApplicationUser` é gerenciado globalmente (SaaS admin precisa acessar usuários de qualquer tenant). O global query filter por `TenantId` **não se aplica** a ele.

Todo código que consulta `_db.Users` no contexto de um tenant (ex: `AdminService`) deve incluir `.Where(u => u.TenantId == tenantId)` explicitamente. Omitir esse filtro é um vazamento LGPD.

**Revisitar:** nunca — é uma exceção estrutural intencional ao modelo multi-tenant.

### D-028: Role é derivada dinamicamente, não é coluna no banco

`AuthService.DeriveRole(user)` encapsula a lógica: `TenantId == null → SaasAdmin`, `MedicoId != null → Medico`, `else → TenantAdmin`. Usar sempre esse método estático — nunca ler uma coluna `Role` que não existe.

**Revisitar:** quando aparecer um quarto role (exigiria nova lógica no método e nova claim no JWT).

### D-029: TenantAdmin não pode desativar a si mesmo

`AdminService.UpdateUserStatusAsync` rejeita com `ForbiddenError` quando `userId == currentUser.UserId && !isActive`. Previne auto-lockout acidental — o SaaS admin é o único que pode desativar um TenantAdmin via painel SaaS.

**Revisitar:** nunca — é uma regra de segurança sem exceção legítima.

### D-023: CLAUDE.md em três níveis

- Raiz: regras gerais do monorepo
- Por app: regras específicas (Angular, .NET, PWA)
- Por bounded context (backend): regras específicas de domínio

Knowledge do Project no Claude.ai contém apenas os documentos destilados (`PROJETO.md`, `ARQUITETURA.md`, `DOMINIO.md`, `DECISOES.md`, `PROXIMOS_PASSOS.md`).

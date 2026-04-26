# Honorare — Arquitetura

## Stack

- **Backend:** .NET 10, ASP.NET Core, EF Core
- **Banco:** PostgreSQL único, schema único
- **Admin web:** Angular (última LTS) como SPA, servido em `/admin/`
- **Médico:** Angular configurado como PWA, servido em `/app/`
- **Monorepo:** pnpm workspaces (JS/TS) + dotnet (backend gerenciado fora dos workspaces)
- **Infra local:** Docker Compose, Nginx como proxy reverso roteando `/admin`, `/app`, `/api`
- **Contratos API:** OpenAPI gerada pelo backend, cliente TypeScript regenerado para os dois Angular

## Estrutura do monorepo

```
honorare/
├── apps/
│   ├── backend/       # .NET API (porta 5000)
│   ├── admin-web/     # Angular SPA do admin
│   └── medico-pwa/    # Angular PWA do médico
├── packages/
│   └── api-contracts/ # Cliente TypeScript gerado da OpenAPI
├── infra/             # docker-compose, nginx, postgres init
├── tools/             # scripts (gerar cliente, seed)
└── docs/              # ADRs, glossário, regras
```

## Bounded contexts (backend)

```
App           # composition root, endpoints HTTP
Identity      # tenants, usuários, convites, autenticação, suspensão
Catalog       # operadoras, procedimentos, tabelas, prestadores, beneficiários
Faturamento   # guias, demonstrativos, cálculo, conciliação, divergências
Reporting     # queries agregadas para portal do médico e admin
```

Dependência só "para baixo": `Reporting → Faturamento → Catalog → Identity`. Nunca o contrário.

Entidade de outro context entra por `Id`, não por referência. Ex: `Guia.OperadoraId`, não `Guia.Operadora`.

`Faturamento` (em português) trata de cobrança contra operadoras de saúde. O nome `Billing` está reservado para futuro contexto de assinatura SaaS — ainda não implementado.

## Princípios de design

### YAGNI radical

Não adicionar padrões sem dor concreta presente. Em particular, NÃO usar:
- CQRS, MediatR
- Repository pattern (EF Core já é repository)
- AutoMapper / Mapster (mapeamento manual enquanto for pequeno)
- Clean Architecture com camadas físicas separadas

### Sem interfaces especulativas

Uma classe = uma implementação. Interface só quando há múltiplas implementações reais.

**Exceção legítima:** `IPricingRuleSet` em `Faturamento/Calculo/`. Justificativa: sistema é agnóstico de operadora; UNIMED é a primeira de várias previstas (Bradesco, Amil, etc.). Implementações previstas:
- `UnimedRuleSet` — motor completo com pipeline de modificadores
- `NullRuleSet` — para convênios sem tabela negociada; retorna `ValorApurado = null`, guia opera só com status e observação

**Exceção futura legítima:** `IGatewayPagamento` quando o contexto `Billing` for criado.

### Estrutura flat dentro de cada context

Dentro de cada bounded context, evitar subpastas `Domain/`, `Application/`, `Infrastructure/`. Subpastas só quando o número de arquivos justificar (geralmente >10).

### Multi-tenant é não-negociável

Toda entidade operacional tem `TenantId`. Toda query filtra por tenant via **global query filter** do EF Core. Vazamento entre tenants é incidente LGPD.

### Português para domínio, inglês para infra

Entidades, services, regras de negócio: português (`Guia`, `ItemGuia`, `Faturamento`).
Configuração, infra, ferramentas: inglês (`appsettings.json`, `Dockerfile`).

Bounded contexts: maioria em inglês (`Identity`, `Catalog`, `Reporting`), exceto `Faturamento` (decisão consciente, ressonância com vocabulário do setor).

### DbContext único

`AppDbContext` único com configurações organizadas por context em pastas separadas. Não usar DbContext por bounded context (complica transações sem benefício real para este tamanho de projeto).

## Frontend — duas apps Angular separadas

`admin-web` e `medico-pwa` são projetos Angular **independentes**, não um único Angular com lazy loading.

Justificativa:
- PWA configuration (service worker, manifest, cache) é específico do médico
- Bundle size do médico precisa ser pequeno (mobile, 4G)
- Contextos mentais diferentes (admin: entrada de dados; médico: visualização)
- Deploy independente

**Compartilham:** apenas o cliente TypeScript gerado da OpenAPI (`@honorare/api-contracts`).

**Não compartilham (no MVP):** componentes UI. Cada app tem os seus. Quando aparecer duplicação real, cria-se `packages/ui-components/`.

## Backward-compat na API

Backend e clientes não fazem deploy juntos. Regras:
- Adicionar campos: ok
- Tornar campos opcionais: ok
- Adicionar endpoints: ok
- Mudar tipo, remover campo, mudar URL: precisa versionar (`/api/v1/`, `/api/v2/`)

Todos os endpoints devem estar em `/api/v1/` desde o início.

## URLs em produção

Caminhos no mesmo domínio (não subdomínios):
- `/admin/` → admin-web
- `/app/` → medico-pwa
- `/api/v1/` → backend

Implicações:
- Nginx ou similar como proxy reverso obrigatório
- `<base href="/admin/">` no admin Angular, `<base href="/app/">` no PWA
- Service Worker do PWA com `scope: "/app/"` e header `Service-Worker-Allowed: /app/`
- PWA manifest com `start_url: "/app/"` e `scope: "/app/"`

## Cobrança SaaS

Cobrança da assinatura do produto Honorare é feita **fora do sistema** no MVP (boleto/PIX manual com contrato direto).

Bloqueio por inadimplência é **manual** via campo `Tenant.Status` no contexto `Identity`. Tela admin altera status; middleware bloqueia acesso quando suspenso. Implementar antes do segundo cliente entrar.

Sistema de cobrança automática (gateway, webhooks, NFS-e, planos) é trabalho de 6-10 semanas adiado para depois do MVP. Quando chegar, será novo bounded context `Billing`.

## Geração de PDF (recurso)

O recurso é gerado no backend e entregue como download pelo endpoint `GET /api/v1/recursos/{id}/pdf`. Biblioteca a definir (QuestPDF ou similar para .NET — não usar ferramentas externas nem serviços cloud para isso).

O template do recurso inclui o logo da billing company — cada `Tenant` armazena `LogoUrl` e `NomeExibicao` para compor o cabeçalho do documento.

## Testes

- **Backend:** xUnit, testes por bounded context. Os 15-20 casos reais UNIMED são o **padrão-ouro do motor de cálculo** — vivem em `Faturamento.Tests/Calculo/`.
- **Frontend:** Jest/Karma (padrão Angular), testes de componentes críticos.
- Testes testam **comportamento, não implementação**. Não mockar o que pertence ao teste.

## CI/CD

Três workflows independentes com path filter:
- `backend-ci.yml` (filtro: `apps/backend/**`)
- `admin-web-ci.yml` (filtro: `apps/admin-web/**, packages/api-contracts/**`)
- `medico-pwa-ci.yml` (filtro: `apps/medico-pwa/**, packages/api-contracts/**`)

Não usar Nx ou Turborepo no MVP — complexidade não compensa para este tamanho.

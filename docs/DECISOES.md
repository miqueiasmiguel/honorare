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

### D-030: `Procedimento` é por tenant (sem tabela global)

Cada billing company mantém sua própria tabela de procedimentos. Não há tabela CBHPM global compartilhada no MVP.

**Justificativa:** cada Unimed Singular pode ter versão específica da tabela CBHPM vigente; tenants precisam ativar/desativar códigos para seu fluxo; campos internos customizados são previstos.

**Revisitar:** se aparecer demanda real de tabela global compartilhada entre tenants.

### D-031: `Porte` armazenado como string (não enum)

Campo `Procedimento.Porte` é `string(4)`, não enum.

**Justificativa:** CBHPM tem ~17 portes cirúrgicos; string evita manutenção de enum cada vez que novos portes surgirem. Validação semântica fica no domínio do cálculo, não no banco.

**Revisitar:** nunca — decisão de conveniência sem custo real.

### D-032: `TipoRuleSet` armazenado como string no banco

`.HasConversion<string>()` no EF Core para a coluna `tipo_rule_set`.

**Justificativa:** legibilidade direta no banco sem JOIN; facilita debug e consultas ad-hoc. Enum permanece no código para type safety.

**Revisitar:** nunca.

### D-033: Import CSV com upsert (`ExecuteUpdate` + `AddRange`)

Endpoint `POST /api/v1/admin/procedimentos/importar-csv` faz upsert por `(TenantId, CodigoTuss)`.

**Justificativa:** permite reimportar tabela completa sem perder dados; elimina necessidade de sincronização incremental; D-013 (sem versionamento no MVP).

**Revisitar:** quando houver versionamento de tabelas (F2.3 ou pós-MVP).

### D-034: Hard delete no MVP para Operadora e Procedimento

Sem soft delete nas entidades `Operadora` e `Procedimento`. `ExcluirAsync` é DELETE físico com TODO para bloquear quando `Guia` associada existir (F3.1).

**Justificativa:** sem guias ainda; soft delete adiciona complexidade sem benefício real no MVP.

**Revisitar:** ao implementar F3.1 — o TODO no service deve ser convertido em validação real.

### D-037: `OrdemProcedimento` enum substituído por `PercentualOrdem decimal`

O enum `OrdemProcedimento` (Único/Principal/SecundarioMesmaVia/SecundarioViaDiferente) foi abolido em F3.8. `ItemGuia.PercentualOrdem decimal` armazena o fator diretamente (ex: 1.0, 0.7, 0.5, 0.4). O `OrdemProcedimentoModifier` aplica o valor sem lógica condicional.

**Justificativa:** o CSV analítico da UNIMED tem progressões além do 2º procedimento (100%/70%/50%/40%/30%/20%/10%) e cada Unimed Singular pode ter progressão própria. Enum fixo não representava essa realidade; decimal parametrizável via `TabelaOrdemOperadora` resolve.

**Revisitar:** nunca — a enum não volta. Adicionar novas posições é só adicionar linhas na `TabelaOrdemOperadora`.

### D-035: Separador CSV é `;` (ponto-e-vírgula)

Formato de importação usa `;` em vez de `,`.

**Justificativa:** planilhas Excel e LibreOffice em locale brasileiro exportam `;` por padrão. Usar `,` forçaria conversão manual antes do upload.

**Revisitar:** nunca — é convenção do público-alvo.

### D-036: Telas de Operadora e Procedimento ficam no `admin-web` (não no painel SaaS)

Operadoras e procedimentos são dados de tenant, gerenciados pelo `TenantAdmin`.

**Justificativa:** cada tenant tem sua própria configuração de operadoras e tabela de procedimentos; SaaS admin não precisa gerenciar isso.

**Revisitar:** nunca — decisão de separação de responsabilidades clara.

### D-038: Deflator é pré-requisito obrigatório para criação de guia

Nenhuma guia pode ser criada ou atualizada a menos que **todos os itens sejam calculados com sucesso** pelo motor (`SituacaoApuracao.Calculado`). O motor executa em pré-voo com IDs temporários antes de qualquer persistência. Se qualquer item retornar `SemDeflator`, `SemTabela` ou `Indeterminado`, a operação é rejeitada com mensagem descritiva indicando quantos itens falharam e por qual motivo.

A importação de guias via CSV (`ImportacaoGuiaCsvService`) faz uma verificação antecipada mais leve: valida que existe `DeflatorPrestador` para cada `PosicaoExecutor` distinta presente no arquivo antes de começar o loop de criação. Se algum deflator estiver faltando, o import inteiro é rejeitado com indicação da posição.

**Exceções intencionais:**

- Guias **pacote** (`EhPacote = true`) têm `ValorApurado` preenchido manualmente — a apuração não é invocada.
- Operadoras com `TipoRuleSet.Nulo` são isentas — não têm motor de cálculo.

**Revisitar:** nunca — deflator negociado é dado primário sem o qual a apuração não pode ocorrer.

### D-039: `ACRESCIMO = 0,00%` no CSV UNIMED não é urgência

A coluna `ACRESCIMO` do CSV analítico UNIMED contém `0,00%` na maioria das linhas (sem urgência) e `30,00%` nas linhas com acréscimo de urgência. O campo não é booleano — é percentual. Somente valores **estritamente maiores que zero** ativam o flag `EhUrgencia` no `ItemGuia`.

Detectar urgência pela presença de qualquer string não-vazia causava todas as guias serem marcadas incorretamente como urgência, inflando o `ValorApurado` em 30% indevidamente.

**Revisitar:** se a UNIMED usar outros valores de acréscimo além de 0% e 30%.

### D-040: `POST /recalcular` para sincronização após mudanças de catálogo

O endpoint `POST /api/v1/admin/guias/{id}/recalcular` descarta o `Calculo` existente, zera `ValorApurado` em todos os itens e re-executa o motor. Necessário quando:

- Um deflator ou tabela é adicionado depois que a guia já foi criada (guias legadas de antes do D-038).
- Os valores do catálogo são corrigidos e o cálculo precisa ser atualizado.

O recálculo não altera `ValorLiquidado` (definido via importação CSV ou edição inline — não pelo motor) nem a situação da guia.

**Guias pacote:** recálculo é rejeitado com erro — `ValorApurado` é manual.

**Revisitar:** quando houver recálculo em lote (por prestador/operadora) — hoje é por guia individual.

### D-041: `ValorApurado` pode ser sobrescrito manualmente pelo operador no contexto de recurso

O endpoint `PATCH /api/v1/admin/guias/{id}/itens/{itemId}/valor-apurado` permite que o `TenantAdmin` corrija o `ValorApurado` de um item, sobrepondo o resultado do motor de cálculo.

**Justificativa:** o motor cobre as regras gerais UNIMED, mas casos atípicos (acordos pontuais, codificações especiais, procedimentos sem correspondência exata na tabela) produzem valores que o operador reconhece como incorretos. A correção manual é a válvula de escape documentada — o PDF do recurso reflete o valor corrigido como "VL CORRETO". `ValorApurado = null` limpa o override e reverte o item para "não apurado". Valor negativo ou zero é rejeitado com 422.

**Não usar em massa** — para recalcular por mudança de catálogo, usar `POST /api/v1/admin/guias/{id}/recalcular` (D-040).

**Revisitar:** se houver demanda de audit trail por item (flag `FoiEditadoManualmente`) — hoje não rastreado.

### D-042: Módulo de Demonstrativo e Conciliação removido

O passo de matching `ItemDemonstrativo ↔ ItemGuia` não agrega valor quando a entrada é manual (D-003). `ValorLiquidado` e `MotivoGlosa` são escritos diretamente em `ItemGuia` — via importação CSV ou edição inline. Não reconstruir.

**Revisitar:** nunca — decisão estrutural do MVP.

### D-043: `LocalAtendimento` é texto livre informativo na guia, com backfill que nunca sobrescreve

A `Guia` tem `LocalAtendimento string` (varchar 200, NOT NULL default `''`) — texto livre, **não** um enum e **não** uma entrada do motor de cálculo. A apuração é dirigida por `Acomodacao`; `LocalAtendimento` existe apenas para exibição (listagem, detalhe, e por-guia no recurso e no PDF).

**Backfill sem sobrescrita:** a importação CSV (`ImportacaoGuiaCsvService`, coluna `LOCAL ATENDIMENTO`) só preenche o campo quando a guia ainda está vazia. Uma guia com local já preenchido **nunca** é sobrescrita por reimportação — o valor editado manualmente pelo operador prevalece sobre o CSV.

**Recurso não tem campo próprio:** o `Recurso` não armazena local de atendimento; o detalhe e o PDF exibem o local **de cada guia** incluída. O segmento "Local" só aparece quando o valor não é vazio.

**Revisitar:** se o local de atendimento passar a influenciar regra de cálculo (hoje é puramente informativo) ou se a UNIMED exigir local agregado por recurso.

### D-023: CLAUDE.md em três níveis

- Raiz: regras gerais do monorepo
- Por app: regras específicas (Angular, .NET, PWA)
- Por bounded context (backend): regras específicas de domínio

Knowledge do Project no Claude.ai contém apenas os documentos destilados (`PROJETO.md`, `ARQUITETURA.md`, `DOMINIO.md`, `DECISOES.md`, `PROXIMOS_PASSOS.md`).

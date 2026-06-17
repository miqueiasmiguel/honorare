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

### D-038: Apuração bem-sucedida é pré-requisito obrigatório para criação de guia

> **Emenda (D-044, 2026-06-08):** a parte relativa a deflator foi revertida. `DeflatorPrestador` foi
> removido por completo (sempre 100% na prática) e `SemDeflator` deixou de existir. O motivo de
> rejeição `SemDeflator` e a pré-validação de deflator no import não se aplicam mais. O restante
> desta decisão (rejeição por `SemTabela`/`Indeterminado`, pré-voo, exceções) permanece em vigor.

Nenhuma guia pode ser criada ou atualizada a menos que **todos os itens sejam calculados com sucesso** pelo motor (`SituacaoApuracao.Calculado`). O motor executa em pré-voo com IDs temporários antes de qualquer persistência. Se qualquer item retornar `SemTabela` ou `Indeterminado`, a operação é rejeitada com mensagem descritiva indicando quantos itens falharam e por qual motivo.

**Exceções intencionais:**

- Guias **pacote** (`EhPacote = true`) têm `ValorApurado` preenchido manualmente — a apuração não é invocada.
- Operadoras com `TipoRuleSet.Nulo` são isentas — não têm motor de cálculo.

**Revisitar:** ver D-044 para a remoção do deflator.

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

### D-044: `DeflatorPrestador` removido — `valor_base` é o valor de tabela cheio

A entidade `DeflatorPrestador` (percentual negociado por prestador/operadora/posição, multiplicador sobre o valor de tabela) era **sempre 100%** na prática: todos os 10 cenários E2E ground-truth usavam `100m`, e os únicos valores ≠100 viviam em testes de matemática pura. Como `valor_base = TabelaProcedimento.Valor × 1.0 = TabelaProcedimento.Valor`, a entidade era puro atrito de cadastro sem efeito sobre os valores apurados. **Removida por completo** — entidade, tabela (`DropTable("DeflatoresPrestador")`), DTOs, endpoints CRUD, situação `SemDeflator` e UI associada.

**Emenda a D-038:** o deflator deixa de ser pré-requisito de criação/importação de guia. `SemDeflator` deixa de existir; a pré-validação de deflator no `ImportacaoGuiaCsvService` foi removida. As rejeições por `SemTabela` e `Indeterminado` (e o pré-voo do motor) **permanecem** inalteradas.

**Não confundir** com os descontos de posição de execução auxiliar (1º aux ×0.6 · 2º ×0.4 · 3º ×0.3): esses vivem em `PosicaoExecutorModifier` e **continuam** no motor, intocados.

**Revisitar:** se a UNIMED passar a negociar deflatores por prestador ≠ 100% — reintroduzir como multiplicador sobre `valor_base`.

### D-045: Adicionar item à guia é append-only e apura só o item novo

O operador pode acrescentar um item a uma guia já existente — inclusive uma já `EmRecurso` — pela tela de guias do recurso (`recurso-guias`), via endpoint granular `POST /api/v1/admin/guias/{id}/itens` (`GuiaService.AdicionarItemAsync`). A operação é **append-only**: cria o item, apura **apenas ele** e **nunca** toca nos itens preexistentes nem em seus `ValorLiquidado`/`MotivoGlosa`/`ValorApurado` corrigido manualmente (D-041).

**Por que é seguro apurar só um item:** o motor (`UnimedRuleSet.ApurarItemAsync`) calcula cada item usando **somente os atributos do próprio item + a operadora** — não há contexto cruzado entre itens. Logo, apurar o item novo isoladamente produz o mesmo resultado que apurar a guia inteira, sem efeito colateral sobre os demais.

**Métodos proibidos dentro de `AdicionarItemAsync`** — ambos são destrutivos para uma guia que já carrega dados de recurso:

- `AtualizarAsync` faz `ExecuteDeleteAsync` em todos os `ItensGuia` e os recria do zero → perde `ValorLiquidado`/`MotivoGlosa`.
- `RecalcularAsync` chama `SetValorApurado(null)` em todos os itens → apaga as correções manuais de `ValorApurado` (D-041).

`AdicionarItemAsync` em vez disso adiciona o item, apura-o e **anexa** os `PassoCalculo` ao `Calculo` existente (criando-o se ainda não houver), continuando a numeração de `Sequencia`.

**Semântica de criação herdada de D-038:** item não-precificável em guia não-pacote (`SemTabela`/`Indeterminado`) é rejeitado com `ValidationError`; guia **pacote** (`EhPacote = true`) exige `ValorApurado` manual e não invoca o motor. Por isso `EhPacote` é exposto no `GuiaNoRecursoDto`, para o frontend mostrar o campo de valor manual no modal.

**Revisitar:** se algum dia uma regra UNIMED passar a depender de contexto cruzado entre itens (hoje não existe) — aí adicionar um item exigiria reapurar a guia inteira e esta decisão cairia.

### D-046: Número do recurso é manual (dígitos), pré-preenchido com o mês anterior à emissão (2026-06-09)

O `Recurso.Numero` deixou de ser gerado automaticamente a partir da data de emissão (`DataEmissao.ToString("yyyyMM")`, comportamento original da entrega F4.1) e passou a ser **informado pelo operador**. Regras:

- Campo de **texto somente-dígitos**, `varchar(20)` (antes `varchar(6)` — migration `AumentaRecursoNumeroParaVinteCaracteres`). É **string, não inteiro**, porque zeros à esquerda são significativos e devem ser preservados (ex.: `00042`).
- **Obrigatório**, validado no servidor em `RecursoService.CriarAsync`/`AtualizarAsync` → `ValidationError` → HTTP 422, no mesmo padrão de validação do `CatalogService`. A entidade `Recurso` apenas persiste o valor (trimmed); **não há mais derivação de número no backend**.
- **Pré-preenchido** no `RecursoFormComponent` com o `AAAAMM` do **mês anterior** à data de emissão (ex.: emissão `2026-01-15` → `202512`), acompanhando a data enquanto o operador não editar o campo manualmente. É apenas sugestão de UX — a regra do "mês anterior" vive só no frontend, não no backend.

**Por que mês anterior:** o recurso é montado no mês corrente sobre competências do mês fechado anterior; o número reflete a competência contestada, não a data de emissão.

**Por que validar no servidor e não só no formulário:** o `Numero` compõe o nome do PDF (`RECURSO_{Numero}_{Operadora}.pdf`) enviado à operadora. Um número vazio — ou inventado por um default de backend — seria o oposto de "manual e obrigatório". Por isso a derivação `yyyyMM` foi removida da entidade em vez de mantida como fallback.

**Frontend — armadilha do `[value]` + signal:** o input filtra não-dígitos no handler; quando o usuário digita uma letra após dígitos válidos, o valor saneado é igual ao do signal e `signal.set()` é no-op → o Angular não repinta o DOM e o caractere inválido fica visível. O handler reescreve `input.value` explicitamente para forçar o repaint (primo da regra do `[value]` em `<select>` no CLAUDE.md).

### D-047: `IFileStorage` é a terceira interface especulativa sancionada

A regra geral do CLAUDE.md é "no speculative interfaces". `IFileStorage` é a terceira exceção declarada, junto com `IPricingRuleSet` e `IGatewayPagamento`. Justificativa: a troca de disco local por S3/Supabase é uma necessidade futura concreta (multi-cloud, SaaS escalável) e não hipotética. O custo de abstrair **agora** é mínimo; o custo de refatorar depois (com dados em produção e vários callers dependendo da impl concreta) é alto.

A implementação inicial (`LocalFileStorage`) usa disco local para desenvolvimento e Docker Compose. Em produção, basta trocar o registro no DI (`AddSingleton<IFileStorage, S3FileStorage>()`) sem tocar em nenhum service de domínio.

**Restrições:** o domínio guarda apenas uma **chave opaca** (`LogoKey`) — nunca os bytes. O `content-type` é derivado da extensão da chave. Path traversal é rejeitado na implementação concreta (não no contrato), pois é um detalhe de infra.

### D-048: `IncluidoNoRecurso` — exclusão não-destrutiva de itens de recurso

O operador pode remover um item de uma guia do recurso sem destruí-lo. A flag `ItemGuia.IncluidoNoRecurso bool` (default `true`) controla a visibilidade no PDF e na tela, deixando o item intacto no restante do sistema (faturamento, cálculo, portal do médico). A operação é reversível via `PATCH /api/v1/admin/recursos/{id}/guias/{guiaId}/itens/{itemId}/inclusao` com `{ "incluido": bool }`.

**Invariante:** excluir o último item incluído de uma guia no recurso lança `InvalidOperationException` (→ 409) — uma guia no recurso deve ter ao menos um item visível no PDF.

**Reset automático:** ao remover a guia do recurso (`RemoverGuiaAsync`), `ItemGuia.RemoverDoRecurso()` reseta `IncluidoNoRecurso = true` em todos os itens da guia, sem efeito colateral em outras guias.

**Por que não deletar o item:** o item pode ter `ValorLiquidado`/`MotivoGlosa` preenchidos por importação CSV e pode aparecer no portal do médico. Destruí-lo causaria perda de dados auditáveis. A flag preserva a rastreabilidade enquanto permite controle fino do conteúdo do recurso.

### D-049: `CodigosNaoRecorriveis` + guia mista — comportamento do lote de recurso

O tenant mantém `CodigosNaoRecorriveis List<string>` (`text[]` no Postgres) — lista de códigos TUSS que o cliente não quer ver em recursos automáticos (ex: consultas). Dois flags derivados em `GuiaDto`:

- `NaoRecorrivel = true` — **todos** os itens da guia têm código NR. O lote pula a guia; o `AdicionarGuiaAsync` individual ainda funciona (escape hatch).
- `MistaComNaoRecorriveis = true` — **alguns** (não todos) itens são NR. O lote inclui a guia, mas chama `ExcluirDoRecurso()` nos itens NR automaticamente. O invariante de "ao menos um item incluído" é garantido pela própria definição de guia mista.

Os dois flags são mutuamente exclusivos. A distinção veio em duas etapas: NREC criou `NaoRecorrivel` para "algum item NR"; GMIX refinou para "todos os itens NR" e adicionou `MistaComNaoRecorriveis` — a versão GMIX é o estado atual.

**Por que a granularidade é na guia e não no item:** na prática, consultas são sempre guias isoladas (nenhuma guia mistura consulta com cirurgia nos dados reais do cliente). O caso de guia mista surgiu como exceção descoberta após a entrega de NREC, não como caso planejado.

**Revisitar:** se um tenant configurar códigos que ocorram misturados com procedimentos recorríveis em muitas guias, a exclusão automática de itens NR via lote pode surpreender. A tela já mostra o badge "Contém não recorrível" como sinal visual.

### D-050: Cascata de atos múltiplos unificada por valor decrescente, fixa no motor

A regra de progressão de atos múltiplos deixou de ser entrada (campo `% VIA` do demonstrativo ou configuração por operadora) e passou a ser **calculada automaticamente pelo motor** (`UnimedRuleSet`) a cada apuração.

**Cascata fixa:** 100% / 50% / 40% / 30% / 20% / 10% / 10% (8º procedimento em diante → 10%). Constante única no motor (`_cascata` em `UnimedRuleSet`). Não configurável por operadora.

**Agrupamento:** por `(GuiaId, PosicaoExecutor)`. Cada posição de executor é um grupo de ranking independente — cirurgião concorre só com cirurgião, cada auxiliar com os seus, anestesista entre os atos anestésicos dele.

**Ordenação dentro do grupo:** valor base decrescente (`TabelaProcedimento.Valor` para cirúrgico; valor de referência de `TabelaPorteAnestesico` para anestesista). Desempate: `ProcedimentoId` → `ItemGuiaId` (ascendente), para reprodutibilidade.

**Via de acesso abolida como critério de ranking:** a distinção "mesma via" × "via diferente" deixou de existir. O `% VIA` do demonstrativo é ignorado no cálculo. A `TabelaOrdemOperadora` configurável foi removida por completo (entidade, tabela, endpoints, enum `TipoViaOrdem`).

**Escopo temporal:** vale apenas para novas apurações. Guias já calculadas antes desta mudança não são reprocessadas automaticamente.

**Justificativa:** o cliente informou que a UNIMED adotou a cascata unificada independentemente de via. A configuração por operadora nunca foi utilizada na prática; a manutenção do `% VIA` como entrada causava confusão (o campo do demonstrativo tem semântica de pagamento realizado, não de regra de cálculo). A cascata fixa no motor elimina atrito de configuração e garante que todas as apurações sigam a mesma progressão.

**Revisitar:** se uma Unimed Singular negociar progressão diferente da cascata padrão — reintroduzir configuração por operadora.

### D-023: CLAUDE.md em três níveis

- Raiz: regras gerais do monorepo
- Por app: regras específicas (Angular, .NET, PWA)
- Por bounded context (backend): regras específicas de domínio

Knowledge do Project no Claude.ai contém apenas os documentos destilados (`PROJETO.md`, `ARQUITETURA.md`, `DOMINIO.md`, `DECISOES.md`, `PROXIMOS_PASSOS.md`).

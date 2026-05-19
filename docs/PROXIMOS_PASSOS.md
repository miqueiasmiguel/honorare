# Honorare — Próximos Passos

Backlog ordenado por precedência. Cada fatia entrega algo verificável.

## Fase 0 — Pré-código (não pular)

Trabalho que **não é código** mas é pré-requisito. Prioridade máxima.

### P0.1 — Verificação de marca (responsável: dono) ✅

- [x] INPI (busca.inpi.gov.br) — classes 9, 35, 36, 42 — "Honorare" disponível
- [x] registro.br — domínios disponíveis; compra pendente (em breve)
- [x] Google + lojas de app — sem concorrentes com nome similar

**Resultado:** nome aprovado. Registrar domínio antes de qualquer comunicação pública.

### P0.2 — Casos reais UNIMED (responsável: dono + cliente)

- [ ] Pedir ao cliente 15-20 guias reais UNIMED já pagas (com demonstrativo de pagamento)
- [ ] Documentar cada uma em `docs/test-cases-real.md` com: inputs, valor apresentado, valor pago, glosas
- [ ] Identificar casos especiais entre elas (urgência noturna, apartamento, múltiplos auxiliares, etc.)

**Bloqueante:** sem isso, motor de cálculo é especulação. Não começar a Fatia 4 sem.

### P0.3 — Ambiente local (responsável: dono)

- [ ] .NET 10 SDK instalado
- [ ] Node 20 + pnpm 9 instalados
- [ ] Docker Desktop ou Rancher Desktop funcionando
- [ ] PostgreSQL acessível (via Docker ou nativo)
- [ ] Editor configurado (Rider / VS Code) com extensões C# e Angular

## Fase 1 — Fundação (3-4 semanas)

Trabalho que precede qualquer feature. Cada item é um prompt isolado para Claude Code.

### F1.1 — Esqueleto do monorepo

Estrutura de pastas, configs (`package.json`, `pnpm-workspace.yaml`, `Directory.Build.props`, etc.), READMEs e CLAUDE.mds placeholder. Sem código de aplicação.

**Critério de pronto:** `pnpm install` funciona, `git commit` limpo.

### F1.2 — Projetos Angular

Criar `admin-web` e `medico-pwa` com Angular CLI dentro de `apps/`. PWA do médico com `@angular/service-worker` configurado. Cada um com `<base href>` correto e roteamento básico.

**Critério de pronto:** `ng serve` funciona em ambos, mostrando "Hello world" em rotas distintas.

### F1.3 — Solution .NET com bounded contexts

Criar `Honorare.sln` em `apps/backend/` com:

- Projeto `App` (host)
- Projetos `Identity`, `Catalog`, `Faturamento`, `Reporting` (class libraries)
- Referências corretas (App referencia todos; Reporting → Faturamento → Catalog → Identity)
- Cada um com classe placeholder e `CLAUDE.md` específico

**Critério de pronto:** `dotnet build` limpo, `dotnet run` no App responde em alguma rota.

### F1.4 — Docker Compose com Postgres + backend

`infra/docker-compose.yml` subindo Postgres com banco `honorare_dev`, backend em modo dev, Nginx roteando subpaths. Variáveis em `.env`.

**Critério de pronto:** `pnpm dev:up` sobe stack, `localhost/api/health` responde.

### F1.5 — Endpoint `/api/v1/health` e cliente OpenAPI

Endpoint trivial respondendo `{ status: "ok" }`. Swashbuckle configurado para gerar OpenAPI. Script `tools/generate-api-client.sh` que regenera `@honorare/api-contracts`. Os dois Angular consumindo o cliente gerado.

**Critério de pronto:** ciclo completo: backend muda → script gera → Angular type-check passa.

### F1.6 — PWA instalável

Manifest, ícones, service worker do `medico-pwa` configurados corretamente para subpath `/app/`. Testar instalação real em iPhone e Android (via ngrok ou similar).

**Critério de pronto:** "Adicionar à tela inicial" funciona em iOS e Android, abre em fullscreen.

### F1.7 — Autenticação ponta-a-ponta ✅ (backend + shell Angular concluídos)

ASP.NET Core Identity com **Google OAuth 2.0** como único método. JWT (15 min) + refresh token (7 dias, hash SHA-256). Três roles: `SaasAdmin`, `TenantAdmin`, `Medico`. Middleware bloqueia tenants suspensos. SaaS admin cadastra usuários por e-mail; `GoogleId` é associado no primeiro login.

**Painel SaaS (admin-web):** shell de rotas, guard, página de detalhe de tenant e gerenciamento de usuários concluídos. **Pendente:** página de listagem de tenants (`/saas/tenants`) — cards de resumo (ativos/suspensos/médicos), tabela com todos os tenants e modal de criação de tenant.

## Fase 2 — Cadastros (2-3 semanas)

Domínio começa aqui. Multi-tenant desde já.

### F2.1 — Gerenciamento de usuários (TenantAdmin) ✅

Telas do admin-web para o `TenantAdmin` gerenciar usuários dentro do seu tenant: listar usuários, ativar/desativar médico, editar perfil. Tela "Meu perfil" para o próprio admin.

**Entregues:** `AdminService`, `AdminEndpoints` (`/api/v1/admin/`), campo `ApplicationUser.Nome` (nullable, max 100), migration `AddNomeToApplicationUser`, suite xUnit `AdminServiceTests`, componentes Angular `UserList` e `ProfilePage` com testes Vitest, `adminGuard`, `homeRedirectGuard`, redirecionamento pós-login por role.

**Não inclui:** convite por email (cortado do MVP — SaaS admin cadastra usuários diretamente via painel SaaS implementado em F1.7). Convite por email entra como fase 2 quando houver múltiplos clientes e o fluxo de onboarding virar gargalo.

### F2.2 — Operadoras e procedimentos ✅

**Entregues:** Entidades `Operadora` e `Procedimento` com `ITenantEntity`, enum `TipoRuleSet` (Unimed / Nulo), configurações EF Core, migration `AddCatalogEntities`, projeto `Catalog.Tests` com fixture Testcontainers e suite completa de testes (schema, CRUD, endpoints, CSV import). `CatalogService` com CRUD de `Operadora` e `Procedimento`, importação batch via CSV (upsert por `CodigoTuss`, separador `;`, limite 10 000 linhas, erros linha a linha sem abortar o batch). Endpoints REST em `/api/v1/admin/operadoras` e `/api/v1/admin/procedimentos` com policy `TenantAccess`. Telas Angular no `admin-web`: `OperadoraList`, `OperadoraForm`, `ProcedimentoList`, `ProcedimentoForm` com modal de importação CSV; sidebar atualizado com seção "Cadastros".

### F2.3 — Tabelas e prestadores ✅

**Entregues:** Entidades `Prestador`, `TabelaProcedimento` e `DeflatorPrestador` com `ITenantEntity`, enum `PosicaoExecutor` (Cirurgião, 1ºAux, 2ºAux, 3ºAux, Anestesista, ClínicoAssistente), configurações EF Core, migration `AddTabelasPrestadores`. `CatalogService` estendido com CRUD de `Prestador` (listar/obter/criar/atualizar/excluir) e CRUD de `TabelaProcedimento` com importação CSV (upsert por `(OperadoraId, CodigoTuss)`, separador `;`, limite 10 000 linhas, erros por linha sem abortar o batch) e CRUD de `DeflatorPrestador` (percentual 0–200 por posição). Endpoints REST em `/api/v1/admin/prestadores`, `/api/v1/admin/tabelas` e `/api/v1/admin/prestadores/{id}/deflatores` com policy `TenantAccess`. Telas Angular: `PrestadorList`, `PrestadorForm` (com seção de deflatores inline), `TabelaList` (filtro obrigatório por operadora), `TabelaForm` e `TabelaCsvModalComponent`; sidebar atualizado com "Tabelas de Valores" e "Prestadores".

### F2.4 — Beneficiários ✅

**Entregues:** Entidade `Beneficiario` com `ITenantEntity` (campos: `Id`, `TenantId`, `Carteira` normalizada uppercase, `Nome`, `CriadoEm`), configuração EF Core, migration `AddBeneficiarios`, `DbSet` no `AppDbContext`. `CatalogService` estendido com CRUD completo (`ListarBeneficiariosAsync`, `ObterBeneficiarioPorIdAsync`, `CriarBeneficiarioAsync`, `AtualizarBeneficiarioAsync`, `ExcluirBeneficiarioAsync`) e método `LookupOrCreateAsync` — padrão lazy que busca por carteira normalizada e cria se não encontrar, retornando flag `Criado`. Endpoints REST em `/api/v1/admin/beneficiarios` com policy `TenantAccess` (`GET` lista paginada com filtros, `GET /{id}`, `POST /lookup-or-create` com `201 + Location` na criação, `PUT /{id}`, `DELETE /{id}`). Suites xUnit completas (`BeneficiarioCrudTests`, `BeneficiarioLookupTests`, `BeneficiarioEndpointTests`). Tela Angular `/admin/catalog/beneficiarios` com `BeneficiarioListComponent` (lista paginada, filtro, edição inline por nome, exclusão com confirmação). Componente reutilizável `BeneficiarioAutocompleteComponent` para F3.1: debounce 400 ms, busca por carteira, campo de nome inline para beneficiários novos, badges "Encontrado"/"Novo", emite `BeneficiarioItem | null` via `beneficiarioChange`. Sidebar atualizada com "Beneficiários".

## Fase 3 — Operação (3-4 semanas)

O coração do MVP.

### F3.1 — Entrada manual de guia e controle de pagamentos ✅

**Entregues:** Enums `SituacaoGuia` (Apresentada/Liquidada/EmRecurso), `ViaAcesso`, `OrdemProcedimento`, `Acomodacao` em `App/Faturamento/`; entidades `Guia` (table `guias`, ITenantEntity) e `ItemGuia` (table `itens_guia`, cascade delete); configurações EF Core e migration `AddGuias`; `GuiaService` com CRUD completo (`CriarAsync`, `ListarAsync`, `ObterPorIdAsync`, `AtualizarAsync`, `ExcluirAsync`) — atualização faz replace completo dos itens; endpoints REST em `/api/v1/admin/guias` com policy `TenantAccess`; guards de delete no `CatalogService` (Prestador/Operadora/Procedimento bloqueados quando possuem guias associadas). Camada Angular: `guia.types.ts`, `GuiaService` Angular, `ItemGuiaFormComponent` (autocomplete TUSS, campo `valorApurado` condicional ao `ehPacote`), `GuiaFormComponent` (modo criar/editar, lista de itens inline), `GuiaListComponent` (tabela paginada com color-coding por situação, filtros por prestador/período/situação); rotas `/admin/guias`, `/admin/guias/nova`, `/admin/guias/:id`; sidebar com seção "Faturamento" → "Controle de Pagamentos". Guias tipo Pacote (`EhPacote = true`) têm `ValorApurado` informado manualmente — motor de cálculo não é invocado nesta fase. Suites de testes backend (`GuiaSchemaTests`, `GuiaCrudTests`, `GuiaListTests`, `GuiaEndpointTests`) e Angular (`guia.service.spec`, `item-guia-form.spec`, `guia-form.spec`, `guia-list.spec`, `faturamento.routes.spec`) cobrindo 29 casos.

### F3.2 — Motor de cálculo (UNIMED) ✅

**Entregues:** Interface `IPricingRuleSet` e types `ApurarGuiaContext`/`ApuracaoItemResult`/`PassoApuracao`/`SituacaoApuracao`; `NullRuleSet` (retorna `Indeterminado` para todos os itens — operadoras sem tabela UNIMED); `UnimedRuleSet` com pipeline de 6 passos (ValorBase → OrdemProcedimento → Videolaparoscopia → Acomodacao → Urgencia → PosicaoExecutor); modifiers estáticos em `App/Faturamento/Calculo/Unimed/Modifiers/`; entidades `Calculo` + `PassoCalculo` com migration `AddCalculo` (trace completo por guia); `PricingRuleSetFactory`; `GuiaService` integrado — motor invocado ao criar/atualizar guias não-pacote, `Calculo` anterior excluído no recálculo via cascade delete. Anestesista retorna `SituacaoApuracao.Indeterminado` aguardando F3.3. 10 cenários E2E passando em `UnimedPipelineTests`.

**Visualização da apuração (entregues):** Endpoint `GET /api/v1/admin/guias/{id}/calculo` retornando `GuiaCalculoDto` com lista de `ItemCalculoDto` (situação, `ValorApurado`, passos de cálculo `PassoCalculoDto`); `CalculoDetalheComponent` Angular com accordion por item — header com badge de situação e valor apurado, body com tabela Regra/Fator/Valor Resultante (fallback "Sem detalhes de cálculo" quando sem passos); seção "Apuração" no `GuiaFormComponent` em modo edição, carregada via `obterCalculo()` com tolerância a erro. Situações possíveis por item: `Calculado`, `SemTabela`, `SemDeflator`, `Indeterminado`, `Pacote`.

**Pendente:** validar os percentuais do pipeline contra os 15-20 casos reais (P0.2) antes de seguir para F3.3.

### F3.3 — Anestesia ✅

**Entregues:** `TempoAnestesicoMin int?` em `ItemGuia` com migration `AddTempoAnestesicoMin`; `AnestesiaCalculator` (puro, sem DB) com pipeline de 6 passos (ValorBase → UnimedAN×1,1719 → OrdemProcedimento → Acomodacao → Urgencia → TempoExtra) e tabela `TempoBasePorPorte` por porte 1–8; integração em `UnimedRuleSet.ApurarAnestesistaAsync` — early-exit para `SemTabela`/`SemDeflator`/`Indeterminado` (PorteAnestesico nulo), retorna `Calculado` com trace completo; `ApurarItemInput` atualizado com `TempoAnestesicoMin`; 8 cenários E2E passando em `UnimedAnestesiaPipelineTests`. Campo `tempoAnestesicoMin` no `ItemGuiaFormComponent` Angular com signal, visível apenas quando `posicaoExecutor === 'Anestesista'`, label "Tempo anestésico (min)", populado ao carregar item existente; `guia.types.ts` atualizado com `tempoAnestesicoMin?: number | null`; 5 casos de teste Vitest cobrindo visibilidade condicional, emissão e carga de item existente.

**Pendente:** validar percentuais do pipeline contra casos reais (P0.2).

### F3.4 ✅ — Demonstrativos e conciliação manual

**Entregues:** Entidades `Demonstrativo` (ITenantEntity, FK `OperadoraId` Restrict) e `ItemDemonstrativo` (cascade delete em `DemonstrativoId`, FK `ItemGuiaId` Restrict) com configurações EF Core e migration `AddDemonstrativo`. `ItemGuia` ganhou `SetValorLiquidado(decimal?)` e `Guia` ganhou `Liquidar()` / `ReverterParaApresentada()`. `DemonstrativoService` com CRUD completo (`CriarAsync`, `ListarAsync`, `ObterPorIdAsync`, `AtualizarAsync`, `ExcluirAsync`, `AdicionarItemAsync`, `RemoverItemAsync`) mais `ConciliarItemAsync` e `DesconciliarItemAsync`. Guards de integridade: excluir demonstrativo com item conciliado ou remover item conciliado lança 409. Auto-liquidação: ao conciliar, se todos os `ItemGuia` da guia ficarem com `ValorLiquidado IS NOT NULL`, a guia avança automaticamente para `Liquidada`; reversão automática ao desconciliar. Re-conciliação desconcilia o vínculo anterior antes de vincular ao novo. Endpoints REST em `/api/v1/admin/demonstrativos` (`POST`, `GET` lista paginada por operadora/competência, `GET /{id}` com itens, `PUT /{id}`, `DELETE /{id}`, `POST /{id}/itens`, `DELETE /{id}/itens/{itemId}`, `POST /{id}/itens/{itemId}/conciliar`, `DELETE /{id}/itens/{itemId}/conciliar`). `InvalidOperationException` mapeada para HTTP 409 no exception handler global. Suites xUnit (`DemonstrativoSchemaTests`, `DemonstrativoCrudTests`, `ConciliacaoTests`) cobrindo 18 casos. Telas Angular: `DemonstrativoListComponent` (tabela paginada, filtros por operadora e competência, badge de conciliados, botão excluir condicional), `DemonstrativoFormComponent` (criar/editar header + lista inline de itens com `valorGlosado` calculado no template, remoção bloqueada para itens conciliados), `ConciliacaoComponent` (painel de itens com badge Conciliado/Pendente, busca de guias por senha, vincular/desvincular inline, progresso "X de Y conciliados"); sidebar atualizado com "Demonstrativos" em "Faturamento".

### F3.5 ✅ — Geração de recurso (PDF)

**Entregues:** Entidade `Recurso` (ITenantEntity, FKs `OperadoraId` e `PrestadorId` Restrict) com número automático `Numero = DataEmissao.ToString("yyyyMM")` (informativo, não único), configuração EF Core e migration `AddRecurso`. `Guia` recebeu `RecursoId Guid?` e métodos `MarcarEmRecurso(Guid)` / `RemoverDoRecurso(bool)` — reversão automática para `Liquidada` ou `Apresentada` conforme presença de `ValorLiquidado`. `RecursoService` com CRUD completo (`CriarAsync`, `ListarAsync`, `ObterPorIdAsync`, `AtualizarAsync`, `ExcluirAsync`), `AdicionarGuiaAsync` (409 se guia já está em outro recurso), `RemoverGuiaAsync` e `ObterDadosPdfAsync` (JOIN completo Recurso → Operadora, Prestador, Tenant, Guias → Beneficiário, ItemGuia → Procedimento, Cálculo/PassoCalculo). `RecursoPdfDocument` (QuestPDF Community, `IDocument`) gerando PDF estruturado: cabeçalho com logo e nome do tenant; título `[Prestador] - CRM [registro] - RECURSO [Operadora] [AAAAMM]`; por guia — linha de resumo (data, senha, carteira, paciente, executor), tabela de itens com colunas Cód. TUSS / Descrição / % / PAGO / CORRETO, subtotais **RESTA PAGAR** em negrito, observação em vermelho (`#CC0000`); totais finais ao final do documento. Fator efetivo por item = produto dos `PassoCalculo.Fator` excluindo `ValorBase` (exibe "—" para pacotes ou sem passos). Endpoints REST em `/api/v1/admin/recursos` (`POST`, `GET` lista paginada, `GET /{id}` com guias, `PUT /{id}`, `DELETE /{id}` com guard 409, `POST /{id}/guias`, `DELETE /{id}/guias/{guiaId}`, `GET /{id}/pdf` retorna `application/pdf`). Suites xUnit (`RecursoSchemaTests`, `RecursoCrudTests`, `RecursoPdfDataTests`) cobrindo 20 casos. Telas Angular: `RecursoListComponent` (tabela paginada, filtros por operadora e prestador com debounce 400 ms, badge de guias, botão "Gerenciar guias", botão "PDF", botão excluir), `RecursoFormComponent` (criar/editar, número calculado no template), `RecursoGuiasComponent` (guias vinculadas com remoção, busca inline por senha filtrada por `Apresentada/Liquidada`, adição via POST, download de PDF via blob URL); sidebar atualizado com "Recursos" em "Faturamento".

## Fase 4 — Visualização (2-3 semanas)

### F4.1 — Portal do médico (PWA)

Lista de guias pendentes onde o médico é executor (guias já baixadas não aparecem). Filtros (período, operadora). Detalhe da guia com observação do admin em destaque — é o que permite ao médico entender o status de cada paciente (não pago, pago parcial, motivo).

Resumo financeiro (total apresentado vs. pago vs. em aberto) é fase 2 — no MVP o médico precisa ver o status e a justificativa, não necessariamente os totais.

**Cuidado:** garantir filtro automático por executor via global query filter. Não confiar em filtros manuais por endpoint.

**Spec:** [`docs/SPEC-F4.1.md`](SPEC-F4.1.md) — 3 tasks (M-01 backend, M-02 GuiaList, M-03 GuiaDetalhe). ✅

### F4.2 — Tela admin com auditoria de divergências

Lista de divergências classificadas por severidade. Detalhe de cada uma com explicação (do trace). Botão "marcar como contestada".

### F4.3 — Suspensão de tenant (inadimplência manual)

Campo `Tenant.Status` (Ativo, Suspenso, Cancelado). Tela admin para alterar. Middleware bloqueia acesso quando suspenso. Mensagem amigável.

**Estimativa:** 3-5 dias. Não deixar para depois do segundo cliente — implementar antes.

## Fase 5 — Relatórios (1-2 semanas)

### F5.1 — Relatório por médico (período)

Total apresentado, pago, em aberto. Detalhamento. Exportação CSV/PDF.

### F5.2 — Relatório por operadora

Mesma coisa, agrupado por operadora.

### F5.3 — Relatório de divergências

Para o admin, lista priorizada de glosas com potencial de contestação.

## Fase 6 — Pré-produção (2 semanas)

### F6.1 — Logs de acesso (LGPD)

`LogAcesso` para eventos relevantes (login, exportação, alteração de permissão). Não logar tudo — só o que importa para responder "quem viu o quê".

### F6.2 — Backup e monitoramento

Backup automático do Postgres. Application Insights / Seq para logs de aplicação. Alertas básicos (erro 500, latência alta).

### F6.3 — Documentação para o cliente

Manual de uso, FAQ. Vídeos curtos dos fluxos principais.

### F6.4 — Testes E2E dos fluxos críticos

Playwright ou Cypress para fluxos críticos:

- Admin cria guia, cálculo aparece correto
- Médico vê suas guias e não vê de outros
- Conciliação detecta divergência

## Cortes possíveis

Se prazo apertar, cortar nesta ordem:

1. F5 (relatórios agregados) — o recurso (F3.5) já entrega o essencial; relatórios analíticos ficam para depois
2. F4.1 (PWA do médico) — médico acessa pelo admin web responsivo, PWA fica para fase 2
3. F3.4 (conciliação automática) — só registra demonstrativo, não bate com guia automaticamente
4. F2.4 (beneficiários como entidade) — campo de texto livre na guia

**Não cortar:** F0, F1.7 (auth), F2.2/F2.3 (operadoras/procedimentos/tabelas), F3.1/F3.2 (guia + cálculo), **F3.5 (geração de recurso)**, F4.3 (suspensão).

O motor de cálculo (F3.2) **não é cortável** — é o que produz o VL CORRETO que aparece no recurso e justifica a contestação perante a operadora. Sem ele o sistema é só uma planilha com PDF.

## Estimativa total

- Fase 0: 1 semana (em paralelo com F1)
- Fase 1: 3-4 semanas
- Fase 2: 2-3 semanas
- Fase 3: 3-4 semanas
- Fase 4: 2-3 semanas
- Fase 5: 1-2 semanas
- Fase 6: 2 semanas

**MVP completo: 12-16 semanas focadas com Claude Code ajudando.**
**MVP enxuto (sem PWA + sem relatórios bonitos): 8-10 semanas.**

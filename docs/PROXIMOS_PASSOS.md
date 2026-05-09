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

### F3.4 — Demonstrativos e conciliação manual

Entrada manual de demonstrativo. Conciliação item-a-item com botão "essa linha é esta guia". Registra `ValorLiquidado` (PG UNIMED) por item. Status da guia atualizado. Quando o convênio pagar integralmente, admin baixa a guia — ela sai do relatório de pendências.

### F3.5 — Geração de recurso (PDF)

Feature central do produto. O admin cria um `Recurso` (recebe número automático no formato `AAAAMM`), seleciona as guias em divergência e gera o PDF. As guias incluídas têm situação atualizada para `EmRecurso` com referência ao número.

Estrutura do PDF:

- Cabeçalho: logo + nome da billing company (por tenant)
- Título: `[Nome do médico] - CRM [número] - RECURSO [OPERADORA] [AAAAMM]`
- Por guia: data, senha, carteira do beneficiário, nome do paciente, papel do executor (ex: CIRURGIÃO)
- Por item da guia: código TUSS + descrição + % aplicado, **PAGO**, **CORRETO**
- Subtotais por guia; **RESTA PAGAR** em destaque (= `sum(CORRETO) − sum(PAGO)`)
- Observação em vermelho (campo `Observacao` da guia)

Labels das colunas: usar **PAGO** e **CORRETO** como padrão (não "PG UNIMED"/"VL CORRETO" — labels variam entre documentos do cliente; a forma curta é mais limpa e não amarra na operadora).

Semântica dos valores:

- **VL CORRETO** por item = `ValorApurado` (o que o motor diz que deveria ser pago; para pacotes, valor informado manualmente)
- **PG UNIMED** por item = `ValorLiquidado` (o que o demonstrativo registrou)
- **RESTA PAGAR** por guia = `sum(ValorApurado) − sum(ValorLiquidado)`

O PDF é o documento que o admin envia à operadora para contestar. Sem esta feature o sistema não substitui a planilha atual.

**Critério de pronto:** PDF gerado é indistinguível (em informação) do documento que o cliente monta hoje manualmente.

## Fase 4 — Visualização (2-3 semanas)

### F4.1 — Portal do médico (PWA)

Lista de guias pendentes onde o médico é executor (guias já baixadas não aparecem). Filtros (período, operadora). Detalhe da guia com observação do admin em destaque — é o que permite ao médico entender o status de cada paciente (não pago, pago parcial, motivo).

Resumo financeiro (total apresentado vs. pago vs. em aberto) é fase 2 — no MVP o médico precisa ver o status e a justificativa, não necessariamente os totais.

**Cuidado:** garantir filtro automático por executor via global query filter. Não confiar em filtros manuais por endpoint.

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

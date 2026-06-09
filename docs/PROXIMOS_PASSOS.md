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

### F2.5 — Acesso do prestador ao portal ✅

**Entregues:** Campo `EmailAcesso string?` em `Prestador` (imutável após definido via `SetEmailAcesso()`), migration `AddEmailAcessoPrestador` (`email_acesso varchar(256) NULL`). `CatalogService` atualizado com `CriarPrestadorCommand` e `AtualizarPrestadorCommand` separados: ao criar prestador com e-mail, `ApplicationUser` com `MedicoId = Prestador.Id` e role `Medico` é criado atomicamente na mesma transação (409 se e-mail duplicado); desativar/reativar prestador propaga para o usuário vinculado; excluir prestador remove o usuário — exceto se `GoogleId` já estiver associado (médico fez login ao menos uma vez), caso em que a exclusão retorna 409 para preservar auditabilidade. `PrestadorDto` atualizado com `EmailAcesso` e `TemUsuario`. Suite xUnit com 11 novos casos em `PrestadorCrudTests`. Tela Angular `PrestadorFormComponent` com campo e-mail editável somente em criação e exibição somente-leitura com badge "Com acesso ao portal" em edição; `PrestadorListComponent` com coluna/badge de acesso.

**Spec:** `docs/SPEC-F2.4.md`

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

**⚠ Superseded por F3.6:** o `AnestesiaCalculator` foi reescrito sem `UnimedAN×1,1719`, sem `Acomodacao` (já vem aplicada na seleção da `TabelaPorteAnestesico`) e sem `TempoExtra`. `TempoBasePorPorte` deixou de existir; `ItemGuia.TempoAnestesicoMin` permanece em DB como campo ignorado. Ver F3.6 abaixo.

### F3.4 ✅ — Demonstrativos e conciliação manual

**⚠ Superseded por RC-09:** entidades `Demonstrativo`/`ItemDemonstrativo` e todos os endpoints `/demonstrativos` foram removidos. `ValorLiquidado` e `MotivoGlosa` são escritos diretamente em `ItemGuia` — via `ImportacaoGuiaCsvService` ou edição inline. Ver D-042 em `DECISOES.md`.

**Entregues (histórico):** Entidades `Demonstrativo` (ITenantEntity, FK `OperadoraId` Restrict) e `ItemDemonstrativo` (cascade delete em `DemonstrativoId`, FK `ItemGuiaId` Restrict) com configurações EF Core e migration `AddDemonstrativo`. `ItemGuia` ganhou `SetValorLiquidado(decimal?)` e `Guia` ganhou `Liquidar()` / `ReverterParaApresentada()`. `DemonstrativoService` com CRUD completo (`CriarAsync`, `ListarAsync`, `ObterPorIdAsync`, `AtualizarAsync`, `ExcluirAsync`, `AdicionarItemAsync`, `RemoverItemAsync`) mais `ConciliarItemAsync` e `DesconciliarItemAsync`. Guards de integridade: excluir demonstrativo com item conciliado ou remover item conciliado lança 409. Auto-liquidação: ao conciliar, se todos os `ItemGuia` da guia ficarem com `ValorLiquidado IS NOT NULL`, a guia avança automaticamente para `Liquidada`; reversão automática ao desconciliar. Re-conciliação desconcilia o vínculo anterior antes de vincular ao novo. Endpoints REST em `/api/v1/admin/demonstrativos` (`POST`, `GET` lista paginada por operadora/competência, `GET /{id}` com itens, `PUT /{id}`, `DELETE /{id}`, `POST /{id}/itens`, `DELETE /{id}/itens/{itemId}`, `POST /{id}/itens/{itemId}/conciliar`, `DELETE /{id}/itens/{itemId}/conciliar`). `InvalidOperationException` mapeada para HTTP 409 no exception handler global. Suites xUnit (`DemonstrativoSchemaTests`, `DemonstrativoCrudTests`, `ConciliacaoTests`) cobrindo 18 casos. Telas Angular: `DemonstrativoListComponent` (tabela paginada, filtros por operadora e competência, badge de conciliados, botão excluir condicional), `DemonstrativoFormComponent` (criar/editar header + lista inline de itens com `valorGlosado` calculado no template, remoção bloqueada para itens conciliados), `ConciliacaoComponent` (painel de itens com badge Conciliado/Pendente, busca de guias por senha, vincular/desvincular inline, progresso "X de Y conciliados"); sidebar atualizado com "Demonstrativos" em "Faturamento".

### F3.5 ✅ — Geração de recurso (PDF)

**Entregues:** Entidade `Recurso` (ITenantEntity, FKs `OperadoraId` e `PrestadorId` Restrict) com número automático `Numero = DataEmissao.ToString("yyyyMM")` (informativo, não único), configuração EF Core e migration `AddRecurso`. `Guia` recebeu `RecursoId Guid?` e métodos `MarcarEmRecurso(Guid)` / `RemoverDoRecurso(bool)` — reversão automática para `Liquidada` ou `Apresentada` conforme presença de `ValorLiquidado`. `RecursoService` com CRUD completo (`CriarAsync`, `ListarAsync`, `ObterPorIdAsync`, `AtualizarAsync`, `ExcluirAsync`), `AdicionarGuiaAsync` (409 se guia já está em outro recurso), `RemoverGuiaAsync` e `ObterDadosPdfAsync` (JOIN completo Recurso → Operadora, Prestador, Tenant, Guias → Beneficiário, ItemGuia → Procedimento, Cálculo/PassoCalculo). `RecursoPdfDocument` (QuestPDF Community, `IDocument`) gerando PDF estruturado: cabeçalho com logo e nome do tenant; título `[Prestador] - CRM [registro] - RECURSO [Operadora] [AAAAMM]`; por guia — linha de resumo (data, senha, carteira, paciente, executor), tabela de itens com colunas Cód. TUSS / Descrição / % / PAGO / CORRETO, subtotais **RESTA PAGAR** em negrito, observação em vermelho (`#CC0000`); totais finais ao final do documento. Fator efetivo por item = produto dos `PassoCalculo.Fator` excluindo `ValorBase` (exibe "—" para pacotes ou sem passos). Endpoints REST em `/api/v1/admin/recursos` (`POST`, `GET` lista paginada, `GET /{id}` com guias, `PUT /{id}`, `DELETE /{id}` com guard 409, `POST /{id}/guias`, `DELETE /{id}/guias/{guiaId}`, `GET /{id}/pdf` retorna `application/pdf`). Suites xUnit (`RecursoSchemaTests`, `RecursoCrudTests`, `RecursoPdfDataTests`) cobrindo 20 casos. Telas Angular: `RecursoListComponent` (tabela paginada, filtros por operadora e prestador com debounce 400 ms, badge de guias, botão "Gerenciar guias", botão "PDF", botão excluir), `RecursoFormComponent` (criar/editar, número calculado no template), `RecursoGuiasComponent` (guias vinculadas com remoção, busca inline por senha filtrada por `Apresentada/Liquidada`, adição via POST, download de PDF via blob URL); sidebar atualizado com "Recursos" em "Faturamento".

**⚠ Emenda (D-046):** o número do recurso deixou de ser automático (`DataEmissao.ToString("yyyyMM")`) e passou a ser **manual** — campo somente-dígitos, `varchar(20)`, obrigatório e validado no servidor, pré-preenchido no formulário com o `AAAAMM` do mês anterior à data de emissão. Migration `AumentaRecursoNumeroParaVinteCaracteres`. Ver D-046 em `DECISOES.md`.

### F3.6 — Porte Anestésico por Letra (UNIMED) + dobra por posição

**Entregues (PA-01 a PA-09):** Nova entidade `TabelaPorteAnestesico` (ITenantEntity, unique `(TenantId, OperadoraId, PorteLetra)`) com par fixo `(ValorEnfermaria, ValorApartamento)` e opcional `ValorAmbulatorial`; migration `AddTabelaPorteAnestesico`; importação CSV no formato UNIMED JPA (separador vírgula, 8 linhas de header, decimal com vírgula entre aspas) via `CatalogService.ImportarTabelaUnimedAnestesistaAsync`; endpoints `POST /api/v1/admin/tabelas-porte-anestesico/importar-unimed-csv` e `GET /api/v1/admin/tabelas-porte-anestesico` com policy `TenantAccess`. `Procedimento.PorteAnestesico` migrado de `int?` (1–8) para `string?` (varchar(2), regex `^[A-NP-Z]$` — A–Z exceto O). `AnestesiaCalculator` reescrito sem `UnimedAN×1,1719` e sem `TempoExtra`: pipeline `ValorBase → OrdemProcedimento → Urgencia` consumindo `valorReferencia` selecionado por acomodação (Apartamento → `ValorApartamento`; Ambulatorial → `ValorAmbulatorial ?? ValorEnfermaria`; demais → `ValorEnfermaria`); early-exits `Indeterminado` / `SemTabela` / `SemDeflator` em `UnimedRuleSet.ApurarAnestesistaAsync`. `AcomodacaoModifier` agora recebe `PosicaoExecutor` e aplica `×2.0` **apenas para `Cirurgiao` em Apartamento** — auxiliares, clínico assistente e anestesista recebem `×1.0` (corrige bug onde 1º auxiliar em apto recebia 1,2× o valor correto). Frontend admin-web: signal `porteAnestesico` em `procedimento-form` migrado para `string` com input texto maxlength 1 + regex; novo modal `tabela-porte-anestesico-csv-modal` integrado em `tabela-list` (botão "Importar Tabela Anestesista" condicional à operadora selecionada). Cobertura ≥ 80% nos dois lados.

**Pendente (PA-10):** verificação end-to-end com os 25 portes UNIMED JPA reais — smoke test manual: importar CSV → conferir lista → criar guia com anestesista TUSS `30101050` em Enfermaria (`ValorApurado == 292.50`) e Apartamento (`ValorApurado == 468.00`).

**Spec:** `docs/SPEC-F3.6.md`

### F3.8 ✅ — PercentualOrdem parametrizável + importação CSV analítico UNIMED

**⚠ Parte superseded por RC-09:** `ImportacaoDemonstrativoService` renomeado para `ImportacaoGuiaCsvService`; criação de `ItemDemonstrativo` removida; endpoint migrado de `/demonstrativos/importar-csv` para `/guias/importar-csv`. A lógica de parse CSV, find-or-create de guias/itens e motor de cálculo permanece intacta.

**Entregues (IO-01 a IO-05):** Enum `OrdemProcedimento` abolido — `ItemGuia.PercentualOrdem decimal` (0.01–1.00) substituiu com migration `RenameOrdemProcedimentoToPercentualOrdem`; `OrdemProcedimentoModifier` reescrito para aplicar o decimal diretamente, sem switch/case; `UnimedPipelineTests` atualizados. Nova entidade `TabelaOrdemOperadora` com CRUD completo (`SalvarTabelaOrdemAsync`, `ListarTabelaOrdemAsync`, `ExcluirTabelaOrdemAsync`, `ResolverPercentualOrdemAsync`) e defaults embutidos (MesmaVia 100%/50%/40%/30%/20%/10%; ViaDiferente 100%/70%/50%/40%/30%/10%); endpoints `GET/PUT/DELETE /api/v1/admin/operadoras/{id}/tabela-ordem`; migration `AddTabelaOrdemOperadora`; suite xUnit `TabelaOrdemOperadoraTests` cobrindo 6 casos. `Guia.NumeroGuia string?` com migration `AddNumeroGuia`. `ImportacaoDemonstrativoService` com parse do CSV analítico UNIMED, upsert de `Guia + ItemGuia + ItemDemonstrativo`, motor invocado ao final, auto-liquidação verificada; endpoint `POST /api/v1/admin/demonstrativos/importar-csv` com `somenteValidar=true` para preview; suite xUnit cobrindo 10 casos (anestesista, múltiplos itens, glosa, beneficiário novo, erro por linha, upsert de guia existente, item de equipamento ignorado, urgência, formato inválido, somenteValidar). Frontend: seção "Tabela de Atos Múltiplos" no `OperadoraFormComponent`; dropdown de ordem no `ItemGuiaFormComponent` alimentado pela tabela da operadora; `ImportarDemonstrativoModalComponent` com fluxo 2 passos (validar → confirmar), lista de erros/alertas por linha, integrado ao `DemonstrativoListComponent`.

**Spec:** `docs/SPEC-F3.8.md`

### F3.9 ✅ — Recurso: guias candidatas com filtros e adição em lote

**Entregues (RC-01 a RC-04):** `ListarGuias` estendido com filtros `OperadoraId`, `Senha`, `Beneficiario`, `SemRecurso` e `SomenteComGlosa` (`SomenteComGlosa = true` retorna apenas guias com `ValorApurado > ValorLiquidado` em algum item); 6 novos casos em `GuiaListTests`. Novo endpoint `POST /api/v1/admin/recursos/{id}/guias/lote` com `AdicionarGuiasEmLoteCommand` — aplica os mesmos filtros server-side, fixa `RecursoId == null` (ignora guias já vinculadas a qualquer recurso), retorna `{ adicionadas: N }`; 4 novos casos em `RecursoCrudTests`. Bug corrigido no frontend: `adicionarGuia` usava `POST .../guias` com body `{ guiaId }` — corrigido para `POST .../guias/{guiaId}` conforme rota do backend. `RecursoGuiasComponent` refatorado com painel de filtros pré-fixado ao prestador/operadora do recurso, tabela de candidatas carregada on-demand (hint quando filtro não aplicado), botão "Adicionar todas" usando endpoint de lote server-side.

**Spec:** `docs/SPEC-F3.9.md`

### F3.10 ✅ — Recurso: edição inline de observação e valor apurado

**Entregues (RC-05 a RC-08):** `GuiaNoRecursoDto` expandido com `Observacao string?` e `Itens IReadOnlyList<ItemGuiaNoRecursoDto>` (substitui `TotalItens`); `ItemGuiaNoRecursoDto` expõe `CodigoTuss`, `DescricaoProcedimento`, `PosicaoExecutor`, `PercentualOrdem`, `ValorApurado`, `ValorLiquidado`. Endpoint `PATCH /api/v1/admin/guias/{id}/observacao` persiste texto livre até 2000 chars (string vazia = limpar). Endpoint `PATCH /api/v1/admin/guias/{id}/itens/{itemId}/valor-apurado` permite override manual do `ValorApurado` (null = limpar; valor > 0 exigido; `itemId` de guia diferente → 404); 3 casos em `GuiaCrudTests`. Frontend: linhas de guia vinculada expansíveis no `RecursoGuiasComponent` — textarea de observação com botão "Salvar observação", input numérico por item salvo ao `blur`/Enter, coluna GLOSA calculada no template (`ValorApurado − ValorLiquidado`); atualização do signal local sem reload da página; 2 novos métodos em `GuiaService` com cobertura via `guia.service.spec`.

**Spec:** `docs/SPEC-F3.10.md`

### RC-09 ✅ — Remover módulo de demonstrativos + importação CSV direta em ItemGuia

**Entregues:** Entidades `Demonstrativo` e `ItemDemonstrativo` removidas; migration `RemoveDemonstrativoAddMotivoGlosa` (DropTable + AddColumn `motivo_glosa`). `ItemGuia` recebeu `MotivoGlosa string?` com `SetMotivoGlosa()`. `ImportacaoDemonstrativoService` → `ImportacaoGuiaCsvService` — escreve `ValorLiquidado` e `MotivoGlosa` diretamente em `ItemGuia` sem staging. Endpoint `POST /api/v1/admin/guias/importar-csv` (migrado de `/demonstrativos/importar-csv`). Endpoint `PATCH /api/v1/admin/guias/{id}/itens/{itemId}/pagamento` para edição manual de `ValorLiquidado`/`MotivoGlosa`. Frontend: modal `importar-csv-modal` integrado ao `guia-list`; edição inline `valorLiquidado`/`motivoGlosa` no `guia-form` com auto-liquidação e reversão de situação; tipos migrados para `guia.types.ts`; cliente TypeScript regenerado.

**Decisão registrada:** D-042 (`DECISOES.md`).

**Spec:** `docs/SPEC-RC-09-remover-demonstrativos.md`

### F3.7 — Unificar Procedimentos × Tabelas de Valores ✅

**Entregues (RP-01 a RP-07):** Backend: 3 endpoints em `/api/v1/admin/procedimentos/{procId}/valores` (GET lista todas operadoras ativas com valor nullable, PUT upsert com 201/200, DELETE idempotente com 204) e `DELETE /api/v1/admin/tabelas-porte-anestesico/{id}`; suite xUnit `ProcedimentoValoresEndpointsTests` cobrindo 9 casos (operadora inativa, upsert sem duplicar, valor negativo 422, tenant isolation 404). Frontend: modal unificado `importar-modal` com 3 tipos de importação (Procedimentos TUSS / Valores por Operadora / Tabela de Porte Anestésico), seleção de operadora condicional ao tipo, bloco de formato esperado dinâmico e botão "Baixar arquivo de exemplo" (blob CSV), spinner e resultado com contadores; sub-componente `valores-operadora.component` com tabela editável inline no detalhe do procedimento (Definir/Editar inline com input + OK/Cancelar, [X] com `window.confirm`, validação `valor > 0`, formatação `Intl.NumberFormat pt-BR BRL`, renderiza apenas em modo edição); seção "Porte Anestésico (UNIMED)" no detalhe da operadora via sub-componente `portes-anestesicos.component` (read-only, visível apenas quando `tipoRuleSet === 'Unimed'`, [X] com DELETE + confirmação); `procedimento-list` simplificado — botão único "Importar dados" abre o modal, removidos `onFileChange`, `onArquivoSelecionado`, `downloadTemplate` e input file inline. Remoção completa: item "Tabelas de Valores" do sidebar, rota `/tabelas` substituída por redirect para `/procedimentos`, pastas `tabela-list/`, `tabela-form/`, `tabela-csv-modal/` e `tabela-porte-anestesico-csv-modal/` excluídas. Cobertura ≥ 80% em todos os novos componentes e sub-componentes.

**Spec:** `docs/SPEC-REFACTOR-PROCEDIMENTOS.md`

### LA — Local de atendimento na guia ✅

**Entregues (LA-01 a LA-05):** `Guia.LocalAtendimento string` (varchar 200, NOT NULL default `''`, guardado com `.Trim()`) com migration `AddLocalAtendimentoGuia`; commands/DTOs/endpoints de guia e projeções `ListarAsync`/`ObterDetalheDtoInternalAsync` propagam o campo; `Guia.AtualizarLocalAtendimento`. Importação CSV (`ImportacaoGuiaCsvService`) lê a coluna `LOCAL ATENDIMENTO` e faz backfill **sem sobrescrever** guia já preenchida. Recurso: `GuiaNoRecursoDto` e `GuiaPdfData` expõem `LocalAtendimento` (`ObterPorIdAsync`/`ObterDadosPdfAsync`); `RecursoPdfDocument.ComposeGuia` acrescenta o segmento "Local" por-guia apenas quando não vazio (Recurso não tem campo próprio). Frontend admin-web: campo no `guia-form`, coluna "Local" no `guia-list`, exibição por-guia no `recurso-guias`; tipos locais atualizados e cliente regenerado.

**Decisão registrada:** D-043 (`DECISOES.md`).

**Spec:** `docs/SPEC-local-atendimento-guia.md`

### ADDITEM — Adicionar item à guia pela tela do recurso ✅

**Entregues (TASK-ADDITEM-01 a 05):** Backend: `GuiaService.AdicionarItemAsync` (append-only — cria o item, apura **só** ele e anexa os `PassoCalculo` ao `Calculo` existente; nunca usa `AtualizarAsync`/`RecalcularAsync`) exposto em `POST /api/v1/admin/guias/{id}/itens`; rejeita item `SemTabela`/`Indeterminado` em guia não-pacote e exige `ValorApurado` manual em guia pacote (semântica de D-038); `GuiaNoRecursoDto` ganhou `bool EhPacote`. Suites xUnit cobrindo apuração do item novo, preservação de `ValorLiquidado`/`MotivoGlosa` dos itens existentes, rejeições e caso pacote. Frontend admin-web: `GuiaService.adicionarItem` + `ehPacote` no tipo do recurso; `AdicionarItemModalComponent` (envolve `app-item-guia-form`, backdrop próprio sem CDK); botão "+ Adicionar item" no card expandido da guia em `recurso-guias` que abre o modal e recarrega o recurso ao concluir.

**Decisão registrada:** D-045 (`DECISOES.md`).

**Spec:** `docs/specs/adicionar-item-guia-recurso.md`

### TCFG ✅ — Configurações do tenant (renomear + logo no PDF)

**Entregues (TASK-TCFG-01 a 06):** Abstração `IFileStorage` com `LocalFileStorage` em disco, volume Docker `honorare_storage` e `StorageOptions` bindadas de `appsettings`. `Tenant` ganhou `Rename(string)`, `SetLogoKey(string)` / `ClearLogoKey()` e `LogoKey string?` com migration `AddTenantLogoKey`. `TenantSettingsService` com `ObterAsync` / `RenomearAsync` / `SalvarLogoAsync` / `ObterLogoAsync` / `RemoverLogoAsync`; endpoints sob `/api/v1/admin/tenant` (policy `TenantAccess`, tenant resolvido por `ICurrentUser`): `GET /settings`, `PUT /settings/nome`, `POST /logo` (upload multipart, magic-number PNG/JPEG, limite 2 MB), `GET /logo`, `DELETE /logo`. Logo renderizada no cabeçalho do PDF do recurso (`RecursoPdfDocument`); sem logo exibe apenas o nome do tenant. Frontend admin-web: `TenantSettingsService` Angular; página `/admin/configuracoes` com formulário de renomeação e seção de logo (preview, upload por input file, remoção); sidebar com item "Configurações" em "Administração".

**Decisão registrada:** D-047 (`DECISOES.md`).

**Spec:** `docs/specs/configuracoes-tenant.md`

### NREC ✅ — Procedimentos não recorríveis

**Entregues (TASK-NREC-01 a 06):** `Tenant.CodigosNaoRecorriveis List<string>` mapeado como `text[]` (migration `AddCodigosNaoRecorriveis`); métodos `AddToNaoRecorriveis` / `RemoveFromNaoRecorriveis`. `GuiaDto.NaoRecorrivel bool` em `GuiaService.ListarAsync`. Endpoint `GET/PUT /api/v1/admin/tenant/codigos-nao-recorriveis` (policy `TenantAccess`). `RecursoService.AdicionarGuiasEmLoteAsync` pula guias com `NaoRecorrivel=true`; o individual `AdicionarGuiaAsync` funciona como escape hatch. Frontend: seção "Procedimentos Não Recorríveis" na página de Configurações com autocomplete do catálogo; badge "Não recorrível" na tabela de candidatas do `recurso-guias`.

**⚠ Refinado por GMIX:** a semântica de `NaoRecorrivel` mudou de "algum item NR" para "todos os itens NR" e foi adicionado `MistaComNaoRecorriveis` para guias com apenas alguns itens NR. Ver GMIX abaixo e D-049.

**Spec:** `docs/specs/procedimentos-nao-recorriveis.md`

### EIR ✅ — Excluir/reincluir itens de recurso

**Entregues (TASK-EIR-01 a 03):** `ItemGuia.IncluidoNoRecurso bool` (default `true`, migration `AddIncluidoNoRecursoItemGuia`); métodos `ExcluirDoRecurso()` / `ReincluirNoRecurso()` com invariante: excluir o último item incluído lança `InvalidOperationException` (→ 409); `Guia.RemoverDoRecurso()` reseta o flag em todos os itens. `RecursoService.AlterarInclusaoItemAsync` exposto em `PATCH /api/v1/admin/recursos/{id}/guias/{guiaId}/itens/{itemId}/inclusao` `{ "incluido": bool }`. `ObterPorIdAsync` expõe `incluidoNoRecurso` por item; `ObterDadosPdfAsync` filtra itens com `IncluidoNoRecurso=false` do PDF. Suites xUnit cobrindo exclusão, reinclussão, invariante do último item e reset ao remover guia. Frontend: item com `incluidoNoRecurso=false` renderiza riscado com botão "Reincluir"; item normal exibe botão "Excluir" com `confirm()`.

**Decisão registrada:** D-048 (`DECISOES.md`).

**Spec:** `docs/specs/excluir-item-recurso.md`

### GMIX ✅ — Guia mista com procedimentos não recorríveis

**Entregues (TASK-GMIX-01 a 03):** `GuiaDto` passa a expor dois flags mutuamente exclusivos: `NaoRecorrivel=true` somente quando **todos** os itens são NR; `MistaComNaoRecorriveis=true` quando **alguns** (não todos) são NR. `GuiaService.ListarAsync` computa `countNrPorGuia` por group-by e deriva os dois conjuntos. `RecursoService.AdicionarGuiasEmLoteAsync` bloqueia apenas guias totalmente NR; guias mistas são adicionadas com seus itens NR automaticamente marcados `IncluidoNoRecurso=false` via `ExcluirDoRecurso()` (invariante garantido pela definição de guia mista). Frontend: badge "Contém não recorrível" (âmbar/neutro) para guias mistas, distinto do badge "Não recorrível" (bloqueante) para guias totalmente NR.

**Decisão registrada:** D-049 (`DECISOES.md`).

**Spec:** `docs/specs/guia-mista-nao-recorrivel.md`

## Fase 4 — Visualização (2-3 semanas)

### F4.1 ✅ — Portal do médico (PWA)

**Entregues:** Endpoints `GET /api/v1/medico/guias` (lista paginada com filtros por operadora/período, exclui `Liquidada`, filtro hard `PrestadorId == currentUser.MedicoId`) e `GET /api/v1/medico/guias/{id}` (detalhe com itens e `situacaoCalculo` por item, 404 se guia não pertence ao médico) em `App/Faturamento/Endpoints/MedicoEndpoints.cs`, registrados em `Program.cs`; policy `MedicoAccess` em todos os handlers; DTOs `MedicoGuiaSummaryDto`/`MedicoListarGuiasResult` e `MedicoGuiaDetalheDto`/`MedicoItemGuiaDto` (`situacaoCalculo` derivado do `Calculo` mais recente por item, `"NaoCalculado"` quando sem cálculo). Suite xUnit `MedicoGuiaTests.cs` cobrindo 8 casos (isolamento por médico, exclusão de liquidadas, filtros, 404 e `situacaoCalculo`). No `medico-pwa`: `medico-guia.types.ts` com tipos `SituacaoGuia`, `SituacaoCalculo`, `MedicoGuiaSummaryItem`, `MedicoGuiaDetalheDto` e `MedicoItemGuiaDto`; `MedicoGuiaService` com `listar()` e `obterPorId()`; `PainelComponent` — shell mobile-first com top bar (wordmark + e-mail do JWT), `BottomNavComponent` (barra de navegação fixa no rodapé com 2 abas: Guias e Perfil, ícones SVG inline, estado ativo em terracota via `routerLinkActive`, `env(safe-area-inset-bottom)` para notch iOS) e `<router-outlet>`; `PerfilComponent` em `/perfil` com e-mail do JWT e botão "Sair da conta" (chama `AuthService.logout()` e navega para `/auth/login`); `GuiaListComponent` (lista responsiva, filtros debounce 400 ms, paginação anterior/próximo, badge âmbar/ferrugem por situação, ícone de observação, clique navega para `/guias/:id`) com 7 testes Vitest; `GuiaDetalheComponent` (cabeçalho, bloco observação sempre visível com fundo âmbar-claro, tabela de itens com badge verde/ferrugem/âmbar por `situacaoCalculo`, totais no rodapé, botão Voltar) com 9 testes Vitest; `BottomNavComponent` com 5 testes Vitest; `PerfilComponent` com 4 testes Vitest; rotas `/guias`, `/guias/:id` e `/perfil` em `app.routes.ts` dentro do `PainelComponent`.

**Não inclui:** resumo financeiro (total apresentado vs. pago vs. em aberto) — médico vê status e observação no MVP; totais ficam para fase 2.

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
3. F2.4 (beneficiários como entidade) — campo de texto livre na guia

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

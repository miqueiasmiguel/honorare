# SPEC: Cascata de atos múltiplos por valor decrescente (UNIMED)

> **Como executar:** uma task por sessão. Rode `/tdd-task docs/specs/cascata-valor-decrescente.md <TASK-ID>`,
> deixe a sessão implementar+commitar, limpe o contexto (`/clear`), rode a próxima.
> CLAUDE.md e MEMORY.md já entram no contexto de cada sessão — as tasks NÃO os repetem.
> **O checkbox `[ ]`/`[x]` é a única memória entre sessões:** comece pela primeira ainda `[ ]`;
> nunca refaça uma já marcada `[x]`. Execute na ordem (01 → 07): há dependência de dados entre elas.

## Objetivo

Mudar a regra de progressão de atos múltiplos da UNIMED. Hoje o fator de via (`PercentualOrdem`)
é **entrada** (vem da coluna `% VIA` do demonstrativo ou do formulário). Passa a ser **calculado pelo
motor**: dentro de cada guia, **por posição de executor**, ordena os procedimentos por **valor base
decrescente** e aplica a cascata fixa **100% / 50% / 40% / 30% / 20% / 10% / 10%** (8º em diante → 10%),
**ignorando** o que o demonstrativo informa. A distinção "mesma via" × "via diferente" deixa de existir
(cascata única). A `TabelaOrdemOperadora` configurável é abolida. O recurso de glosa parcial passa a
ordenar os itens por **posição → valor → percentual** decrescente. Vale só para **novas apurações**
(sem reprocessar guias já calculadas).

## Contexto compartilhado (válido para todas as tasks)

- **Bounded contexts envolvidos:** `Faturamento` (motor, guia, importação, recurso) e `Catalog`
  (operadora, tabelas). Direção: `Faturamento → Catalog`.
- **Cascata fixa (decimal):** índice 0→`1.0`, 1→`0.5`, 2→`0.4`, 3→`0.3`, 4→`0.2`, 5→`0.1`, 6→`0.1`;
  rank ≥ 7 (8º procedimento) → `0.1`. Constante única no motor.
- **Agrupamento do ranking:** por `(GuiaId, PosicaoExecutor)`. Cirurgião concorre só com cirurgião;
  cada auxiliar com os seus (depois leva o deflator de posição 0.6/0.4/0.3); anestesista entre os atos
  anestésicos dele. **O mesmo médico pode ocupar duas posições na mesma cirurgia** — cada posição é um
  grupo de ranking independente.
- **Valor de ordenação:** o **valor base** de cada item (não o valor final apurado). Para cirúrgico =
  `TabelaProcedimento.Valor`; para anestesista = valor de referência do porte anestésico
  (`TabelaPorteAnestesico`, selecionado pela acomodação). Desempate determinístico: maior valor primeiro;
  empate → `ProcedimentoId` depois `ItemGuiaId` (ascendente), para o cálculo ser reproduzível.
- **Build/test backend:** `make build` e `make test` (NÃO use `dotnet` cru — sem asdf no PATH; ver MEMORY.md).
  Filtro: `make test-filter FILTER=<NomeClasse>`.
- **Migrations:** rode de dentro de `apps/backend/App`:
  `dotnet ef migrations add <Nome> --output-dir Migrations --namespace App.Migrations`
  (precisa de `DOTNET_ROOT`+PATH — ver bloco "EF Core migrations" no CLAUDE.md). O
  `apps/backend/App/Migrations/.editorconfig` já existe e suprime IDE0005/IDE0161/CA1515/CA1861.

## Tasks

### TASK-CASCATA-01 — Motor calcula a cascata por valor base decrescente

- [x] concluída

**Objetivo:** `UnimedRuleSet` passa a derivar o `PercentualOrdem` de cada item por ranking de valor base
dentro de `(guia, posição)`, ignorando o valor de entrada. O resultado da apuração passa a expor o
percentual derivado. Apenas arquivos do motor + teste do motor.

**Já no contexto (NÃO reler):** CLAUDE.md (regras de domínio UNIMED, pipeline de modifiers), MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Faturamento/Calculo/Unimed/UnimedRuleSet.cs` (arquivo inteiro, ~101 linhas) — é o alvo principal.
- `apps/backend/App/Faturamento/Calculo/CalculoTypes.cs:1-35` — records do motor.
- `apps/backend/App/Faturamento/Calculo/Unimed/AnestesiaCalculator.cs:1-31` — recebe `percentualOrdem`.
- `apps/backend/App/Faturamento/Calculo/NullRuleSet.cs:1-15` — também constrói `ApuracaoItemResult`.
- `apps/backend/tests/Faturamento.Tests/Calculo/Unimed/UnimedPipelineTests.cs:1-65,99-130` — padrão de teste E2E via `GuiaService`.

**Criar/Editar:**

- `CalculoTypes.cs` (editar): remover `decimal PercentualOrdem` de `ApurarItemInput`; adicionar
  `decimal PercentualOrdem` em `ApuracaoItemResult` (após `ValorApurado`).
- `UnimedRuleSet.cs` (editar): reescrever `ApurarAsync` em duas fases (abaixo). O loop atual apura item a
  item isolado — trocar por: (1) calcular valor base de cada item; (2) agrupar por `PosicaoExecutor`,
  ordenar desc por valor base, atribuir o fator da cascata por rank; (3) seguir o pipeline com o fator
  derivado. `ApurarAnestesistaAsync` passa a receber o percentual derivado (não `item.PercentualOrdem`).
- `AnestesiaCalculator.cs` (sem mudança de assinatura — já recebe `percentualOrdem`; só será chamado com o derivado).
- `NullRuleSet.cs` (editar): adicionar o novo campo no construtor de `ApuracaoItemResult` (use `1.0m`).
- `UnimedPipelineTests.cs` (editar): ver "Testes".

**Forma atual (será substituída) — `UnimedRuleSet.ApurarAsync`:**

```csharp
// apps/backend/App/Faturamento/Calculo/Unimed/UnimedRuleSet.cs:10-21 — hoje apura item a item, isolado
public async Task<IReadOnlyList<ApuracaoItemResult>> ApurarAsync(ApurarGuiaContext ctx, CancellationToken ct = default)
{
    var resultados = new List<ApuracaoItemResult>(ctx.Itens.Count);
    foreach (var item in ctx.Itens) { resultados.Add(await ApurarItemAsync(ctx, item, ct)); }
    return resultados;
}
```

**Desenho alvo (duas fases):**

1. Para cada item, resolver o **valor base** e a **situação** (sem aplicar a cascata ainda):
   cirúrgico → `TabelaProcedimento.Valor` (ausente ⇒ `SemTabela`); anestesista → valor de referência do
   porte (`PorteAnestesico` nulo ⇒ `Indeterminado`; sem `TabelaPorteAnestesico` ⇒ `SemTabela`). Itens
   sem valor base não entram no ranking e retornam o early-exit (como hoje).
2. Agrupar os itens **calculáveis** por `PosicaoExecutor`. Em cada grupo, ordenar por valor base desc
   (desempate `ProcedimentoId` → `ItemGuiaId`) e mapear o rank ao fator da cascata (`CascataFator(rank)`).
3. Apurar cada item aplicando o fator derivado no passo `OrdemProcedimento` (o resto do pipeline —
   videolaparoscopia, acomodação, urgência, posição — permanece **igual e na mesma ordem** de
   `UnimedRuleSet.cs:48-52`). O `OrdemProcedimentoModifier.Aplicar(fatorDerivado, valorBase)` continua
   sendo o aplicador puro. Preencher `ApuracaoItemResult.PercentualOrdem` com o fator derivado.

```csharp
private static readonly decimal[] _cascata = [1.0m, 0.5m, 0.4m, 0.3m, 0.2m, 0.1m, 0.1m];
private static decimal CascataFator(int rank) => rank < _cascata.Length ? _cascata[rank] : 0.1m;
```

**ATENÇÃO (regressão de cálculo é ground truth):** os testes atuais de item único que passavam
`percentualOrdem: 0.5m` esperando `500m` (`Cirurgiao_SecundarioMesmaViaAsync`,
`Cirurgiao_SecundarioMesmaVia_Apartamento_UrgenciaAsync`) **mudam de valor**: item único = rank 0 = 100%.
Atualizar esses esperados e adicionar os cenários multi-item abaixo. Cada mudança de cálculo reflete a
nova regra acordada com o cliente.

**Testes (red primeiro) — em `UnimedPipelineTests` (Testcontainers, `[Collection(nameof(PostgresCollection))]`):**

O `SeedAsync` atual cria 1 procedimento de `1000m`. Para multi-item, semear ≥3 procedimentos com valores
distintos (ex.: 1000, 600, 300) e montar uma guia com vários itens. Casos:

- `Deve aplicar 100/50/40 por valor decrescente quando 3 procedimentos do cirurgião na mesma guia`
  (maior=100%, médio=50%, menor=40% — independente da ordem de inserção e do `via`/`percentualOrdem` passado).
- `Deve aplicar a mesma cascata para vias diferentes` (idem acima com `ViaAcesso` distintos por item — resultado idêntico).
- `Deve rankear cirurgião e 1º auxiliar em grupos separados` (cirurgião do mesmo proc = 100%; auxiliar do mesmo proc = 100%×0.6).
- `Deve ignorar o PercentualOrdem de entrada` (passar 0.3 num item único e ainda obter 100%).
- Ajustar os dois testes de item único citados acima (0.5 → agora 100%).

**Aceite (checklist objetivo):**

- [ ] `make build` sem warnings (TreatWarningsAsErrors)
- [ ] `make test-filter FILTER=UnimedPipelineTests` verde
- [ ] `make test-filter FILTER=OrdemProcedimentoModifierTests` verde (modifier intacto)
- [ ] motor não lê mais `ApurarItemInput.PercentualOrdem` (o campo foi removido do record)
- [ ] `ApuracaoItemResult.PercentualOrdem` traz o fator derivado por item

**Commit:** `feat(faturamento): motor apura cascata de atos múltiplos por valor decrescente (TASK-CASCATA-01)`

---

### TASK-CASCATA-02 — GuiaService: persistir % derivado e remover entrada manual

- [ ] pendente

**Objetivo:** Parar de receber/validar `PercentualOrdem` na criação/edição de guia; gravar no `ItemGuia`
o percentual que o motor derivou; e garantir que adicionar item reapure a **guia inteira** (ranking correto).

**Depende de:** TASK-CASCATA-01. Contratos já existentes após a 01 (não precisa abrir CalculoTypes):

```csharp
// ApurarItemInput NÃO tem mais PercentualOrdem:
internal sealed record ApurarItemInput(Guid ItemGuiaId, Guid ProcedimentoId, PosicaoExecutor Posicao,
    ViaAcesso Via, Acomodacao Acomodacao, bool EhUrgencia, int? TempoAnestesicoMin = null);
// ApuracaoItemResult ganhou PercentualOrdem:
internal sealed record ApuracaoItemResult(Guid ItemGuiaId, SituacaoApuracao Situacao,
    decimal? ValorApurado, decimal PercentualOrdem, IReadOnlyList<PassoApuracao> Passos);
```

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md, esta SPEC (contratos acima).

**Ler (só isto):**

- `apps/backend/App/Faturamento/GuiaService.cs:9-22` (records de comando), `94-257` (CriarAsync, AdicionarItemAsync),
  `419-520` (AtualizarAsync), `590-623` (RecalcularAsync — padrão de reapuração total), `730-802` (ExecutarCalculoAsync, ValidarCalculoViavelAsync).
- `apps/backend/App/Faturamento/ItemGuia.cs` (arquivo inteiro, ~71 linhas).

**Criar/Editar:**

- `GuiaService.cs`:
  - Remover de `CriarItemGuiaCommand` (linha 16) o campo `decimal PercentualOrdem`.
  - Remover as 3 validações `PercentualOrdem deve estar entre 0.01 e 1.00` (linhas ~109-113, ~178-182, ~434-437).
  - Nas chamadas `ItemGuia.Create(...)` (linhas ~157, ~214, e a do AtualizarAsync ~477) remover o argumento de percentual.
  - Construções de `ApurarItemInput` (linhas ~226-229, ~737-740, ~781-784): remover o argumento de percentual (campo já não existe após a 01).
  - Em `ExecutarCalculoAsync` (~756): além de `item.SetValorApurado(...)`, gravar `item.SetPercentualOrdem(resultado.PercentualOrdem)`.
  - `AdicionarItemAsync` (~221-254): hoje apura **só o item novo** em contexto isolado — ranking erra.
    Trocar por reapuração da **guia inteira**: após persistir o novo item, espelhar `RecalcularAsync`
    (apagar `Calculos` da guia via `ExecuteDeleteAsync`, carregar todos os itens, `SetValorApurado(null)`,
    `SaveChanges`, e chamar `ExecutarCalculoAsync(guia, operadora, todosOsItens, ct)`). Manter o early-return
    para `EhPacote`/`TipoRuleSet.Nulo`.
- `ItemGuia.cs`:
  - Remover `percentualOrdem` da assinatura de `Create` (linhas 43-70); inicializar `PercentualOrdem = 1.0m`
    no objeto (placeholder até o motor gravar).
  - Adicionar setter: `internal void SetPercentualOrdem(decimal valor) => PercentualOrdem = valor;`.
  - O método `Atualizar(decimal percentualOrdem, ...)` (linha 36) é usado só pela importação — **não** mexer aqui (fica para a TASK-03).

**Padrão de reapuração total (colado de RecalcularAsync:611-620):**

```csharp
await _db.Calculos.Where(c => c.GuiaId == guiaId).ExecuteDeleteAsync(ct);
var itens = await _db.ItensGuia.Where(i => i.GuiaId == guiaId).ToListAsync(ct);
foreach (var item in itens) { item.SetValorApurado(null); }
await _db.SaveChangesAsync(ct);
await ExecutarCalculoAsync(guia, operadora, itens, ct);
```

**Testes (red primeiro):**

- `UnimedPipelineTests` — atualizar o helper `Cmd`/`CriarItemGuiaCommand` para não passar `percentualOrdem`
  (a 01 ainda passava; agora o campo não existe). Os asserts de `ValorApurado` continuam válidos.
- `GuiaService` (procurar testes que constroem `CriarItemGuiaCommand` com percentual) — remover o argumento.
- Novo: `Deve reapurar a guia inteira ao adicionar item` — guia com 1 proc (100%); adiciona 2º proc de maior
  valor; após adicionar, o novo vira 100% e o antigo cai para 50%.

**Aceite:**

- [ ] `make build` sem warnings
- [ ] `make test-filter FILTER=UnimedPipelineTests` verde
- [ ] `make test-filter FILTER=GuiaService` verde
- [ ] `CriarItemGuiaCommand` não tem mais `PercentualOrdem`; `ItemGuia.PercentualOrdem` é gravado pelo motor
- [ ] `AdicionarItemAsync` reapura a guia inteira

**Commit:** `feat(faturamento): grava percentual derivado e remove entrada manual de ordem (TASK-CASCATA-02)`

---

### TASK-CASCATA-03 — Importação do demonstrativo: derivar % e tornar `% VIA` opcional

- [ ] pendente

**Objetivo:** A importação CSV deixa de ler `% VIA` para o cálculo (motor deriva); a coluna `% VIA` deixa
de ser obrigatória; o percentual derivado é gravado no item; a apuração roda sobre **todos** os itens da guia.

**Depende de:** TASK-CASCATA-01 e 02. Contratos já valendo:
`ItemGuia.Create(...)` **não** recebe percentual; existe `ItemGuia.SetPercentualOrdem(decimal)`;
`ApuracaoItemResult` tem `PercentualOrdem`; `ApurarItemInput` **não** tem percentual.

**Já no contexto (NÃO reler):** CLAUDE.md (D-039 sobre `ACRESCIMO`, regra de importação), MEMORY.md, esta SPEC.

**Ler (só isto):**

- `apps/backend/App/Faturamento/ImportacaoGuiaCsvService.cs:28-32` (headers obrigatórios), `182-215`
  (criação/atualização de item), `228-246` (loop de cálculo pós-import), `306-345` (parsing de linha),
  `410-457` (MapearPercentualOrdem + ExecutarCalculoAsync).
- `apps/backend/App/Faturamento/ItemGuia.cs:36-41` (método `Atualizar`).

**Criar/Editar:**

- `ImportacaoGuiaCsvService.cs`:
  - Remover `"% VIA"` de `_requiredHeaders` (linha 32).
  - Remover o método `MapearPercentualOrdem` (linhas 410-411) e seu uso (linha 182).
  - No parsing (linha ~306-313): `percentVia` deixa de ser obrigatório — **não** descartar a linha quando
    ele estiver ausente. Manter `HONORARIO` e `TOTAL` como condição de linha válida (remover `percentVia` da guarda da linha 310).
    `LinhaCSV.PercentVia` pode virar `decimal?` ou ser simplesmente ignorado; se a coluna sumir do header,
    `Col(...)` devolve `""` e `ParseDecimal` devolve `null` — trate sem quebrar.
  - `ItemGuia.Create(...)` (linha ~201) sem o argumento de percentual.
  - `ItemGuia.Atualizar(...)` (linha ~194) sem o argumento de percentual — ver mudança em ItemGuia abaixo.
  - `ExecutarCalculoAsync` (linhas 413-457): apurar sobre **todos os itens da guia** (não só os do grupo do
    CSV) para o ranking ficar correto em reimportações parciais — carregar
    `await db.ItensGuia.Where(i => i.GuiaId == guia.Id).ToListAsync(ct)` e apurar essa lista; antes, apagar
    o `Calculo` anterior da guia (`ExecuteDeleteAsync`) para não duplicar passos. Gravar
    `item.SetValorApurado(...)` **e** `item.SetPercentualOrdem(resultado.PercentualOrdem)`. Construir
    `ApurarItemInput` sem o argumento de percentual.
- `ItemGuia.cs`: alterar `Atualizar(decimal percentualOrdem, Acomodacao acomodacao, bool ehUrgencia)` para
  `Atualizar(Acomodacao acomodacao, bool ehUrgencia)` (remover a atribuição de `PercentualOrdem`).

**Testes (red primeiro) — `ImportacaoGuiaCsvTests`:**

- `Deve importar CSV sem a coluna % VIA` (header sem `% VIA`; importação conclui; itens calculados).
- `Deve derivar a cascata por valor na importação` (guia com ≥2 procedimentos de valores distintos →
  maior 100%, próximo 50%, independente do que vier em `% VIA`).
- Ajustar testes existentes que dependiam do `% VIA` alimentar o cálculo.

**Aceite:**

- [ ] `make build` sem warnings
- [ ] `make test-filter FILTER=ImportacaoGuiaCsvTests` verde
- [ ] importar CSV sem `% VIA` não falha; `% VIA` presente é ignorado no cálculo
- [ ] percentual derivado persistido nos itens importados

**Commit:** `feat(faturamento): importação deriva cascata e torna % VIA opcional (TASK-CASCATA-03)`

---

### TASK-CASCATA-04 — Recurso de glosa parcial: ordenar por posição → valor → percentual

- [ ] pendente

**Objetivo:** No recurso (PDF e detalhe), ordenar os itens de cada guia por **posição de executor**, depois
**valor apurado** desc, depois **percentual** desc. Hoje o PDF ordena só por `PercentualOrdem desc`.

**Depende de:** TASK-CASCATA-02/03 (o `ItemGuia.PercentualOrdem` agora reflete a cascata derivada).

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md, esta SPEC.

**Ler (só isto):**

- `apps/backend/App/Faturamento/RecursoService.cs:239-266` (itens no detalhe), `551-595` (itens do PDF, ordenação atual).
- `apps/backend/App/Catalog/PosicaoExecutor.cs` (ordem do enum, para confirmar a ordenação por posição).
- `apps/backend/tests/Faturamento.Tests/Recurso/RecursoPdfDataTests.cs:1-40` (formato de teste).

**Editar:**

- `RecursoService.cs`, `ObterDadosPdfAsync` (linha ~551-566): trocar `orderby i.PercentualOrdem descending`.
  Como a ordenação combina 3 chaves e o agrupamento é por guia, ordene **em memória** após o `ToListAsync`,
  dentro de cada guia: `OrderBy(PosicaoExecutor).ThenByDescending(ValorApurado ?? 0).ThenByDescending(PercentualOrdem)`.
  (A query pode trazer sem `orderby`; a ordenação final acontece por guia ao montar `itemDtos`.)
- `RecursoService.cs`, `ObterPorIdAsync` (linha ~259-266): aplicar a **mesma** ordenação ao montar
  `itensPorGuia`, para a tela de detalhe casar com o PDF.

**Ordenação alvo (dentro de cada guia):**

```csharp
.OrderBy(i => i.PosicaoExecutor)
.ThenByDescending(i => i.ValorApurado ?? 0m)
.ThenByDescending(i => i.PercentualOrdem)
```

**Testes (red primeiro) — `RecursoPdfDataTests`:**

- `Deve ordenar itens do recurso por posição, depois valor, depois percentual` (montar guia com cirurgião
  de 2 valores + 1 auxiliar; verificar a ordem dos `ItemPdfData`).

**Aceite:**

- [ ] `make build` sem warnings
- [ ] `make test-filter FILTER=RecursoPdfDataTests` verde
- [ ] PDF e detalhe usam a mesma ordenação posição → valor → %

**Commit:** `feat(recurso): ordena itens por posição, valor e percentual decrescente (TASK-CASCATA-04)`

---

### TASK-CASCATA-05 — Abolir `TabelaOrdemOperadora` (backend + migração)

- [ ] pendente

**Objetivo:** Remover por completo a tabela de progressão configurável: entidade, configuração, DbSet,
métodos de serviço, endpoints, enum `TipoViaOrdem`, testes e a tabela no banco (migração de drop).

**Depende de:** TASK-CASCATA-01..03 (nada mais lê essa tabela no cálculo).

**Já no contexto (NÃO reler):** CLAUDE.md (estrutura de migrations), MEMORY.md, esta SPEC.

**Ler (só isto):**

- `apps/backend/App/Catalog/CatalogService.cs:94-96` (records), `1370-1465` (bloco TabelaOrdem: defaults,
  Listar/Salvar/Excluir/ResolverPercentualOrdem).
- `apps/backend/App/Catalog/Endpoints/CatalogEndpoints.cs:60-68` (MapGroup `tabela-ordem`), `615-647`
  (handlers), `725-730` (record `SalvarTabelaOrdemRequest`).
- `apps/backend/App/Data/AppDbContext.cs:25` (DbSet). Não há registro manual de configuration —
  `ApplyConfigurationsFromAssembly` descobre sozinho; basta apagar o arquivo de configuração.

**Excluir/Editar:**

- Excluir: `apps/backend/App/Catalog/TabelaOrdemOperadora.cs`,
  `apps/backend/App/Catalog/Configurations/TabelaOrdemOperadoraConfiguration.cs`,
  `apps/backend/App/Catalog/TipoViaOrdem.cs`,
  `apps/backend/tests/Catalog.Tests/TabelaOrdemOperadora/TabelaOrdemOperadoraTests.cs`.
- `AppDbContext.cs`: remover a linha 25 (`DbSet<TabelaOrdemOperadora>`).
- `CatalogService.cs`: remover os records `SalvarOrdemItem`/`TabelaOrdemItem`, o dicionário `_padraoOrdem`
  e os métodos `ListarTabelaOrdemAsync`, `SalvarTabelaOrdemAsync`, `ExcluirTabelaOrdemAsync`,
  `ResolverPercentualOrdemAsync`, `ResolverPadrao`.
- `CatalogEndpoints.cs`: remover o `MapGroup(".../tabela-ordem")` e os 3 handlers + o record `SalvarTabelaOrdemRequest`.
- Confirmar (grep) que nada mais referencia `TipoViaOrdem`/`TabelaOrdem` no backend antes de buildar.
- **Migração:** `dotnet ef migrations add AboleTabelaOrdemOperadora --output-dir Migrations --namespace App.Migrations`
  (de dentro de `apps/backend/App`, com `DOTNET_ROOT`+PATH). Conferir que o `Up` faz `DropTable` da tabela
  de ordem e o `Down` recria (gerado automaticamente). O `.editorconfig` de migrations já existe.

**Testes:** os de Catalog que sobraram devem continuar verdes; a classe de teste da tabela de ordem é removida.

**Aceite:**

- [ ] `make build` sem warnings; nenhuma referência pendente a `TabelaOrdem`/`TipoViaOrdem` no backend
- [ ] `make test` verde
- [ ] migração de drop gerada em `apps/backend/App/Migrations`
- [ ] `AppDbContextModelSnapshot.cs` atualizado (sem `TabelaOrdemOperadora`)

**Commit:** `refactor(catalog): remove TabelaOrdemOperadora (cascata agora é fixa) (TASK-CASCATA-05)`

---

### TASK-CASCATA-06 — Frontend: remover % VIA / atos múltiplos + regenerar client

- [ ] pendente

**Objetivo:** Tirar do admin-web tudo que pedia/configurava `% VIA`/atos múltiplos e regenerar o client TS
do OpenAPI (o backend mudou contratos: `CriarItemGuiaCommand` sem `percentualOrdem`, endpoints `tabela-ordem` removidos).

**Depende de:** TASK-CASCATA-01..05 concluídas (backend final). O `ItemGuia` retornado pela API **ainda**
expõe `percentualOrdem` (leitura, para exibir no detalhe/recurso) — só o **payload de criação** perde o campo.

**Já no contexto (NÃO reler):** CLAUDE.md (Frontend Conventions: `[selected]` não `[value]`; sem pipes de
locale — usar `Intl.*`; todo `.subscribe` com `error`), `apps/admin-web/STYLES.md`, MEMORY.md, esta SPEC.

**Ler (só isto):**

- `apps/admin-web/src/app/admin/faturamento/guia-form/item-guia-form/item-guia-form.component.ts`
  (arquivo inteiro — campo "Ordem", `PADRAO_OPCOES`, `opcoesDeTabela`, `_carregarOpcoes`).
- `apps/admin-web/src/app/admin/catalog/catalog.types.ts:187-195` (tipos TabelaOrdem).
- `apps/admin-web/src/app/admin/faturamento/guia.types.ts` e `recurso.types.ts` (onde `percentualOrdem` aparece).
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/adicionar-item-modal/adicionar-item-modal.component.ts`
  (monta payload de item).

**Primeiro passo — regenerar o client:** rodar `pnpm generate-api-client` (CLAUDE.md). Isso atualiza
`packages/api-contracts/` removendo `percentualOrdem` do request de criação e os tipos de `tabela-ordem`.
Os erros de compilação TS resultantes guiam o resto da remoção.

**Editar/Excluir (admin-web):**

- `item-guia-form.component.ts`: remover o `<div>` do campo "Ordem" (select + info de percentual), o signal
  `percentualOrdem`, `ordemOpcoes`, `onOrdemChange`, `formatarPercentual` (se ficar sem uso), `_carregarOpcoes`,
  `PADRAO_OPCOES`, `opcoesDeTabela`, a chamada `listarTabelaOrdem`, e o `percentualOrdem` do objeto emitido
  em `onFormSubmit` e do `ngOnInit`/`item()`.
- `catalog.service.ts` + `catalog.types.ts`: remover `listarTabelaOrdem`/`salvar`/`excluir` e os tipos
  `TipoViaOrdem`/`TabelaOrdemOperadoraItem`.
- Excluir `tabela-atos-multiplos.component.ts` + `.spec.ts`; remover a seção/uso em `operadora-form.component.{ts,html,spec.ts}`.
- `adicionar-item-modal.component.ts` (+ spec): remover `percentualOrdem` do payload de criação.
- `guia.types.ts`/`recurso.types.ts`: remover `percentualOrdem` apenas dos tipos de **criação/payload**;
  manter nos tipos de **leitura** (DTO de detalhe/recurso) se o backend ainda o retorna.
- Atualizar specs que quebrarem: `guia-form.*.spec.ts`, `item-guia-form.*.spec.ts`, `recurso-guias.*.spec.ts`,
  `catalog.service.spec.ts`, `operadora-form.*.spec.ts`, `adicionar-item-modal.*.spec.ts`.

**Aceite:**

- [ ] `pnpm -F admin-web lint` (0 warnings) e `pnpm -F admin-web stylelint` verdes
- [ ] `pnpm -F admin-web test:ci` verde (coverage ≥ 80%)
- [ ] `pnpm -F admin-web build` compila
- [ ] formulário de item não exibe mais o campo "Ordem"/% VIA; tela da operadora sem "Tabela de Atos Múltiplos"

**Commit:** `feat(admin-web): remove campo de via/atos múltiplos e regenera client (TASK-CASCATA-06)`

---

### TASK-CASCATA-07 — Documentação (DOMINIO.md + DECISOES.md)

- [ ] pendente

**Objetivo:** Refletir a nova regra na documentação de domínio e registrar a decisão.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md, esta SPEC.

**Ler (só isto):**

- `docs/DOMINIO.md:60-66` (Via de acesso, PercentualOrdem), `82` (TabelaOrdemOperadora), `124-132`
  (atos múltiplos / defaults), `160-166` (ordem dos modifiers).
- `docs/DECISOES.md` (final do arquivo, para o número da próxima decisão).

**Editar:**

- `DOMINIO.md`: reescrever o verbete **PercentualOrdem** (agora é derivado pelo motor por ranking de valor,
  não mais entrada); remover o verbete **TabelaOrdemOperadora**; reescrever a seção de **atos múltiplos**
  para a cascata única `100/50/40/30/20/10/10` (8º+→10%), agrupada por `(guia, posição)`, ordenada por valor
  base desc, idêntica para mesma/diferente via; ajustar o passo 2 do pipeline de modifiers para "fator
  derivado pelo ranking".
- `DECISOES.md`: nova decisão (próximo `D-0xx`) — "Cascata de atos múltiplos unificada por valor decrescente,
  fixa no motor; `% VIA` do demonstrativo ignorada; `TabelaOrdemOperadora` abolida; vale só para novas
  apurações". Incluir o racional (regras novas da UNIMED informadas pelo cliente).

**Aceite:**

- [ ] `DOMINIO.md` sem menção a `% VIA`/`TabelaOrdemOperadora` como fonte do percentual
- [ ] nova decisão registrada em `DECISOES.md` com número sequencial e racional

**Commit:** `docs: registra cascata de atos múltiplos por valor decrescente (TASK-CASCATA-07)`

---

## Checklist final

- [x] TASK-CASCATA-01 — Motor calcula a cascata por valor base decrescente
- [ ] TASK-CASCATA-02 — GuiaService persiste % derivado e remove entrada manual
- [ ] TASK-CASCATA-03 — Importação deriva % e torna `% VIA` opcional
- [ ] TASK-CASCATA-04 — Recurso ordena por posição → valor → percentual
- [ ] TASK-CASCATA-05 — Abolir `TabelaOrdemOperadora` (backend + migração)
- [ ] TASK-CASCATA-06 — Frontend remove % VIA / atos múltiplos + regenera client
- [ ] TASK-CASCATA-07 — Documentação (DOMINIO.md + DECISOES.md)

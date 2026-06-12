# SPEC: Recurso de Glosa Branca + Convênio sem cálculo

> **Como executar:** uma task por sessão. Rode `/tdd-task docs/specs/glosa-branca-glosa-parcial.md <TASK-ID>`,
> deixe a sessão implementar+commitar, limpe o contexto (`/clear`), rode a próxima.
> CLAUDE.md e MEMORY.md já entram no contexto de cada sessão — as tasks NÃO os repetem.
> **O checkbox `[ ]`/`[x]` é a única memória entre sessões:** uma sessão fria se orienta
> só por ele. Comece pela primeira task ainda `[ ]`; nunca refaça uma já marcada `[x]`.

## Objetivo

Dois recursos relacionados de disputa de glosa:

1. **Glosa parcial** (já existe): operadora pagou **menos** que o valor correto. PDF mostra `PG UNIMED` × `VL CORRETO`.
2. **Glosa branca** (novo): procedimento **não foi pago de jeito nenhum**. PDF lista só os procedimentos + dados da guia, **sem nenhum valor**.

O `Recurso` ganha um `Tipo` intrínseco (`GlosaParcial | GlosaBranca`), escolhido na criação, que decide o template de PDF. Ambos ficam disponíveis para download.

Em paralelo, conclui o **convênio sem cálculo** (operadora `TipoRuleSet.Nulo`, hoje rotulada "Sem apuração"): o motor já é pulado no backend; falta (a) renomear o rótulo na UI e (b) enxugar o formulário de guia para não pedir campos de cálculo quando a operadora não calcula. Operadora sem cálculo **só pode** gerar recurso de glosa branca (não há valor a disputar).

## Contexto compartilhado (válido para todas as tasks)

- Bounded context backend: `App.Faturamento`. Entidade `Recurso` em `apps/backend/App/Faturamento/Recurso.cs`.
- Enums no EF são gravados como **string** via `.HasConversion<string>().IsRequired()` (padrão do projeto — ex.: `Operadora.TipoRuleSet`, `Guia.Situacao`).
- Migrations ficam em `apps/backend/App/Migrations/` e **já existe** `apps/backend/App/Migrations/.editorconfig` (não criar). Gerar com:
  `cd apps/backend/App && dotnet ef migrations add <Nome> --output-dir Migrations --namespace App.Migrations`
  (precisa de `DOTNET_ROOT` + PATH — ver MEMORY.md). O snapshot é atualizado automaticamente.
- Build: `dotnet build apps/backend/Honorare.slnx` (warnings = erros). Testes: `dotnet test apps/backend/Honorare.slnx`.
- Frontend admin: `pnpm -F admin-web lint` e `pnpm -F admin-web test:ci`. As types do recurso são **mantidas à mão** em `apps/admin-web/src/app/admin/faturamento/recurso.types.ts` (NÃO há passo `generate-api-client` para este módulo).

## Tasks

### TASK-GLOSA-01 — Entidade Recurso ganha `Tipo` + migration

- [x] concluída

**Objetivo:** adicionar enum `TipoRecurso` e a propriedade `Tipo` em `Recurso`, persistida como string, com migration.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Faturamento/Recurso.cs` (entidade inteira — 52 linhas; padrão Create/Atualizar)
- `apps/backend/App/Faturamento/Configurations/RecursoConfiguration.cs` (config EF inteira — 28 linhas)

**Criar/Editar:**

- `apps/backend/App/Faturamento/TipoRecurso.cs` (novo)
- `apps/backend/App/Faturamento/Recurso.cs` (editar: propriedade `Tipo` + param em `Create` e `Atualizar`)
- `apps/backend/App/Faturamento/Configurations/RecursoConfiguration.cs` (editar: `.HasConversion<string>()`)
- Nova migration em `apps/backend/App/Migrations/`

**⚠️ Default da coluna na migration (obrigatório):** `dotnet ef` gera `AddColumn<string>("tipo", nullable: false, defaultValue: "")`. Empty string **não** é um `TipoRecurso` válido → qualquer `recurso` pré-existente fica ilegível (materialização lança ao parsear `""`). Os testes não pegam isso (schema sobe vazio). **Edite a migration** para `defaultValue: "GlosaParcial"`.

**Padrão a seguir (colado):**

```csharp
// TipoRecurso.cs — espelha App/Catalog/TipoRuleSet.cs
namespace App.Faturamento;

internal enum TipoRecurso
{
    GlosaParcial,
    GlosaBranca,
}
```

```csharp
// Recurso.cs — Create/Atualizar recebem `tipo` como NOVO ÚLTIMO parâmetro.
// Default no domínio = GlosaParcial (compatibilidade com chamadas existentes).
// Propriedade: public TipoRecurso Tipo { get; private set; }
internal static Recurso Create(
    Guid tenantId, Guid operadoraId, Guid prestadorId,
    DateOnly dataEmissao, string? observacao, string numero,
    TipoRecurso tipo = TipoRecurso.GlosaParcial) { /* ...set Tipo = tipo... */ }
```

```csharp
// RecursoConfiguration.cs — adicionar junto às outras Property():
builder.Property(r => r.Tipo).HasConversion<string>().IsRequired();
```

**Testes (red primeiro):** em `apps/backend/tests/Faturamento.Tests/Recurso/` (coleção `PostgresCollection`, ver `RecursoCrudTests.cs` para o boilerplate de fixture):

- `Deve persistir e reler Recurso com Tipo GlosaBranca`
- `Deve usar GlosaParcial como default quando Tipo não é informado`

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] migration criada; `dotnet test apps/backend/Honorare.slnx` verde
- [ ] coluna `tipo` (string) existe na tabela `recursos`

**Commit:** `feat(faturamento): Recurso ganha campo Tipo (parcial/branca) (TASK-GLOSA-01)`

---

### TASK-GLOSA-02 — Service + endpoint propagam `Tipo` na criação/edição do recurso

- [x] concluída

**Objetivo:** `Tipo` flui de ponta a ponta no CRUD do recurso (request → command → service → dto). Validação: recurso `GlosaParcial` **não** pode usar operadora `TipoRuleSet.Nulo` (sem valor a disputar); `GlosaBranca` é permitido para qualquer operadora.

**Depende de:** TASK-GLOSA-01. Contrato já existente:

```csharp
internal enum TipoRecurso { GlosaParcial, GlosaBranca }
// Recurso.Create(..., string numero, TipoRecurso tipo = TipoRecurso.GlosaParcial)
// Recurso.Atualizar(..., string numero, TipoRecurso tipo)
// Operadora.TipoRuleSet (enum App.Catalog.TipoRuleSet { Unimed, Nulo })
```

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Faturamento/RecursoService.cs:9-19` (records `CriarRecursoCommand`, `AtualizarRecursoCommand`, `RecursoDto`)
- `apps/backend/App/Faturamento/RecursoService.cs:89-121` (`CriarAsync` — onde buscar operadora e montar o DTO)
- `apps/backend/App/Faturamento/RecursoService.cs:271-308` (`AtualizarAsync`)
- `apps/backend/App/Faturamento/Endpoints/RecursoEndpoints.cs:44-80,154-158` (handlers + records de request)

**Editar:**

- `RecursoService.cs`: add `TipoRecurso Tipo` aos records `CriarRecursoCommand`, `AtualizarRecursoCommand`, `RecursoDto`; passar `cmd.Tipo` para `Recurso.Create`/`Atualizar`; preencher `recurso.Tipo` nos DTOs retornados (em `CriarAsync`, `AtualizarAsync`, `ListarAsync`, `ObterPorIdAsync` — todos que constroem `RecursoDto`); validar Nulo×Parcial.
- `RecursoEndpoints.cs`: add `TipoRecurso Tipo` em `CriarRecursoRequest` e `AtualizarRecursoRequest`; passar para os commands.

**Evitar churn (ler antes de editar):**

- Dê **default** ao `Tipo` nos records de command (`TipoRecurso Tipo = TipoRecurso.GlosaParcial`) — assim os call-sites posicionais existentes continuam compilando, ex.: `RecursoPdfDataTests.cs:59` (`new CriarRecursoCommand(opId, prestId, new DateOnly(2026,3,1), null, "202512")`) e o helper de `RecursoCrudTests`.
- `ListarAsync` (:126-141) e `ObterPorIdAsync` (:183-200) montam **projeções anônimas** antes do `RecursoDto`. Adicione `r.Tipo` à projeção anônima também — senão o campo não existe na hora de construir o DTO.
- Como o projeto serializa enums como string (já há `JsonStringEnumConverter` global — `Situacao␣Guia`/`PosicaoExecutor` chegam como string no frontend), **não** precisa de atributo de conversor no DTO.

**Padrão da validação (colado — `operadora` já é buscada nessas funções):**

```csharp
// após carregar `operadora`, antes de criar/atualizar:
if (cmd.Tipo == TipoRecurso.GlosaParcial && operadora.TipoRuleSet == TipoRuleSet.Nulo)
{
    return Result<RecursoDto>.Fail(new ValidationError(
        "Operadora sem cálculo só permite recurso de glosa branca."));
}
```

**Testes (red primeiro):** em `RecursoCrudTests.cs` (mesma classe/fixture):

- `Deve criar recurso GlosaBranca para operadora Nulo`
- `Deve rejeitar recurso GlosaParcial para operadora Nulo` (espera `ValidationError`)
- `Deve retornar Tipo no RecursoDto`

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde
- [ ] endpoint `POST /api/v1/admin/recursos` aceita e devolve `tipo`

**Commit:** `feat(faturamento): recurso aceita tipo parcial/branca com validação por operadora (TASK-GLOSA-02)`

---

### TASK-GLOSA-03 — Filtro "somente nunca pagos" no lote (glosa branca)

- [x] concluída

**Objetivo:** o lote de adição de guias ganha um filtro `SomenteNuncaPago` (itens com `ValorLiquidado` nulo ou zero), análogo ao `SomenteComGlosa` existente, para alimentar recursos de glosa branca.

**Depende de:** nada das tasks anteriores além do que está colado.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Faturamento/RecursoService.cs:48-52` (record `AdicionarGuiasEmLoteCommand`)
- `apps/backend/App/Faturamento/RecursoService.cs:400-485` (`AdicionarGuiasEmLoteAsync` — ver o bloco `SomenteComGlosa` em :433-439 como molde)
- `apps/backend/App/Faturamento/Endpoints/RecursoEndpoints.cs:94-108,160-164` (handler + `AdicionarGuiasEmLoteRequest`)

**Editar:**

- `RecursoService.cs`: `bool? SomenteNuncaPago` no record `AdicionarGuiasEmLoteCommand` e novo predicado no `AdicionarGuiasEmLoteAsync`.
- `RecursoEndpoints.cs`: `bool? SomenteNuncaPago = null` em `AdicionarGuiasEmLoteRequest` + repassar no command.

**Padrão a seguir (colado — molde do filtro existente):**

```csharp
// existente (parcial): subpago = apurado > liquidado, ambos presentes
if (cmd.SomenteComGlosa == true)
{
    q = q.Where(g => _db.ItensGuia.Any(i =>
        i.GuiaId == g.Id &&
        i.ValorApurado.HasValue && i.ValorLiquidado.HasValue &&
        i.ValorApurado > i.ValorLiquidado));
}

// NOVO (branca): item nunca pago = liquidado nulo ou zero
if (cmd.SomenteNuncaPago == true)
{
    q = q.Where(g => _db.ItensGuia.Any(i =>
        i.GuiaId == g.Id &&
        (!i.ValorLiquidado.HasValue || i.ValorLiquidado == 0m)));
}
```

**Testes (red primeiro):** em `RecursoCrudTests.cs`:

- `Deve incluir no lote guia com item ValorLiquidado nulo quando SomenteNuncaPago`
- `Deve excluir do lote guia totalmente liquidada quando SomenteNuncaPago`

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde

**Commit:** `feat(faturamento): filtro somente-nunca-pago no lote de recurso (TASK-GLOSA-03)`

---

### TASK-GLOSA-04 — PDF: template de glosa branca (sem valores)

- [x] concluída

**Objetivo:** o PDF do recurso respeita `Recurso.Tipo`. Para `GlosaBranca`, o documento lista **só** Código + Descrição por item (sem colunas `Fator`/`PG UNIMED`/`VL CORRETO`) e **sem** o bloco de totais financeiros. `GlosaParcial` mantém o layout atual.

**Depende de:** TASK-GLOSA-01. Contrato:

```csharp
internal enum TipoRecurso { GlosaParcial, GlosaBranca }
// recurso.Tipo já existe e é carregado em ObterDadosPdfAsync.
```

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/backend/App/Faturamento/Pdf/RecursoPdfDocument.cs` (documento inteiro — 156 linhas; QuestPDF)
- `apps/backend/App/Faturamento/RecursoService.cs:54-78` (records `RecursoPdfData`, `GuiaPdfData`, `ItemPdfData`)
- `apps/backend/App/Faturamento/RecursoService.cs:487-602` (`ObterDadosPdfAsync` — onde `recurso.Tipo` está disponível)

**Editar:**

- `RecursoService.cs`: add `TipoRecurso Tipo` ao record `RecursoPdfData`; preencher com `recurso.Tipo` no `ObterDadosPdfAsync`.
- `RecursoPdfDocument.cs`: ramificar `ComposeItensTable` (colunas) e `ComposeContent`/totais por `data.Tipo`. Para branca: header de colunas `["Código", "Descrição"]`, 2 colunas relativas (3 e 8), sem células de valor, e **não** renderizar `ComposeTotaisFinais`.

**Atenção:** `ComposeItensTable` e `ComposeTotaisFinais` são `static` e recebem `(IContainer, GuiaPdfData)` — **não** enxergam `data.Tipo`. Passe `TipoRecurso tipo` como parâmetro (ou remova `static`) para o branch ver o tipo.

**Padrão a seguir (colado — tabela atual a ramificar):**

```csharp
// hoje: 5 colunas fixas. Para GlosaBranca, montar só 2 colunas (Código, Descrição).
private static readonly string[] _colunasParcial =
    ["Código", "Descrição", "Fator", "PG UNIMED", "VL CORRETO"];
private static readonly string[] _colunasBranca = ["Código", "Descrição"];
```

**Testes (red primeiro):** em `apps/backend/tests/Faturamento.Tests/Recurso/RecursoPdfDataTests.cs` (fixture já pronta nessa classe). Como QuestPDF gera bytes, asserir no nível de `RecursoPdfData`/geração sem exceção:

- `Deve gerar PDF de recurso GlosaBranca sem lançar`
- `RecursoPdfData deve carregar Tipo do recurso`
  (se houver helper de extração de texto do PDF no projeto, asserir ausência de "VL CORRETO" na branca; senão, validar o `Tipo` no `RecursoPdfData` e a geração sem exceção.)

**Aceite:**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] `dotnet test apps/backend/Honorare.slnx` verde
- [ ] PDF de branca não contém colunas/totais de valor

**Commit:** `feat(faturamento): template de PDF para glosa branca (TASK-GLOSA-04)`

---

### TASK-GLOSA-05 — Renomear rótulo "Sem apuração" → "Sem cálculo"

- [x] concluída

**Objetivo:** rótulo da operadora `Nulo` fica mais claro na UI admin. Apenas frontend, sem mudança de valor (`'Nulo'` continua no backend/types).

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/admin-web/src/app/admin/catalog/operadoras/operadora-form/operadora-form.component.ts:9-12`
- `apps/admin-web/src/app/admin/catalog/operadoras/operadora-list/operadora-list.component.ts:84-89`

**Editar (substituir a string visível):**

- `operadora-form.component.ts:11` → `{ value: 'Nulo', label: 'Sem cálculo' }`
- `operadora-list.component.ts:88` → retornar `'Sem cálculo'` no lugar de `'Sem apuração'`

**Testes (red primeiro):** ajustar/garantir nos specs existentes (`operadora-list.component.spec.ts`, `operadora-form.component.spec.ts`) que o texto renderizado é "Sem cálculo".

**Aceite:**

- [ ] `pnpm -F admin-web lint` sem warnings
- [ ] `pnpm -F admin-web test:ci` verde
- [ ] nenhuma ocorrência de "Sem apuração" em `apps/admin-web/src`

**Commit:** `refactor(admin-web): renomeia rótulo da operadora Nulo para "Sem cálculo" (TASK-GLOSA-05)`

---

### TASK-GLOSA-06 — Recurso form/list: seletor e badge de Tipo + filtro de lote

- [ ] pendente

**Objetivo:** operador escolhe o tipo (Glosa Parcial / Glosa Branca) ao criar o recurso; a lista mostra um badge do tipo; o filtro de lote expõe "somente nunca pagos".

**Depende de:** TASK-GLOSA-02 (backend aceita/devolve `tipo`) e TASK-GLOSA-03 (`somenteNuncaPago` no lote). Contratos backend:

```
POST /api/v1/admin/recursos  body: { ..., tipo: "GlosaParcial" | "GlosaBranca" }
RecursoDto inclui  tipo: "GlosaParcial" | "GlosaBranca"
POST /api/v1/admin/recursos/{id}/guias/lote  body inclui  somenteNuncaPago?: boolean
```

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md (atenção: em `<select>` Angular use `[selected]` nas `<option>`, **nunca** `[value]` no select; ver MEMORY.md).

**Ler (só isto):**

- `apps/admin-web/src/app/admin/faturamento/recurso.types.ts` (inteiro — 81 linhas)
- `apps/admin-web/src/app/admin/faturamento/recurso-form/recurso-form.component.ts:91-211` (signals + onSubmit/payload)
- `apps/admin-web/src/app/admin/faturamento/recurso-list/recurso-list.component.ts` (onde montar o badge — abrir só se necessário)
- `apps/admin-web/src/app/admin/faturamento/recurso-guias/recurso-guias.component.ts:255-265,443,635-680` (checkbox `filtroSomenteGlosa` é o molde; `somenteComGlosa` é montado em :641 e :677 para os dois fluxos de lote)

**Editar:**

- `recurso.types.ts`: `export type TipoRecurso = 'GlosaParcial' | 'GlosaBranca';` add `tipo: TipoRecurso` em `RecursoForm` e `RecursoDto`; add `somenteNuncaPago?: boolean` em `AdicionarGuiasLoteParams`.
- `recurso-form.component.ts`: signal `tipo = signal<TipoRecurso>('GlosaParcial')`, `<select>` com `[selected]`, incluir `tipo` no payload de criar/editar, carregar do header em `_carregarRecurso`.
- `recurso-list.component.(ts|html)`: badge "Parcial"/"Branca" conforme `r.tipo`.
- `recurso-guias.component.ts`: novo signal `filtroSomenteNuncaPago = signal(false)` + checkbox (molde do `filtroSomenteGlosa` em :255-265) e incluir `somenteNuncaPago: this.filtroSomenteNuncaPago() || undefined` nos **dois** payloads de lote (:641 e :677).

**Padrão `<select>` (colado — `[selected]`, não `[value]`):**

```html
<select (change)="tipo.set($any($event.target).value)">
  <option value="GlosaParcial" [selected]="tipo() === 'GlosaParcial'">Glosa Parcial</option>
  <option value="GlosaBranca" [selected]="tipo() === 'GlosaBranca'">Glosa Branca</option>
</select>
```

**Testes (red primeiro):** `recurso-form.component.spec.ts`:

- `Deve enviar tipo GlosaBranca no payload quando selecionado`
- `Deve usar GlosaParcial como padrão`

**Aceite:**

- [ ] `pnpm -F admin-web lint` sem warnings
- [ ] `pnpm -F admin-web test:ci` verde
- [ ] criar recurso com cada tipo funciona; lista mostra o badge

**Commit:** `feat(admin-web): seleção e exibição do tipo de recurso (parcial/branca) (TASK-GLOSA-06)`

---

### TASK-GLOSA-07 — Formulário de guia enxuto para operadora sem cálculo

- [ ] pendente

**Objetivo:** quando a operadora selecionada na guia é `Nulo` (sem cálculo), o formulário não pede campos que só servem ao motor (posição/ordem/via/acomodação/urgência/tempo anestésico) — só código TUSS + descrição do procedimento. Os badges de cálculo e o bloco financeiro de "Apurado" somem da listagem de itens. Os valores default (`Cirurgiao`, `1.0`, `Convencional`, `Enfermaria`, `false`) continuam sendo emitidos — o backend os ignora para operadora Nulo.

**Depende de:** nada das tasks anteriores. Fato já verificado: `guia-form` carrega a lista `operadoras()` cujos itens têm `tipoRuleSet` (`OperadoraItem.tipoRuleSet: 'Unimed' | 'Nulo'`), então dá para derivar "sem cálculo" sem HTTP extra.

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `apps/admin-web/src/app/admin/faturamento/guia-form/guia-form.component.ts:160-270` (listagem de itens + badges + bloco financeiro + uso de `<app-item-guia-form [operadoraId]=...>`)
- `apps/admin-web/src/app/admin/faturamento/guia-form/guia-form.component.ts:326-372` (signals `operadoras`, `operadoraId`)
- `apps/admin-web/src/app/admin/faturamento/guia-form/item-guia-form/item-guia-form.component.ts:78-263` (template: campos de cálculo a esconder; inputs do componente)

**Editar:**

- `guia-form.component.ts`: computed `operadoraSemCalculo = computed(() => this.operadoras().find(o => o.id === this.operadoraId())?.tipoRuleSet === 'Nulo')`. Esconder, quando `true`: os badges de `posicaoExecutor/percentualOrdem/viaAcesso/acomodacao/urgência/tempo` (bloco `guia-form__item-meta`) e o bloco `guia-form__item-financeiro` (Apurado). Passar `[semCalculo]="operadoraSemCalculo()"` para `<app-item-guia-form>`.
- `item-guia-form.component.ts`: novo `readonly semCalculo = input<boolean>(false);`. Envolver com `@if (!semCalculo()) { ... }` os blocos de Posição, Ordem, Via, Acomodação, Urgência e Tempo anestésico (manter sempre a busca de procedimento e os botões Cancelar/Salvar). `onFormSubmit` continua emitindo os defaults dos signals — não mudar.

**Armadilha (MEMORY.md):** não usar `effect()` para reagir a mudança de input; `computed()` derivado de signals é seguro aqui. Em `<select>` use `[selected]`.

**Testes (red primeiro):** `guia-form.component.spec.ts` e/ou `item-guia-form.component.spec.ts`:

- `Não deve exibir campos de cálculo quando operadora é Nulo`
- `Deve exibir campos de cálculo quando operadora é Unimed`

**Aceite:**

- [ ] `pnpm -F admin-web lint` sem warnings
- [ ] `pnpm -F admin-web test:ci` verde
- [ ] guia de operadora Unimed inalterada; guia de operadora "Sem cálculo" mostra só procedimento

**Commit:** `feat(admin-web): formulário de guia enxuto para operadora sem cálculo (TASK-GLOSA-07)`

---

## Checklist final

- [x] TASK-GLOSA-01 — Recurso.Tipo + migration
- [x] TASK-GLOSA-02 — service/endpoint propagam Tipo + validação Nulo×Parcial
- [x] TASK-GLOSA-03 — filtro somente-nunca-pago no lote
- [x] TASK-GLOSA-04 — template PDF glosa branca
- [x] TASK-GLOSA-05 — rótulo "Sem cálculo"
- [ ] TASK-GLOSA-06 — seletor/badge de tipo no recurso (frontend)
- [ ] TASK-GLOSA-07 — formulário de guia enxuto sem cálculo

## Fora de escopo (decisões registradas)

- **Tipo no nível do recurso**, não por item. Uma guia com itens subpagos E nunca-pagos entra em recursos separados (um parcial, um branca) — não há classificação item a item.
- Reaproveitar `TipoRuleSet.Nulo` existente; **não** criar um terceiro tipo de operadora.
- Toques a jusante de guias sem valor (faturamento/lote, reporting/conta-corrente) **não** são tratados aqui — levantar em spec própria se necessário.

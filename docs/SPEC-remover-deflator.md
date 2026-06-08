# Remover feature de Deflator (Caminho A — sempre 100%)

## Decisão

`DeflatorPrestador.Percentual` é **sempre 100%** na prática (todos os 10 cenários E2E
ground-truth usam `100m`; os únicos valores ≠100 estão em testes de matemática pura).
Logo `valor_base = TabelaProcedimento.Valor × 1.0 = TabelaProcedimento.Valor`, e a entidade
é puro atrito de cadastro. **Elimina-se a entidade por completo.**

Consequência: afrouxa **D-038** (deflator deixa de ser motivo de rejeição pré-voo).
`SemTabela` e `Indeterminado` permanecem. Registrar **D-044** revertendo a parte de deflator
da D-038.

NÃO confundir com os descontos de posição auxiliar (1ºaux ×0.6 · 2º ×0.4 · 3º ×0.3) — esses
vivem em `PosicaoExecutorModifier` e **continuam** no motor, intocados.

Estado inicial: as 3 classes puramente-deflator já foram deletadas para forçar build vermelho
(`DeflatorPrestador.cs`, `DeflatorPrestadorConfiguration.cs`, `DeflatorServiceTests.cs`).
**Use os erros do compilador como worklist.**

---

## Sessão 1 — Backend: build verde + migration ✅ concluída

**Objetivo:** de "build vermelho (classes deletadas)" → `dotnet build` verde + migration que dropa a tabela.

### 1.1 `Data/AppDbContext.cs`

Remover linha 23: `public DbSet<DeflatorPrestador> DeflatoresPrestador => Set<DeflatorPrestador>();`

### 1.2 `Catalog/CatalogService.cs`

Deletar: records `DeflatorDto` (l.43) e `SalvarDeflatorCommand` (l.46); métodos
`ListarDeflatoresAsync`, `CriarDeflatorAsync`, `AtualizarDeflatorAsync`, `ExcluirDeflatorAsync`,
`ValidarComandoDeflator` (bloco l.1062–fim). Sem refs órfãs a `PosicaoExecutor`? deixar import.

### 1.3 `Catalog/Endpoints/CatalogEndpoints.cs`

Deletar: `MapGroup(".../deflatores")` + 4 `Map*` (l.46–52); handlers `ListarDeflatoresAsync`/
`CriarDeflatorAsync`/`AtualizarDeflatorAsync`/`ExcluirDeflatorAsync` (l.480–533); record
`SalvarDeflatorRequest` (l.788).

### 1.4 `Faturamento/Calculo/CalculoTypes.cs`

Remover `SemDeflator,` do enum `SituacaoApuracao` (l.9).

### 1.5 `Faturamento/Calculo/Unimed/UnimedRuleSet.cs`

**Cirúrgico** (`ApurarItemAsync`): apagar query `deflator` (l.40–42) e early-exit (l.44–47).
Trocar l.53–55 por:

```csharp
var valorBase = tabela.Valor;
var passos = new List<PassoApuracao> { new("ValorBase", 1.0m, valorBase) };
```

**Anestesia** (`ApurarAnestesistaAsync`): apagar query `deflator` (l.96–99) e early-exit (l.101–104).
Trocar chamada (l.106–108) por:

```csharp
var (valorFinal, passos) = AnestesiaCalculator.Calcular(
    valorReferencia, item.PercentualOrdem, item.EhUrgencia, procedimento.EhSadt);
```

### 1.6 `Faturamento/Calculo/Unimed/AnestesiaCalculator.cs`

Remover parâmetro `decimal deflatorPercentual`. Trocar início do corpo por:

```csharp
var valorAtual = valorReferencia;
passos.Add(new PassoApuracao("ValorBase", 1.0m, valorAtual));
```

### 1.7 `Faturamento/ImportacaoGuiaCsvService.cs`

Apagar o bloco `if (operadora.TipoRuleSet != TipoRuleSet.Nulo) { ... }` que valida `temDeflator`
(l.90–113). A importação não pré-valida deflator.

### 1.8 `Faturamento/GuiaService.cs`

Linha ~692: trocar `"Verifique deflators, tabelas de procedimento e portes anestésicos."`
por `"Verifique tabelas de procedimento e portes anestésicos."`

### 1.9 Migration

```bash
cd apps/backend/App
dotnet ef migrations add RemoveDeflatorPrestador --output-dir Migrations --namespace App.Migrations
```

Conferir que o `Up()` só faz `DropTable("DeflatoresPrestador")` (nome real conforme snapshot).

### Critério de saída

`dotnet build apps/backend/Honorare.slnx` **verde**. Migration gerada. (Testes ainda podem
falhar — Sessão 2.)

---

## Sessão 2 — Backend TDD: ajustar/remover testes ✅ concluída

**Requer Sessão 1.** Rodar `dotnet test apps/backend/Honorare.slnx`, corrigir até verde + cobertura.

### 2.1 Remover setup de deflator dos testes que calculam

Em todo teste que faz `DeflatorPrestador.Create(...)`: apagar a(s) linha(s). Como base agora é
100%, os valores esperados de `valor_base` passam a ser o valor de tabela cheio — **ajustar
assertions** onde o fixture usava 70m/80m:

```
Faturamento.Tests/Calculo/Unimed/UnimedRuleSetValorBaseTests.cs   (80m → base = tabela)
Faturamento.Tests/Calculo/Unimed/UnimedAnestesiaPipelineTests.cs
Faturamento.Tests/Calculo/Unimed/UnimedPipelineTests.cs           (100m → assertion já bate)
Faturamento.Tests/Calculo/Unimed/AnestesiaCalculatorTests.cs      (assinatura mudou)
Faturamento.Tests/Demonstrativo/ImportacaoGuiaCsvTests.cs         (70m + caso "sem deflator")
Faturamento.Tests/Recurso/RecursoCrudTests.cs                     (70m)
Faturamento.Tests/Recurso/RecursoPdfDataTests.cs
Faturamento.Tests/Calculo/GuiaServiceCalculoTests.cs
Faturamento.Tests/Calculo/GuiaCalculoEndpointTests.cs
Faturamento.Tests/Guia/*  (GuiaCrudTests, GuiaListTests, GuiaListHttpTests, GuiaEndpointTests,
                            GuiaLocalAtendimentoTests)
```

### 2.2 Deletar testes que validavam `SemDeflator`/early-exit/pré-voo

Casos cujo único propósito era provar rejeição por deflator ausente (em
`GuiaServiceCalculoTests`, `ImportacaoGuiaCsvTests`, `CalculoTypesTests`, `CalculoSchemaTests`,
`CatalogSchemaTests`) — remover. Buscar por `SemDeflator` e `temDeflator`/"Cadastre o deflator".

### 2.3 Schema tests

`CatalogSchemaTests.cs` / `CalculoSchemaTests.cs`: remover qualquer assert sobre a tabela
`DeflatoresPrestador` ou o enum `SemDeflator`.

### Critério de saída

`dotnet test apps/backend/Honorare.slnx` **verde**, cobertura ≥ limites (Faturamento 90%).

---

## Sessão 3 — Frontend: remover UI de deflator ✅ concluída

**Requer Sessão 1** (contratos do backend já mudaram).

### 3.1 admin-web — catálogo (CRUD de deflator)

```
catalog/catalog.types.ts      → del interfaces DeflatorItem (l.133), SalvarDeflatorPayload (l.141)
catalog/catalog.service.ts     → del imports + métodos listar/criar/atualizar/excluirDeflator
catalog/prestadores/prestador-form/prestador-form.component.{ts,html,scss}
                               → remover a aba/seção de deflatores inteira (29 refs ts / 30 html)
catalog/prestadores/prestador-form/prestador-form.component.spec.ts → del specs de deflator
```

### 3.2 admin-web — situação `SemDeflator`

```
faturamento/guia.types.ts (l.128)              → remover 'SemDeflator' do union
faturamento/calculo-detalhe/calculo-detalhe.component.ts (l.65) → remover map badge SemDeflator
faturamento/calculo-detalhe/calculo-detalhe.component.scss      → remover .badge--sem-deflator
faturamento/guia-form/guia-form.component.spec.ts (l.295–404)   → atualizar fixtures de erro
                                                  (texto agora sem "deflators"; usar SemTabela)
```

### 3.3 medico-pwa — situação `SemDeflator`

```
guias/medico-guia.types.ts (l.5)        → remover 'SemDeflator' do union
guias/guia-detalhe/guia-detalhe.ts (l.517, l.527) → remover SemDeflator da condição e do label map
```

### Critério de saída

`pnpm -F admin-web lint && pnpm -F admin-web test:ci` e idem `medico-pwa` — **verde**.
`grep -rli deflator apps/admin-web/src apps/medico-pwa/src` **vazio**.

---

## Sessão 4 — Docs

### 4.1 `docs/DECISOES.md`

Adicionar **D-044**: deflator por prestador removido (sempre 100%); emenda a D-038 — deflator
deixa de ser pré-requisito de criação/importação; `SemTabela`/`Indeterminado` permanecem.

### 4.2 `docs/DOMINIO.md`

- Fórmula `valor_base`: passa a `valor_base = TabelaProcedimento.Valor`.
- Remover `DeflatorPrestador`, `Deflator (negociado)`, `SemDeflator` das listas/early-exits.
- Manter descontos de posição auxiliar (`PosicaoExecutorModifier`).

### 4.3 `CLAUDE.md`

Atualizar o bullet "Deflator é mandatório" em **Key Architectural Constraints** (D-038) para
refletir que o deflator-por-prestador foi removido.

### Critério de saída

`grep -rli "DeflatorPrestador" docs CLAUDE.md` só retorna menções históricas em D-038/D-044.

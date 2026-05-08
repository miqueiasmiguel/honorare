# SPEC F3.3 — Anestesia (Motor de Cálculo UNIMED)

**Pré-requisito:** F3.2 concluído. `Procedimento.PorteAnestesico` (int?, 1–8) já existe.
**Pós-condição:** `PosicaoExecutor.Anestesista` produz `SituacaoApuracao.Calculado` com trace completo, em vez de `Indeterminado`.

---

## Fórmula

```
Pipeline (ordem obrigatória):

1. ValorBase     = TabelaProcedimento.Valor × (DeflatorAnestesista / 100)
2. UnimedAN      = ValorBase × 1.1719           ← 17,19% sobre CBHPM 2015
3. OrdemProc     = Único/Principal×1.0 · SecMesmaVia×0.5 · SecViaDif×0.7
4. Acomodacao    = Apartamento×2.0 · Ambulatorial/Enfermaria×1.0
5. Urgencia      = EhUrgencia && !EhSadt → ×1.3 · senão ×1.0
6. TempoExtra    = (detalhes abaixo)

ValorFinal = ValorApos5 + AcrescimoTempo
```

### Passo 6 — Tempo anestésico extra

```
TempoBaseMin = TempoBasePorPorte[PorteAN]   ← tabela abaixo
TempoExtraHoras = max(0, ceil((TempoAnestesicoMin - TempoBaseMin) / 60.0))
FatorExtra = PorteAN <= 4 ? 0.30m : 0.50m
AcrescimoTempo = TempoExtraHoras × FatorExtra × (ValorBase × 1.1719)
```

**Nota:** acréscimo de tempo incide sobre `ValorBase × 1.1719` (não sobre o valor pós-acomodação/urgência). A validar contra casos reais (P0.2).

### Tabela TempoBasePorPorte

| PA  | Minutos base |
| --- | ------------ |
| 1   | 60           |
| 2   | 90           |
| 3   | 120          |
| 4   | 150          |
| 5   | 180          |
| 6   | 240          |
| 7   | 300          |
| 8   | 360          |

### Condições de saída antecipada (mantidas de F3.2)

| Condição                                        | Situação        | ValorApurado |
| ----------------------------------------------- | --------------- | ------------ |
| `TabelaProcedimento` não existe                 | `SemTabela`     | null         |
| `DeflatorPrestador` para Anestesista não existe | `SemDeflator`   | null         |
| `PorteAnestesico` nulo no procedimento          | `Indeterminado` | null         |
| Pipeline executado com sucesso                  | `Calculado`     | decimal      |

---

## Arquivos-chave

```
App/Faturamento/
  ItemGuia.cs                                     ← adicionar TempoAnestesicoMin
  Migrations/                                     ← AddTempoAnestesicoMin
  Calculo/Unimed/
    AnestesiaCalculator.cs                        ← novo (puro, sem DB)
    UnimedRuleSet.cs                              ← integrar AnestesiaCalculator

tests/Faturamento.Tests/Calculo/Unimed/
  AnestesiaCalculatorTests.cs                     ← novo (TASK-AN-02)
  UnimedAnestesiaPipelineTests.cs                 ← novo (TASK-AN-03)

apps/admin-web/src/app/faturamento/
  guia.types.ts                                   ← adicionar tempoAnestesicoMin
  item-guia-form/item-guia-form.component.ts      ← campo condicional
  item-guia-form/item-guia-form.component.spec.ts ← testes Vitest
```

---

## TASK-AN-01 — Schema: TempoAnestesicoMin em ItemGuia ✅

**Sessão única. TDD: schema test → migration → build.**

### O que fazer

1. Adicionar `TempoAnestesicoMin int?` a `ItemGuia`:
   - Propriedade `public int? TempoAnestesicoMin { get; private set; }`
   - Parâmetro opcional em `ItemGuia.Create(... int? tempoAnestesicoMin = null)`
   - Setter interno: `internal void SetTempoAnestesicoMin(int? valor) => TempoAnestesicoMin = valor;`

2. EF Core config (`ItemGuiaConfiguration` ou inline):
   - `.Property(x => x.TempoAnestesicoMin).IsRequired(false)`

3. Atualizar `CriarItemGuiaCommand` / `AtualizarItemGuiaCommand` em `GuiaService`:
   - Adicionar `int? TempoAnestesicoMin` ao record de comando
   - Passar para `ItemGuia.Create`

4. Atualizar endpoints (`GuiaEndpoints.cs`): mapear o novo campo do body JSON para o comando.

5. Criar migration `AddTempoAnestesicoMin` com `.editorconfig` na pasta:
   ```ini
   [*.cs]
   dotnet_diagnostic.IDE0005.severity = none
   dotnet_diagnostic.IDE0161.severity = none
   dotnet_diagnostic.CA1515.severity = none
   dotnet_diagnostic.CA1861.severity = none
   ```

### Testes (escrever primeiro — TDD)

Arquivo: `tests/Faturamento.Tests/Calculo/AnestesiaSchemaTests.cs`

```csharp
// [Fact] TempoAnestesicoMin_NulloPorPadrao
// Criar ItemGuia sem tempo → TempoAnestesicoMin == null no banco

// [Fact] TempoAnestesicoMin_Persistido
// Criar Guia com item Anestesista, TempoAnestesicoMin = 180
// → ler do banco → TempoAnestesicoMin == 180

// [Fact] TempoAnestesicoMin_NaoObrigatorioParaNaoAnestesista
// Criar item Cirurgião sem campo → build sem erro, valor nulo
```

**Critério de pronto:** `dotnet test` passa; `dotnet build` limpo; migration aplicada.

---

## TASK-AN-02 — AnestesiaCalculator (puro, sem DB)

**Sessão única. TDD: todos os testes antes do código de produção.**

### O que fazer

Criar `App/Faturamento/Calculo/Unimed/AnestesiaCalculator.cs` como classe `internal static`:

```csharp
internal static class AnestesiaCalculator
{
    // tabela interna: PorteAN → minutos base
    private static readonly IReadOnlyDictionary<int, int> TempoBase = new Dictionary<int, int>
    {
        [1] = 60, [2] = 90, [3] = 120, [4] = 150,
        [5] = 180, [6] = 240, [7] = 300, [8] = 360,
    };

    internal static (decimal valorFinal, IReadOnlyList<PassoApuracao> passos)
        Calcular(
            decimal valorTabela,
            decimal deflatorPercentual,
            int porteAnestesico,
            int? tempoAnestesicoMin,
            OrdemProcedimento ordem,
            Acomodacao acomodacao,
            bool ehUrgencia,
            bool ehSadt);
}
```

**Nenhuma dependência de DB.** Retorna tuple com valor e lista de passos (trace).

### Testes (escrever primeiro)

Arquivo: `tests/Faturamento.Tests/Calculo/Unimed/AnestesiaCalculatorTests.cs`

```
[Fact] Basico_SemTempoExtra_Enfermaria
  valorTabela=1000, deflator=100, PA=5, tempo=180 (= base), ordem=Unico, acomodacao=Enfermaria, urgencia=false
  esperado = 1000 × 1.0 × 1.1719 × 1.0 × 1.0 × 1.0 = 1171.90

[Fact] ComMultiplicadorApartamento
  mesmos params, acomodacao=Apartamento
  esperado = 1000 × 1.1719 × 2.0 = 2343.80

[Fact] ComUrgencia
  acomodacao=Enfermaria, ehUrgencia=true, ehSadt=false
  esperado = 1000 × 1.1719 × 1.3 = 1523.47

[Fact] ComTempoExtraPA5_UmaHora
  PA=5, tempoAnestesicoMin=240 (60 min extra), FatorExtra=0.50
  AcrescimoTempo = 1 × 0.50 × (1000 × 1.1719) = 585.95
  esperado = 1171.90 + 585.95 = 1757.85

[Fact] ComTempoExtraPA3_UmaHora
  PA=3, tempoAnestesicoMin=180 (60 min extra), FatorExtra=0.30
  AcrescimoTempo = 1 × 0.30 × (1000 × 1.1719) = 351.57
  esperado = 1171.90 + 351.57 = 1523.47

[Fact] TempoNulo_NaoAplicaAcrescimo
  PA=5, tempoAnestesicoMin=null → sem acréscimo, valor = 1171.90

[Fact] TempoMenorQueBase_NaoAplicaAcrescimo
  PA=5, tempoAnestesicoMin=120 (< 180 base) → sem acréscimo

[Fact] OrdemSecundarioMesmaVia_Aplicada
  ordem=SecundarioMesmaVia → fator 0.5 aplicado após UnimedAN
  esperado = 1000 × 1.1719 × 0.5 = 585.95

[Fact] Trace_ContemTodosPassosAplicados
  verificar que passos retornados incluem: ValorBase, UnimedAN,
  OrdemProcedimento (se != 1.0), Acomodacao (se != 1.0),
  Urgencia (se != 1.0), TempoExtra (se aplicável)

[Fact] PaForaDaFaixa_LancaArgumentException
  porteAnestesico=9 → ArgumentOutOfRangeException
```

**Critério de pronto:** todos os testes passam; `dotnet build` limpo; sem dependência de EF/banco.

---

## TASK-AN-03 — Integração no UnimedRuleSet

**Sessão única. TDD: pipeline E2E tests com Postgres → produção.**

### O que fazer

Em `UnimedRuleSet.ApurarItemAsync`:

```csharp
if (item.Posicao == PosicaoExecutor.Anestesista)
{
    // buscar tabela, deflator, procedimento (igual ao path normal)
    // verificar SemTabela / SemDeflator
    // verificar PorteAnestesico não nulo → senão Indeterminado
    var (valorFinal, passos) = AnestesiaCalculator.Calcular(
        tabela.Valor, deflator.Percentual,
        procedimento.PorteAnestesico!.Value,
        item.TempoAnestesicoMin,
        item.Ordem, item.Acomodacao, item.EhUrgencia, procedimento.EhSadt);
    return new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.Calculado, valorFinal, passos);
}
```

Atualizar `ApurarItemInput` para incluir `int? TempoAnestesicoMin`.
Atualizar `GuiaService` para popular `TempoAnestesicoMin` no contexto de apuração.

### Testes (escrever primeiro)

Arquivo: `tests/Faturamento.Tests/Calculo/Unimed/UnimedAnestesiaPipelineTests.cs`

Seguir mesmo padrão de `UnimedPipelineTests` (PostgresContainerFixture, SeedAsync, Build).

```
[Fact] Anestesista_PA5_SemTempo_Enfermaria_Calculado
  seed: TabelaProcedimento.Valor=1000, Deflator=100%, proc.PorteAnestesico=5
  item: Anestesista, Enfermaria, SemUrgencia, TempoAnestesicoMin=null
  esperado: Situacao=Calculado, ValorApurado=1171.90m

[Fact] Anestesista_PA5_SemPorteAnestesico_Indeterminado
  proc.PorteAnestesico=null
  esperado: Situacao=Indeterminado, ValorApurado=null

[Fact] Anestesista_SemTabela_RetornaSemTabela
  sem TabelaProcedimento no seed
  esperado: Situacao=SemTabela

[Fact] Anestesista_SemDeflator_RetornaSemDeflator
  sem DeflatorPrestador para Anestesista no seed
  esperado: Situacao=SemDeflator

[Fact] Anestesista_PA5_Apartamento_Calculado
  esperado: ValorApurado=2343.80m

[Fact] Anestesista_PA5_ComTempoExtra_UmaHora_Calculado
  TempoAnestesicoMin=240
  esperado: ValorApurado=1757.85m

[Fact] Anestesista_PA5_Urgencia_Calculado
  EhUrgencia=true, EhSadt=false
  esperado: ValorApurado=1523.47m

[Fact] Guia_ComAnestesista_TracePersistePassos
  criar guia → obter calculo → ItemCalculoDto do anestesista tem passos >= 2
```

**Critério de pronto:** todos os testes passam; build limpo; `Anestesista_RetornaIndeterminadoAsync` do F3.2 pode ser removido ou atualizado.

---

## TASK-AN-04 — UI Angular: campo TempoAnestesicoMin

**Sessão única. TDD: testes Vitest → componente → build.**

### O que fazer

1. `guia.types.ts`: adicionar `tempoAnestesicoMin?: number | null` a `ItemGuiaForm` e `ItemGuiaDto`.

2. `ItemGuiaFormComponent`:
   - Signal: `tempoAnestesicoMin = signal<number | null>(null)`
   - Campo `<input type="number">` visível **somente quando** `posicaoExecutor() === 'Anestesista'`
   - Label: "Tempo anestésico (min)"
   - Ao emitir o item, incluir `tempoAnestesicoMin: this.tempoAnestesicoMin() || null`
   - Ao carregar item existente, setar: `this.tempoAnestesicoMin.set(item.tempoAnestesicoMin ?? null)`

3. Regenerar cliente OpenAPI após mudança de contrato: `pnpm generate-api-client`

### Testes (escrever primeiro)

Arquivo: `item-guia-form.component.spec.ts` (adicionar aos testes existentes)

```
[it] campo tempo oculto para Cirurgiao
  setInput posicaoExecutor='Cirurgiao' → fixture.detectChanges()
  expect(el.querySelector('[data-testid="tempo-anestesico"]')).toBeNull()

[it] campo tempo visível para Anestesista
  setInput posicaoExecutor='Anestesista' → detectChanges
  expect(el.querySelector('[data-testid="tempo-anestesico"]')).not.toBeNull()

[it] emite tempoAnestesicoMin ao mudar campo
  setar Anestesista → digitar 180 no campo → triggerEventHandler('input', ...)
  expect(emittedItem.tempoAnestesicoMin).toBe(180)

[it] emite null quando campo vazio
  Anestesista → campo vazio → emittedItem.tempoAnestesicoMin === null

[it] popula campo ao carregar item existente com tempo
  item.tempoAnestesicoMin=120 → component.carregarItem(item)
  expect(tempoAnestesicoMin()).toBe(120)
```

**Nota Angular JIT:** para signal inputs, usar `Object.assign(component, { posicaoExecutor: signal('Anestesista') })` se `setInput` falhar com NG0303 (ver memória `feedback_angular_disabled_signal_input_jit`).

**Critério de pronto:** `pnpm -F admin-web test:ci` passa; `pnpm -F admin-web lint` sem warnings; campo aparece/some corretamente na tela.

---

## Resumo de entregáveis por task

| Task     | Backend | Frontend | Migration | Testes novos                 |
| -------- | ------- | -------- | --------- | ---------------------------- |
| AN-01 ✅ | ✓       | —        | ✓         | AnestesiaSchemaTests         |
| AN-02    | ✓       | —        | —         | AnestesiaCalculatorTests     |
| AN-03    | ✓       | —        | —         | UnimedAnestesiaPipelineTests |
| AN-04    | —       | ✓        | —         | item-guia-form.spec.ts       |

**Após AN-03:** atualizar `PROXIMOS_PASSOS.md` marcando F3.3 como ✅ e descrever entregues.

# SPEC-F3.2 — Motor de Cálculo UNIMED

**Contexto:** F3.1 entregou guias com `ValorApurado` manual. F3.2 calcula `ValorApurado` automaticamente ao salvar a guia (exceto pacotes e anestesia). F3.3 implementa o `AnestesiaCalculator`.

**Não implementar agora:** anestesia (F3.3), conciliação (F3.4), NullRuleSet para UI (apenas backend).

---

## Modelo de dados — novas entidades

```
Calculo (table: calculos)
  Id              uuid PK
  TenantId        uuid NOT NULL (ITenantEntity, query filter)
  GuiaId          uuid NOT NULL FK → guias RESTRICT
  RealizadoEm     timestamptz NOT NULL

PassoCalculo (table: passos_calculo)
  Id              uuid PK
  CalculoId       uuid NOT NULL FK → calculos CASCADE
  ItemGuiaId      uuid NOT NULL FK → itens_guia RESTRICT
  Sequencia       int NOT NULL
  Regra           varchar(100) NOT NULL   -- ex: "ValorBase", "OrdemProcedimento", "Urgencia"
  Fator           decimal(10,6) NOT NULL  -- multiplicador aplicado neste passo
  ValorResultante decimal(18,2) NOT NULL  -- valor após aplicar Fator
```

**Alteração em `ItemGuia`:** nenhuma — o `ValorApurado` já existe como `decimal?`.

---

## Arquitetura do motor

```
App/Faturamento/Calculo/
  IPricingRuleSet.cs         ← interface pública do motor
  CalculoTypes.cs            ← records e enums de resultado
  NullRuleSet.cs             ← operadoras sem tabela UNIMED
  Unimed/
    UnimedRuleSet.cs         ← pipeline UNIMED
    Modifiers/
      OrdemProcedimentoModifier.cs
      VideolaparoscopiaModifier.cs
      AcomodacaoModifier.cs
      UrgenciaModifier.cs
      PosicaoExecutorModifier.cs
```

### Interface

```csharp
internal interface IPricingRuleSet
{
    Task<IReadOnlyList<ApuracaoItemResult>> ApurarAsync(
        ApurarGuiaContext ctx, CancellationToken ct = default);
}
```

### Types

```csharp
// Entrada do motor
internal sealed record ApurarGuiaContext(
    Guid TenantId, Guid PrestadorId, Guid OperadoraId,
    IReadOnlyList<ApurarItemInput> Itens);

internal sealed record ApurarItemInput(
    Guid ItemGuiaId, Guid ProcedimentoId, PosicaoExecutor Posicao,
    OrdemProcedimento Ordem, ViaAcesso Via, Acomodacao Acomodacao,
    bool EhUrgencia);

// Saída por item
internal sealed record ApuracaoItemResult(
    Guid ItemGuiaId,
    SituacaoApuracao Situacao,
    decimal? ValorApurado,
    IReadOnlyList<PassoApuracao> Passos);

internal sealed record PassoApuracao(
    string Regra, decimal Fator, decimal ValorResultante);

internal enum SituacaoApuracao
{
    Calculado,       // pipeline rodou com sucesso
    SemTabela,       // TabelaProcedimento não encontrada
    SemDeflator,     // DeflatorPrestador não encontrado
    Indeterminado,   // Anestesia (aguarda F3.3) ou outra razão
}
```

---

## Pipeline UnimedRuleSet (ordem obrigatória)

| #   | Passo              | Regra (string)        | Fator                                                                               |
| --- | ------------------ | --------------------- | ----------------------------------------------------------------------------------- |
| 1   | Valor base         | `"ValorBase"`         | `TabelaProcedimento.Valor × (Deflator.Percentual / 100)`                            |
| 2   | Ordem procedimento | `"OrdemProcedimento"` | Único/Principal=1.0 · SecundarioMesmaVia=0.5 · SecundarioViaDiferente=0.7           |
| 3   | Via laparoscópica  | `"Videolaparoscopia"` | Videolaparoscopia e !TemPorteProprioVideo → 1.5; senão 1.0                          |
| 4   | Acomodação         | `"Acomodacao"`        | Apartamento → 2.0; Ambulatorial/Enfermaria → 1.0                                    |
| 5   | Urgência           | `"Urgencia"`          | EhUrgencia e !EhSadt → 1.3; senão 1.0                                               |
| 6   | Posição executor   | `"PosicaoExecutor"`   | PrimeiroAuxiliar → 0.6; SegundoAuxiliar → 0.4; TerceiroAuxiliar → 0.3; demais → 1.0 |

**Anestesista:** retorna `SituacaoApuracao.Indeterminado` sem executar pipeline (F3.3).

**Fórmula geral:** `valor = passo_anterior × fator_atual` (encadeado).

---

## DI e seleção de RuleSet

Registrar ambos no DI. `GuiaService` recebe `IEnumerable<IPricingRuleSet>` e usa método de fábrica ou seleciona por tipo de operadora:

```csharp
// Program.cs
builder.Services.AddScoped<IPricingRuleSet, UnimedRuleSet>();
builder.Services.AddScoped<NullRuleSet>();

// GuiaService injeta IEnumerable<IPricingRuleSet> e IServiceProvider,
// ou usa uma fábrica simples que retorna UnimedRuleSet para TipoRuleSet.Unimed
// e NullRuleSet para demais.
```

Alternativa mais simples: `PricingRuleSetFactory` (static ou scoped) que recebe `TipoRuleSet` e retorna o serviço correto.

---

## Integração com GuiaService

`GuiaService.CriarAsync` e `AtualizarAsync` devem, após salvar os itens:

1. Verificar `EhPacote` → se verdadeiro, **não invocar** o motor (ValorApurado já foi informado manualmente).
2. Buscar `operadora.TipoRuleSet`, selecionar o `IPricingRuleSet` correto.
3. Montar `ApurarGuiaContext` com os itens salvos.
4. Chamar `ApurarAsync`.
5. Para cada `ApuracaoItemResult` com `Situacao == Calculado`, atualizar `ItemGuia.ValorApurado` via `ItemGuia.SetValorApurado(decimal valor)` (novo método interno).
6. Persistir `Calculo` + `PassoCalculo` na mesma transação.
7. Salvar uma única vez (`SaveChangesAsync`).

Em `AtualizarAsync`: excluir `Calculo` anterior da guia (cascade deleta `PassoCalculo`) antes de recalcular.

---

## Tasks

> Cada task segue Red → Green → Refactor. Escreva os testes ANTES do código de produção. O build deve compilar (sem erros) após o Red — crie skeletons vazios se necessário. Os testes devem **falhar** antes do Green.

---

### TASK-F32-01 — Types e interface [x] concluída

**Escopo:** `App/Faturamento/Calculo/IPricingRuleSet.cs`, `App/Faturamento/Calculo/CalculoTypes.cs`
**Depende de:** nenhuma

**Red — escrever os testes:**
Criar `Faturamento.Tests/Calculo/CalculoTypesTests.cs` com os cenários abaixo. Para o projeto compilar, criar os arquivos de escopo com apenas as declarações (sem lógica):

```csharp
// skeleton: App/Faturamento/Calculo/CalculoTypes.cs
internal enum SituacaoApuracao { Calculado, SemTabela, SemDeflator, Indeterminado }
internal sealed record PassoApuracao(string Regra, decimal Fator, decimal ValorResultante);
internal sealed record ApuracaoItemResult(Guid ItemGuiaId, SituacaoApuracao Situacao,
    decimal? ValorApurado, IReadOnlyList<PassoApuracao> Passos);
internal sealed record ApurarItemInput(Guid ItemGuiaId, Guid ProcedimentoId,
    PosicaoExecutor Posicao, OrdemProcedimento Ordem, ViaAcesso Via,
    Acomodacao Acomodacao, bool EhUrgencia);
internal sealed record ApurarGuiaContext(Guid TenantId, Guid PrestadorId,
    Guid OperadoraId, IReadOnlyList<ApurarItemInput> Itens);
internal interface IPricingRuleSet
{
    Task<IReadOnlyList<ApuracaoItemResult>> ApurarAsync(
        ApurarGuiaContext ctx, CancellationToken ct = default);
}
```

Cenários de teste (namespace: `Faturamento.Tests.Calculo`):

- `SituacaoApuracao_PossuiExatamente4Membros` — `Enum.GetValues<SituacaoApuracao>()` tem 4 elementos: `Calculado`, `SemTabela`, `SemDeflator`, `Indeterminado`
- `ApuracaoItemResult_Calculado_ValorApuradoNaoNulo` — construir com `Situacao=Calculado`, `ValorApurado=100m`; `ValorApurado` não é nulo
- `ApuracaoItemResult_SemTabela_ValorApuradoNulo` — construir com `Situacao=SemTabela`, `ValorApurado=null`; `ValorApurado` é nulo
- `PassoApuracao_ArmazenaPropriedadesCorretamente` — `new PassoApuracao("ValorBase", 0.8m, 80m)` retorna os valores corretos

**Green — declarar os types completos:**
Os records já estão no skeleton — confirmar que cobrem todos os campos da seção "Types" desta spec. `IPricingRuleSet` declarado e sem implementação nesta task.

**Refactor:** nenhum.

**Critério:** `dotnet build` limpo; 4 testes verdes; sem acesso a DB.

---

### TASK-F32-02 — Entidades Calculo + PassoCalculo + migration [x] concluída

**Escopo:**

- `App/Faturamento/Calculo.cs`
- `App/Faturamento/PassoCalculo.cs`
- `App/Faturamento/Configurations/CalculoConfiguration.cs`
- `App/Faturamento/Configurations/PassoCalculoConfiguration.cs`
- `App/Data/AppDbContext.cs` (adicionar `DbSet<Calculo>` e `DbSet<PassoCalculo>`)
- Nova migration `AddCalculo` em `App/Faturamento/Migrations/` (o `.editorconfig` existente já cobre)

**Depende de:** TASK-F32-01

**Red — escrever os testes:**
Criar `Faturamento.Tests/Calculo/CalculoSchemaTests.cs` (namespace `Faturamento.Tests.Calculo`). Para compilar, criar skeletons mínimos das entidades com as propriedades sem setter privado ainda:

```csharp
// skeleton inicial — propriedades públicas para compilar; Green tornará private set
internal sealed class Calculo : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid GuiaId { get; set; }
    public DateTimeOffset RealizadoEm { get; set; }
    internal static Calculo Create(Guid tenantId, Guid guiaId) => throw new NotImplementedException();
}
internal sealed class PassoCalculo
{
    public Guid Id { get; set; }
    public Guid CalculoId { get; set; }
    public Guid ItemGuiaId { get; set; }
    public int Sequencia { get; set; }
    public string Regra { get; set; } = "";
    public decimal Fator { get; set; }
    public decimal ValorResultante { get; set; }
    internal static PassoCalculo Create(Guid calculoId, Guid itemGuiaId, int seq,
        string regra, decimal fator, decimal valorResultante) => throw new NotImplementedException();
}
```

Cenários de teste (requerem `PostgresContainerFixture`):

- `Calculo_PersistidoERecuperado` — criar `Calculo` via `Calculo.Create(tenantId, guiaId)`, salvar, buscar por Id; verificar `TenantId`, `GuiaId`, `RealizadoEm` não-nulo
- `PassoCalculo_PersistidoERecuperado` — criar `Calculo` + `PassoCalculo` via `PassoCalculo.Create(...)`, salvar, buscar; verificar todos os campos
- `PassoCalculo_SequenciaUnicaPorCalculo` — inserir dois `PassoCalculo` com mesmo `CalculoId` e mesma `Sequencia`; `SaveChangesAsync` lança `DbUpdateException`
- `Calculo_QueryFilterIsolaTenant` — tenant A cria `Calculo`; tenant B não enxerga via `CreateTenantContext`

**Green — implementar entidades e EF:**

- Entidades com `private set`, factory methods `Create(...)` retornando instância (não `throw`)
- `CalculoConfiguration`: `HasQueryFilter` por `TenantId`; FK `GuiaId` com `DeleteBehavior.Restrict`; `HasIndex(c => c.GuiaId)`
- `PassoCalculoConfiguration`: FK `CalculoId` com `DeleteBehavior.Cascade`; FK `ItemGuiaId` com `DeleteBehavior.Restrict`; índice único em `(CalculoId, Sequencia)`; `HasColumnType("decimal(10,6)")` em `Fator`; `HasColumnType("decimal(18,2)")` em `ValorResultante`; `HasMaxLength(100)` em `Regra`
- Adicionar `DbSet` no `AppDbContext`
- Criar migration `AddCalculo` via `dotnet ef migrations add AddCalculo`

**Refactor:** nenhum.

**Critério:** migration aplica sem erro; 4 testes verdes.

---

### TASK-F32-03 — NullRuleSet [ ] pendente

**Escopo:** `App/Faturamento/Calculo/NullRuleSet.cs`
**Depende de:** TASK-F32-01

**Red — escrever os testes:**
Criar `Faturamento.Tests/Calculo/NullRuleSetTests.cs` (namespace `Faturamento.Tests.Calculo`). Para compilar, criar skeleton:

```csharp
// skeleton: App/Faturamento/Calculo/NullRuleSet.cs
internal sealed class NullRuleSet : IPricingRuleSet
{
    public Task<IReadOnlyList<ApuracaoItemResult>> ApurarAsync(
        ApurarGuiaContext ctx, CancellationToken ct = default) => throw new NotImplementedException();
}
```

Cenários de teste (sem DB — instanciar `NullRuleSet` diretamente):

- `ApurarAsync_RetornaTodosOsItensComoIndeterminado` — contexto com 3 itens; todos os resultados têm `Situacao=Indeterminado`
- `ApurarAsync_ValorApuradoSempreNulo` — qualquer item retorna `ValorApurado=null`
- `ApurarAsync_PassosSempreVazios` — qualquer item retorna `Passos` com 0 elementos
- `ApurarAsync_RetornaUmResultadoPorItem` — contexto com 5 itens → lista de resultado com 5 elementos

**Green:**
Implementar `NullRuleSet.ApurarAsync`: para cada item em `ctx.Itens`, projetar `new ApuracaoItemResult(item.ItemGuiaId, SituacaoApuracao.Indeterminado, null, [])`.

**Refactor:** nenhum.

**Critério:** 4 testes verdes; zero acesso a DB; `dotnet build` limpo.

---

### TASK-F32-04 — UnimedRuleSet: valor base [ ] pendente

**Escopo:** `App/Faturamento/Calculo/Unimed/UnimedRuleSet.cs`
**Depende de:** TASK-F32-01, TASK-F32-02

**Red — escrever os testes:**
Criar `Faturamento.Tests/Calculo/Unimed/UnimedRuleSetValorBaseTests.cs` (namespace `Faturamento.Tests.Calculo.Unimed`). Para compilar, criar skeleton:

```csharp
// skeleton
internal sealed class UnimedRuleSet(AppDbContext db) : IPricingRuleSet
{
    public Task<IReadOnlyList<ApuracaoItemResult>> ApurarAsync(
        ApurarGuiaContext ctx, CancellationToken ct = default) => throw new NotImplementedException();
}
```

Cenários de teste (requerem `PostgresContainerFixture`; usar `tenantId = Guid.NewGuid()` por teste):

- `ApurarAsync_TabelaEDeflatorPresentes_RetornaCalculado` — seed: `TabelaProcedimento.Valor=100m`, `DeflatorPrestador.Percentual=80m`; resultado: `Situacao=Calculado`, `ValorApurado=80m`, primeiro passo `Regra="ValorBase"`, `Fator=0.8m`, `ValorResultante=80m`
- `ApurarAsync_SemTabela_RetornaSemTabela` — sem `TabelaProcedimento` para o procedimento; resultado: `Situacao=SemTabela`, `ValorApurado=null`
- `ApurarAsync_SemDeflator_RetornaSemDeflator` — tabela presente, sem `DeflatorPrestador` para a posição; resultado: `Situacao=SemDeflator`, `ValorApurado=null`
- `ApurarAsync_Anestesista_RetornaIndeterminado` — item com `Posicao=Anestesista`; resultado: `Situacao=Indeterminado` independentemente de tabela/deflator existirem
- `ApurarAsync_DoisItens_UmSemTabelaOutroCalculado_RetornaAmbos` — dois itens no mesmo contexto; resultados independentes; contagem = 2

**Green:**
Implementar `UnimedRuleSet.ApurarAsync`:

1. Para `Posicao == Anestesista`: retornar `Indeterminado` sem consultar DB
2. Buscar `TabelaProcedimento` por `(OperadoraId, ProcedimentoId, TenantId)` → se nulo: `SemTabela`
3. Buscar `DeflatorPrestador` por `(PrestadorId, OperadoraId, Posicao, TenantId)` → se nulo: `SemDeflator`
4. `valorBase = tabela.Valor × (deflator.Percentual / 100m)`; passo `"ValorBase"` com `Fator = deflator.Percentual / 100m`
5. Retornar `Calculado` com `ValorApurado = valorBase` e passo registrado

Nesta task, **nenhum modifier** é aplicado ainda — apenas o passo ValorBase.

**Refactor:** nenhum.

**Critério:** 5 testes verdes com Postgres; `dotnet build` limpo.

---

### TASK-F32-05 — Modifiers [ ] pendente

**Escopo:**

- `App/Faturamento/Calculo/Unimed/Modifiers/OrdemProcedimentoModifier.cs`
- `App/Faturamento/Calculo/Unimed/Modifiers/VideolaparoscopiaModifier.cs`
- `App/Faturamento/Calculo/Unimed/Modifiers/AcomodacaoModifier.cs`
- `App/Faturamento/Calculo/Unimed/Modifiers/UrgenciaModifier.cs`
- `App/Faturamento/Calculo/Unimed/Modifiers/PosicaoExecutorModifier.cs`
- `App/Faturamento/Calculo/Unimed/UnimedRuleSet.cs` (integrar modifiers no pipeline)

**Depende de:** TASK-F32-04

**Contrato dos modifiers (definir antes de escrever os testes):**

```csharp
// App/Faturamento/Calculo/Unimed/Modifiers/IModifier.cs  (não é IPricingRuleSet)
// ou simplesmente métodos estáticos internos por modifier — sem interface extra
internal static class OrdemProcedimentoModifier
{
    internal static PassoApuracao Aplicar(OrdemProcedimento ordem, decimal valorAtual)
        => throw new NotImplementedException();
}
// idem para os demais
```

**Red — escrever os testes:**
Criar um arquivo por modifier em `Faturamento.Tests/Calculo/Unimed/Modifiers/` (namespace `Faturamento.Tests.Calculo.Unimed.Modifiers`). Criar skeletons com `throw new NotImplementedException()`.

`OrdemProcedimentoModifierTests` (4 cenários — todos os enum members):

- `Unico_Fator1_0` — `Aplicar(Unico, 100m)` → `Fator=1.0m`, `ValorResultante=100m`
- `Principal_Fator1_0` — fator 1.0, valor inalterado
- `SecundarioMesmaVia_Fator0_5` — `Aplicar(SecundarioMesmaVia, 100m)` → `Fator=0.5m`, `ValorResultante=50m`
- `SecundarioViaDiferente_Fator0_7` — `Aplicar(SecundarioViaDiferente, 100m)` → `Fator=0.7m`, `ValorResultante=70m`

`VideolaparoscopiaModifierTests` (3 cenários):

- `Convencional_FatorNeutro` — `Via=Convencional` → `Fator=1.0m`
- `Videolaparoscopia_SemPorteProprio_Fator1_5` — `Via=Videolaparoscopia`, `temPorteProprioVideo=false` → `Fator=1.5m`, `ValorResultante=150m`
- `Videolaparoscopia_ComPorteProprio_FatorNeutro` — `Via=Videolaparoscopia`, `temPorteProprioVideo=true` → `Fator=1.0m`

`AcomodacaoModifierTests` (3 cenários):

- `Enfermaria_FatorNeutro` — `Fator=1.0m`
- `Ambulatorial_FatorNeutro` — `Fator=1.0m`
- `Apartamento_Fator2_0` — `Aplicar(Apartamento, 100m)` → `Fator=2.0m`, `ValorResultante=200m`

`UrgenciaModifierTests` (3 cenários):

- `SemUrgencia_FatorNeutro` — `ehUrgencia=false` → `Fator=1.0m`
- `Urgencia_NaoSadt_Fator1_3` — `ehUrgencia=true`, `ehSadt=false` → `Fator=1.3m`, `ValorResultante=130m`
- `Urgencia_EhSadt_FatorNeutro` — `ehUrgencia=true`, `ehSadt=true` → `Fator=1.0m` (SADT não recebe urgência)

`PosicaoExecutorModifierTests` (5 cenários — todos os enum members relevantes):

- `Cirurgiao_FatorNeutro` — `Fator=1.0m`
- `PrimeiroAuxiliar_Fator0_6` — `Aplicar(PrimeiroAuxiliar, 100m)` → `Fator=0.6m`, `ValorResultante=60m`
- `SegundoAuxiliar_Fator0_4` — `Fator=0.4m`, `ValorResultante=40m`
- `TerceiroAuxiliar_Fator0_3` — `Fator=0.3m`, `ValorResultante=30m`
- `ClinicoAssistente_FatorNeutro` — `Fator=1.0m`

**Green — implementar cada modifier:**
Cada modifier: método estático `Aplicar(...)` que retorna `PassoApuracao`. Regra string = nome da classe sem o sufixo "Modifier" (ex: `"OrdemProcedimento"`). Após todos verdes, integrar a cadeia no `UnimedRuleSet`: valor base → 5 modifiers em sequência; `ValorApurado` final = `ValorResultante` do último passo.

**Refactor:** verificar que a cadeia em `UnimedRuleSet` não tem lógica duplicada dos modifiers.

**Critério:** 18 testes unitários verdes sem DB; `UnimedRuleSet` agora aplica a cadeia completa.

---

### TASK-F32-06 — Integração GuiaService + persistência de Calculo [ ] pendente

**Escopo:**

- `App/Faturamento/ItemGuia.cs` (adicionar `SetValorApurado`)
- `App/Faturamento/Calculo/PricingRuleSetFactory.cs`
- `App/Faturamento/GuiaService.cs`
- `App/Program.cs`

**Depende de:** TASK-F32-03, TASK-F32-05

**Red — escrever os testes:**
Criar `Faturamento.Tests/Calculo/GuiaServiceCalculoTests.cs` (namespace `Faturamento.Tests.Calculo`). Para compilar, adicionar `SetValorApurado` como `throw new NotImplementedException()` e criar skeleton da factory.

Cenários (todos com `PostgresContainerFixture`, seed: operadora UNIMED, prestador, procedimento TUSS, tabela valor=200m, deflator cirurgião=100m):

- `CriarGuia_Unimed_ItemCalculado_ValorApuradoPreenchido` — criar guia UNIMED com 1 item cirurgião; `result.Value.Itens[0].ValorApurado` == 200m; um `Calculo` salvo no DB para a guia; `PassoCalculo` com `Regra="ValorBase"` existe
- `CriarGuia_Unimed_SemTabela_ValorApuradoNulo` — procedimento sem `TabelaProcedimento`; `ValorApurado == null`; `Calculo` persiste com 0 `PassoCalculo` de `Calculado`
- `CriarGuia_Pacote_NaoInvocaMotor_ValorApuradoManualPreservado` — `EhPacote=true`, item com `ValorApurado=500m` informado manualmente; após criar: `ValorApurado` == 500m; **nenhum** `Calculo` criado no DB
- `AtualizarGuia_RecalculaESubstituiCalculo` — criar guia (gera `Calculo`); atualizar guia; DB tem exatamente 1 `Calculo` para a guia (o antigo foi excluído pelo cascade)
- `CriarGuia_OperadoraSemUnimed_NullRuleSetExecuta_SemValorApurado` — operadora com `TipoRuleSet` diferente de `Unimed`; `ValorApurado == null`; `Calculo` criado com `PassoCalculo` vazio (NullRuleSet retorna Indeterminado)

**Green:**

1. `ItemGuia.SetValorApurado(decimal? valor)` — setter interno que atribui `ValorApurado`
2. `PricingRuleSetFactory` — recebe `AppDbContext`; método `Criar(TipoRuleSet)` retorna `UnimedRuleSet` para `Unimed`, `NullRuleSet` para demais
3. `GuiaService.CriarAsync` — após persistir itens: se `!guia.EhPacote`, obter `operadora.TipoRuleSet`, criar rule set via factory, montar `ApurarGuiaContext`, chamar `ApurarAsync`, para cada resultado `Calculado` chamar `item.SetValorApurado(resultado.ValorApurado)`, salvar `Calculo` + `PassoCalculo` de todos os itens, `SaveChangesAsync` adicional
4. `GuiaService.AtualizarAsync` — excluir `Calculo` existente (cascade apaga `PassoCalculo`) antes de recriar itens; depois recalcular como no `CriarAsync`
5. `Program.cs` — registrar `PricingRuleSetFactory` como scoped; `UnimedRuleSet` como scoped; `NullRuleSet` como scoped

**Refactor:** extrair helper `ExecutarCalculoAsync(Guia, Operadora, CancellationToken)` em `GuiaService` se `CriarAsync` e `AtualizarAsync` ficarem com código duplicado.

**Critério:** 5 testes de integração verdes; `dotnet build` limpo; `dotnet test` geral verde.

---

### TASK-F32-07 — Pipeline completo: cenários de domínio [ ] pendente

**Escopo:** `Faturamento.Tests/Calculo/Unimed/UnimedPipelineTests.cs`
**Depende de:** TASK-F32-06

**Red — escrever os testes (todos falham porque os modifiers não estão integrados no GuiaService ainda):**

> **Nota:** ao final de F32-05 o `UnimedRuleSet` já aplica os modifiers, mas o `GuiaService` integrado é testado aqui de ponta a ponta via `GuiaService.CriarAsync`. Se F32-06 estiver verde, esses testes já podem passar no Green — o objetivo é documenta os casos de negócio e detectar regressões futuras.

Seed compartilhado por classe (helper `SeedAsync`): operadora UNIMED, prestador, procedimento TUSS não-SADT sem `TemPorteProprioVideo`, tabela valor=1000m, deflator Cirurgião=100m, deflator PrimeiroAuxiliar=60m, deflator SegundoAuxiliar=40m, deflator TerceiroAuxiliar=30m.

Cenários (namespace `Faturamento.Tests.Calculo.Unimed`; 1 `Guid.NewGuid()` de tenant por classe):

| #   | Nome do teste                                       | Parâmetros do item                                                      | `ValorApurado` esperado         |
| --- | --------------------------------------------------- | ----------------------------------------------------------------------- | ------------------------------- |
| 1   | `Cirurgiao_Unico_Enfermaria_SemUrgencia`            | Cirurgião, Único, Convencional, Enfermaria, urgência=false              | 1000m                           |
| 2   | `Cirurgiao_Unico_Apartamento`                       | Cirurgião, Único, Convencional, Apartamento, urgência=false             | 2000m                           |
| 3   | `Cirurgiao_Unico_Urgencia_NaoSadt`                  | Cirurgião, Único, Convencional, Enfermaria, urgência=true               | 1300m                           |
| 4   | `Cirurgiao_SecundarioMesmaVia`                      | Cirurgião, SecundarioMesmaVia, Convencional, Enfermaria                 | 500m                            |
| 5   | `Cirurgiao_Videolaparoscopia_SemPorteProprio`       | Cirurgião, Único, Videolaparoscopia, Enfermaria                         | 1500m                           |
| 6   | `PrimeiroAuxiliar_Unico_Enfermaria`                 | PrimeiroAuxiliar, Único, Convencional, Enfermaria                       | 600m                            |
| 7   | `SegundoAuxiliar_Unico_Enfermaria`                  | SegundoAuxiliar, Único, Convencional, Enfermaria                        | 400m                            |
| 8   | `Anestesista_RetornaIndeterminado`                  | Anestesista, Único, Convencional, Enfermaria                            | null + `Situacao=Indeterminado` |
| 9   | `Cirurgiao_SecundarioMesmaVia_Apartamento_Urgencia` | Cirurgião, SecundarioMesmaVia, Convencional, Apartamento, urgência=true | 1000m × 0.5 × 2 × 1.3 = 1300m   |
| 10  | `Cirurgiao_Videolaparoscopia_Apartamento`           | Cirurgião, Único, Videolaparoscopia, Apartamento                        | 1000m × 1.5 × 2 = 3000m         |

Cada teste: criar guia via `GuiaService.CriarAsync`, ler `result.Value.Itens[0].ValorApurado` e `result.Value.Itens[0].Situacao`.

**Green:** se TASK-F32-06 estiver corretamente implementada, os testes passam sem código adicional. Corrigir qualquer divergência de cálculo encontrada.

**Refactor:** nenhum.

**Critério:** 10 cenários verdes. Qualquer falha indica bug no pipeline — corrigir no `UnimedRuleSet` ou nos modifiers, não nos testes.

---

## Critério de pronto da feature

- [ ] `dotnet build` limpo (zero warnings).
- [ ] `dotnet test` verde, cobertura de `Faturamento` ≥ 90%.
- [ ] Criar guia UNIMED via API → `ItemGuia.ValorApurado` populado na resposta.
- [ ] Criar guia pacote → `ValorApurado` inalterado.
- [ ] Todos os 10 cenários do TASK-F32-07 passando.
- [ ] Guia com item sem tabela: `ValorApurado = null` na resposta (sem erro 500).
- [ ] Trace completo (`Calculo` + `PassoCalculo`) persistido e consultável via `AppDbContext`.

---

## Restrições e decisões

- **Sem endpoint de leitura do trace** nesta fase — o trace é infraestrutura para F4.2 (auditoria de divergências).
- **Sem recálculo automático** ao alterar tabela/deflator — apenas ao salvar a guia.
- **Ordem dos modifiers é fixa** conforme pipeline acima; não configurável por Singular.
- **Sem UCO** (Unidade de Custo Operacional) — `TabelaProcedimento.Valor` já é o valor negociado final, não UCO.
- **Percentual de deflator por posição**: padrão CBHPM (Cirurgião=100%, 1ºAux=30%, 2ºAux/3ºAux=20%) é configurado manualmente via `DeflatorPrestador` pelo admin — o motor não assume padrão; se não houver deflator, retorna `SemDeflator`.
- **ClinicoAssistente**: sem regra especial — usa o deflator configurado; fator de posição = 1.0.
- **Migrations folder**: criar `.editorconfig` suprimindo IDE0005/IDE0161/CA1515/CA1861 em `App/Faturamento/Migrations/` (já existe o padrão de F3.1 — verificar se a nova migration está na mesma pasta; se sim, `.editorconfig` já cobre).

---

## Alertas de discrepância — validar com casos reais (P0.2)

| Ponto                   | DOMINIO.md (hipótese inicial)      | Pesquisa CBHPM 2018+/UNIMED 2024                                  | Decisão para MVP                |
| ----------------------- | ---------------------------------- | ----------------------------------------------------------------- | ------------------------------- |
| 1º Auxiliar             | 30% do cirurgião                   | **60%** do cirurgião                                              | Usar 60% — atualizado           |
| 2º Auxiliar             | 20% do cirurgião                   | **40%** do cirurgião                                              | Usar 40% — atualizado           |
| 3º Auxiliar             | 20% do cirurgião                   | **30%** do cirurgião                                              | Usar 30% — atualizado           |
| Anestesia multiplicador | 17,19% UNIMED sobre CBHPM 2015     | Não confirmado na pesquisa — calculado por porte anestésico (UCO) | **Aguardar F3.3 + casos reais** |
| Urgência horário        | 19h–7h + fins de semana + feriados | Confirmado idêntico                                               | OK                              |
| Videolaparoscopia       | +50%                               | Confirmado idêntico                                               | OK                              |
| Apartamento             | ×2                                 | Confirmado idêntico (+100%)                                       | OK                              |
| Procedimentos múltiplos | 50% mesma via / 70% via diferente  | Confirmado idêntico                                               | OK                              |

**Ação obrigatória antes de F3.3:** verificar os percentuais de auxiliar em pelo menos 3 guias reais de cirurgia com auxiliares. Os 60%/40%/30% são a norma CBHPM atual, mas cada Unimed Singular pode ter deflator negociado diferente armazenado em `DeflatorPrestador`.

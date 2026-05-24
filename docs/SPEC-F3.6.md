# SPEC F3.6 — Porte Anestésico por Letra (UNIMED) + Regra de Dobra por Posição

**Pré-requisito:** F3.3 concluído (pipeline de anestesia atual com `PorteAnestesico: int? 1–8` e `AnestesiaCalculator` baseado em `TabelaProcedimento`).
**Pós-condição:** Anestesista usa nova `TabelaPorteAnestesico` (letra A–Z exceto O, par `(ValorENF, ValorAP)` por operadora). `AcomodacaoModifier` aplica dobra apenas para `PosicaoExecutor.Cirurgiao`. Importação CSV admin entrega os 25 portes UNIMED JPA.

---

## Contexto rápido

Tabela UNIMED JPA: porte anestésico agora é **letra A–Z (sem O)**; cada letra tem par fixo `(ValorENF, ValorAP)` para todo o tenant/operadora. Não há mais porte numérico 1–8 nem coluna "Honorários". O pipeline de cálculo muda: valor vem de `TabelaPorteAnestesico(OperadoraId, PorteLetra, Acomodacao)`, não de `TabelaProcedimento`.

**Regra de dobra confirmada pelo cliente:** a dobra de apartamento (×2,0) aplica-se **exclusivamente ao cirurgião** na UNIMED. Auxiliares (1º/2º/3º) e clínico assistente em apartamento recebem fator 1,0. Anestesista também não dobra — no novo pipeline (PA-03/PA-04) o valor já vem pré-selecionado da `TabelaPorteAnestesico` pela acomodação, então não há multiplicação posterior. Hoje `AcomodacaoModifier` aplica ×2 para qualquer posição: bug confirmado, ver PA-04B.

## Ordem de execução: PA-01 → PA-04B → PA-08 (backend), PA-09 → PA-11 (frontend)

---

## PA-01 · Entidade TabelaPorteAnestesico [x]

**TDD — escrever test primeiro, depois implementar.**

### Arquivo de teste (novo)

`tests/Catalog.Tests/TabelaPorteAnestesico/TabelaPorteAnestesicoImportTests.cs`

```
namespace: Catalog.Tests.TabelaPorteAnestesico
fixture: PostgresContainerFixture (mesmo padrão de TabelaCrudTests.cs)
```

Casos obrigatórios:

```
ImportarCsv_2Portes_5Procs_RetornaContadoresCorretos
  seed: 2 procedimentos com CodigoTuss "30101050","30101069" no tenant
  csv-input: 5 linhas do arquivo UNIMED JPA (ver formato abaixo), 2 portes distintos (E, D)
  assert: result.PortesAtualizados == 2
  assert: result.ProcedimentosAtualizados == 2
  assert: result.ProcedimentosNaoEncontrados vazio

ImportarCsv_TussInexistente_ListaCodigoNaoEncontrado
  csv-input: 1 linha com TUSS "99999999" que não existe no tenant
  assert: result.ProcedimentosNaoEncontrados == ["99999999"]
  assert: result.PortesAtualizados == 1  (porte ainda é criado)

ImportarCsv_PorteDuplicado_Upsert_NaoDuplica
  import mesma tabela 2x
  assert: db.TabelasPorteAnestesico.Count(t => t.OperadoraId == oid && t.PorteLetra == "J") == 1

ImportarCsv_LinhaMalformada_RegistraErroEContinua
  csv com 1 linha válida + 1 linha sem colunas suficientes
  assert: result.Erros[0].Linha == <linha-errada>
  assert: result.PortesAtualizados == 1
```

**Formato CSV de entrada (arquivo UNIMED JPA):**

```
separador: vírgula; decimal: vírgula entre aspas duplas
8 linhas de cabeçalho a ignorar
linha 9 = header: Código,Procedimento,Honorários,VL AMB,VL ENF,VL AP,Porte
dados: 30101050,APENDICE PRE-AURICULAR,"224,64",,"292,5",468,E
colunas usadas: [0]=CodigoTuss [4]=VlEnfermaria [5]=VlApartamento [6]=PorteLetra
```

### Arquivos de implementação

| Arquivo                                                            | Ação                                                            |
| ------------------------------------------------------------------ | --------------------------------------------------------------- |
| `App/Catalog/TabelaPorteAnestesico.cs`                             | Novo — entidade                                                 |
| `App/Catalog/Configurations/TabelaPorteAnestesicoConfiguration.cs` | Novo — EF config                                                |
| `App/Data/AppDbContext.cs`                                         | Adicionar `DbSet<TabelaPorteAnestesico> TabelasPorteAnestesico` |
| `App/Catalog/CatalogService.cs`                                    | Novo método `ImportarTabelaUnimedAnestesistaAsync`              |
| `App/Catalog/Endpoints/CatalogEndpoints.cs`                        | 2 endpoints novos                                               |
| migration em `App/Catalog/Migrations/`                             | `AddTabelaPorteAnestesico`                                      |

**Entidade:**

```csharp
sealed class TabelaPorteAnestesico : ITenantEntity {
    Guid Id; Guid TenantId; Guid OperadoraId;
    string PorteLetra;          // 1 char, A–Z sem O
    decimal ValorEnfermaria;
    decimal ValorApartamento;
    decimal? ValorAmbulatorial;
    DateTimeOffset AtualizadoEm;
    static Create(tenantId, operadoraId, porteletra, valEnf, valAp, valAmb?)
    void Atualizar(valEnf, valAp, valAmb?)
}
// Unique index: (TenantId, OperadoraId, PorteLetra)
// decimal(18,4) para valores
```

**Resultado do import:**

```csharp
record ImportarTabelaPorteResult(
    int PortesAtualizados,
    int ProcedimentosAtualizados,
    IReadOnlyList<string> ProcedimentosNaoEncontrados,
    IReadOnlyList<ImportarCsvErro> Erros);
```

**Endpoints:**

```
POST /api/v1/admin/tabelas-porte-anestesico/importar-unimed-csv?operadoraId={guid}
  multipart .csv, max 5 MB → ImportarTabelaPorteResult

GET  /api/v1/admin/tabelas-porte-anestesico?operadoraId={guid}
  → IReadOnlyList<TabelaPorteAnestesicoItem>
     { id, porteletra, valorEnfermaria, valorApartamento, valorAmbulatorial, atualizadoEm }
```

**Verificar:** `dotnet test tests/Catalog.Tests/ -t -f "TabelaPorteAnestesicoImport"`

---

## PA-02 · Procedimento.PorteAnestesico: int? → string? [x]

### Testes a modificar

`tests/Catalog.Tests/Procedimento/ProcedimentoCsvImportTests.cs`

```diff
- Assert.Equal(3, proc.PorteAnestesico);          // linha 67
+ Assert.Equal("J", proc.PorteAnestesico);         // após fixar csv abaixo

- csv: "30715013;Descricao atualizada;7A;3;false;false"   // PorteAnestesico = 3
+ csv: "30715013;Descricao atualizada;7A;J;false;false"   // PorteAnestesico = "J"

// Caso inválido existente: "9" → erro (dígito não é letra)
// Adicionar caso: "O" → erro (letra proibida)
// Adicionar caso: "J" → aceito
```

`tests/Faturamento.Tests/Calculo/AnestesiaSchemaTests.cs`

- Ajustar tipo de `PorteAnestesico` de `int?` para `string?` (verificar assertions)

### Arquivos de implementação

| Arquivo                                                   | Mudança                                                 |
| --------------------------------------------------------- | ------------------------------------------------------- |
| `App/Catalog/Procedimento.cs`                             | `int? PorteAnestesico` → `string? PorteAnestesico`      |
| `App/Catalog/Configurations/ProcedimentoConfiguration.cs` | coluna `varchar(2)`                                     |
| `App/Catalog/CatalogService.cs`                           | Validação: remover `0..8`, adicionar regex `^[A-NP-Z]$` |
| migration (mesma de PA-01)                                | ALTER COLUMN `porte_anestesico`                         |

**Validação nova em CatalogService:**

```csharp
// se PorteAnestesico não nulo:
if (!System.Text.RegularExpressions.Regex.IsMatch(porteAnestesico, @"^[A-NP-Z]$"))
    return ValidationError("PorteAnestesico deve ser letra A–Z exceto O");
```

**Verificar:** `dotnet test tests/Catalog.Tests/ -t -f "ProcedimentoCsvImport"`

---

## PA-03 · AnestesiaCalculator — novo pipeline [x]

### Testes a substituir

`tests/Faturamento.Tests/Calculo/Unimed/AnestesiaCalculatorTests.cs`

Substituir **todos os 10 testes existentes** por novos (pipeline mudou completamente):

```csharp
// Nova assinatura do calculator:
AnestesiaCalculator.Calcular(
    valorReferencia: decimal,   // já é ENF ou AP — escolhido antes de chamar
    deflatorPercentual: decimal,
    ordem: OrdemProcedimento,
    ehUrgencia: bool,
    ehSadt: bool)
// retorna: (decimal valorFinal, IReadOnlyList<PassoApuracao> passos)

// Casos:
Basico_Enfermaria_SemUrgencia
  input: valorRef=526.50m, deflator=100m, Unico, !urgencia, !sadt
  assert: valor == 526.50m
  assert: passos contém "ValorBase"
  assert: passos NÃO contém "UnimedAN"
  assert: passos NÃO contém "TempoExtra"

ComDeflator80
  input: valorRef=526.50m, deflator=80m, Unico, !urgencia, !sadt
  assert: valor == 421.20m

ComUrgencia
  input: valorRef=526.50m, deflator=100m, Unico, urgencia=true, sadt=false
  assert: valor == 684.45m   // 526.50 × 1.3

UrgenciaEmSadt_NaoAplica
  input: urgencia=true, sadt=true
  assert: valor == 526.50m

SecundarioMesmaVia
  input: valorRef=526.50m, deflator=100m, SecundarioMesmaVia
  assert: valor == 263.25m   // × 0.5

SecundarioViaDiferente
  input: valorRef=526.50m, deflator=100m, SecundarioViaDiferente
  assert: valor == 368.55m   // × 0.7

Trace_ContemPassosAplicados
  input: urgencia, SecundarioMesmaVia, deflator=80m
  assert: passos contém "ValorBase", "OrdemProcedimento", "Urgencia"
  assert: passos NÃO contém "Acomodacao", "UnimedAN", "TempoExtra"
```

### Arquivos de implementação

`App/Faturamento/Calculo/Unimed/AnestesiaCalculator.cs` — reescrever completo:

```csharp
internal static class AnestesiaCalculator {
    internal static (decimal, IReadOnlyList<PassoApuracao>) Calcular(
        decimal valorReferencia,
        decimal deflatorPercentual,
        OrdemProcedimento ordem,
        bool ehUrgencia,
        bool ehSadt)
    {
        // passo 1: ValorBase = valorReferencia × (deflator/100)
        // passo 2: OrdemProcedimento (skip se fator == 1.0)
        // passo 3: Urgencia (skip se fator == 1.0)
        // SEM: UnimedAN, Acomodacao, TempoExtra
    }
}
```

**Verificar:** `dotnet test tests/Faturamento.Tests/ -t -f "AnestesiaCalculator"`

---

## PA-04 · UnimedRuleSet — ApurarAnestesistaAsync [x]

### Testes a substituir

`tests/Faturamento.Tests/Calculo/Unimed/UnimedAnestesiaPipelineTests.cs`

Substituir seed e assertions — `SeedCompletoAsync` agora cria `TabelaPorteAnestesico` em vez de `TabelaProcedimento` para anestesista:

```csharp
// SeedCompletoAsync novo:
//   proc = Procedimento.Create(..., porte: "1", porteAnestesico: "J", ...)
//   ctx.Add(TabelaPorteAnestesico.Create(tenantId, operadora.Id, "J", 526.50m, 842.40m, null))
//   ctx.Add(DeflatorPrestador.Create(..., Anestesista, 100m))
//   NÃO cria TabelaProcedimento para o anestesista

// Valores esperados novos:
Anestesista_PorteJ_Enfermaria         → 526.50m   (era 1171.90m)
Anestesista_PorteJ_Apartamento        → 842.40m   (era 2343.80m)
Anestesista_PorteJ_Urgencia_Enfermaria → 684.45m  (526.50 × 1.3)
Anestesista_SemTabelaPorte            → ValorApurado == null, SituacaoApuracao.SemTabela
Anestesista_PorteAnestesicoNulo       → ValorApurado == null, SituacaoApuracao.Indeterminado
Anestesista_SemDeflator               → ValorApurado == null, SituacaoApuracao.SemDeflator
Anestesista_Deflator80_Enfermaria     → 421.20m   (526.50 × 0.8)
TracePersistePassos                   → passos.Count >= 1, contém "ValorBase"
```

**Remover** caso `Anestesista_PA5_ComTempoExtra_UmaHora` (TempoExtra suspenso).

### Arquivo de implementação

`App/Faturamento/Calculo/Unimed/UnimedRuleSet.cs` — método `ApurarAnestesistaAsync`:

```
1. procedimento = lookup por item.ProcedimentoId
2. se procedimento?.PorteAnestesico is null → Indeterminado
3. tabelaPorte = db.TabelasPorteAnestesico WHERE (TenantId, OperadoraId, PorteLetra == porteAnestesico)
   se null → SemTabela
4. valorReferencia = acomodacao switch {
     Apartamento   → tabelaPorte.ValorApartamento,
     Ambulatorial  → tabelaPorte.ValorAmbulatorial ?? tabelaPorte.ValorEnfermaria,
     _             → tabelaPorte.ValorEnfermaria
   }
5. deflator = db.DeflatoresPrestador WHERE (PrestadorId, OperadoraId, Anestesista)
   se null → SemDeflator
6. return AnestesiaCalculator.Calcular(valorReferencia, deflator.Percentual, item.Ordem, item.EhUrgencia, procedimento.EhSadt)
```

**NÃO remover** `item.TempoAnestesicoMin` de `ApurarItemInput` — manter campo ignorado para não exigir migration em tabela guia.

**Verificar:** `dotnet test tests/Faturamento.Tests/ -t -f "UnimedAnestesiaPipeline"`

---

## PA-04B · AcomodacaoModifier — dobra apenas para cirurgião [x]

**Bug atual:** `AcomodacaoModifier.Aplicar(Acomodacao, decimal)` aplica ×2 para qualquer posição em apartamento. 1º auxiliar em apto recebe hoje `valor × 2 × 0,6 = 1,2× valor` (errado; correto é `0,6× valor`). Nenhum teste de pipeline cobre `Auxiliar + Apartamento`, então o bug passou despercebido. Anestesista também ficava errado pela mesma razão, mas o novo pipeline de PA-03 já remove o passo Acomodação para anestesia — esta tarefa cobre só o pipeline cirúrgico.

### Testes a substituir

`tests/Faturamento.Tests/Calculo/Unimed/Modifiers/AcomodacaoModifierTests.cs`

Substituir os 3 testes existentes (nova assinatura recebe `PosicaoExecutor`):

```csharp
// Nova assinatura:
AcomodacaoModifier.Aplicar(Acomodacao, PosicaoExecutor, decimal)

// Casos:
Cirurgiao_Apartamento_Dobra
  Aplicar(Apartamento, Cirurgiao, 100m)
  assert: fator == 2.0m, valorResultante == 200m

Cirurgiao_Enfermaria_Neutro
  Aplicar(Enfermaria, Cirurgiao, 100m)
  assert: fator == 1.0m

Cirurgiao_Ambulatorial_Neutro
  Aplicar(Ambulatorial, Cirurgiao, 100m)
  assert: fator == 1.0m

PrimeiroAuxiliar_Apartamento_NaoDobra
  Aplicar(Apartamento, PrimeiroAuxiliar, 100m)
  assert: fator == 1.0m, valorResultante == 100m

SegundoAuxiliar_Apartamento_NaoDobra
  assert: fator == 1.0m

TerceiroAuxiliar_Apartamento_NaoDobra
  assert: fator == 1.0m

ClinicoAssistente_Apartamento_NaoDobra
  assert: fator == 1.0m

Anestesista_Apartamento_NaoDobra
  // defensivo: AcomodacaoModifier não é chamado pelo pipeline de anestesia,
  // mas se for chamado direto, não pode dobrar
  assert: fator == 1.0m
```

### Testes a adicionar (gap de cobertura)

`tests/Faturamento.Tests/Calculo/Unimed/UnimedPipelineTests.cs`

```csharp
PrimeiroAuxiliar_Apartamento_NaoDobra
  cmd: PrimeiroAuxiliar + Apartamento + Convencional + Unico
  seed: TabelaProcedimento.Valor == 1000m, DeflatorPrestador.Percentual == 100m
  assert: ValorApurado == 600m   // 1000 × 1.0 (sem ×2) × 0.6
  // antes do fix retornaria 1200m

SegundoAuxiliar_Apartamento_NaoDobra
  assert: ValorApurado == 400m   // 1000 × 1.0 × 0.4
```

### Arquivos de implementação

| Arquivo                                                          | Mudança                                                                                                                |
| ---------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `App/Faturamento/Calculo/Unimed/Modifiers/AcomodacaoModifier.cs` | Adicionar parâmetro `PosicaoExecutor posicao`; fator 2.0 apenas se `posicao == Cirurgiao && acomodacao == Apartamento` |
| `App/Faturamento/Calculo/Unimed/UnimedRuleSet.cs`                | Linha que chama `AcomodacaoModifier.Aplicar(item.Acomodacao, valorAtual)` → passar `item.Posicao` adicional            |

### Documentação

`docs/DOMINIO.md` — atualizar:

- Seção **"Dobra por acomodação"**: adicionar nota _"Aplica-se exclusivamente ao cirurgião. Auxiliares, clínico assistente e anestesista em apartamento recebem fator 1,0."_
- Seção **"Ordem dos modifiers no pipeline"** passo 4: trocar para _"Acomodação: `Apartamento && Posicao == Cirurgiao` → ×2.0; demais → ×1.0"_
- Seção **"Anestesia — pipeline próprio"**: já será reescrita por PA-03; garantir que não restou nenhuma menção a passo de Acomodação.

**Verificar:** `dotnet test apps/backend/Honorare.slnx -t -f "AcomodacaoModifier|Apartamento_NaoDobra"`

---

## PA-05 · Migration [x]

Executar de dentro de `apps/backend/App/`:

```bash
dotnet ef migrations add AddTabelaPorteAnestesico \
  --output-dir Catalog/Migrations \
  --namespace App.Catalog.Migrations
```

Verificar que a migration gerada inclui:

- Criação de `tabelas_porte_anestesico` com colunas `porte_letra varchar(2)`, `valor_enfermaria numeric(18,4)`, `valor_apartamento numeric(18,4)`, `valor_ambulatorial numeric(18,4) nullable`
- ALTER COLUMN `procedimentos.porte_anestesico` para `varchar(2) nullable`
- Criar `App/Catalog/Migrations/.editorconfig` com 4 supressões se não existir (ver padrão de `App/Faturamento/Migrations/.editorconfig`)

**Verificar:** `dotnet build apps/backend/Honorare.slnx` → zero erros

---

## PA-06 · Run all backend tests [ ]

```bash
dotnet test apps/backend/Honorare.slnx
```

Todos os testes verdes. Cobertura ≥ 80%.

---

## PA-07 · Admin-web: tipos + serviço [ ]

### Arquivos

`apps/admin-web/src/app/admin/catalog/catalog.types.ts`

```typescript
// Adicionar:
export interface TabelaPorteAnestesicoItem {
  id: string;
  porteletra: string;
  valorEnfermaria: number;
  valorApartamento: number;
  valorAmbulatorial: number | null;
  atualizadoEm: string;
}

export interface ImportarTabelaPorteResult {
  portesAtualizados: number;
  procedimentosAtualizados: number;
  procedimentosNaoEncontrados: string[];
  erros: ImportarCsvErro[];
}
```

`apps/admin-web/src/app/admin/catalog/catalog.service.ts`

```typescript
// Adicionar:
importarTabelaPorteAnestesico(operadoraId: string, file: File): Observable<ImportarTabelaPorteResult> {
  const form = new FormData();
  form.append('file', file);
  return this._http.post<ImportarTabelaPorteResult>(
    `${this._base}/tabelas-porte-anestesico/importar-unimed-csv`,
    form,
    { params: { operadoraId } }
  );
}

listarPortesAnestesico(operadoraId: string): Observable<TabelaPorteAnestesicoItem[]> {
  return this._http.get<TabelaPorteAnestesicoItem[]>(
    `${this._base}/tabelas-porte-anestesico`,
    { params: { operadoraId } }
  );
}
```

---

## PA-08 · Admin-web: procedimento-form PorteAnestesico [ ]

`apps/admin-web/src/app/admin/catalog/procedimentos/procedimento-form/procedimento-form.component.ts`

```typescript
// Alterar sinal:
porteAnestesico = signal<string>('');   // era number | ''

// Alterar payload:
porteAnestesico: this.porteAnestesico() || null,   // string | null (nunca '')
```

`procedimento-form.component.html`

```html
<!-- Substituir input number por: -->
<input type="text" [value]="porteAnestesico()" (input)="porteAnestesico.set($any($event.target).value.toUpperCase().slice(0,1))" placeholder="ex: J" maxlength="1" pattern="[A-NP-Za-np-z]" />
```

Atualizar spec do componente: `porteAnestesico` signal de `''` string, não número.

**Verificar:** `pnpm -F admin-web lint && pnpm -F admin-web test:ci`

---

## PA-09 · Admin-web: modal importação porte anestésico [ ]

Criar seguindo o padrão exato de `apps/admin-web/src/app/admin/catalog/tabelas/tabela-csv-modal/`.

**Novo componente:** `tabelas/tabela-porte-anestesico-csv-modal/`

- Selector: `app-tabela-porte-anestesico-csv-modal`
- Inputs: `@Input() operadoraId: string`
- Outputs: `@Output() concluido`, `@Output() cancelado`
- Chama: `_catalogService.importarTabelaPorteAnestesico(operadoraId, file)`
- Template exibe: `portesAtualizados`, `procedimentosAtualizados`, lista `procedimentosNaoEncontrados` (se houver)
- Instrução: _"Exporte o arquivo XLSX como CSV (separador vírgula) antes de importar."_

**Integrar em** `tabela-list.component.ts / .html`:

- Novo signal: `mostrarModalPorte = signal(false)`
- Botão "Importar Tabela Anestesista" ao lado do botão CSV existente, desabilitado sem operadora selecionada
- Renderizar `<app-tabela-porte-anestesico-csv-modal>` condicionalmente

**Verificar:** `pnpm -F admin-web lint && pnpm -F admin-web test:ci`

---

## PA-10 · Verificação end-to-end [ ]

```bash
dotnet test apps/backend/Honorare.slnx   # todos verdes
pnpm -F admin-web test:ci                # cobertura ≥ 80%
pnpm -F admin-web lint                   # 0 warnings
pnpm -F admin-web stylelint             # 0 warnings
```

Smoke test manual (Docker Compose up):

1. Admin → Tabelas → selecionar UNIMED JPA → "Importar Tabela Anestesista" → upload CSV
2. Confirmar 25 portes na listagem
3. Conferir procedimento `30101050` → `PorteAnestesico == "E"`
4. Criar guia com anestesista + TUSS `30101050` + Enfermaria → `ValorApurado == 292.50`
5. Mesmo + Apartamento → `ValorApurado == 468.00`

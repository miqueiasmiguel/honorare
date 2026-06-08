# SPEC F3.8 — Importação de Demonstrativo + PercentualOrdem parametrizável

**Pré-requisito:** F3.7 concluído. Entidades `Guia`, `ItemGuia`, `Demonstrativo`, `ItemDemonstrativo`, `Beneficiario` existentes.

**Pós-condição:** (1) `ItemGuia.OrdemProcedimento: enum` substituído por `PercentualOrdem: decimal`. (2) Nova entidade `TabelaOrdemOperadora` permite configurar a tabela de percentuais múltiplos por operadora. (3) `Guia.NumeroGuia` armazena o número TISS da guia. (4) Endpoint de importação processa o CSV analítico da UNIMED criando `Guia` + `ItemGuia` + `ItemDemonstrativo` em uma única operação, rodando o motor de cálculo ao final.

---

## Contexto e decisões de design

### Por que substituir o enum por decimal

O campo `OrdemProcedimento` (enum com 4 valores: Único, Principal, SecundarioMesmaVia, SecundarioViaDiferente) só modela até o 2º procedimento. O CSV analítico da UNIMED mostra valores de `% VIA` de 100%, 70%, 50%, 40%, 30%, 20%, 10% — correspondentes à progressão real de atos múltiplos. Cada Unimed Singular pode ter progressão diferente da norma CBHPM nacional.

A solução é armazenar o percentual efetivo como `decimal` (ex: `1.0`, `0.7`, `0.5`, `0.4`). O modifier passa a usar esse valor diretamente. A enum é abolida — não é mantida como helper interno.

### TabelaOrdemOperadora — parametrização por operadora

Para que o usuário não precise preencher o percentual manualmente ao criar uma guia, cada operadora tem uma tabela de progressão configurável. O sistema usa essa tabela para gerar as opções do dropdown de ordem no formulário de guia.

Valores padrão aplicados quando nenhuma tabela está configurada:

| NumeroProcedimento | MesmaVia | ViaDiferente |
| ------------------ | -------- | ------------ |
| 1                  | 100%     | 100%         |
| 2                  | 50%      | 70%          |
| 3                  | 40%      | 50%          |
| 4                  | 30%      | 40%          |
| 5                  | 20%      | 30%          |
| 6+                 | 10%      | 10%          |

> ⚠️ A progressão da UNIMED JPA confirmada pelo cliente é: 100%, 50%, 40%, 30%, 20%, 10% (coluna MesmaVia). A coluna ViaDiferente usa a referência CBHPM/UNIMED intercâmbio — **confirmar com o cliente** a progressão além do 2º procedimento em via diferente antes de usar em produção.

### Campos derivados do CSV analítico UNIMED

| Coluna CSV                       | Campo destino                        | Observações                                                                   |
| -------------------------------- | ------------------------------------ | ----------------------------------------------------------------------------- |
| `GUIA`                           | `Guia.NumeroGuia`                    | Novo campo — identifica a guia TISS                                           |
| `CODIGO`                         | `Beneficiario.Carteira`              | Carteirinha do beneficiário (constante por paciente)                          |
| `BENEFICIARIO`                   | `Beneficiario.Nome`                  | Lookup por Carteira → cria se não encontrar                                   |
| `DATA SERVICO`                   | `Guia.DataAtendimento`               | Formato `dd/MM/yy` ou `dd/MM/yyyy`                                            |
| `CODIGO PROCEDIMENTO`            | `ItemGuia.ProcedimentoId`            | Lookup por `CodigoTuss` — erro se não encontrar                               |
| `NOME PROCEDIMENTO`              | Validação apenas                     | Compara com `Procedimento.Descricao` — aviso se divergir                      |
| `FUNCAO`                         | `ItemGuia.PosicaoExecutor`           | Mapeamento: ver tabela abaixo                                                 |
| `ACOMODACAO`                     | `ItemGuia.Acomodacao`                | `ENFERMARIA`→`Enfermaria`, `APARTAMENTO`→`Apartamento`, demais→`Ambulatorial` |
| `ACRESCIMO`                      | `ItemGuia.EhUrgencia`                | `30,00%` (ou qualquer valor > 0%) → `true`; `0,00%` → `false`                 |
| `% VIA`                          | `ItemGuia.PercentualOrdem`           | Dividir por 100: `70` → `0.70`; vazio ou `0` → `1.0` (principal)              |
| `HONORARIO`                      | `ItemDemonstrativo.ValorApresentado` | Decimal pt-BR (vírgula)                                                       |
| `GLOSA`                          | `ItemDemonstrativo.ValorGlosado`     | Decimal pt-BR (vírgula)                                                       |
| `COD_GLOSA`                      | `ItemDemonstrativo.MotivoGlosa`      | String — pode ser vazio                                                       |
| `TOTAL`                          | `ItemDemonstrativo.ValorPago`        | Decimal pt-BR (vírgula)                                                       |
| `LOCAL ATENDIMENTO`              | `Guia.LocalAtendimento`              | Texto livre informativo; backfill que não sobrescreve valor existente (D-043) |
| `HORA DE REALIZACAO`             | Ignorado                             | Não há campo horário em `ItemGuia`                                            |
| `QTDE APRESENTADA` / `QTDE PAGA` | Ignorados                            | Glosa já capturada por `ValorGlosado`                                         |
| `CUSTO` / `FILME`                | Ignorados                            | Sempre zero nos demonstrativos analisados                                     |

**Mapeamento FUNCAO → PosicaoExecutor:**

| FUNCAO (CSV)         | PosicaoExecutor                      |
| -------------------- | ------------------------------------ |
| `Anestesista`        | `Anestesista`                        |
| `Honorario princ.`   | `Cirurgiao`                          |
| `1º Auxiliar`        | `PrimeiroAuxiliar`                   |
| `2º Auxiliar`        | `SegundoAuxiliar`                    |
| `3º Auxiliar`        | `TerceiroAuxiliar`                   |
| Qualquer outro valor | Aviso; usa `Cirurgiao` como fallback |

**ViaAcesso:** sempre `NaoAplicavel` para itens importados. Procedimentos videolaparoscópicos têm `TemPorteProprioVideo = true` (código TISS próprio), portanto o `VideolaparoscopiaModifier` é ignorado independentemente do valor de `ViaAcesso`.

### Estrutura do CSV analítico UNIMED

```
Linha 1:  IDENTIFICADOR PAGAMENTO: {número}
Linha 2:  GUIA;LOTE;LOTE PRES.;CONTA;SOLICITANTE;CODIGO;BENEFICIARIO;PLANO;
          DATA CONTA;DATA SERVICO;CODIGO PROCEDIMENTO;NOME PROCEDIMENTO;FUNCAO;
          QTDE APRESENTADA;QTDE PAGA;CUSTO;FILME;HONORARIO;GLOSA;COD_GLOSA;
          TOTAL;EXECUTANTE DO SERVICO;LOCAL ATENDIMENTO;PRESTADOR ARQUIVO;
          ACOMODACAO;HORA DE REALIZACAO;ACRESCIMO;% VIA;
Linha 3+: dados (separador `;`, decimal pt-BR com vírgula)
```

Linhas sem `CODIGO PROCEDIMENTO` (ex: linhas de equipamento como `60024380 ALUGUEL/TAXA DE APARELHO`) são **ignoradas silenciosamente** — registradas no relatório de importação como "itens não mapeados".

### Agrupamento em guias

Todas as linhas com o mesmo valor na coluna `GUIA` + mesma `DATA SERVICO` formam uma única `Guia`. O campo `SENHA` da Guia recebe o valor da coluna `CODIGO` (carteirinha do beneficiário) da primeira linha do grupo — ou vazio se o campo for relaxado para nullable (ver IO-03).

### Estratégia de upsert

- **Guia:** busca por `(TenantId, PrestadorId, NumeroGuia)`. Se não encontrar, cria nova.
- **ItemGuia:** se a Guia foi encontrada (pré-existente), verifica se já existe `ItemGuia` com mesmo `ProcedimentoId` + `PosicaoExecutor`. Se sim, atualiza `PercentualOrdem`, `Acomodacao`, `EhUrgencia`. Se não, cria novo.
- **ItemDemonstrativo:** sempre cria novo (vinculado ao `DemonstrativoId` criado para este lote de importação + `ItemGuiaId`).
- **Beneficiário:** usa `LookupOrCreateAsync` por `Carteira` (normalizada uppercase).

---

## Ordem de execução

```
IO-01 (backend) → IO-02 (backend) → IO-03 (backend) → IO-04 (backend) → IO-05 (frontend)
```

IO-01 e IO-03 podem ser feitos em paralelo. IO-02 depende de IO-01. IO-04 depende de IO-01, IO-02 e IO-03. IO-05 depende de IO-04.

---

## IO-01 · PercentualOrdem — substituir enum por decimal

**TDD — escrever test primeiro.**

### Arquivos de teste modificados

`tests/Faturamento.Tests/Calculo/Unimed/Modifiers/OrdemProcedimentoModifierTests.cs`

Reescrever os 4 casos existentes para usar decimal:

```
Percentual1_0_RetornaFator1_0
  input: percentual=1.0m, valor=100m
  assert: passo.Fator == 1.0m, passo.ValorResultante == 100m

Percentual0_7_RetornaFator0_7
  input: percentual=0.7m, valor=100m
  assert: passo.Fator == 0.7m, passo.ValorResultante == 70m

Percentual0_5_RetornaFator0_5
  input: percentual=0.5m, valor=100m
  assert: passo.Fator == 0.5m, passo.ValorResultante == 50m

Percentual0_4_RetornaFator0_4
  input: percentual=0.4m, valor=100m
  assert: passo.Fator == 0.4m, passo.ValorResultante == 40m

Percentual0_3_RetornaFator0_3
  input: percentual=0.3m, valor=100m
  assert: passo.Fator == 0.3m, passo.ValorResultante == 30m
```

Atualizar `UnimedPipelineTests.cs`: substituir `OrdemProcedimento.SecundarioMesmaVia` etc. por `percentualOrdem: 0.5m`.

### Arquivos de implementação

| Arquivo                                                                 | Ação                                                                                                                                 |
| ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| `App/Faturamento/OrdemProcedimento.cs`                                  | **Excluir**                                                                                                                          |
| `App/Faturamento/ItemGuia.cs`                                           | Substituir `OrdemProcedimento` por `PercentualOrdem decimal`                                                                         |
| `App/Faturamento/GuiaService.cs`                                        | Atualizar `CriarItemGuiaCommand` e `CriarItemGuiaRequest`                                                                            |
| `App/Faturamento/Endpoints/GuiaEndpoints.cs`                            | Atualizar `CriarItemGuiaRequest`                                                                                                     |
| `App/Faturamento/Calculo/CalculoTypes.cs`                               | Substituir `OrdemProcedimento` por `PercentualOrdem decimal` em `ApurarItemInput`                                                    |
| `App/Faturamento/Calculo/Unimed/Modifiers/OrdemProcedimentoModifier.cs` | Reescrever: recebe `decimal percentualOrdem`, retorna `PassoApuracao("OrdemProcedimento", percentualOrdem, valor * percentualOrdem)` |
| `App/Faturamento/Calculo/Unimed/UnimedRuleSet.cs`                       | Atualizar chamada do modifier                                                                                                        |
| migration em `App/Faturamento/Migrations/`                              | `RenameOrdemProcedimentoToPercentualOrdem` — renomear coluna `ordem_procedimento` (int) para `percentual_ordem` (numeric(5,4))       |

**ItemGuia após mudança:**

```csharp
public decimal PercentualOrdem { get; private set; }  // ex: 1.0m, 0.7m, 0.5m, 0.4m

internal static ItemGuia Create(
    Guid guiaId, Guid procedimentoId, PosicaoExecutor posicao,
    decimal percentualOrdem,                          // substituiu OrdemProcedimento
    ViaAcesso via, Acomodacao acomodacao,
    bool ehUrgencia, decimal? valorApurado, int? tempoAnestesicoMin = null)
```

**OrdemProcedimentoModifier após mudança:**

```csharp
internal static PassoApuracao Aplicar(decimal percentualOrdem, decimal valorAtual)
{
    return new PassoApuracao("OrdemProcedimento", percentualOrdem, valorAtual * percentualOrdem);
}
```

O modifier deixa de ter lógica condicional — aplica o valor recebido diretamente. A responsabilidade de determinar qual percentual usar passa para quem cria o `ItemGuia` (formulário manual via `TabelaOrdemOperadora`, ou importação via coluna `% VIA`).

**Regra de validação:** `PercentualOrdem` deve estar entre `0.01` e `1.00` (inclusive). Lançar `ValidationError` se fora do range.

---

## IO-02 · TabelaOrdemOperadora — tabela parametrizável por operadora

**TDD — escrever test primeiro.**

### Arquivo de teste (novo)

`tests/Catalog.Tests/TabelaOrdemOperadora/TabelaOrdemOperadoraTests.cs`

```
namespace: Catalog.Tests.TabelaOrdemOperadora
fixture: PostgresContainerFixture
```

Casos obrigatórios:

```
SalvarTabela_NovosRegistros_PersisteTodos
  seed: operadora + tenant
  action: salvar 6 entradas (MesmaVia: 1→1.0, 2→0.5, 3→0.4, 4→0.3, 5→0.2, 6→0.1)
  assert: db.TabelasOrdemOperadora.Count(t => t.OperadoraId == oid) == 6

SalvarTabela_UpsertExistente_NaoDuplica
  salvar mesma (OperadoraId, NumeroProcedimento=2, MesmaVia) 2x com percentuais diferentes
  assert: count == 1; percentual == segundo valor

ResolverPercentual_TabelaExiste_RetornaPercentualConfigurado
  seed: tabela com MesmaVia, NumeroProcedimento=3, Percentual=0.40
  assert: resolver(operadoraId, 3, MesmaVia) == 0.40m

ResolverPercentual_TabelaNaoExiste_RetornaPadrao
  sem nenhuma tabela configurada
  assert: resolver(operadoraId, 2, MesmaVia) == 0.50m   (padrão)
  assert: resolver(operadoraId, 2, ViaDiferente) == 0.70m  (padrão)

ResolverPercentual_AlemUltimaPosicao_RetornaUltimoDefinido
  tabela com posições 1–6, 6 → 10%
  assert: resolver(operadoraId, 9, MesmaVia) == 0.10m  (clamped ao último)

ExcluirTabela_Remove_TodasAsColunasDaOperadora
  salvar 6 entradas, depois excluir todas
  assert: db.TabelasOrdemOperadora.Count(t => t.OperadoraId == oid) == 0
```

### Arquivos de implementação

| Arquivo                                                           | Ação                                                                                                                   |
| ----------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| `App/Catalog/TabelaOrdemOperadora.cs`                             | Novo — entidade                                                                                                        |
| `App/Catalog/TipoViaOrdem.cs`                                     | Novo — enum `{ MesmaVia, ViaDiferente }`                                                                               |
| `App/Catalog/Configurations/TabelaOrdemOperadoraConfiguration.cs` | Novo — EF config                                                                                                       |
| `App/Data/AppDbContext.cs`                                        | Adicionar `DbSet<TabelaOrdemOperadora>`                                                                                |
| `App/Catalog/CatalogService.cs`                                   | Métodos: `SalvarTabelaOrdemAsync`, `ListarTabelaOrdemAsync`, `ExcluirTabelaOrdemAsync`, `ResolverPercentualOrdemAsync` |
| `App/Catalog/Endpoints/CatalogEndpoints.cs`                       | 3 endpoints novos                                                                                                      |
| migration em `App/Catalog/Migrations/`                            | `AddTabelaOrdemOperadora`                                                                                              |

**Entidade:**

```csharp
internal sealed class TabelaOrdemOperadora : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OperadoraId { get; private set; }
    public int NumeroProcedimento { get; private set; }  // 1 = principal, 2, 3... N
    public TipoViaOrdem TipoVia { get; private set; }
    public decimal Percentual { get; private set; }      // 0.01 – 1.00
    // UK: (TenantId, OperadoraId, NumeroProcedimento, TipoVia)
}
```

**Tabela padrão (`ResolverPercentualOrdemAsync` quando nenhuma tabela existe):**

```csharp
private static readonly Dictionary<(int, TipoViaOrdem), decimal> _padrao = new()
{
    { (1, MesmaVia), 1.00m }, { (1, ViaDiferente), 1.00m },
    { (2, MesmaVia), 0.50m }, { (2, ViaDiferente), 0.70m },
    { (3, MesmaVia), 0.40m }, { (3, ViaDiferente), 0.50m },
    { (4, MesmaVia), 0.30m }, { (4, ViaDiferente), 0.40m },
    { (5, MesmaVia), 0.20m }, { (5, ViaDiferente), 0.30m },
    // 6+ → 0.10m para ambos
};
```

**Endpoints:**

```
GET    /api/v1/admin/operadoras/{operadoraId}/tabela-ordem
       → lista todas as entradas da tabela para a operadora
         (retorna array vazio se não configurada — o frontend mostra os valores padrão)

PUT    /api/v1/admin/operadoras/{operadoraId}/tabela-ordem
       body: [{ numeroProcedimento, tipoVia, percentual }, ...]
       → salva (upsert) a tabela completa da operadora (substitui tudo)
       → 422 se algum percentual fora de [0.01, 1.00]

DELETE /api/v1/admin/operadoras/{operadoraId}/tabela-ordem
       → exclui toda a tabela da operadora (volta aos padrões)
```

**Frontend — seção na tela de detalhe da Operadora:**

Adicionar seção "Tabela de Atos Múltiplos" no `OperadoraFormComponent` (modo edição), abaixo de "Porte Anestésico". Exibe uma tabela editável com duas colunas de percentual (Mesma Via / Via Diferente) e até 7 linhas (posições 1 a 6+). Pré-preenche com os padrões quando nenhuma tabela existe. Botão "Salvar tabela" envia `PUT`. Botão "Restaurar padrões" chama `DELETE` e recarrega os valores padrão visualmente.

**Formulário de guia — dropdown de Ordem:**

No `ItemGuiaFormComponent`, substituir o `<select>` de `OrdemProcedimento` por um dropdown que, ao ser carregado, consulta `GET /api/v1/admin/operadoras/{operadoraId}/tabela-ordem` (ou usa padrão local se não configurada) e monta opções como:

```
1º Procedimento — 100%
2º Mesma Via — 50%
2º Via Diferente — 70%
3º Procedimento — 40%
4º Procedimento — 30%
5º Procedimento — 20%
6º ou mais — 10%
```

Selecionar uma opção preenche automaticamente o signal `percentualOrdem` com o valor numérico. O campo `PercentualOrdem` fica visível como texto informativo ("Percentual: 40%") — editável somente em modo avançado (não MVP).

---

## IO-03 · NumeroGuia — novo campo em Guia

Alteração mínima, sem TDD obrigatório (campo de infraestrutura).

### Arquivos de implementação

| Arquivo                                      | Ação                                                                                               |
| -------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| `App/Faturamento/Guia.cs`                    | Adicionar `NumeroGuia string?`                                                                     |
| `App/Faturamento/GuiaService.cs`             | Adicionar `NumeroGuia?` em `CriarGuiaCommand`, `AtualizarGuiaCommand`, `GuiaDto`, `GuiaDetalheDto` |
| `App/Faturamento/Endpoints/GuiaEndpoints.cs` | Adicionar `NumeroGuia?` em `CriarGuiaRequest`, `AtualizarGuiaRequest`                              |
| migration em `App/Faturamento/Migrations/`   | `AddNumeroGuia` — coluna `numero_guia varchar(50) NULL`                                            |

`NumeroGuia` é opcional — guias criadas manualmente podem deixá-lo vazio. Para guias importadas, é preenchido com a coluna `GUIA` do CSV.

---

## IO-04 · Endpoint de importação de demonstrativo

**TDD — escrever test primeiro.**

### Arquivo de teste (novo)

`tests/Faturamento.Tests/Demonstrativo/ImportacaoDemonstrativoTests.cs`

```
namespace: Faturamento.Tests.Demonstrativo
fixture: PostgresContainerFixture
```

Casos obrigatórios:

```
ImportarCsv_AnestesistaValido_CriaGuiaItemDemonstrativo
  seed: prestador, operadora (Unimed), beneficiario, procedimento (TUSS 30501326),
        TabelaPorteAnestesico com PorteLetra do proc, DeflatorPrestador (Anestesista)
  csv: 1 guia, 1 item (Anestesista, % VIA 100, acomodação Enfermaria, sem urgência)
  assert: Guia criada com NumeroGuia = "34280511"
  assert: ItemGuia criado com PosicaoExecutor=Anestesista, PercentualOrdem=1.0m, ValorApurado IS NOT NULL
  assert: ItemDemonstrativo criado vinculado ao ItemGuia
  assert: ItemGuia.ValorLiquidado == ItemDemonstrativo.ValorPago
  assert: resultado.GuiasCriadas == 1, resultado.ItensCriados == 1

ImportarCsv_CirurgiaoMultiplosItens_OrdemPercentualCorreto
  seed: 3 procedimentos distintos, TabelaProcedimento para cada, DeflatorPrestador (Cirurgiao)
  csv: 1 guia, 3 itens (% VIA 100, 50, 40)
  assert: item[0].PercentualOrdem == 1.0m
  assert: item[1].PercentualOrdem == 0.5m
  assert: item[2].PercentualOrdem == 0.4m

ImportarCsv_GlosaItem_ValorLiquidadoZero
  csv: 1 item com QTDE PAGA=0, GLOSA > 0, COD_GLOSA="2602", TOTAL=0
  assert: ItemDemonstrativo.ValorPago == 0
  assert: ItemDemonstrativo.ValorGlosado > 0
  assert: ItemDemonstrativo.MotivoGlosa == "2602"
  assert: ItemGuia.ValorLiquidado == 0

ImportarCsv_BeneficiarioNaoExiste_CriaBeneficiario
  seed: nenhum beneficiário
  csv: 1 linha com CODIGO "0332800031800013", BENEFICIARIO "ANDREA SANTOS DE SOUZA"
  assert: Beneficiario criado com Carteira="0332800031800013", Nome="ANDREA SANTOS DE SOUZA"
  assert: resultado.BeneficiariosCriados == 1

ImportarCsv_ProcedimentoNaoEncontrado_RegistraErroEContinua
  csv: 2 itens, 1 com TUSS inexistente
  assert: resultado.ItensCriados == 1
  assert: resultado.Erros[0] contains TUSS inexistente
  assert: resultado.Erros[0].Linha == <linha do TUSS inválido>

ImportarCsv_GuiaJaExiste_NaoDuplicaGuia
  seed: guia pré-existente com NumeroGuia="34280511", PrestadorId=prestador
  csv: 1 guia com GUIA="34280511" e 1 item novo
  assert: Guias.Count(NumeroGuia="34280511") == 1  (não duplicou)
  assert: ItemGuia adicionado à guia existente
  assert: resultado.GuiasAtualizadas == 1, resultado.GuiasCriadas == 0

ImportarCsv_ItemEquipamento_Ignorado
  csv: 1 linha com CODIGO PROCEDIMENTO="60024380" (aluguel de equipamento)
       FUNCAO em branco
  assert: ItemGuia não criado para esse item
  assert: resultado.ItensIgnorados == 1

ImportarCsv_UrgenciaDetectada_EhUrgenciaTrue
  csv: 1 item com ACRESCIMO="30,00%"
  assert: ItemGuia.EhUrgencia == true

ImportarCsv_FormatoInvalido_RetornaErro400
  csv com linha 2 sem o header esperado
  assert: resultado HTTP 400 (não começa o processamento)

ImportarCsv_SomenteValidar_NaoPersiste
  csv válido, somenteValidar=true
  assert: nenhuma entidade criada no banco
  assert: resposta contém preview com GuiasPrevistas, ItensPrevistas
```

### Arquivos de implementação

| Arquivo                                               | Ação                                             |
| ----------------------------------------------------- | ------------------------------------------------ |
| `App/Faturamento/ImportacaoDemonstrativoService.cs`   | Novo — lógica de parse, validação e persistência |
| `App/Faturamento/Endpoints/DemonstrativoEndpoints.cs` | Adicionar 1 endpoint                             |
| `App/Faturamento/DemonstrativoService.cs`             | Sem alteração                                    |

**Endpoint:**

```
POST /api/v1/admin/demonstrativos/importar-csv
Content-Type: multipart/form-data
  arquivo:       File (CSV)
  prestadorId:   Guid
  operadoraId:   Guid
  somenteValidar: bool = false
```

Resposta `200 OK`:

```json
{
  "identificadorPagamento": "780091936",
  "somenteValidar": false,
  "demonstrativoId": "guid-ou-null-se-somenteValidar",
  "guiasCriadas": 12,
  "guiasAtualizadas": 3,
  "itensCriados": 28,
  "itensAtualizados": 5,
  "itensIgnorados": 2,
  "beneficiariosCriados": 4,
  "erros": [{ "linha": 7, "mensagem": "Procedimento TUSS '99999999' não encontrado no catálogo." }],
  "alertas": [
    { "linha": 12, "mensagem": "TabelaProcedimento ausente para 30101050 — ValorApurado ficará nulo." },
    { "linha": 15, "mensagem": "DeflatorPrestador ausente para Anestesista — ValorApurado ficará nulo." }
  ]
}
```

**Validações obrigatórias (retornam HTTP 400 se inválidas):**

1. Arquivo não vazio e extensão `.csv`.
2. Linha 2 contém o header esperado (presença das colunas `GUIA`, `CODIGO PROCEDIMENTO`, `HONORARIO`, `% VIA`, `ACOMODACAO`).
3. `prestadorId` existe no tenant.
4. `operadoraId` existe no tenant.

**Validações por linha (não abortam o batch — geram erro na linha):**

1. `CODIGO PROCEDIMENTO` não vazio e encontrado no catálogo.
2. `% VIA` é numérico entre 0 e 100.
3. `HONORARIO`, `GLOSA`, `TOTAL` são decimais válidos.
4. `ACOMODACAO` é um dos valores conhecidos.
5. `DATA SERVICO` é data válida.

**Validações por linha (geram aviso, não erro):**

1. `TabelaProcedimento` ausente para o par `(OperadoraId, ProcedimentoId)` — motor retornará `SemTabela`.
2. `DeflatorPrestador` ausente para o par `(PrestadorId, OperadoraId, PosicaoExecutor)` — motor retornará `SemDeflator`.
3. `NOME PROCEDIMENTO` diverge do `Procedimento.Descricao` cadastrado.
4. `EXECUTANTE DO SERVICO` difere do nome do `Prestador` selecionado.

**Lógica de processamento (quando `somenteValidar = false`):**

```
Para cada grupo GUIA + DATA SERVICO no CSV:
  1. LookupOrCreate Beneficiário (Carteira = CODIGO; Nome = BENEFICIARIO)
  2. Buscar Guia existente por (TenantId, PrestadorId, NumeroGuia=GUIA)
     → Se não existe: criar Guia com NumeroGuia, Senha=CODIGO, DataAtendimento, PrestadorId, OperadoraId, BeneficiarioId
  3. Para cada item do grupo (linhas com a mesma GUIA):
     a. Ignorar se FUNCAO vazia ou CODIGO PROCEDIMENTO não encontrado
     b. Buscar ItemGuia existente por (GuiaId, ProcedimentoId, PosicaoExecutor)
        → Se não existe: criar ItemGuia com PercentualOrdem=(%VIA/100), ViaAcesso=NaoAplicavel
        → Se existe: atualizar PercentualOrdem, Acomodacao, EhUrgencia
     c. Criar ItemDemonstrativo vinculado ao DemonstrativoId e ao ItemGuiaId
     d. SetValorLiquidado no ItemGuia
  4. Executar motor para todos os ItemGuia da Guia (mesmo padrão de CriarAsync)
  5. Verificar auto-liquidação (todos os itens com ValorLiquidado IS NOT NULL → Liquidar guia)

Criar 1 Demonstrativo para o lote inteiro:
  Demonstrativo.IdentificadorPagamento = linha 1 do CSV
  Demonstrativo.OperadoraId = operadoraId do request
  Demonstrativo.Competencia = data mais frequente entre os itens importados (ou mês/ano da data do arquivo)
```

**`ImportacaoDemonstrativoService` — estrutura:**

```csharp
internal sealed class ImportacaoDemonstrativoService(
    AppDbContext db, ICurrentUser currentUser, PricingRuleSetFactory factory, CatalogService catalog)
{
    internal async Task<ImportacaoResultado> ImportarAsync(
        Stream csvStream, Guid prestadorId, Guid operadoraId,
        bool somenteValidar, CancellationToken ct);

    private static IEnumerable<LinhaCSV> ParsearCsv(StreamReader reader);
    private static PosicaoExecutor? MapearFuncao(string funcao);
    private static Acomodacao MapearAcomodacao(string acomodacao);
    private static bool MapearUrgencia(string acrescimo);
    private static decimal MapearPercentualOrdem(string percentVia);
}
```

---

## IO-05 · Frontend — UI de importação de demonstrativo

### Arquivo de teste (novo)

`apps/admin-web/src/app/admin/faturamento/demonstrativos/importar-demonstrativo-modal/importar-demonstrativo-modal.component.spec.ts`

Casos:

```
Exibe_BotaoImportar_AbrirModal
Selecionar_Arquivo_HabilitaBotaoEnviar
SomenteValidar_True_ExibePreview_NaoFechaModal
Importacao_Completa_ExibeResumoEFechaModal
Erros_Exibidos_PorLinha_ComMensagem
Alertas_Exibidos_ComCorAmbar
```

### Componente: `ImportarDemonstrativoModalComponent`

`apps/admin-web/src/app/admin/faturamento/demonstrativos/importar-demonstrativo-modal/`

**Template (fluxo em 2 passos):**

**Passo 1 — Seleção:**

- Seletor de arquivo (accept=".csv")
- Select de Prestador (lista todos os prestadores do tenant)
- Select de Operadora (lista todas as operadoras do tenant)
- Botão "Validar" → chama endpoint com `somenteValidar=true`

**Passo 2 — Preview/resultado:**

- Exibe resumo do que será importado (guias, itens, beneficiários novos)
- Lista de erros (vermelho) e alertas (âmbar) por linha
- Se erros = 0: botão "Confirmar importação" → chama endpoint com `somenteValidar=false`
- Se erros > 0: mensagem "Corrija os erros antes de importar" + botão "Voltar"
- Após importação bem-sucedida: exibe resumo final e emite `importacaoConcluida` para o pai recarregar a lista

### Integração em `DemonstrativoListComponent`

Adicionar botão "Importar demonstrativo CSV" no header da lista. Ao clicar, abre `ImportarDemonstrativoModalComponent`. Ao receber `importacaoConcluida`, recarregar a lista.

---

## Critérios de pronto

- [x] `OrdemProcedimento` enum removido de todo o codebase (backend + frontend)
- [x] `ItemGuia.PercentualOrdem decimal` persistido corretamente, migration aplicada
- [x] `OrdemProcedimentoModifier` usa o percentual diretamente, sem switch/case
- [x] Todos os testes existentes de `UnimedPipelineTests` passando com a nova assinatura
- [x] `TabelaOrdemOperadora` com CRUD completo e testes xUnit cobrindo ≥ 80%
- [x] `ResolverPercentualOrdemAsync` retorna padrão correto quando sem tabela configurada
- [x] Seção "Tabela de Atos Múltiplos" funcional no detalhe da Operadora
- [x] Dropdown de ordem no formulário de guia exibe opções da tabela configurada
- [x] `Guia.NumeroGuia` persistido, migration aplicada
- [x] Endpoint `POST /api/v1/admin/demonstrativos/importar-csv` aceita `somenteValidar=true` e `false`
- [x] Importação cria `Guia` + `ItemGuia` + `ItemDemonstrativo` corretamente para anestesista e cirurgião
- [x] Motor roda após importação; `ValorApurado` e `ValorLiquidado` preenchidos
- [x] Itens de equipamento (FUNCAO vazia) ignorados silenciosamente
- [x] Erros por linha não abortam o batch; alertas de SemTabela/SemDeflator informados
- [x] Upsert correto: guia pré-existente não é duplicada
- [x] Frontend exibe preview antes de confirmar; erros bloqueiam confirmação
- [x] Cobertura ≥ 80% em todos os novos arquivos de teste

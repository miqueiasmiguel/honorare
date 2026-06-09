# Honorare — Domínio

## Glossário

### Conceitos centrais

- **Guia:** documento de cobrança apresentado pelo prestador (médico/clínica) à operadora. Tipos principais: Consulta, SP/SADT (serviços profissionais e SADT), Internação, Honorários. Uma guia tem múltiplos itens. `NumeroGuia string?` armazena o número TISS da guia (preenchido na importação via CSV; opcional em guias criadas manualmente). `LocalAtendimento string` (varchar 200, NOT NULL default `''`) é um campo de texto livre informativo — preenchido na importação CSV (coluna `LOCAL ATENDIMENTO`) ou editado no formulário; exibido na listagem, no detalhe da guia e por-guia no recurso/PDF. **Não é usado no cálculo** (o motor usa `Acomodacao`). Ver D-043.

- **ItemGuia:** uma linha da guia. Representa **um procedimento executado por um profissional num papel**. Se o cirurgião teve 2 auxiliares e 1 anestesista, a mesma cirurgia gera 4 itens (cirurgião, 1º aux, 2º aux, anestesista). `IncluidoNoRecurso bool` (default `true`): quando `false`, o item é ocultado do PDF do recurso sem ser destruído — a operação é reversível. Invariante: excluir o último item incluído de uma guia lança 409. Ao remover a guia do recurso, todos os itens voltam a `true`. Ver D-048.

- **Procedimento:** entrada do Rol de Procedimentos da ANS, identificada por código TUSS (Terminologia Unificada da Saúde Suplementar). Cada procedimento tem porte, porte anestésico, etc.

- **Demonstrativo:** entidade removida em RC-09; substituído por edição direta em `ItemGuia` (ver D-042).

- **ItemDemonstrativo:** entidade removida em RC-09; substituído por edição direta em `ItemGuia` (ver D-042).

- **Glosa:** valor não pago pela operadora, com motivo informado. Pode ser legítima (procedimento realmente não autorizado) ou indevida (oportunidade de contestar).

- **NumeroGuia:** número da guia de autorização emitido pela operadora. Campo obrigatório na `Guia` — é o identificador que a operadora usa para cruzar a guia apresentada com o demonstrativo de pagamento (anteriormente chamado de "Senha").

- **Conciliação:** edição direta de `ValorLiquidado` e `MotivoGlosa` por `ItemGuia` — via importação CSV da UNIMED ou edição inline no admin. **Auto-liquidação:** quando todos os `ItemGuia` de uma guia têm `ValorLiquidado IS NOT NULL`, a guia avança automaticamente para `Liquidada`. **Reversão:** ao limpar `ValorLiquidado` de qualquer item, se a guia estava `Liquidada`, ela volta para `Apresentada`.

- **Divergência:** quando o valor calculado pelo sistema difere do valor pago pela operadora. Pode indicar glosa indevida, subpagamento sem glosa, ou erro de cálculo.

- **Recurso:** documento formal gerado pelo sistema e enviado pelo admin à operadora para contestar divergências. Lista guias com divergência de um médico em um período, detalhando por procedimento o PG UNIMED, o VL Pendente e a observação de justificativa. É o entregável central do produto — substitui a planilha + PDF montados manualmente hoje.

- **VL CORRETO:** no contexto do recurso, o valor apurado pelo sistema para o procedimento (`ValorApurado`). **Não é a diferença** — é o valor cheio que deveria ter sido liquidado. A diferença a cobrar (RESTA PAGAR) é calculada no nível da guia: `sum(ValorApurado) − sum(ValorLiquidado)`.

- **Observação:** campo de texto livre por guia, preenchido pelo admin. Aparece em vermelho no recurso e no portal do médico. Explica a natureza da divergência (ex: "Valor pago a menor do acordado, foi pago como ambulatorial e se trata de uma cesariana").

- **Situação da guia:** status do ciclo de vida da guia no controle de pagamentos. Valores:
  - `Apresentada` — guia submetida à operadora, aguardando demonstrativo de pagamento (exibido em amarelo). Termo padrão do setor: "guia apresentada".
  - `Liquidada` — operadora liquidou integralmente (exibido em verde). "Liquidação" é o termo financeiro correto para o pagamento efetuado pelo convênio.
  - `EmRecurso` — guia com divergência incluída em recurso formal enviado à operadora (exibido em vermelho); armazena referência ao `Recurso`.

  Fluxo normal: `Apresentada` → `Liquidada` (automaticamente quando todos os `ItemGuia` têm `ValorLiquidado` preenchido). Fluxo de divergência: `Apresentada` → `EmRecurso`.

- **Recurso (entidade):** representa um conjunto de guias com divergência agrupadas num envio formal à operadora. Tem número próprio (`Numero`, `varchar(20)`) **informado manualmente pelo operador** — campo somente-dígitos, com zeros à esquerda preservados (ex: `202512`, `00042`). O formulário pré-preenche o número com o `AAAAMM` do **mês anterior** à data de emissão (a competência contestada), mas o operador pode sobrescrever; é obrigatório e validado no servidor. Ver D-046. O título do documento gerado é `[Nome do médico] CRM [número] — RECURSO [número]`. Guias incluídas em um recurso têm situação `EmRecurso` com referência ao número.

- **VL CORRETO:** label usado no PDF do recurso para o valor apurado por procedimento. Internamente o campo chama `ValorApurado`. "Apuração de honorários" é o processo padrão do setor; "valor apurado" é o resultado.

- **RESTA PAGAR:** label usado no PDF do recurso para a diferença por guia: `sum(ValorApurado) − sum(ValorLiquidado)`. É o valor que o admin reivindica no recurso.

- **Guia não recorrível:** guia em que **todos** os itens têm código TUSS presente em `Tenant.CodigosNaoRecorriveis`. `GuiaDto.NaoRecorrivel = true`. Exibida com badge "Não recorrível" na seleção de candidatas do recurso; o lote ("Adicionar todas") a pula automaticamente. O operador ainda pode adicioná-la individualmente via `AdicionarGuiaAsync` (escape hatch). Ver D-049.

- **Guia mista:** guia que tem **ao menos um** item com código NR e **ao menos um** item recorrível. `GuiaDto.MistaComNaoRecorriveis = true`. Incluída pelo lote, mas os itens NR recebem `IncluidoNoRecurso=false` automaticamente ao ser adicionada. Exibida com badge "Contém não recorrível". Os dois flags são mutuamente exclusivos; nenhuma guia tem ambos `true`. Ver D-049.

- **CodigosNaoRecorriveis:** lista de códigos TUSS configurada por tenant (`Tenant.CodigosNaoRecorriveis List<string>`, mapeada como `text[]` no Postgres). Define quais procedimentos o tenant não quer que entrem num recurso automaticamente (ex: consultas). Gerenciada na tela de Configurações do tenant.

### Atributos que afetam preço

- **Porte:** classificação de complexidade do procedimento (1A, 1B, 2A, ..., 16A, etc.). Define o valor base.

- **Porte anestésico:** classificação separada para anestesia, expressa como **letra A–Z (exceto O)** no `Procedimento`. Cada operadora mantém o par `(ValorEnfermaria, ValorApartamento)` (e opcionalmente `ValorAmbulatorial`) por letra em `TabelaPorteAnestesico`. Não confundir com porte cirúrgico.

- **Acomodação:** tipo de internação contratada no plano do paciente. Valores: Enfermaria, Apartamento, Ambulatorial. **Plano contratado é o que importa** para cálculo, não onde o paciente está fisicamente (com exceções abaixo).

- **Local de atendimento:** onde o procedimento aconteceu. Valores conceituais: Ambulatorial, Enfermaria, Apartamento, UTI, Day Clinic, PS/Consultório. **A chave de preço é `Acomodacao`** (Enfermaria/Apartamento/Ambulatorial), não este atributo. A captura informativa do local fica no campo de texto livre `Guia.LocalAtendimento` (armazena a string do CSV verbatim, sem restrição de enum) — ver glossário da Guia e D-043.

- **Via de acesso:** Convencional, Videolaparoscopia, Endoscópica, Percutânea, Não Aplicável.

- **Papel do executor:** Cirurgião, 1º Auxiliar, 2º Auxiliar, 3º Auxiliar, Anestesista, Clínico Assistente.

- **Urgência/emergência:** sim/não. Considera horário e dia da semana.

- **PercentualOrdem:** decimal (0.01–1.00) armazenado em `ItemGuia` que representa o fator de progressão de atos múltiplos. Substituiu o enum `OrdemProcedimento` (abolido em F3.8). O modifier aplica o valor diretamente, sem switch/case. Fonte do valor: formulário de guia (via `TabelaOrdemOperadora` da operadora) ou importação CSV (coluna `% VIA` ÷ 100).

### CBHPM e UCO

- **CBHPM:** Classificação Brasileira Hierarquizada de Procedimentos Médicos. Tabela de referência editada pela AMB. UNIMED usa CBHPM 2015 como base.

- **UCO:** Unidade de Custo Operacional. Valor unitário usado em algumas componentes do cálculo (raramente relevante no MVP UNIMED).

### Termos comerciais

- **Prestador:** médico ou profissional de saúde cadastrado dentro de um tenant. É o executor dos procedimentos nas guias. Identificado por nome e registro profissional (CRM/CRO/RQE). Um tenant pode ter múltiplos prestadores.

- **TabelaProcedimento:** valor negociado de um procedimento para uma operadora específica (tabela de honorários). A combinação `(OperadoraId, ProcedimentoId)` é única por tenant. Pode ser importada via CSV. É a fonte do `valor_base` no motor de cálculo cirúrgico.

- **TabelaPorteAnestesico:** valor de referência da anestesia por operadora e letra de porte (A–Z, exceto O). Cada porte tem par fixo `(ValorEnfermaria, ValorApartamento)` e opcional `ValorAmbulatorial`. A combinação `(OperadoraId, PorteLetra)` é única por tenant. Importada via CSV no formato UNIMED JPA (separador vírgula, decimal com vírgula entre aspas, 8 linhas de cabeçalho a ignorar). É a fonte do valor de referência no `AnestesiaCalculator` — substituiu o uso da `TabelaProcedimento` para a posição Anestesista.

- **TabelaOrdemOperadora:** tabela de progressão de atos múltiplos configurável por operadora. Define o `PercentualOrdem` para cada posição (`NumeroProcedimento` 1, 2, 3…) e tipo de via (`MesmaVia` / `ViaDiferente`). Chave única `(TenantId, OperadoraId, NumeroProcedimento, TipoVia)`. Quando não configurada, o sistema usa defaults embutidos: MesmaVia 100%/50%/40%/30%/20%/10%; ViaDiferente 100%/70%/50%/40%/30%/10% (6+→10% em ambas). Gerenciada via endpoints `GET/PUT/DELETE /api/v1/admin/operadoras/{id}/tabela-ordem`.

- **PosicaoExecutor:** papel do profissional na execução do procedimento. Valores: `Cirurgiao`, `PrimeiroAuxiliar`, `SegundoAuxiliar`, `TerceiroAuxiliar`, `Anestesista`, `ClinicoAssistente`. Os auxiliares aplicam os descontos de posição do `PosicaoExecutorModifier` (1º aux ×0.6 · 2º ×0.4 · 3º ×0.3) sobre o valor apurado.

- **Operadora:** plano de saúde. **Cada Unimed Singular (JPA, Recife, Fortaleza, etc.) é uma operadora separada** — não confundir com "UNIMED" como rede.

- **Tenant (configurações):** além dos dados operacionais, o tenant tem `Nome` (renomeável pelo `TenantAdmin`) e `LogoKey string?` — chave opaca apontando para o arquivo de logo armazenado via `IFileStorage` (nunca os bytes em banco). A logo (PNG ou JPEG, max 2 MB, validado por magic number) é renderizada no cabeçalho do PDF do recurso. Ver D-047.

## Regras de cálculo UNIMED

> ⚠️ Estas são as regras conhecidas até este momento. **Validar contra Instrução Geral do Rol Unimed e contra os 15-20 casos reais antes de tratar como verdade absoluta.**

### Valor base

`valor_base = TabelaProcedimento.Valor`

`TabelaProcedimento` armazena o valor negociado do procedimento para a operadora — é a fonte direta do `valor_base`. A tabela é específica de cada Unimed Singular. (Ver D-044: o deflator por prestador, sempre 100% na prática, foi removido.)

### Multiplicador UNIMED sobre CBHPM 2015

Historicamente UNIMED aplica acréscimo de **17,19%** sobre o valor da CBHPM 2015 (em vários componentes — verificar caso a caso). Isso é atualização da tabela base, **não** uma regra de acomodação.

**Atenção:** o "1,17x" às vezes é confundido com regra de enfermaria/apartamento. **Não é.** É o multiplicador de atualização da tabela.

Para **anestesia**, esse acréscimo **não é mais aplicado em runtime** — desde F3.6 o valor já vem pré-ajustado por operadora dentro da `TabelaPorteAnestesico`. Para procedimentos cirúrgicos a regra continua valendo na composição da `TabelaProcedimento` quando aplicável.

### Dobra por acomodação

- **Enfermaria contratada:** valor de referência, sem multiplicador.
- **Apartamento contratado:** honorários × 2 (a "dobra de apartamento"). **Aplica-se exclusivamente ao cirurgião. Auxiliares (1º/2º/3º), clínico assistente e anestesista em apartamento recebem fator 1,0.**
- **Ambulatorial:** sem dobra.
- **UTI:** segue o tipo de acomodação contratada (regra própria).

### Exceções de acomodação por falta de leito

- Paciente com direito a apartamento internado em enfermaria por falta de leito → **paga como apartamento** (dobra aplica).
- Paciente com direito a enfermaria internado em apartamento por falta de leito → **paga como enfermaria** (sem dobra).

A regra é: **plano contratado manda**, não o local físico.

### Procedimentos múltiplos

O fator de progressão é armazenado diretamente em `ItemGuia.PercentualOrdem` (decimal 0.01–1.00) e aplicado pelo `OrdemProcedimentoModifier` sem lógica condicional. Defaults quando nenhuma `TabelaOrdemOperadora` está configurada:

- 1º procedimento: 100%
- 2º mesma via: **50%** · 2º via diferente: **70%**
- 3º: 40% · 4º: 30% · 5º: 20% · 6º ou mais: 10%

A progressão real pode variar por Unimed Singular — configure via seção "Tabela de Atos Múltiplos" na tela da operadora.

O mesmo código TUSS pode aparecer múltiplas vezes numa guia quando o procedimento é realizado em vias de acesso diferentes (ex: denervação percutânea de faceta 3×, cada uma em nível vertebral diferente). Cada ocorrência é um `ItemGuia` separado com seu próprio `PercentualOrdem`. Aparece em guias reais do cliente.

### Videolaparoscopia

- Acréscimo de **50%** sobre o valor — **mas apenas se** o código TUSS é convencional e está sendo executado por vídeo.
- Se o código TUSS já tem porte próprio para vídeo, **não aplica o acréscimo** (já está embutido no porte).

A entidade `Procedimento` deve ter flag `TemPorteProprioVideo` para o motor saber.

### Auxiliares cirúrgicos

Percentuais **CBHPM 2018+** (atualização vs. regra antiga de 30%/20%):

- 1º auxiliar: **60%** do honorário do cirurgião.
- 2º auxiliar: **40%** do honorário do cirurgião.
- 3º auxiliar: **30%** do honorário do cirurgião.
- Aplica sobre o valor já com todos os outros multiplicadores.

> ⚠️ Confirmar com guias reais (P0.2) — os percentuais de auxiliar podem variar por Singular.

### Urgência/emergência

- Acréscimo de **30%** sobre o porte.
- Considera-se urgência: entre 19h e 7h, sábados, domingos e feriados.
- **Não aplica em SADT** (exames, diagnósticos). A entidade `Procedimento` deve ter flag `EhSadt`.

**Detecção via CSV analítico UNIMED:** a coluna `ACRESCIMO` contém o percentual de acréscimo aplicado pela operadora (ex: `0,00%` ou `30,00%`). O sistema detecta urgência **somente quando o valor for estritamente maior que zero** — `0,00%` significa sem urgência. Uma string vazia ou ausente também é sem urgência. Nunca tratar presença de qualquer valor não-vazio como urgência.

### Ordem dos modifiers no pipeline

A ordem afeta o resultado. **Esta é a ordem implementada no `UnimedRuleSet`, validada por 10 cenários E2E (F3.2).**

1. Valor base: `TabelaProcedimento.Valor`
2. Ordem de procedimento: aplica `ItemGuia.PercentualOrdem` diretamente (ex: 1.0, 0.5, 0.4…) — sem enum, sem switch/case
3. Videolaparoscopia: `Via=Videolaparoscopia` e `!TemPorteProprioVideo` → ×1.5; senão ×1.0
4. Acomodação: `Apartamento && Posicao == Cirurgiao` → ×2.0; demais → ×1.0
5. Urgência: `EhUrgencia` e `!EhSadt` → ×1.3; senão ×1.0
6. Posição do executor: PrimeiroAuxiliar=0.6 · SegundoAuxiliar=0.4 · TerceiroAuxiliar=0.3 · demais=1.0

Confirmar com guias reais (P0.2) se há variação por Singular. Anestesista usa pipeline próprio — ver seção abaixo.

**Early-exits do pipeline cirúrgico:** `SemTabela` (sem TabelaProcedimento para a operadora). Assim como na anestesia, este early-exit bloqueia a criação da guia. O motor roda em pré-voo — ver D-038.

**Diagnóstico em guias existentes:** `GET /api/v1/admin/guias/{id}/calculo` sempre re-executa o motor para itens sem `ValorApurado` e retorna a `SituacaoApuracao` real (`SemTabela`, `Indeterminado`), em vez de mostrar um valor genérico. Permite ao admin diagnosticar o que falta no catálogo sem precisar inspecionar o banco.

**Recálculo após correção de catálogo:** `POST /api/v1/admin/guias/{id}/recalcular` descarta o `Calculo` anterior, zera `ValorApurado` e re-apura todos os itens. Usar quando uma tabela for adicionada ou corrigida após a criação da guia (guias legadas importadas antes da validação obrigatória).

## Anestesia — pipeline próprio

Anestesista tem mecânica separada dos honorários cirúrgicos, implementada em `UnimedRuleSet.ApurarAnestesistaAsync` + `AnestesiaCalculator`. **Não passa pelo `AcomodacaoModifier`** — a acomodação já determina o valor de referência na seleção da `TabelaPorteAnestesico`. Também **não** aplica o multiplicador `UnimedAN×1,1719` nem `TempoExtra` (suspensos em F3.6).

**Seleção do valor de referência pela acomodação:**

- Apartamento → `TabelaPorteAnestesico.ValorApartamento`
- Ambulatorial → `TabelaPorteAnestesico.ValorAmbulatorial ?? ValorEnfermaria`
- Enfermaria / demais → `TabelaPorteAnestesico.ValorEnfermaria`

**Pipeline (ordem obrigatória):**

1. `ValorBase = valorReferencia`
2. `PercentualOrdem`: aplica `ItemGuia.PercentualOrdem` diretamente (mesmo mecanismo do pipeline cirúrgico)
3. `Urgencia`: `EhUrgencia && !EhSadt` → ×1.3; senão ×1.0

**Early-exits do motor:** `Indeterminado` (PorteAnestesico nulo no Procedimento), `SemTabela` (sem TabelaPorteAnestesico para o porte).

**Estes early-exits bloqueiam a criação da guia.** O motor roda em pré-voo antes de persistir qualquer dado. Se qualquer item retornar status diferente de `Calculado`, a operação é rejeitada com erro descritivo. Guias nunca são criadas sem ter `ValorApurado` em todos os itens. Ver D-038.

## Casos especiais não tratados no MVP

Não implementar agora, mas saber que existem:

- **Pacotes** — preço flat negociado para um conjunto de procedimentos, ignorando a apuração por porte individual. **Aparece em guias reais do cliente atual** (ex: "PACOTE ABLAÇÃO R$ 2.500,00"). No MVP, o admin informa manualmente o `ValorApurado` na guia quando for pacote — a apuração não é invocada, mas o valor entra no recurso normalmente. Tratar como entrada manual obrigatória até ter suporte nativo.
- **OPME** (Órteses, Próteses e Materiais Especiais — outra mecânica de cobrança)
- **Lista Referencial de Intercâmbio** (quando beneficiário de uma Unimed usa rede de outra)
- **UTI específica** (regras detalhadas próprias)

Esses casos vão gerar erros ou cálculos errados se aparecerem. Documentar e tratar conforme a dor aparecer.

## UNIMED não é uma operadora — são ~340 cooperativas

Cada Unimed Singular (João Pessoa, Recife, BH, Dracena, etc.) é uma operadora independente:

- Tabela de honorários própria
- Valores de UCO próprios
- Pode ter ajustes locais nas Instruções Gerais

No modelo de dados, **cada Unimed Singular é uma `Operadora` separada**. A regra geral UNIMED está no `IPricingRuleSet` `UnimedRuleSet`, mas tabelas e valores são por Singular.

## Auditoria de glosa — o que o sistema produz

Para cada `ItemGuia` calculado, o sistema gera:

- `ValorApurado`: resultado da apuração de honorários — o que deveria ter sido liquidado (aparece como "VL CORRETO" no recurso)
- `ValorLiquidado`: o que a operadora efetivamente liquidou (aparece como "PG UNIMED" no recurso; editado via importação CSV ou inline no admin)
- `MotivoGlosa`: código de glosa informado pela operadora (ex: "CB"); preenchido via importação CSV ou edição inline
- `Divergencia`: classificação (Conforme, SubLiquidado, SobrepLiquidado, Pendente, IndeterminadoCalculo)
- `Trace`: passo a passo de qual regra aplicou e como (auditoria/explicabilidade)

**Sem o trace não há auditoria — há chute.** O trace é o que permite ao admin contestar uma glosa fundamentadamente.

A apuração é **independente por item**: cada `ItemGuia` é calculado só com seus próprios atributos + a operadora, sem contexto cruzado entre itens. Por isso o operador pode acrescentar um item esquecido a uma guia já existente (inclusive já `EmRecurso`) e apurar apenas esse item, sem reapurar nem perturbar os demais (ver D-045).

## Convênios sem apuração de honorários

Para operadoras sem tabela de honorários negociada (convênios diversos, pequenos), o sistema opera em modo simplificado via `NullRuleSet` (`TipoRuleSet.Nulo`):

- Nenhum `ValorApurado` é gerado
- A guia funciona como registro de status + observação
- Quando liquidado a menor, o admin registra no campo `Observacao` o que falta receber
- O recurso pode ser gerado sem VL CORRETO apurado — só com a observação textual
- **A validação pré-criação de guia (D-038) não se aplica a operadoras com `TipoRuleSet.Nulo`** — elas são isentas por design

Esse modo não substitui o recurso UNIMED, mas permite ao cliente usar o mesmo sistema para controle de todos os convênios.

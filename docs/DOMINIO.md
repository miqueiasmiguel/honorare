# Honorare — Domínio

## Glossário

### Conceitos centrais

- **Guia:** documento de cobrança apresentado pelo prestador (médico/clínica) à operadora. Tipos principais: Consulta, SP/SADT (serviços profissionais e SADT), Internação, Honorários. Uma guia tem múltiplos itens.

- **ItemGuia:** uma linha da guia. Representa **um procedimento executado por um profissional num papel**. Se o cirurgião teve 2 auxiliares e 1 anestesista, a mesma cirurgia gera 4 itens (cirurgião, 1º aux, 2º aux, anestesista).

- **Procedimento:** entrada do Rol de Procedimentos da ANS, identificada por código TUSS (Terminologia Unificada da Saúde Suplementar). Cada procedimento tem porte, porte anestésico, etc.

- **Demonstrativo:** documento que a operadora devolve ao prestador informando o que foi pago e o que foi glosado.

- **Glosa:** valor não pago pela operadora, com motivo informado. Pode ser legítima (procedimento realmente não autorizado) ou indevida (oportunidade de contestar).

- **Senha:** código de pré-autorização emitido pela operadora para o procedimento. Campo obrigatório na `Guia` — é o identificador que a operadora usa para cruzar a guia apresentada com o demonstrativo de pagamento.

- **Conciliação:** processo de match entre item de guia (o que o médico cobrou) e item do demonstrativo (o que a operadora pagou).

- **Divergência:** quando o valor calculado pelo sistema difere do valor pago pela operadora. Pode indicar glosa indevida, subpagamento sem glosa, ou erro de cálculo.

- **Recurso:** documento formal gerado pelo sistema e enviado pelo admin à operadora para contestar divergências. Lista guias com divergência de um médico em um período, detalhando por procedimento o PG UNIMED, o VL Pendente e a observação de justificativa. É o entregável central do produto — substitui a planilha + PDF montados manualmente hoje.

- **VL CORRETO:** no contexto do recurso, o valor apurado pelo sistema para o procedimento (`ValorApurado`). **Não é a diferença** — é o valor cheio que deveria ter sido liquidado. A diferença a cobrar (RESTA PAGAR) é calculada no nível da guia: `sum(ValorApurado) − sum(ValorLiquidado)`.

- **Observação:** campo de texto livre por guia, preenchido pelo admin. Aparece em vermelho no recurso e no portal do médico. Explica a natureza da divergência (ex: "Valor pago a menor do acordado, foi pago como ambulatorial e se trata de uma cesariana").

- **Situação da guia:** status do ciclo de vida da guia no controle de pagamentos. Valores:
  - `Apresentada` — guia submetida à operadora, aguardando demonstrativo de pagamento (exibido em amarelo). Termo padrão do setor: "guia apresentada".
  - `Liquidada` — operadora liquidou integralmente (exibido em verde). "Liquidação" é o termo financeiro correto para o pagamento efetuado pelo convênio.
  - `EmRecurso` — guia com divergência incluída em recurso formal enviado à operadora (exibido em vermelho); armazena referência ao `Recurso`.

  Fluxo normal: `Apresentada` → `Liquidada`. Fluxo de divergência: `Apresentada` → `EmRecurso`.

- **Recurso (entidade):** representa um conjunto de guias com divergência agrupadas num envio formal à operadora. Tem número próprio no formato `AAAAMM` + sequencial (ex: `202512` = primeiro recurso de dezembro/2025). O título do documento gerado é `[Nome do médico] CRM [número] — RECURSO [número]`. Guias incluídas em um recurso têm situação `EmRecurso` com referência ao número.

- **VL CORRETO:** label usado no PDF do recurso para o valor apurado por procedimento. Internamente o campo chama `ValorApurado`. "Apuração de honorários" é o processo padrão do setor; "valor apurado" é o resultado.

- **RESTA PAGAR:** label usado no PDF do recurso para a diferença por guia: `sum(ValorApurado) − sum(ValorLiquidado)`. É o valor que o admin reivindica no recurso.

### Atributos que afetam preço

- **Porte:** classificação de complexidade do procedimento (1A, 1B, 2A, ..., 16A, etc.). Define o valor base.

- **Porte anestésico:** classificação separada para anestesia (1 a 8). Não confundir com porte cirúrgico.

- **Acomodação:** tipo de internação contratada no plano do paciente. Valores: Enfermaria, Apartamento, Ambulatorial. **Plano contratado é o que importa** para cálculo, não onde o paciente está fisicamente (com exceções abaixo).

- **Local de atendimento:** onde o procedimento aconteceu. Valores: Ambulatorial, Enfermaria, Apartamento, UTI, Day Clinic, PS/Consultório.

- **Via de acesso:** Convencional, Videolaparoscopia, Endoscópica, Percutânea, Não Aplicável.

- **Papel do executor:** Cirurgião, 1º Auxiliar, 2º Auxiliar, 3º Auxiliar, Anestesista, Clínico Assistente.

- **Urgência/emergência:** sim/não. Considera horário e dia da semana.

- **Ordem do procedimento:** Único, Principal, Secundário Mesma Via, Secundário Via Diferente.

### CBHPM e UCO

- **CBHPM:** Classificação Brasileira Hierarquizada de Procedimentos Médicos. Tabela de referência editada pela AMB. UNIMED usa CBHPM 2015 como base.

- **UCO:** Unidade de Custo Operacional. Valor unitário usado em algumas componentes do cálculo (raramente relevante no MVP UNIMED).

### Termos comerciais

- **Deflator:** percentual negociado entre operadora e prestador sobre o valor de tabela. Varia por prestador.

- **Operadora:** plano de saúde. **Cada Unimed Singular (JPA, Recife, Fortaleza, etc.) é uma operadora separada** — não confundir com "UNIMED" como rede.

## Regras de cálculo UNIMED

> ⚠️ Estas são as regras conhecidas até este momento. **Validar contra Instrução Geral do Rol Unimed e contra os 15-20 casos reais antes de tratar como verdade absoluta.**

### Valor base

Valor base = `Valor de tabela do procedimento` × `Deflator do prestador`.

A tabela é específica de cada Unimed Singular.

### Multiplicador UNIMED sobre CBHPM 2015

UNIMED aplica acréscimo de **17,19%** sobre o valor da CBHPM 2015 para o porte anestésico (e em vários outros componentes — verificar caso a caso). Isso é atualização da tabela base, **não** uma regra de acomodação.

**Atenção:** o "1,17x" às vezes é confundido com regra de enfermaria/apartamento. **Não é.** É o multiplicador de atualização da tabela.

### Dobra por acomodação

- **Enfermaria contratada:** valor de referência, sem multiplicador.
- **Apartamento contratado:** honorários × 2 (a "dobra de apartamento").
- **Ambulatorial:** sem dobra.
- **UTI:** segue o tipo de acomodação contratada (regra própria).

### Exceções de acomodação por falta de leito

- Paciente com direito a apartamento internado em enfermaria por falta de leito → **paga como apartamento** (dobra aplica).
- Paciente com direito a enfermaria internado em apartamento por falta de leito → **paga como enfermaria** (sem dobra).

A regra é: **plano contratado manda**, não o local físico.

### Procedimentos múltiplos

- Procedimento principal: 100% do valor.
- Secundário na mesma via de acesso: **50%** do valor calculado.
- Secundário em via diferente: **70%** do valor calculado.

O mesmo código TUSS pode aparecer múltiplas vezes numa guia quando o procedimento é realizado em vias de acesso diferentes (ex: denervação percutânea de faceta 3×, cada uma em nível vertebral diferente). Cada ocorrência é um `ItemGuia` separado com seu próprio percentual. Aparece em guias reais do cliente.

### Videolaparoscopia

- Acréscimo de **50%** sobre o valor — **mas apenas se** o código TUSS é convencional e está sendo executado por vídeo.
- Se o código TUSS já tem porte próprio para vídeo, **não aplica o acréscimo** (já está embutido no porte).

A entidade `Procedimento` deve ter flag `TemPorteProprioVideo` para o motor saber.

### Auxiliares cirúrgicos

- 1º auxiliar: **30%** do honorário do cirurgião.
- 2º e 3º auxiliares: **20%** do honorário do cirurgião.
- Aplica sobre o valor já com todos os outros multiplicadores.

### Urgência/emergência

- Acréscimo de **30%** sobre o porte.
- Considera-se urgência: entre 19h e 7h, sábados, domingos e feriados.
- **Não aplica em SADT** (exames, diagnósticos). A entidade `Procedimento` deve ter flag `EhSadt`.

### Ordem dos modifiers no pipeline

Importante: a ordem afeta o resultado.

1. Valor base (porte × deflator)
2. Redutor de procedimento múltiplo (50% mesma via / 70% via diferente)
3. Acréscimo de videolaparoscopia (+50%, se aplicável)
4. Dobra por acomodação (×2 se apartamento contratado)
5. Acréscimo de urgência (+30%, se aplicável e não SADT)
6. Ajuste por papel do executor (auxiliares 30%/20% do total)

**Validar essa ordem contra casos reais.** A ordem aqui é hipótese baseada em prática comum; pode haver variação por Singular.

## Anestesia — caso especial

Anestesia tem mecânica suficientemente diferente de honorários cirúrgicos:
- Porte AN próprio (1 a 8), não o porte cirúrgico
- Cálculo envolve tempo anestésico em minutos
- Multiplicador 17,19% UNIMED sobre CBHPM 2015

Recomendação: **`AnestesiaCalculator` separado** dentro de `Faturamento/Calculo/Unimed/`, ou `IPricingRuleSet` próprio. Não enfiar no mesmo pipeline dos honorários cirúrgicos.

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
- Deflatores negociados próprios
- Pode ter ajustes locais nas Instruções Gerais

No modelo de dados, **cada Unimed Singular é uma `Operadora` separada**. A regra geral UNIMED está no `IPricingRuleSet` `UnimedRuleSet`, mas tabelas e valores são por Singular.

## Auditoria de glosa — o que o sistema produz

Para cada `ItemGuia` calculado, o sistema gera:
- `ValorApurado`: resultado da apuração de honorários — o que deveria ter sido liquidado (aparece como "VL CORRETO" no recurso)
- `ValorLiquidado`: o que a operadora efetivamente liquidou (aparece como "PG UNIMED" no recurso; vem do demonstrativo)
- `Divergencia`: classificação (Conforme, SubLiquidado, SobrepLiquidado, Pendente, IndeterminadoCalculo)
- `Trace`: passo a passo de qual regra aplicou e como (auditoria/explicabilidade)

**Sem o trace não há auditoria — há chute.** O trace é o que permite ao admin contestar uma glosa fundamentadamente.

## Convênios sem apuração de honorários

Para operadoras sem tabela de honorários negociada (convênios diversos, pequenos), o sistema opera em modo simplificado via `NullRuleSet`:
- Nenhum `ValorApurado` é gerado
- A guia funciona como registro de status + observação
- Quando liquidado a menor, o admin registra no campo `Observacao` o que falta receber
- O recurso pode ser gerado sem VL CORRETO apurado — só com a observação textual

Esse modo não substitui o recurso UNIMED, mas permite ao cliente usar o mesmo sistema para controle de todos os convênios.

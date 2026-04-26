# Honorare — Projeto

## O que é

SaaS para controle de pagamentos médicos focado em UNIMED. Não é prontuário, agenda ou gestão de clínica. É um **conta-corrente do médico com a operadora**: prevê o que o médico deveria receber e concilia com o que efetivamente foi pago.

## Cliente

Empresa única (billing company) que faz controle de pagamento para médicos, principalmente cirurgiões e anestesistas que atendem UNIMED. Hoje opera com planilhas Excel.

O cliente é a billing company. Os médicos são **usuários finais** com login próprio para visualizar seus pagamentos e relatórios.

## Problema que resolve

Médico que atende convênio não tem visão clara de:
- Quanto vai receber por cada procedimento (regras complexas: porte, dobra de apartamento, via de acesso, urgência, papel)
- O que já foi pago vs o que ainda está em aberto
- Quando a operadora glosou indevidamente (oportunidade de contestar)

A billing company hoje faz esse controle manual em planilhas. Honorare automatiza.

## Escopo do MVP

1. **Autenticação multi-nível** (Admin + Médico, multi-tenant)
2. **Cadastro manual de dados** (sem OCR no MVP — entrada manual igual à planilha atual)
3. **Cálculo de pagamentos** (motor com regras UNIMED — indispensável; é o que determina o VL CORRETO de cada procedimento)
4. **Conciliação** com demonstrativos da operadora (registra o PG UNIMED por item)
5. **Geração de recurso** (PDF formatado por médico/período listando guias com divergência, VL CORRETO, valor pago, valor a cobrar e observação — este é o entregável central do produto)
6. **Portal do médico** (PWA) para visualizar guias pendentes e observações
7. **Auditoria de glosa** com identificação de divergências

**Campo Observação:** toda guia tem um campo de texto livre `Observacao`, preenchido pelo admin, visível ao médico. É onde se registra a justificativa da divergência (ex: "Valor pago a menor do acordado, foi pago como ambulatorial e se trata de uma cesariana"). Esse campo aparece em destaque no recurso gerado.

## Fora do escopo do MVP

- OCR/extração automática de fotos (fase 2 — entrada via foto de guia)
- App nativo Flutter (escolhido PWA; Flutter como fase 2 se justificar)
- Cobrança automática de assinatura SaaS (manual no MVP)
- Múltiplos clientes/billing companies (estrutura multi-tenant pronta, mas operação inicial é cliente único)
- Outras operadoras além de UNIMED com cálculo automático (estrutura pluggable pronta via `IPricingRuleSet`; convênios sem tabela negociada usam `NullRuleSet` — só controle de status e observação, sem cálculo de valor esperado)

## Volume esperado

100-500 guias/mês no cliente atual.

## Prazo realista

12-16 semanas focadas para MVP completo (backend + admin + PWA do médico básico).
Cortes possíveis trazem para 6-10 semanas se necessário (ver `PROXIMOS_PASSOS.md`).

## Equipe e ferramentas

- Desenvolvimento solo (provavelmente)
- Claude Code como assistente
- Repositório em monorepo

## Stakeholders críticos

- **Cliente atual** (billing company): decisões de produto, validação de fluxo
- **Médicos do cliente**: usuários do PWA, validação de UX
- **Operadoras (UNIMED)**: contraparte; sistema precisa estar correto contra suas regras para gerar contestações válidas

## Pendências críticas (não-código)

- [x] Verificar marca "Honorare" no INPI (classes 9, 35, 36, 42) — disponível
- [ ] Registrar domínios `.com.br`, `.app.br`, `.med.br` — disponíveis, compra pendente
- [ ] Conseguir 15-20 guias reais UNIMED já pagas para validação do motor de cálculo
- [ ] Definir contrato com cliente: limitação de responsabilidade, LGPD (controlador vs operador)

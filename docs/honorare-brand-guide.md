# Honorare — Brand Guide v0.1

> **Status:** rascunho de fundação. Ainda não validado contra cliente real, médicos usuários ou olho profissional de design. Itens marcados `[PROVISÓRIO]` precisam de validação antes de serem tratados como definitivos.

---

## 1. Posicionamento

### O que Honorare é
Honorare é o **conta-corrente do médico com a operadora**. Calcula o que cada procedimento deveria pagar, concilia com o que foi efetivamente pago, e identifica divergências passíveis de contestação.

### O que Honorare **não** é
- Não é prontuário eletrônico
- Não é agenda médica
- Não é gestão de clínica
- Não é "saúde" — é **financeiro especializado para médicos**

Essa distinção governa toda decisão visual e de copy. Se um elemento (cor, ícone, ilustração, palavra) puxar a marca para "saúde" em vez de "financeiro", está errado.

### Para quem
Cliente direto: billing companies (empresas que fazem controle de pagamento médico).
Usuário final: médicos, especialmente cirurgiões e anestesistas atendendo UNIMED.

### Categoria mental
Honorare está mais próximo de **Conta Azul, Stripe, Wise, Linear** do que de **Doctoralia, iClinic, Memed, Tasy**. A vibe é fintech especializada / produto de produtividade — não software hospitalar.

---

## 2. Atributos da marca

Definidos em conjunto com o dono do projeto e fixados como bússola para decisões.

| Atributo | Significa | Não significa |
|---|---|---|
| **Modernidade** | Produto contemporâneo, bem desenhado, atualizado | Trendy, gradiente roxo, ilustrações 3D, "tech jovem" |
| **Alívio** | "Alguém cuida disso por mim", calma, confiança silenciosa | Fofo, descontraído, mascote, copy "amigável demais" |
| **Voz direta e clara** | Fala como gente sensata, sem jargão nem firula | Frio robótico, formal jurídico, gíria fintech |

**Tensão a gerenciar:** "modernidade" + "alívio" pode virar "moderno calmo" (Linear, Things, Stripe Atlas) ou "moderno energético" (Notion, Figma). Honorare é **moderno calmo**. Sempre.

---

## 3. Voz e tom

### Princípios de copy

1. **Diga o que importa, depois explique.** "Sua guia foi paga abaixo do esperado. Faltam R$ 340,00 — dá para contestar."
2. **Não use jargão de design ou tech.** Diga "valor pago", não "amount". Diga "guia", não "claim".
3. **Não use jargão financeiro inflado.** Diga "diferença", não "divergência financeira material".
4. **Use português direto.** Frases curtas. Vírgulas em vez de travessões dramáticos.
5. **Não tente parecer humano demais.** Sem emoji, sem "Oi! 👋", sem exclamações. Médico cirurgião não tem paciência para isso.
6. **Reconheça o problema antes de oferecer solução.** "Demonstrativo difícil de ler? Esse é o ponto." é melhor do que "Simplifique seu controle financeiro!".

### Exemplos lado-a-lado

| ❌ Evitar | ✅ Preferir |
|---|---|
| "Ops! Algo deu errado 😅" | "Não consegui salvar. Tenta de novo?" |
| "Identificamos uma possível glosa indevida" | "Essa guia foi paga R$ 340 a menos. Dá para contestar." |
| "Dashboard de Performance Financeira" | "Pagamentos do mês" |
| "Onboarding completo!" | "Pronto. Seus dados estão configurados." |
| "Suas guias estão sendo processadas..." | "Calculando." |
| "Bem-vindo ao Honorare!" | "Bom te ver." (ou nada — vai direto à tela) |

### Tom em situações específicas

- **Erro do usuário:** factual, sem culpar. *"O CPF tem 11 dígitos. Esse veio com 10."*
- **Erro do sistema:** assume responsabilidade. *"Não consegui carregar suas guias. Já tentamos de novo automaticamente."*
- **Boa notícia (pagamento conforme):** discreta. *"Conforme: R$ 1.580,00."* — sem emoji, sem "Parabéns!"
- **Má notícia (divergência):** direta e útil. *"Diferença de R$ 340. Provável: tabela de porte aplicada errada."*
- **Email/notificação:** assunto descritivo, não-clickbait. *"3 divergências novas em guias de outubro"*, não *"Você precisa ver isso!"*

---

## 4. Paleta de cores

> **[PROVISÓRIO]** Valores hexadecimais são ponto de partida. Validar em telas reais com dados reais antes de fixar. Especialmente: contraste em mobile, legibilidade em condições de baixa luz, e como a paleta se comporta com tabelas densas de números.

### Princípio
Off-white quente ("papel de algodão de boa qualidade"), tinta marrom-quase-preta, acentos terrosos parcimoniosos. Verde e vermelho de status pertencem à mesma família terrosa — não são primários puros.

### Cores

| Nome | Hex | Uso |
|---|---|---|
| Pergaminho | `#F6F1EA` | Background principal de páginas |
| Pergaminho claro | `#FBF7F1` | Cards, superfícies elevadas, modais |
| Tinta | `#1F1B16` | Texto principal. Nunca usar `#000000` |
| Tinta secundária | `#6B6259` | Labels, metadados, texto auxiliar |
| Tinta terciária | `#9A8F82` | Placeholders, texto desabilitado |
| Borda discreta | `#E5DCCC` | Divisões sutis, separadores |
| Borda média | `#CFC4B0` | Bordas de inputs, tabelas |
| Terracota (acento da marca) | `#B8593A` | Logo, links importantes, CTA primário. **Parcimônia máxima.** |
| Terracota escuro | `#8F4329` | Hover/active de terracota |
| Verde-musgo (positivo) | `#5C7A4E` | "Conforme", "Pago" |
| Verde-musgo claro | `#E8EFE2` | Background de badges positivas |
| Ferrugem (negativo) | `#A14B3A` | "Divergência", "Glosa indevida" |
| Ferrugem claro | `#F4E4DD` | Background de badges negativas |
| Âmbar (atenção) | `#A8702C` | "Pendente", "Aguardando" |
| Âmbar claro | `#F2E9D6` | Background de badges de atenção |

### Regras de uso

- **80% pergaminho + 15% tintas + 5% acentos.** Se acentos passam de 5% da tela, está errado.
- **Terracota não vai em fundo grande.** É cor de logo, link, CTA primário, ícone destacado. Em fundo grande ela cansa e perde sofisticação.
- **Nunca verde puro `#00FF00` ou vermelho puro `#FF0000`.** Status fica na família terrosa.
- **Sem gradientes.** Em nada. Nenhum.
- **Sem dark mode no MVP.** Designar bem em pergaminho primeiro.

---

## 5. Tipografia

### Princípio
Uma sans-serif moderna bem trabalhada, sem combinação serifa+sans. Números tabulares ativados em qualquer lugar que mostre valor monetário.

### Família tipográfica

**[PROVISÓRIO — escolher 1 das 3 antes de implementar]**

#### Opção A — Inter (recomendada para começar)
- Gratuita, Google Fonts, ampla cobertura latim
- Excelente para interface, números tabulares maduros
- Risco: extremamente comum, marca pode parecer "outro SaaS"

#### Opção B — Geist
- Gratuita, criada pela Vercel
- Mais contemporânea, leve sensação "tech atual"
- Risco: também já popularizada, e desenhada para tema escuro (verificar em pergaminho)

#### Opção C — IBM Plex Sans
- Gratuita, Google Fonts
- Tem caráter próprio, diferencia mais
- Combina bem com IBM Plex Mono para wordmark e números — possível dupla coesa
- Risco: peso visual ligeiramente maior, validar em mobile

**Recomendação prática:** começar com **IBM Plex Sans + IBM Plex Mono**. Mais distintivo, par tipográfico já desenhado para conviver, gratuito.

### Hierarquia

| Nível | Tamanho | Peso | Uso |
|---|---|---|---|
| Display | 32-40px | 600 | Título de página principal |
| H1 | 24-28px | 600 | Título de seção principal |
| H2 | 20-22px | 600 | Título de seção secundária |
| H3 | 16-18px | 600 | Subtítulo |
| Corpo | 15-16px | 400 | Texto base |
| Corpo pequeno | 13-14px | 400 | Texto secundário |
| Label | 12-13px | 500 | Labels de formulário, metadados |
| Mono (números) | 15-16px | 400 | Valores monetários, IDs, códigos TUSS |

### Regras

- **Números monetários sempre em mono ou com `font-variant-numeric: tabular-nums`.** Não-negociável.
- **Line-height generoso.** 1.5 para corpo, 1.3 para títulos.
- **Letter-spacing levemente negativo em títulos grandes** (-0.01 a -0.02em). Em corpo, neutro.
- **Sem maiúsculas em corpo.** Caps só em labels muito específicas e curtas (ex: "TUSS", "CRM").

---

## 6. Logo / Wordmark

### [PROVISÓRIO] Direção

**Wordmark, sem símbolo.** Resistir explicitamente à tentação de criar ícone abstrato.

Direção principal: `honorare` em **lowercase**, em IBM Plex Mono (ou similar monospace de boa qualidade), em terracota `#B8593A` ou tinta `#1F1B16` dependendo do fundo.

Justificativa:
- Lowercase transmite contemporaneidade sem agressividade
- Monospace remete a precisão, sistema, código — diferencia instantaneamente de SaaS médico genérico
- Sem símbolo evita armadilha de ícone que envelhece em 2 anos
- A palavra "honorare" tem ritmo bom (4 sílabas, termina firme), sustenta wordmark sozinha

### Versões necessárias

- **Wordmark primário:** `honorare` em terracota sobre pergaminho
- **Wordmark monocromático claro:** `honorare` em pergaminho sobre tinta (para fundos escuros, raros)
- **Wordmark mono escuro:** `honorare` em tinta sobre pergaminho (uso versátil)
- **Favicon/ícone de app:** primeiro caractere `h` ou monograma, em terracota sobre pergaminho

### Não fazer

- Símbolo abstrato (gota, círculo, monograma cubista, "h" estilizado virando coração/cruz)
- Logo com slogan abaixo (ex: "honorare — gestão financeira médica")
- Versão colorida com mais de 2 cores
- Variação "alegre" para datas comemorativas

---

## 7. Iconografia

### Princípio
Ícones lineares, peso uniforme, geometria sóbria. **Nunca ícones de saúde** (estetoscópio, cruz, coração, jaleco, pulso).

### Biblioteca recomendada
- **Lucide** (gratuita, MIT) — minimalista, peso consistente
- **Phosphor** (gratuita, MIT) — variação `Regular` ou `Light`
- **Heroicons** — usar variação outline, evitar solid

### Regras
- Tamanho padrão: 16px ou 20px
- Cor padrão: tinta secundária `#6B6259`
- Peso: 1.5px stroke, **nunca mais grosso que 2px**
- Sem preenchimentos coloridos
- Sem badges em cima de ícones (use número ao lado, não bolinha vermelha)

---

## 8. Layout e espaçamento

### Princípio
**Densidade calma.** Médico tem que ver muita coisa numa tela (lista de guias, valores, status), mas a tela não pode parecer planilha. Resolver com hierarquia e respiro, não com cor.

### Sistema de espaçamento (escala de 4px)
4, 8, 12, 16, 24, 32, 48, 64, 96

### Container e largura
- Largura máxima de conteúdo: 1200px no admin
- PWA do médico: full-width até 480px, depois centralizado com 480px

### Componentes-chave

- **Cards:** background pergaminho-claro, borda 1px discreta, raio 8px, sombra mínima ou nenhuma
- **Inputs:** altura 40px, borda 1px média, raio 6px, focus ring terracota com 30% opacity
- **Botões primários:** terracota, raio 6px, peso 500
- **Botões secundários:** outline tinta, fundo transparente
- **Tabelas:** zebra-striping com pergaminho/pergaminho-claro, **não** linha-de-grade pesada
- **Status badges:** background da família terrosa, sem borda, peso 500, padding 4px 10px

---

## 9. Movimento e animação

### Princípio
Mínimo. Animação serve à compreensão, nunca à decoração.

### Regras
- Transições de UI: 150-200ms, easing `ease-out`
- Sem animação em mudança de tela (o load do dado é o feedback)
- Sem skeleton loaders animados pulsantes — use texto direto: "Calculando."
- Sem confetti, parabéns, micro-recompensas
- Sem hover transforms (escala, rotação)

---

## 10. Aplicação prática — checklist anti-erro

Antes de aprovar qualquer tela ou material:

- [ ] Tem ícone de saúde (estetoscópio, cruz, jaleco)? **Remover.**
- [ ] Tem gradiente? **Remover.**
- [ ] Texto usa "Ops!", "Hora de...", "Vamos lá!", "Bora!"? **Reescrever.**
- [ ] Mais de 2 cores além da paleta neutra na tela? **Tirar uma.**
- [ ] Número monetário em fonte proporcional? **Trocar para tabular.**
- [ ] Botão primário em verde, azul, roxo? **Voltar para terracota.**
- [ ] Emoji em texto de produto? **Remover.**
- [ ] Mascote, ilustração de pessoa, ícone 3D? **Remover.**
- [ ] Mensagem de sucesso muito comemorativa? **Cortar 50%.**
- [ ] Mensagem de erro culpando o usuário ("Você esqueceu de...")? **Reescrever em tom factual.**

---

## 11. Pendências de validação

Antes de tratar este guia como definitivo:

1. **Testar paleta em telas reais** com dados reais (lista de 50+ guias, tabela densa).
2. **Testar contraste em mobile** sob luz forte (PWA do médico será usado em hospital, ambulatório, carro).
3. **Validar voz com 3-5 médicos do cliente.** Mostrar 3 versões de mensagem e perguntar qual prefere e por quê.
4. **Validar wordmark com olho profissional.** Idealmente um designer de marca por 1-2h, mesmo pago avulso.
5. **Verificar acessibilidade.** Contraste WCAG AA mínimo. Terracota sobre pergaminho passa? Verde-musgo sobre verde-musgo-claro passa?
6. **Definir IBM Plex Sans vs Inter vs Geist.** Testar os três no mesmo mockup e decidir.

---

## 12. Como usar este guia

### No Claude Design
Cole as seções 4 (paleta), 5 (tipografia), 6 (logo) e 8 (layout) como contexto inicial do projeto. Adicione o atributo "moderno calmo, voz direta, off-white quente" como descritor de estilo.

### Com designer freelancer
Envie o guia inteiro como briefing. Peça especificamente para entregar: wordmark final em SVG, sistema de cores documentado em Figma, par tipográfico testado, e 3 telas-chave (login, lista de guias, detalhe de guia).

### No código (Angular)
Traduzir paleta para CSS variables em `:root`. Tipografia via `@font-face` ou Google Fonts link. Componentes seguindo seção 8. Lucide ou Phosphor como pacote de ícones.

---

*Versão 0.1 — rascunho de fundação. Atualizar conforme decisões reais aparecerem.*

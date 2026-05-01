# Honorare — Frontend Styles (admin-web)

Quick reference for writing SCSS. Read this before writing any component styles.

---

## Usando tokens em componentes

```scss
// No topo do .component.scss — importa mixins e funções
@use 'styles/tokens' as *;

.meu-bloco {
  @include text-body;
  color: var(--color-tinta);
  padding: space(4) space(6);
  @include transition(color, background-color);
}
```

> CSS custom properties (`var(--...)`) funcionam sem `@use`. O `@use` só é necessário para usar os mixins e a função `space()`.

---

## CSS Custom Properties

Definidas em `src/styles.scss`. Disponíveis globalmente — nunca redefina em componentes.

### Cores

| Variável                    | Uso                                               |
| --------------------------- | ------------------------------------------------- |
| `--color-pergaminho`        | Background de páginas                             |
| `--color-pergaminho-claro`  | Cards, modais, superfícies elevadas               |
| `--color-tinta`             | Texto principal — nunca use `#000`                |
| `--color-tinta-secundaria`  | Labels, metadados, texto auxiliar                 |
| `--color-tinta-terciaria`   | Placeholders, texto desabilitado                  |
| `--color-borda-discreta`    | Divisores sutis, separadores                      |
| `--color-borda-media`       | Bordas de inputs, tabelas                         |
| `--color-terracota`         | Logo, links, CTA primário — **parcimônia máxima** |
| `--color-terracota-escuro`  | Hover/active de terracota                         |
| `--color-verde-musgo`       | Status positivo: "Conforme", "Pago"               |
| `--color-verde-musgo-claro` | Background de badge positiva                      |
| `--color-ferrugem`          | Status negativo: "Divergência", "Glosa"           |
| `--color-ferrugem-claro`    | Background de badge negativa                      |
| `--color-ambar`             | Status de atenção: "Pendente", "Aguardando"       |
| `--color-ambar-claro`       | Background de badge de atenção                    |

### Fontes

| Variável      | Valor                                       |
| ------------- | ------------------------------------------- |
| `--font-sans` | `'IBM Plex Sans', system-ui, sans-serif`    |
| `--font-mono` | `'IBM Plex Mono', 'Courier New', monospace` |

### Motion

| Variável           | Valor      |
| ------------------ | ---------- |
| `--duration-fast`  | `150ms`    |
| `--duration-base`  | `200ms`    |
| `--easing-default` | `ease-out` |

---

## Mixins de tipografia

```scss
@use 'styles/tokens' as *;
```

| Mixin                      | Tamanho   | Peso | Uso                                       |
| -------------------------- | --------- | ---- | ----------------------------------------- |
| `@include text-display`    | 36px      | 600  | Título principal de página                |
| `@include text-h1`         | 26px      | 600  | Título de seção principal                 |
| `@include text-h2`         | 21px      | 600  | Título de seção secundária                |
| `@include text-h3`         | 17px      | 600  | Subtítulo                                 |
| `@include text-body`       | 15px      | 400  | Texto base — padrão de corpo              |
| `@include text-body-small` | 13px      | 400  | Texto secundário                          |
| `@include text-label`      | 12px      | 500  | Labels de formulário, metadados           |
| `@include text-mono-value` | 15px mono | 400  | **Valores monetários, IDs, códigos TUSS** |

> `text-mono-value` ativa `font-variant-numeric: tabular-nums` automaticamente. Use em qualquer número financeiro.

---

## Função de espaçamento

```scss
@use 'styles/tokens' as *;

padding: space(4); // → 16px
gap: space(6); // → 24px
margin: space(2) space(4); // → 8px 16px
```

| `space(n)`  | Valor |
| ----------- | ----- |
| `space(1)`  | 4px   |
| `space(2)`  | 8px   |
| `space(3)`  | 12px  |
| `space(4)`  | 16px  |
| `space(6)`  | 24px  |
| `space(8)`  | 32px  |
| `space(12)` | 48px  |
| `space(16)` | 64px  |
| `space(24)` | 96px  |

> Passo inválido gera **erro de compilação** SCSS. Nunca use px arbitrário — escolha o step mais próximo.

---

## Mixin de transição

```scss
@include transition(color); // → color 200ms ease-out
@include transition(color, background-color); // → ambas com 200ms
@include transition-fast(opacity); // → opacity 150ms ease-out
```

---

## Padrões de componentes

### Badge de status

```scss
.status-badge {
  @include text-label;
  border-radius: space(1);
  padding: space(1) space(3);

  &--positivo {
    color: var(--color-verde-musgo);
    background-color: var(--color-verde-musgo-claro);
  }

  &--negativo {
    color: var(--color-ferrugem);
    background-color: var(--color-ferrugem-claro);
  }

  &--atencao {
    color: var(--color-ambar);
    background-color: var(--color-ambar-claro);
  }
}
```

### Card

```scss
.card {
  background-color: var(--color-pergaminho-claro);
  border: 1px solid var(--color-borda-discreta);
  border-radius: space(2);
  padding: space(6);
}
```

### Input

```scss
.input {
  height: 40px;
  border: 1px solid var(--color-borda-media);
  border-radius: 6px;
  padding: 0 space(3);
  @include text-body;
  @include transition(border-color, box-shadow);

  &:focus {
    outline: none;
    border-color: var(--color-terracota);
  }

  &::placeholder {
    color: var(--color-tinta-terciaria);
  }
}
```

### Botão primário

```scss
.btn--primary {
  height: 40px;
  padding: 0 space(4);
  border-radius: 6px;
  border: none;
  background-color: var(--color-terracota);
  color: var(--color-pergaminho-claro);
  @include text-label;
  font-size: 14px;
  cursor: pointer;
  @include transition(background-color);

  &:hover {
    background-color: var(--color-terracota-escuro);
  }
}
```

---

## Regras (StyleLint enforça automaticamente)

| Regra                                                                                                             | Motivo                                   |
| ----------------------------------------------------------------------------------------------------------------- | ---------------------------------------- |
| Proibido hex literal em `color`, `background`, `border`, `outline`, `fill`, `stroke`, `box-shadow`, `text-shadow` | Todo valor de cor passa por `var(--...)` |
| Proibido `color-named` (ex: `red`, `green`)                                                                       | Idem acima                               |
| Proibido `!important`                                                                                             | Zero exceções                            |
| BEM obrigatório em seletores de classe                                                                            | `bloco__elemento--modificador`           |
| Máximo 3 níveis de nesting                                                                                        | Clareza e especificidade                 |

---

## Anti-padrões

| ❌ Errado                                             | ✅ Correto                                       |
| ----------------------------------------------------- | ------------------------------------------------ |
| `color: #1f1b16`                                      | `color: var(--color-tinta)`                      |
| `background: #f6f1ea`                                 | `background-color: var(--color-pergaminho)`      |
| `font-size: 15px; font-weight: 400; line-height: 1.5` | `@include text-body`                             |
| `font-variant-numeric: tabular-nums` (manual)         | `@include text-mono-value`                       |
| `padding: 16px`                                       | `padding: space(4)`                              |
| `margin-top: 7px`                                     | `margin-top: space(2)` (use o step mais próximo) |
| `transition: color 200ms ease-out`                    | `@include transition(color)`                     |
| `color: green`                                        | `color: var(--color-verde-musgo)`                |
| `background: linear-gradient(...)`                    | **Gradientes são proibidos**                     |
| `font-family: 'IBM Plex Sans', sans-serif` (inline)   | `font-family: var(--font-sans)`                  |

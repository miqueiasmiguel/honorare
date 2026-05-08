# SPEC-F3.2-VISUALIZACAO — Visualização do Cálculo da Guia

Feature: exibir o trace de apuração de cada item da guia na tela de detalhe.

## Contexto

Motor de cálculo (F3.2) já persiste `calculos` + `passos_calculo`. Falta:

- Endpoint que exponha esses dados
- Seção na UI da guia (guia-form, modo edição/visualização) com accordion por item

Situações possíveis por item: `Calculado`, `SemTabela`, `SemDeflator`, `Indeterminado`.
Somente itens `Calculado` geram `PassoCalculo`. Para os demais, exibir só o rótulo da situação.

## Sessão 1 — Backend: endpoint de cálculo

### TASK-VIS-01 — DTO + query no GuiaService ✓

**Arquivo:** `apps/backend/App/Faturamento/GuiaService.cs`

Adicionar método `ObterCalculoAsync(Guid guiaId, CancellationToken)` retornando:

```csharp
internal sealed record PassoCalculoDto(string Regra, decimal Fator, decimal ValorResultante);

internal sealed record ItemCalculoDto(
    Guid ItemGuiaId,
    string CodigoTuss,
    string DescricaoProcedimento,
    string Situacao,         // "Calculado" | "SemTabela" | "SemDeflator" | "Indeterminado"
    decimal? ValorApurado,
    IReadOnlyList<PassoCalculoDto> Passos);

internal sealed record GuiaCalculoDto(
    Guid GuiaId,
    bool EhPacote,
    DateTimeOffset? RealizadoEm,         // null se EhPacote
    IReadOnlyList<ItemCalculoDto> Itens);
```

**Lógica:**

1. Carregar header da guia (precisa de `EhPacote`). 404 se não encontrada.
2. Se `EhPacote`: retornar `GuiaCalculoDto(guiaId, true, null, itens)` onde cada item tem `Situacao = "Pacote"` e `ValorApurado` do `ItemGuia`.
3. Caso contrário: fazer LEFT JOIN `calculos` → `passos_calculo`, agrupar passos por `ItemGuiaId`, cruzar com `itens_guia` + `procedimentos` para obter TUSS e descrição.
4. Itens sem passos: `Situacao` derivada do estado real — se `ValorApurado != null` e sem passos → `"Calculado"` sem breakdown; se `ValorApurado == null` → `"SemTabela"` (default visual, pois sem passos e sem valor).

> Simplificação: `PassoCalculo` não armazena `SituacaoApuracao`. A situação exibida é inferida: item com `ValorApurado` preenchido = Calculado; item sem valor = "Não Calculado" (label genérico).

**TDD:**

- Teste em novo arquivo `Faturamento.Tests/Calculo/GuiaCalculoEndpointTests.cs` usando `PostgresContainerFixture`
- Setup: criar guia UNIMED com tabela+deflator → cálculo completo → `ObterCalculoAsync` retorna item com `Situacao = "Calculado"` e passos `ValorBase` presente
- Guia sem tabela → item com `ValorApurado == null`, passos vazios
- Guia pacote → `EhPacote = true`, `RealizadoEm == null`, item com `Situacao = "Pacote"`

---

### TASK-VIS-02 — Endpoint `GET /api/v1/admin/guias/{id}/calculo` ✓

**Arquivo:** `apps/backend/App/Faturamento/Endpoints/GuiaEndpoints.cs`

```csharp
g.MapGet("{id:guid}/calculo", ObterCalculoAsync);
```

Handler delegando para `GuiaService.ObterCalculoAsync`. Retorna 200 com `GuiaCalculoDto` ou 404.

**TDD:** `GuiaCalculoEndpointTests` (mesmo arquivo da TASK-VIS-01) — teste de integração HTTP: `POST /guias` → `GET /guias/{id}/calculo` retorna 200 com `itens[0].passos` não vazio quando calculado.

Reutilizar padrão de `GuiaEndpointTests.cs` para o `WebApplicationFactory`.

---

## Sessão 2 — Frontend: types + service

### TASK-VIS-03 — Types e método no GuiaService Angular ✓

**Arquivo:** `apps/admin-web/src/app/admin/faturamento/guia.types.ts`

```ts
export interface PassoCalculoItem {
  regra: string;
  fator: number;
  valorResultante: number;
}

export interface ItemCalculoItem {
  itemGuiaId: string;
  codigoTuss: string;
  descricaoProcedimento: string;
  situacao: "Calculado" | "SemTabela" | "SemDeflator" | "Indeterminado" | "Pacote";
  valorApurado: number | null;
  passos: PassoCalculoItem[];
}

export interface GuiaCalculoResult {
  guiaId: string;
  ehPacote: boolean;
  realizadoEm: string | null;
  itens: ItemCalculoItem[];
}
```

**Arquivo:** `apps/admin-web/src/app/admin/faturamento/guia.service.ts`

Adicionar método:

```ts
obterCalculo(id: string): Observable<GuiaCalculoResult> {
  return this._http.get<GuiaCalculoResult>(`/api/v1/admin/guias/${id}/calculo`);
}
```

**TDD:** `guia.service.spec.ts` — adicionar dois casos:

- `obterCalculo` emite GET correto
- resposta mapeada sem transformação (pass-through)

---

## Sessão 3 — Frontend: componente CalculoDetalhe

### TASK-VIS-04 — `CalculoDetalheComponent` ✓

**Arquivo novo:** `apps/admin-web/src/app/admin/faturamento/calculo-detalhe/calculo-detalhe.component.ts`

**Input:** `calculo = input<GuiaCalculoResult | null>(null)`

**Template (inline):** lista de itens em accordion. Para cada item:

- Header: `{codigoTuss} — {descricaoProcedimento}` + badge situação + valor apurado (ou `—`)
- Body (expandido por click): tabela de passos com colunas Regra / Fator / Valor Resultante
- Se sem passos: mensagem "Sem detalhes de cálculo" ou badge da situação explicando

Badge CSS classes: `.badge--calculado`, `.badge--sem-tabela`, `.badge--indeterminado`, `.badge--pacote`

**Estado:** signal `aberto = signal<string | null>(null)` (id do item expandido; toggle)

**TDD:** `calculo-detalhe.component.spec.ts`

- Renderiza N itens quando `calculo` tem N itens
- Click no header do item expande o body
- Click duplo colapsa
- Item `Calculado` com 2 passos renderiza 2 linhas na tabela
- Item sem passos exibe mensagem fallback
- `calculo = null` → renderiza vazio sem erro

---

## Sessão 4 — Frontend: integração na tela de guia

### TASK-VIS-05 — Seção de cálculo no `GuiaFormComponent`

**Arquivo:** `apps/admin-web/src/app/admin/faturamento/guia-form/guia-form.component.ts`

Quando em modo edição (`guiaId` presente na rota):

1. Após carregar a guia, chamar `_guiaService.obterCalculo(guiaId)` e armazenar em `calculo = signal<GuiaCalculoResult | null>(null)`
2. Exibir `<app-calculo-detalhe [calculo]="calculo()" />` abaixo da lista de itens, dentro de uma seção com título "Apuração"
3. Seção só aparece em modo edição (`modoEdicao()` signal já existente ou baseado na presença do id na rota)

**Erro tolerado:** se `obterCalculo` falhar (ex.: guia pacote sem cálculo no servidor), `calculo` fica `null` e componente não renderiza nada — sem travar o form.

**TDD:** `guia-form.component.spec.ts` — adicionar:

- Modo criação: `app-calculo-detalhe` **não** é renderizado
- Modo edição: `obterCalculo` é chamado com o id correto e `app-calculo-detalhe` é renderizado
- Erro em `obterCalculo`: form continua funcional, seção de cálculo ausente

---

## Critério de pronto

- `GET /api/v1/admin/guias/{id}/calculo` retorna 200 com passos quando calculado
- Tela de edição da guia exibe accordion com breakdown por item
- Guia pacote: seção mostra "Pacote" por item
- Guia sem tabela: seção mostra item sem passos com badge "Sem tabela"
- `dotnet test` verde; `pnpm -F admin-web test:ci` verde

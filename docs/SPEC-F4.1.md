# SPEC F4.1 — Portal do Médico (PWA)

**Pré-requisito:** F3.5 concluído.
**Pós-condição:** Médico autenticado acessa `medico-pwa` (`/app/`), visualiza suas guias pendentes com situação e observação do admin, e vê o detalhe com itens e valores apurados.

**Projeto:** `medico-pwa` (separado — base-href `/app/`, service worker próprio, policy `MedicoAccess`).
Manter projetos separados: base-href diferente, API prefix diferente (`/api/v1/medico/`), UI mobile-first read-only.

---

## Modelo de leitura

Sem novas entidades ou migrations. Apenas endpoints de leitura sobre `Guia` / `ItemGuia` / `Calculo` já existentes, filtrados por `PrestadorId = currentUser.MedicoId`.

**Regra de visibilidade:** guias com `Situacao == Liquidada` não aparecem na lista (MVP — médico vê apenas pendentes).

---

## Endpoints (todos policy `MedicoAccess`)

```
GET /api/v1/medico/guias
    ?operadoraId  Guid?
    ?dataInicio   DateOnly?   (yyyy-MM-dd)
    ?dataFim      DateOnly?   (yyyy-MM-dd)
    ?pagina       int  default 1
    ?itensPorPagina int default 20
    → MedicoListarGuiasResult

GET /api/v1/medico/guias/{id}
    → MedicoGuiaDetalheDto   (404 se guia não pertence ao médico)
```

`MedicoListarGuiasResult`:

```
{
  itens: MedicoGuiaSummaryDto[]
  total, pagina, itensPorPagina
}

MedicoGuiaSummaryDto {
  id, operadoraNome, beneficiarioNome, beneficiarioCarteira
  senha, dataAtendimento, situacao, totalItens
  temObservacao: bool        // true se Observacao não vazio — destaque na lista
}

MedicoGuiaDetalheDto {
  id, operadoraNome, beneficiarioNome, beneficiarioCarteira
  senha, dataAtendimento, situacao, ehPacote
  observacao                 // string (pode ser vazio)
  itens: MedicoItemGuiaDto[]
}

MedicoItemGuiaDto {
  id, codigoTuss, descricaoProcedimento, posicaoExecutor
  situacaoCalculo            // "Calculado"|"SemTabela"|"SemDeflator"|"Indeterminado"|"Pacote"|"NaoCalculado"
  valorApurado: number|null
  valorLiquidado: number|null
}
```

`situacaoCalculo` = valor do `Calculo` mais recente para o item; `"NaoCalculado"` se não há `Calculo`.

---

## Arquivos novos no backend

```
App/Faturamento/Endpoints/MedicoEndpoints.cs   (novo)
tests/Faturamento.Tests/MedicoGuiaTests.cs     (novo)
```

Registrar rotas em `Program.cs` junto dos outros `Map*Endpoints`.

`MedicoEndpoints.cs` usa `AppDbContext` diretamente (sem service intermediário — volume de lógica não justifica).
Valida `currentUser.MedicoId ?? Results.Forbid()` no início de cada handler.

---

## TASK-M-01 — Backend: endpoints `/api/v1/medico/guias` [x] concluída

**TDD: testes → implementação.**

### O que fazer

1. `MedicoEndpoints.cs`:
   - `GET /api/v1/medico/guias` — LINQ join `Guias → Operadoras → Beneficiarios`; filtro hard `PrestadorId == currentUser.MedicoId`; exclui `Liquidada`; aplica filtros opcionais; paginação.
   - `GET /api/v1/medico/guias/{id}` — obtem guia + itens + left join em `Calculos/PassosCalculo`; projeta `situacaoCalculo` por item; retorna 404 se não encontrou ou PrestadorId != MedicoId.

2. `MedicoGuiaTests.cs`:

```
[Fact] MedicoNaoVeGuiasDeMedicoDiferente
[Fact] MedicoNaoVeGuiasLiquidadas
[Fact] MedicoVeGuiasApresentadaEEmRecurso
[Fact] FiltroOperadoraFunciona
[Fact] FiltroDataInicioFim
[Fact] DetalheRetorna404SeGuiaNaoEDoMedico
[Fact] DetalheEmbuteSituacaoCalculoPorItem
[Fact] DetalheRetornaNaoCalculadoSemCalculo
```

3. `Program.cs` — adicionar `app.MapMedicoEndpoints()`.

**Critério de pronto:** `dotnet test` passa; `dotnet build` limpo.

---

## TASK-M-02 — medico-pwa: shell + GuiaListComponent [x] concluída

**TDD: testes Vitest → componente → build.**

### O que fazer

1. `medico-guia.types.ts` em `src/app/guias/`:

```typescript
export type SituacaoGuia = 'Apresentada' | 'Liquidada' | 'EmRecurso';
export type SituacaoCalculo =
  | 'Calculado' | 'SemTabela' | 'SemDeflator'
  | 'Indeterminado' | 'Pacote' | 'NaoCalculado';

export interface MedicoGuiaSummaryItem { ... }   // espelha MedicoGuiaSummaryDto
export interface MedicoListarGuiasResult { itens: MedicoGuiaSummaryItem[]; total: number; pagina: number; itensPorPagina: number; }
export interface ListarGuiasParams { operadoraId?: string; dataInicio?: string; dataFim?: string; pagina: number; itensPorPagina: number; }
```

2. `MedicoGuiaService` em `src/app/guias/medico-guia.service.ts`:
   - `listar(params): Observable<MedicoListarGuiasResult>` — `GET /api/v1/medico/guias`
   - Todos os métodos com `error` handler nos callers.

3. Atualizar `PainelComponent` (`src/app/painel/painel.ts`):
   - Shell mobile-first: top bar com nome do médico (do `AuthService`) + logo; `<router-outlet>` central.
   - Remover o template placeholder.

4. `GuiaListComponent` em `src/app/guias/guia-list/`:
   - Tabela/lista responsiva; colunas: Data, Senha, Beneficiário, Operadora, Situação (badge), Observação (ícone se `temObservacao`).
   - Filtros: período (2 date inputs) e operadora (texto livre, debounce 400 ms).
   - Paginação simples (anterior/próximo).
   - Linha clicável → navega para `/guias/:id`.
   - Badge color-coding: Apresentada → `--color-ambar`, EmRecurso → `--color-ferrugem`.

5. Rotas (`app.routes.ts`):

   ```typescript
   { path: '', canActivate: [authGuard], component: Painel, children: [
       { path: 'guias', loadComponent: () => GuiaListComponent },
       { path: '', redirectTo: 'guias', pathMatch: 'full' },
   ]}
   ```

6. SCSS segue os mesmos tokens de `admin-web` (`var(--color-*)`, `space()`, mixins `text-*`). O `medico-pwa` tem a mesma configuração de tokens SCSS que o `admin-web`.

### Testes

```
[it] exibe lista de guias do service
[it] linha com temObservacao exibe ícone de alerta
[it] filtro de operadora com debounce dispara nova busca
[it] guia Apresentada exibe badge âmbar
[it] guia EmRecurso exibe badge ferrugem
[it] clique na linha navega para /guias/:id
[it] paginação exibe botão próximo quando total > itensPorPagina
```

**Critério de pronto:** `pnpm -F medico-pwa test:ci` passa; `pnpm -F medico-pwa lint` sem warnings.

---

## TASK-M-03 — medico-pwa: GuiaDetalheComponent

**TDD: testes Vitest → componente → build.**

### O que fazer

1. Adicionar método ao `MedicoGuiaService`:
   - `obterPorId(id: string): Observable<MedicoGuiaDetalheDto>`

2. Adicionar tipo `MedicoGuiaDetalheDto` / `MedicoItemGuiaDto` em `medico-guia.types.ts`.

3. `GuiaDetalheComponent` em `src/app/guias/guia-detalhe/`:

   **Cabeçalho:** operadora, beneficiário (nome + carteira), data, senha, situação badge.

   **Bloco observação** (exibir SEMPRE, mesmo vazio):
   - Fundo `--color-ambar-claro`, borda `--color-ambar`, label "Observação do responsável".
   - Se vazio, exibir texto em `--color-tinta-secundaria`: "Nenhuma observação registrada."
   - Este é o campo mais importante — deve ter destaque visual claro.

   **Tabela de itens:**
   Colunas: Cód. TUSS | Descrição | Posição | VL Apurado | VL Pago | Situação Cálculo (badge).
   Badge color-coding: Calculado → verde, SemTabela/SemDeflator/Indeterminado → ferrugem, Pacote/NaoCalculado → ambar.

   **Rodapé:**
   - Total VL Apurado / Total VL Pago usando `text-mono-value`.
   - Botão "Voltar" → `/guias`.

4. Rota em `app.routes.ts`:

   ```typescript
   { path: 'guias/:id', loadComponent: () => GuiaDetalheComponent }
   ```

   (dentro do children do Painel, após `guias`).

5. `GuiaListComponent` — confirmar que clique na linha usa `[routerLink]="['/guias', guia.id]"`.

### Testes

```
[it] exibe nome do beneficiário e senha no cabeçalho
[it] bloco observação exibe texto quando preenchido
[it] bloco observação exibe mensagem padrão quando vazio
[it] tabela exibe todos os itens da guia
[it] item Calculado exibe badge verde com valor apurado
[it] item SemTabela exibe badge ferrugem
[it] item NaoCalculado exibe badge âmbar
[it] botão voltar navega para /guias
[it] loading state exibido enquanto aguarda resposta
```

**Critério de pronto:** `pnpm -F medico-pwa test:ci` passa; `pnpm -F medico-pwa lint` sem warnings.

---

## Resumo de entregáveis por task

| Task | Backend            | Frontend (medico-pwa)      | Migration | Testes                         |
| ---- | ------------------ | -------------------------- | --------- | ------------------------------ |
| M-01 | MedicoEndpoints.cs | —                          | —         | MedicoGuiaTests.cs (8 casos)   |
| M-02 | —                  | PainelComponent + GuiaList | —         | guia-list.spec.ts (7 casos)    |
| M-03 | —                  | GuiaDetalheComponent       | —         | guia-detalhe.spec.ts (9 casos) |

**Após M-03:** atualizar `PROXIMOS_PASSOS.md` marcando F4.1 como ✅.

# SPEC REFACTOR — Unificar Procedimentos × Tabelas de Valores

**Tipo:** Refatoração (sem feature nova).
**Pré-requisito:** F3.6 concluído (PA-01 a PA-11).
**Pós-condição:**

- Módulo "Tabelas de Valores" deixa de existir como rota e item de menu.
- O detalhe do procedimento expõe inline os valores por operadora (CRUD).
- A `TabelaPorteAnestesico` vive no detalhe da operadora.
- Toda importação de CSV passa por um modal único acessado da lista de procedimentos.
- Backend mantém entidades atuais — apenas ganha rotas de conveniência por procedimento.

---

## Contexto rápido

Hoje a UI tem `procedimentos` e `tabelas` como módulos paralelos no sidebar (`apps/admin-web/src/app/admin/admin-shell.html:22`). Operadores que precisam precificar um TUSS já encontrado fazem o trabalho duas vezes: buscam o TUSS na lista de procedimentos, depois trocam de página para "Tabelas de Valores", filtram operadora e buscam o mesmo TUSS de novo via `app-tabela-form`.

Importação de CSV está fragmentada em três experiências distintas:

| Onde                                                                                        | O quê                                        |
| ------------------------------------------------------------------------------------------- | -------------------------------------------- |
| `apps/admin-web/.../procedimentos/procedimento-list/procedimento-list.component.html:25-39` | Input file inline + download template inline |
| `apps/admin-web/.../tabelas/tabela-csv-modal/`                                              | Modal para CSV de valores                    |
| `apps/admin-web/.../tabelas/tabela-porte-anestesico-csv-modal/`                             | Modal para CSV de porte anestésico           |

A `TabelaPorteAnestesico` foi colocada na tela de Tabelas por proximidade, mas ela não é por procedimento (PA-01, SPEC-F3.6) — é por `(OperadoraId, PorteLetra)`. Está no lugar errado.

## Ordem de execução: RP-01 → RP-02 (backend) → RP-03 → RP-04 → RP-05 → RP-06 → RP-07 (frontend)

---

## RP-01 · Backend — Endpoints de valor por procedimento [x]

**TDD — escrever test primeiro, depois implementar.**

### Arquivo de teste (novo)

`tests/Catalog.Tests/TabelaProcedimento/ProcedimentoValoresEndpointsTests.cs`

```
namespace: Catalog.Tests.TabelaProcedimento
fixture: PostgresContainerFixture
```

Casos obrigatórios:

```
Listar_ProcedimentoSemValores_RetornaUmaLinhaPorOperadoraAtiva
  seed: 1 procedimento + 3 operadoras ativas + 0 valores
  GET /api/v1/admin/procedimentos/{procId}/valores
  assert: 3 linhas; cada uma com tabelaId == null e valor == null

Listar_ProcedimentoComValorParcial_RetornaTodasOperadorasEMarcaTabelaId
  seed: 1 procedimento + 3 operadoras + 1 TabelaProcedimento (operadora 1, valor 526.50)
  assert: linha da operadora 1 tem tabelaId != null e valor == 526.50
  assert: linhas das outras têm tabelaId == null

Listar_OperadoraInativa_NaoApareceNaLista
  seed: 1 operadora ativa + 1 inativa, ambas com valor para o procedimento
  assert: lista contém apenas a operadora ativa

Upsert_NovoValor_CriaTabelaProcedimento
  PUT /api/v1/admin/procedimentos/{procId}/valores/{operadoraId} { valor: 410.00 }
  assert: 201 Created; TabelaProcedimento existe com valor 410.00

Upsert_ValorExistente_AtualizaSemDuplicar
  seed: 1 TabelaProcedimento existente para (proc, op) com valor 526.50
  PUT mesmo endpoint { valor: 600.00 }
  assert: 200 OK; tabela única; valor == 600.00

Upsert_ValorNegativo_Retorna422
  PUT { valor: -10 }
  assert: 422 Unprocessable Entity

Excluir_TabelaInexistente_Retorna204Idempotente
  DELETE /api/v1/admin/procedimentos/{procId}/valores/{operadoraId} (sem seed)
  assert: 204 No Content

Excluir_ValorExistente_Remove
  seed: 1 TabelaProcedimento
  DELETE → 204
  assert: tabela não existe mais

Listar_TenantOutro_Retorna404
  seed: procedimento de tenant A
  GET com tenant B
  assert: 404
```

### Arquivos de implementação

| Arquivo                                     | Ação                                                                                             |
| ------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `App/Catalog/CatalogService.cs`             | 3 métodos: `ListarValoresPorProcedimentoAsync`, `UpsertValorAsync`, `ExcluirValorPorProcOpAsync` |
| `App/Catalog/Endpoints/CatalogEndpoints.cs` | 3 rotas novas em `/api/v1/admin/procedimentos/{procId}/valores`                                  |
| `apps/admin-web/.../catalog.types.ts`       | `ProcedimentoValorOperadoraItem`, `UpsertValorPayload`                                           |
| `apps/admin-web/.../catalog.service.ts`     | 3 métodos espelhando endpoints                                                                   |

**Contrato dos endpoints:**

```
GET    /api/v1/admin/procedimentos/{procId}/valores
       → ProcedimentoValorOperadoraItem[]
         {
           operadoraId,
           operadoraNome,
           tipoRuleSet,
           tabelaId: string | null,
           valor: number | null,
           atualizadoEm: string | null
         }
       Inclui todas as operadoras ATIVAS do tenant. Operadoras inativas não aparecem.

PUT    /api/v1/admin/procedimentos/{procId}/valores/{operadoraId}
       body: { valor: number }   // > 0
       → TabelaItem
       201 se criou, 200 se atualizou.

DELETE /api/v1/admin/procedimentos/{procId}/valores/{operadoraId}
       → 204 No Content (idempotente — sem entrada também retorna 204)
```

**Validação:**

- `valor` deve ser > 0 (422 caso contrário).
- Procedimento e operadora precisam pertencer ao tenant atual (404 caso contrário).

**Observação:** os endpoints existentes `/api/v1/admin/tabelas` permanecem — são usados pela importação CSV e pelos testes legados. Os novos endpoints são camada de conveniência para a UI nova.

**Verificar:** `dotnet test apps/backend/tests/Catalog.Tests/ --filter "ProcedimentoValoresEndpoints"`

---

## RP-02 · Backend — endpoint de listagem porte anestésico já existe [x]

Nada para implementar. `GET /api/v1/admin/tabelas-porte-anestesico?operadoraId={guid}` já está disponível desde PA-01 (`CatalogEndpoints.cs:59`).

Adicionar apenas:

```
DELETE /api/v1/admin/tabelas-porte-anestesico/{id}
       → 204 No Content
       Usado para o caso de o admin quer remover um porte importado errado.
```

### Teste

```
Excluir_PorteAnestesico_Remove
  seed: 1 TabelaPorteAnestesico
  DELETE → 204
  assert: registro inexistente
```

---

## RP-03 · Frontend — Modal unificado de importação [x]

### Arquivo novo

`apps/admin-web/src/app/admin/catalog/procedimentos/importar-modal/importar-modal.component.{ts,html,scss,spec.ts}`

### Comportamento

Modal com três seções verticais:

**1. Tipo de importação** (radio cards):

| Opção                      | Backend usado                                                                  | Requer operadora? |
| -------------------------- | ------------------------------------------------------------------------------ | ----------------- |
| Procedimentos (TUSS)       | `POST /api/v1/admin/procedimentos/importar-csv`                                | Não               |
| Valores por Operadora      | `POST /api/v1/admin/tabelas/importar-csv?operadoraId=`                         | Sim               |
| Tabela de Porte Anestésico | `POST /api/v1/admin/tabelas-porte-anestesico/importar-unimed-csv?operadoraId=` | Sim               |

**2. Operadora** (`<select>` carregado de `listarOperadoras({ ativa: true })`). Visível apenas quando o tipo selecionado a exige. Obrigatória — botão "Importar" desabilitado sem seleção.

**3. Formato esperado** (bloco read-only): muda conforme o tipo selecionado. Conteúdo:

```ts
const FORMATOS = {
  procedimentos: {
    separador: ";",
    encoding: "UTF-8",
    colunas: "CodigoTuss;Descricao;Porte;PorteAnestesico;EhSadt;TemPorteProprioVideo",
    exemplo: ["CodigoTuss;Descricao;Porte;PorteAnestesico;EhSadt;TemPorteProprioVideo", "30715013;Herniorrafia inguinal;6B;J;false;false", "40314340;Eletroencefalograma;;;true;false"].join("\n"),
    arquivo: "template-procedimentos.csv",
  },
  valoresOperadora: {
    separador: ";",
    encoding: "UTF-8",
    colunas: "CodigoTuss;Valor",
    exemplo: ["CodigoTuss;Valor", "30715013;526,50", "40314340;124,80"].join("\n"),
    arquivo: "template-valores-operadora.csv",
  },
  porteAnestesico: {
    separador: "vírgula com aspas",
    encoding: "UTF-8",
    colunas: "Código,Procedimento,Honorários,VL AMB,VL ENF,VL AP,Porte (8 linhas de header)",
    exemplo: '… (8 linhas de header) …\nCódigo,Procedimento,Honorários,VL AMB,VL ENF,VL AP,Porte\n30101050,APENDICE PRE-AURICULAR,"224,64",,"292,5",468,E',
    arquivo: "exemplo-unimed-jpa.csv",
    obsExtra: "Arquivo no formato UNIMED JPA — manter o cabeçalho de 8 linhas.",
  },
} as const;
```

**4. Botões:**

- `Baixar arquivo de exemplo` — gera blob CSV a partir do campo `exemplo` e dispara download.
- `Escolher arquivo` — input file `accept=".csv"`.
- `Importar` — chama o `CatalogService` apropriado. Mostra spinner. Após resposta, renderiza:
  - **Procedimentos / Valores**: `inseridos`, `atualizados`, `ignorados`, lista de erros.
  - **Porte anestésico**: `portesAtualizados`, `procedimentosAtualizados`, `procedimentosNaoEncontrados[]`, lista de erros.
- `Concluir` (após resultado): emite `concluido` (parent recarrega lista).
- `Cancelar` (a qualquer momento): emite `cancelado`.

### Entradas / Saídas

```ts
@Input() open: boolean;          // controla visibilidade
@Output() concluido: EventEmitter<void>;
@Output() cancelado: EventEmitter<void>;
```

### Testes (`importar-modal.component.spec.ts`)

```
- ao trocar tipo para "valoresOperadora", select de operadora aparece
- botão Importar desabilitado sem arquivo
- botão Importar desabilitado quando tipo exige operadora e nenhuma foi selecionada
- selecionar arquivo + clicar Importar chama service correto com argumentos certos
- após resposta de sucesso, renderiza contadores
- baixar exemplo gera blob com nome correto
```

### Componentes legados a remover

- `apps/admin-web/.../tabelas/tabela-csv-modal/` — apagar.
- `apps/admin-web/.../tabelas/tabela-porte-anestesico-csv-modal/` — apagar.

(Removidos em RP-07 para evitar quebrar a compilação durante as etapas.)

---

## RP-04 · Frontend — Detalhe do procedimento ganha seção "Valores por Operadora" [x]

### Arquivos a modificar

`apps/admin-web/src/app/admin/catalog/procedimentos/procedimento-form/`

### Comportamento

A página atual continua um formulário simples no caso de **novo procedimento** (`/procedimentos/novo`). A seção de valores só aparece em **edição** (`/procedimentos/:id`) — sem ID ainda não há FK para criar `TabelaProcedimento`.

Estrutura da página em modo edição:

```
┌─ Editar procedimento ─────────────────────────────┐
│  [form atual — TUSS, descrição, porte, flags]     │
│  [Salvar] [Cancelar]                              │
└───────────────────────────────────────────────────┘
┌─ Valores por Operadora ───────────────────────────┐
│  UNIMED                   R$ 526,50   [Editar][X] │
│  Bradesco Saúde                  —    [Definir]   │
│  Amil                     R$ 410,00   [Editar][X] │
└───────────────────────────────────────────────────┘
```

### Detalhes da seção "Valores por Operadora"

- Carrega de `GET /admin/procedimentos/:id/valores` ao entrar na página.
- Uma linha por operadora ativa. Ordenada por `operadoraNome`.
- Coluna "Valor": formatado em BRL via `Intl.NumberFormat('pt-BR', { style:'currency', currency:'BRL' })`, ou `—` se ausente.
- Ações:
  - **[Definir]**: aparece quando `tabelaId === null`. Troca célula por `<input type="number" step="0.01" min="0.01">` + botões `[OK][Cancelar]`. OK chama `PUT` (cria).
  - **[Editar]**: idem, mas pré-preenche com valor atual. OK chama `PUT` (atualiza).
  - **[X]**: aparece apenas quando `tabelaId !== null`. Confirma via `window.confirm('Remover valor para essa operadora?')` e chama `DELETE`.
- Validação client-side: `valor > 0`. Mostrar erro inline `Valor deve ser maior que zero.`
- Erros do backend (422 / 404): exibir mensagem amigável abaixo da linha.

### Implementação

Sub-componente novo `valores-operadora.component.ts` para isolar a tabela editável:

```ts
@Input() procedimentoId: string;
// internamente chama CatalogService.{listar,upsert,excluir}ValorPorProcedimento
```

### Testes

`procedimento-form.component.spec.ts` — adicionar:

```
- modo "novo": seção valores NÃO renderiza
- modo "edição": seção valores renderiza após carregar valoresPorProcedimento
- clicar "Definir" e digitar valor 600.00 → chama upsertValor com (procId, opId, 600)
- após upsert bem-sucedido, célula mostra "R$ 600,00" e botão vira "Editar"
- clicar "X" e confirmar → chama excluirValor + célula mostra "—"
```

---

## RP-05 · Frontend — Porte Anestésico migra para o detalhe da Operadora [x]

### Arquivo a modificar

`apps/admin-web/src/app/admin/catalog/operadoras/operadora-form/operadora-form.component.{ts,html,scss,spec.ts}`

### Comportamento

No modo edição (`/operadoras/:id`), abaixo do formulário, nova seção:

```
┌─ Porte Anestésico (UNIMED) ────────────────────────┐
│  Letra │ Enfermaria │ Apartamento │ Ambulatorial   │
│   A    │  R$ 150,00 │  R$ 240,00  │      —    [X]  │
│   B    │  R$ 180,00 │  R$ 288,00  │      —    [X]  │
│   …                                                │
└────────────────────────────────────────────────────┘
                  Importar via modal de procedimentos
```

- Visível apenas quando `tipoRuleSet === 'Unimed'`.
- Tabela read-only (não inline-editable — a entrada acontece via importação UNIMED JPA).
- Botão **[X]** chama `DELETE /api/v1/admin/tabelas-porte-anestesico/{id}` com confirmação.
- Texto informativo abaixo: "Para importar a tabela, use 'Importar dados' na lista de procedimentos e selecione 'Tabela de Porte Anestésico'."

### Implementação

Sub-componente novo `portes-anestesicos.component.ts`:

```ts
@Input() operadoraId: string;
// chama CatalogService.listarPortesAnestesico(operadoraId)
```

### Testes

```
- operadora com tipoRuleSet='Nulo' → seção NÃO renderiza
- operadora com tipoRuleSet='Unimed' → seção renderiza, lista portes ordenados por letra
- clicar [X] confirma e chama excluirPorteAnestesico
```

---

## RP-06 · Frontend — Lista de procedimentos usa o novo modal [x]

### Arquivo a modificar

`apps/admin-web/src/app/admin/catalog/procedimentos/procedimento-list/procedimento-list.component.{ts,html,scss}`

### Mudanças

1. **Remover** o bloco `<div class="procedimento-list__import-actions">` (linhas 25–39 do HTML atual) com seu `<input #csvInput>`, "Importar CSV" e "Download template".
2. **Substituir** por um único botão `<button class="procedimento-list__btn-importar">Importar dados</button>` ao lado de "Novo procedimento" no header.
3. **Adicionar** signal `mostrarImportarModal: boolean` e renderização condicional do `<app-importar-modal>` no fim do template.
4. **Remover** os métodos `onFileChange`, `onArquivoSelecionado`, `downloadTemplate` e o signal `importResult` — a lógica vive no novo modal.
5. **Ao concluir importação**: handler `onImportConcluido()` chama `_carregarProcedimentos()` e fecha o modal.

### Testes a ajustar

`procedimento-list.component.spec.ts` — remover testes de download/upload inline, adicionar teste do botão "Importar dados" abrir o modal.

---

## RP-07 · Frontend — Remoção da rota e link de "Tabelas de Valores"

### Arquivos a modificar

| Arquivo                                                                           | Ação                                                                                                                                       |
| --------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `apps/admin-web/src/app/admin/catalog/catalog.routes.ts`                          | Remover o bloco `path: 'tabelas'` (linhas 68–71). Adicionar redirect `{ path: 'tabelas', redirectTo: 'procedimentos', pathMatch: 'full' }` |
| `apps/admin-web/src/app/admin/admin-shell.html`                                   | Remover o `<a routerLink="catalog/tabelas">` (linhas 20–25)                                                                                |
| `apps/admin-web/src/app/admin/catalog/tabelas/tabela-list/`                       | Apagar pasta inteira                                                                                                                       |
| `apps/admin-web/src/app/admin/catalog/tabelas/tabela-form/`                       | Apagar pasta inteira                                                                                                                       |
| `apps/admin-web/src/app/admin/catalog/tabelas/tabela-csv-modal/`                  | Apagar pasta inteira (substituído pelo novo modal unificado)                                                                               |
| `apps/admin-web/src/app/admin/catalog/tabelas/tabela-porte-anestesico-csv-modal/` | Apagar pasta inteira                                                                                                                       |
| `apps/admin-web/src/app/admin/catalog/tabelas/`                                   | A pasta `tabelas/` fica vazia após as remoções — apagar também                                                                             |

### O que NÃO remover

- `CatalogService.{listarTabelas,obterTabela,criarTabela,atualizarTabela,excluirTabela,importarTabelaCsv}` — continuam usados pelo modal unificado e podem ter consumers em testes.
- `CatalogService.{listarPortesAnestesico,importarTabelaPorteAnestesico}` — idem.
- Backend `/api/v1/admin/tabelas` e `/api/v1/admin/tabelas-porte-anestesico` — endpoints permanecem.

### Verificar

```bash
pnpm -F admin-web lint
pnpm -F admin-web test:ci
pnpm -F admin-web build
```

---

## Critério global de pronto

1. `pnpm -F admin-web lint` limpo (zero warnings).
2. `pnpm -F admin-web test:ci` passa com cobertura ≥ 80%.
3. `pnpm -F admin-web build` produz bundle sem erros.
4. `dotnet test apps/backend/Honorare.slnx` passa.
5. Fluxos manuais validados em `pnpm dev:up`:
   - Criar procedimento → editar → adicionar valor para 2 operadoras → recarregar → valores persistem.
   - Excluir valor de uma operadora → linha vira "—".
   - Importar CSV de procedimentos via modal → resultado contabiliza corretamente.
   - Importar CSV de valores selecionando operadora → tabela do procedimento reflete novos valores.
   - Importar CSV UNIMED JPA via modal → operadora detail mostra os portes.
   - Acessar URL antiga `/admin/catalog/tabelas` → redireciona para `/admin/catalog/procedimentos`.
   - Item "Tabelas de Valores" não aparece no sidebar.

---

## Riscos e mitigações

| Risco                                                             | Mitigação                                                                                                                                                   |
| ----------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Bookmark antigo em `/admin/catalog/tabelas` quebra                | Redirect declarado em `catalog.routes.ts`                                                                                                                   |
| Operador tenta precificar antes de salvar procedimento novo       | Seção "Valores por Operadora" só renderiza em modo edição. Mensagem no `novo`: "Salve o procedimento para definir valores."                                 |
| Endpoints novos `/procedimentos/{id}/valores` duplicam `/tabelas` | Decisão consciente — `/tabelas` continua sendo a API "transacional" usada pela importação CSV; `/procedimentos/{id}/valores` é a view orientada ao recurso. |
| Importação CSV de valores precisa de TUSS já cadastrado           | Comportamento inalterado — o CSV de valores referencia `CodigoTuss`, o backend já trata `procedimentosNaoEncontrados`                                       |
| Cobertura de testes cai por remoção de componentes                | Adicionar testes para o novo modal e para os sub-componentes inline antes de remover os antigos                                                             |

---

## Resumo dos arquivos tocados

**Backend (.NET):**

- `App/Catalog/CatalogService.cs` — 3 métodos novos
- `App/Catalog/Endpoints/CatalogEndpoints.cs` — 4 rotas novas (3 valores + 1 delete porte)
- `tests/Catalog.Tests/TabelaProcedimento/ProcedimentoValoresEndpointsTests.cs` — novo

**Frontend (admin-web):**

- `catalog.types.ts` — novos types
- `catalog.service.ts` — novos métodos
- `procedimentos/importar-modal/` — novo
- `procedimentos/procedimento-form/valores-operadora.component.ts` — novo
- `procedimentos/procedimento-form/procedimento-form.component.{ts,html,scss,spec.ts}` — alterado
- `procedimentos/procedimento-list/procedimento-list.component.{ts,html,scss,spec.ts}` — alterado
- `operadoras/operadora-form/portes-anestesicos.component.ts` — novo
- `operadoras/operadora-form/operadora-form.component.{ts,html,scss,spec.ts}` — alterado
- `catalog/catalog.routes.ts` — remoção + redirect
- `admin/admin-shell.html` — remoção do link
- `catalog/tabelas/**` — pasta inteira removida

---

## Atualização do PROXIMOS_PASSOS.md

Adicionar entrada na seção Fase 3 (ou seção dedicada de "Refatorações"):

```markdown
### F3.X — Unificar Procedimentos × Tabelas de Valores

Spec: `docs/SPEC-REFACTOR-PROCEDIMENTOS.md`. Consolida CRUD de pricing dentro do detalhe do procedimento, move tabela de porte anestésico para o detalhe da operadora, unifica importação de CSV em um único modal.

Critério de pronto: ver SPEC.
```

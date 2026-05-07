# SPEC-F2.3 — Tabelas e Prestadores

**Contexto:** Fase 2 do Honorare. Depende de F2.2 (Operadora e Procedimento já existem). Estas entidades são pré-requisito do motor de cálculo (F3.2): `TabelaProcedimento` fornece o valor base; `DeflatorPrestador` fornece o percentual do prestador por operadora.

---

## Domínio

### Entidades novas

| Entidade             | Responsabilidade                                                                                                          |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `Prestador`          | Médico/profissional de saúde dentro de um tenant. É o executor dos procedimentos nas guias.                               |
| `TabelaProcedimento` | Valor do procedimento para uma operadora específica (= tabela de honorários). Fonte do `valor_base` no motor.             |
| `DeflatorPrestador`  | Percentual negociado entre prestador e operadora por posição de execução. Multiplicador aplicado sobre o valor de tabela. |

### Fórmula relevante (F3.2 depende disto)

```
valor_base = TabelaProcedimento.Valor × (DeflatorPrestador.Percentual / 100)
```

### Campos

**Prestador**
| Campo | Tipo | Restrições |
|---|---|---|
| `Id` | `Guid` | PK |
| `TenantId` | `Guid` | FK tenant, global query filter |
| `Nome` | `string` | obrigatório, max 150 |
| `RegistroProfissional` | `string?` | CRM/CRO/RQE etc., max 20 |
| `Ativo` | `bool` | default true |
| `CriadoEm` | `DateTimeOffset` | UTC, set on create |

**TabelaProcedimento**
| Campo | Tipo | Restrições |
|---|---|---|
| `Id` | `Guid` | PK |
| `TenantId` | `Guid` | FK tenant, global query filter |
| `OperadoraId` | `Guid` | FK Operadora |
| `ProcedimentoId` | `Guid` | FK Procedimento |
| `Valor` | `decimal(18,4)` | > 0 |
| `AtualizadoEm` | `DateTimeOffset` | UTC, atualizado em upserts |

Constraint único: `(TenantId, OperadoraId, ProcedimentoId)`.

**DeflatorPrestador**
| Campo | Tipo | Restrições |
|---|---|---|
| `Id` | `Guid` | PK |
| `TenantId` | `Guid` | FK tenant, global query filter |
| `PrestadorId` | `Guid` | FK Prestador |
| `OperadoraId` | `Guid` | FK Operadora |
| `Posicao` | `PosicaoExecutor` | enum |
| `Percentual` | `decimal(6,2)` | 0 < valor ≤ 200 |

Constraint único: `(TenantId, PrestadorId, OperadoraId, Posicao)`.

```csharp
// enum — namespace App.Catalog
internal enum PosicaoExecutor
{
    Cirurgiao = 1,
    PrimeiroAuxiliar = 2,
    SegundoAuxiliar = 3,
    TerceiroAuxiliar = 4,
    Anestesista = 5,
    ClinicoAssistente = 6
}
```

---

## API Endpoints

Todos sob policy `TenantAccess` (`/api/v1/admin/`).

### Prestador

| Método   | Rota                             | Body / Query                         | Retorno                          |
| -------- | -------------------------------- | ------------------------------------ | -------------------------------- |
| `GET`    | `/api/v1/admin/prestadores`      | `?busca&ativo&pagina&itensPorPagina` | `ListarPrestadoresResult`        |
| `GET`    | `/api/v1/admin/prestadores/{id}` | —                                    | `PrestadorDto`                   |
| `POST`   | `/api/v1/admin/prestadores`      | `SalvarPrestadorCommand`             | `201 PrestadorDto`               |
| `PUT`    | `/api/v1/admin/prestadores/{id}` | `SalvarPrestadorCommand`             | `PrestadorDto`                   |
| `DELETE` | `/api/v1/admin/prestadores/{id}` | —                                    | `204` / `409` se guias existirem |

### TabelaProcedimento

| Método   | Rota                                 | Body / Query                                    | Retorno               |
| -------- | ------------------------------------ | ----------------------------------------------- | --------------------- |
| `GET`    | `/api/v1/admin/tabelas`              | `?operadoraId&codigoTuss&pagina&itensPorPagina` | `ListarTabelasResult` |
| `GET`    | `/api/v1/admin/tabelas/{id}`         | —                                               | `TabelaDto`           |
| `POST`   | `/api/v1/admin/tabelas`              | `SalvarTabelaCommand`                           | `201 TabelaDto`       |
| `PUT`    | `/api/v1/admin/tabelas/{id}`         | `SalvarTabelaCommand`                           | `TabelaDto`           |
| `DELETE` | `/api/v1/admin/tabelas/{id}`         | —                                               | `204`                 |
| `POST`   | `/api/v1/admin/tabelas/importar-csv` | `multipart/form-data` file                      | `ImportarCsvResult`   |

**CSV TabelaProcedimento** — separador `;`, primeira linha = header, limite 10 000 linhas.
Colunas: `CodigoTuss;Valor`. `OperadoraId` passado como query param `?operadoraId=<guid>`.
Upsert por `(OperadoraId, CodigoTuss)`: insere novo ou atualiza `Valor` se já existir. Erros linha a linha não abortam o batch.

### DeflatorPrestador (aninhado em Prestador)

| Método   | Rota                                                      | Body                    | Retorno                      |
| -------- | --------------------------------------------------------- | ----------------------- | ---------------------------- |
| `GET`    | `/api/v1/admin/prestadores/{prestadorId}/deflatores`      | —                       | `IReadOnlyList<DeflatorDto>` |
| `POST`   | `/api/v1/admin/prestadores/{prestadorId}/deflatores`      | `SalvarDeflatorCommand` | `201 DeflatorDto`            |
| `PUT`    | `/api/v1/admin/prestadores/{prestadorId}/deflatores/{id}` | `SalvarDeflatorCommand` | `DeflatorDto`                |
| `DELETE` | `/api/v1/admin/prestadores/{prestadorId}/deflatores/{id}` | —                       | `204`                        |

---

## Telas Angular (admin-web)

### Sidebar — seção "Cadastros" (acrescentar)

```
Cadastros
  ├─ Operadoras         (já existe)
  ├─ Procedimentos      (já existe)
  ├─ Tabelas de Valores  ← nova
  └─ Prestadores         ← nova
```

### PrestadorList (`/admin/cadastros/prestadores`)

- Tabela paginada: Nome, Registro, Ativo, ações Editar/Excluir
- Botão "Novo prestador" → modal/form inline
- Filtro por nome (debounce 400 ms) e ativo
- Excluir: confirmar se sem guias; 409 → toast "Prestador possui guias associadas"

### PrestadorForm (modal ou rota `/admin/cadastros/prestadores/:id`)

- Campos: Nome (obrigatório), RegistroProfissional, Ativo
- Seção "Deflatores por Operadora": lista + botão "+ Deflator"
  - Por deflator: selecionar Operadora (dropdown), Posicao (dropdown), Percentual
  - Editar/excluir inline

### TabelaList (`/admin/cadastros/tabelas`)

- Filtro por Operadora (obrigatório para exibir dados — dropdown de operadoras ativas)
- Tabela: CodigoTuss, Descrição (join Procedimento), Valor, ações Editar/Excluir
- Botão "Importar CSV" → modal igual ao de Procedimentos
- Botão "Nova entrada"

### TabelaForm (modal)

- Campos: Operadora (dropdown), Procedimento (autocomplete por código/descrição), Valor
- Validação: Valor > 0

---

## Tasks (TDD — Red → Green → Refactor)

### TASK-F23-01 — Entidades + Migration ✅

**Red (escrever primeiro — arquivo `Catalog.Tests/SchemaTests.cs`):**

```
- tabela "prestadores" existe com colunas esperadas
- tabela "tabelas_procedimento" existe + unique (tenant_id, operadora_id, procedimento_id)
- tabela "deflatores_prestador" existe + unique (tenant_id, prestador_id, operadora_id, posicao)
- FK prestadores → identity.tenants
- FK tabelas_procedimento → operadoras, procedimentos
- FK deflatores_prestador → prestadores, operadoras
```

**Green:**

- Criar `Prestador.cs`, `TabelaProcedimento.cs`, `DeflatorPrestador.cs` em `App/Catalog/`
- Criar `PosicaoExecutor.cs` (enum)
- Criar `PrestadorConfiguration.cs`, `TabelaProcedimentoConfiguration.cs`, `DeflatorPrestadorConfiguration.cs` em `App/Catalog/Configurations/`
- Registrar `DbSet<>` em `AppDbContext`
- Gerar migration `AddTabelasPrestadores`
- Adicionar `.editorconfig` na pasta `Migrations/` com supressões IDE0005/IDE0161/CA1515/CA1861

**Critério de pronto:** `dotnet test` no suite de schema passa; `dotnet build` limpo.

---

### TASK-F23-02 — PrestadorService + Endpoints ✅

**Red (arquivo `Catalog.Tests/PrestadorServiceTests.cs`):**

```
Listar_RetornaVazioQuandoSemPrestadores
Listar_FiltraPorNome_RetornaSomenteCorrespondentes
Listar_FiltraPorAtivo_RetornaSomenteAtivos
Criar_ComDadosValidos_RetornaPrestadorDto
Criar_SemNome_RetornaValidationError
Criar_NomeMuitoLongo_RetornaValidationError
Atualizar_PrestadorExistente_AtualizaCampos
Atualizar_PrestadorInexistente_RetornaNotFoundError
Excluir_PrestadorExistente_Remove
Excluir_PrestadorInexistente_RetornaNotFoundError
Listar_NaoRetornaPrestadoresDeOutroTenant
```

**Green:**

- Records: `PrestadorDto`, `SalvarPrestadorCommand`, `ListarPrestadoresQuery`, `ListarPrestadoresResult`
- Métodos em `CatalogService`: `ListarPrestadoresAsync`, `ObterPrestadorPorIdAsync`, `CriarPrestadorAsync`, `AtualizarPrestadorAsync`, `ExcluirPrestadorAsync`
- Registrar endpoints em `CatalogEndpoints.cs` (padrão `MapGroup`)

**Critério de pronto:** todos os testes passam; `dotnet build` limpo; cobertura ≥ 80%.

---

### TASK-F23-03 — TabelaService + CSV Import + Endpoints ✅

**Red (arquivo `Catalog.Tests/TabelaServiceTests.cs`):**

```
Listar_FiltraPorOperadoraId_RetornaSomenteDaOperadora
Criar_ComDadosValidos_RetornaTabelaDto
Criar_ValorZeroOuNegativo_RetornaValidationError
Criar_DuplicadoMesmaOperadoraProcedimento_RetornaConflictError
Atualizar_EntradaExistente_AtualizaValor
Excluir_EntradaExistente_Remove
ImportarCsv_ArquivoValido_InsereNovas
ImportarCsv_CodigoExistente_AtualizaValor
ImportarCsv_LinhaComErro_NaoAbortaBatch
ImportarCsv_CodigoTussInexistente_RegistraErroDeLinha
ImportarCsv_ValorInvalido_RegistraErroDeLinha
ImportarCsv_AcimaLimite_RetornaErroGeral
Listar_NaoRetornaTabelasDeOutroTenant
```

**Green:**

- Records: `TabelaDto`, `SalvarTabelaCommand`, `ListarTabelasQuery`, `ListarTabelasResult`
- Métodos em `CatalogService`: `ListarTabelasAsync`, `ObterTabelaPorIdAsync`, `CriarTabelaAsync`, `AtualizarTabelaAsync`, `ExcluirTabelaAsync`, `ImportarTabelaCsvAsync`
- Registrar endpoints em `CatalogEndpoints.cs`

**Critério de pronto:** todos os testes passam; `dotnet build` limpo; cobertura ≥ 80%.

---

### TASK-F23-04 — DeflatorService + Endpoints ✅

**Red (arquivo `Catalog.Tests/DeflatorServiceTests.cs`):**

```
Listar_RetornaDeflatoresDoPrestador
Listar_NaoRetornaDeflatoresDeOutroPrestador
Criar_ComDadosValidos_RetornaDeflatorDto
Criar_PercentualZero_RetornaValidationError
Criar_PercentualAcima200_RetornaValidationError
Criar_DuplicadoMesmaPosicaoOperadora_RetornaConflictError
Criar_PrestadorDeOutroTenant_RetornaNotFoundError
Atualizar_DeflatorExistente_AtualizaPercentual
Excluir_DeflatorExistente_Remove
Excluir_DeflatorInexistente_RetornaNotFoundError
```

**Green:**

- Records: `DeflatorDto`, `SalvarDeflatorCommand`
- Métodos em `CatalogService`: `ListarDeflatoresAsync`, `CriarDeflatorAsync`, `AtualizarDeflatorAsync`, `ExcluirDeflatorAsync`
- Registrar endpoints aninhados sob `/prestadores/{prestadorId}/deflatores`

**Critério de pronto:** todos os testes passam; `dotnet build` limpo; cobertura ≥ 80%.

---

### TASK-F23-05 — Angular: Telas Prestador ✅

**Red (escrever specs Vitest primeiro):**

`prestador-list.component.spec.ts`:

```
renderiza tabela vazia com mensagem "Nenhum prestador cadastrado"
exibe prestadores retornados pela API
filtro por nome dispara request após debounce (vi.useFakeTimers)
clicar "Novo prestador" abre formulário
clicar "Editar" abre formulário com dados preenchidos
clicar "Excluir" e confirmar chama DELETE
409 na exclusão exibe toast de erro correto
```

`prestador-form.component.spec.ts`:

```
submit sem nome exibe erro de validação
submit válido chama POST com payload correto
edição carrega dados e chama PUT
seção deflatores lista deflatores do prestador
"+ Deflator" abre inline form; submit chama POST deflatores
excluir deflator chama DELETE deflatores/{id}
```

**Green:**

- Gerar cliente OpenAPI (`pnpm generate-api-client`) após TASK-F23-04
- `PrestadorListComponent`, `PrestadorFormComponent` (com seção de deflatores inline)
- Adicionar rota `/admin/cadastros/prestadores` e link no sidebar

**Critério de pronto:** `pnpm -F admin-web test:ci` passa; `pnpm -F admin-web lint` limpo; cobertura ≥ 80%.

---

### TASK-F23-06 — Angular: Telas Tabela ✅

**Red:**

`tabela-list.component.spec.ts`:

```
exibe seletor de operadora antes de mostrar tabela
selecionar operadora dispara GET /tabelas?operadoraId=...
exibe entradas retornadas com CodigoTuss, Descrição e Valor formatado
clicar "Importar CSV" abre modal
clicar "Nova entrada" abre modal de formulário
clicar "Editar" abre form preenchido
clicar "Excluir" e confirmar chama DELETE
```

`tabela-form.component.spec.ts`:

```
submit sem operadora exibe erro de validação
submit sem procedimento exibe erro de validação
submit com valor 0 exibe erro de validação
submit válido chama POST com payload correto
```

`tabela-csv-modal.component.spec.ts`:

```
upload de arquivo válido chama POST importar-csv com operadoraId correto
exibe contadores inseridos/atualizados/erros
erros de linha são listados
```

**Green:**

- `TabelaListComponent`, `TabelaFormComponent`, `TabelaCsvModalComponent`
- Adicionar rota `/admin/cadastros/tabelas` e link no sidebar

**Critério de pronto:** `pnpm -F admin-web test:ci` passa; `pnpm -F admin-web lint` limpo; cobertura ≥ 80%.

---

## Ordem de execução recomendada

```
TASK-F23-01 → TASK-F23-02 → TASK-F23-03 → TASK-F23-04
                                                    ↓
                              gerar cliente OpenAPI
                                                    ↓
                    TASK-F23-05 ← TASK-F23-06 (paralelos)
```

## Critério de pronto da feature completa

- [ ] `dotnet test` passa com cobertura ≥ 80% no projeto `Catalog.Tests`
- [ ] `pnpm -F admin-web test:ci` passa com cobertura ≥ 80%
- [ ] `dotnet build` e `pnpm -F admin-web lint` sem warnings
- [ ] Motor F3.2 pode resolver `valor_base` dado `(OperadoraId, ProcedimentoId, PrestadorId, PosicaoExecutor)` consultando as tabelas geradas aqui

# SPEC-F2.2 — Operadoras e Procedimentos

**Fase:** F2.2 — Cadastros  
**Contexto backend:** `Catalog`  
**Pré-requisito:** F2.1 concluída (auth, AdminShell, rotas admin funcionando)  
**Foco inicial:** UNIMED — campos e regras pensados a partir das necessidades do motor de cálculo UNIMED

---

## Objetivo

Criar o catálogo de `Operadora` e `Procedimento` — as duas entidades mestras que sustentam todo o cálculo de honorários. Sem esses cadastros, o motor de cálculo (F3.2) não tem como funcionar.

**Entregáveis:**

- API REST para CRUD de Operadora (com paginação e filtros)
- API REST para CRUD de Procedimento (com paginação e filtros)
- Importação de Procedimentos via CSV (batch upsert)
- Telas Angular no `admin-web` para ambos os cadastros

---

## Modelo de dados

### Entidade `Operadora`

Representa uma operadora de saúde. Cada Unimed Singular (João Pessoa, Recife, Fortaleza, etc.) é uma `Operadora` separada — não confundir com "UNIMED" como rede.

```
Operadora
├── Id               Guid         PK
├── TenantId         Guid         FK, NOT NULL, implements ITenantEntity
├── Nome             string(200)  NOT NULL
├── RegistroAns      string(6)    NULL  — código ANS da operadora (ex: "012345")
├── Cnpj             string(14)   NULL  — apenas dígitos, sem formatação (único por tenant)
├── TipoRuleSet      enum         NOT NULL  — determina o IPricingRuleSet a usar
├── Ativa            bool         NOT NULL, DEFAULT true
└── CriadaEm        DateTimeOffset NOT NULL, DEFAULT now()
```

**Enum `TipoRuleSet`:**

- `Unimed` — usa `UnimedRuleSet` (motor completo com pipeline de modificadores)
- `Nulo` — usa `NullRuleSet` (sem apuração de honorários; guia opera só com status e observação)

**Índices:**

- Unique: `(TenantId, Cnpj)` — CNPJ único por tenant (quando informado)
- Index: `(TenantId, Ativa)` — filtro de listagem

**Regras de negócio:**

- Não pode excluir Operadora que já tenha Guias associadas (validação no service — futura)
- CNPJ, se informado, deve ter exatamente 14 dígitos numéricos (sem formatação)
- RegistroAns, se informado, deve ter exatamente 6 dígitos numéricos
- Nome obrigatório; RegistroAns e Cnpj opcionais no MVP

---

### Entidade `Procedimento`

Representa um procedimento médico identificado por código TUSS. É a base para o cálculo de honorários. Campos pensados a partir das necessidades do motor UNIMED (CBHPM 2015).

```
Procedimento
├── Id                    Guid         PK
├── TenantId              Guid         FK, NOT NULL, implements ITenantEntity
├── CodigoTuss            string(10)   NOT NULL  — ex: "30715013"
├── Descricao             string(500)  NOT NULL
├── Porte                 string(4)    NULL  — porte cirúrgico CBHPM: "1A", "2B", "6B", "16A"
├── PorteAnestesico       int          NULL  — porte AN: 0 a 8 (apenas para procedimentos com anestesia)
├── EhSadt                bool         NOT NULL, DEFAULT false
│   — true: SADT (exames, diagnósticos). Acréscimo de urgência NÃO aplica.
├── TemPorteProprioVideo  bool         NOT NULL, DEFAULT false
│   — true: código TUSS já contempla videolaparoscopia; acréscimo de 50% NÃO aplica.
├── Ativo                 bool         NOT NULL, DEFAULT true
└── CriadoEm             DateTimeOffset NOT NULL, DEFAULT now()
```

**Índices:**

- Unique: `(TenantId, CodigoTuss)` — código TUSS único por tenant
- Index: `(TenantId, Ativo)` — filtro de listagem
- Index: `(TenantId, CodigoTuss)` — busca por código (autocomplete em guias)

**Regras de negócio:**

- `CodigoTuss` obrigatório, único por tenant
- `PorteAnestesico`, se informado, deve ser entre 0 e 8
- `Porte` é livre (string) para suportar todos os códigos CBHPM sem manutenção de enum
- Na importação CSV: upsert por `(TenantId, CodigoTuss)` — atualiza se já existe, insere se novo

---

### Por que `TenantId` em `Procedimento`?

Cada billing company pode customizar sua tabela de procedimentos (ex: adicionar campos internos, ativar/desativar para o seu fluxo, importar versão específica da tabela CBHPM vigente para cada Unimed Singular). Não há tabela global compartilhada no MVP — cada tenant gerencia a sua.

---

## Estrutura de arquivos — backend

```
App/
└── Catalog/
    ├── Operadora.cs
    ├── Procedimento.cs
    ├── TipoRuleSet.cs               (enum)
    ├── CatalogService.cs            (CRUD de Operadora e Procedimento, import CSV)
    ├── Configurations/
    │   ├── OperadoraConfiguration.cs
    │   └── ProcedimentoConfiguration.cs
    └── Endpoints/
        └── CatalogEndpoints.cs

tests/
└── Catalog.Tests/
    ├── Catalog.Tests.csproj
    ├── Fixtures/
    │   └── PostgresContainerFixture.cs   (copiar padrão de Identity.Tests)
    ├── Operadora/
    │   └── OperadoraCrudTests.cs
    └── Procedimento/
        ├── ProcedimentoCrudTests.cs
        └── ProcedimentoCsvImportTests.cs
```

---

## API REST — contrato

### Operadoras

Todos os endpoints exigem role `TenantAdmin` ou `SaasAdmin` (política `TenantAccess`).

| Método   | Rota                            | Descrição                                                |
| -------- | ------------------------------- | -------------------------------------------------------- |
| `GET`    | `/api/v1/admin/operadoras`      | Lista operadoras do tenant (paginada)                    |
| `GET`    | `/api/v1/admin/operadoras/{id}` | Detalhe de uma operadora                                 |
| `POST`   | `/api/v1/admin/operadoras`      | Cria nova operadora                                      |
| `PUT`    | `/api/v1/admin/operadoras/{id}` | Atualiza operadora                                       |
| `DELETE` | `/api/v1/admin/operadoras/{id}` | Exclui operadora (hard delete; bloqueia se houver guias) |

**Query params de listagem:**

- `nome` (string, optional) — filtro por nome (case-insensitive, contains)
- `ativa` (bool, optional) — filtro por status; sem parâmetro retorna todas
- `pagina` (int, default 1)
- `itensPorPagina` (int, default 20, max 100)

**Request body — POST/PUT:**

```json
{
  "nome": "UNIMED João Pessoa",
  "registroAns": "012345",
  "cnpj": "12345678000195",
  "tipoRuleSet": "Unimed",
  "ativa": true
}
```

**Response — item:**

```json
{
  "id": "guid",
  "nome": "UNIMED João Pessoa",
  "registroAns": "012345",
  "cnpj": "12345678000195",
  "tipoRuleSet": "Unimed",
  "ativa": true,
  "criadaEm": "2026-05-02T10:00:00Z"
}
```

**Response — lista:**

```json
{
  "itens": [...],
  "total": 5,
  "pagina": 1,
  "itensPorPagina": 20
}
```

---

### Procedimentos

Todos os endpoints exigem política `TenantAccess`.

| Método   | Rota                                       | Descrição                                |
| -------- | ------------------------------------------ | ---------------------------------------- |
| `GET`    | `/api/v1/admin/procedimentos`              | Lista procedimentos (paginada)           |
| `GET`    | `/api/v1/admin/procedimentos/{id}`         | Detalhe de um procedimento               |
| `POST`   | `/api/v1/admin/procedimentos`              | Cria procedimento manualmente            |
| `PUT`    | `/api/v1/admin/procedimentos/{id}`         | Atualiza procedimento                    |
| `DELETE` | `/api/v1/admin/procedimentos/{id}`         | Exclui procedimento (bloqueia se em uso) |
| `POST`   | `/api/v1/admin/procedimentos/importar-csv` | Import em batch via CSV                  |

**Query params de listagem:**

- `busca` (string, optional) — filtro por `CodigoTuss` ou `Descricao` (case-insensitive, contains)
- `ativo` (bool, optional) — filtro por status
- `pagina` (int, default 1)
- `itensPorPagina` (int, default 20, max 100)

**Request body — POST/PUT:**

```json
{
  "codigoTuss": "30715013",
  "descricao": "Herniorrafia inguinal",
  "porte": "6B",
  "porteAnestesico": 4,
  "ehSadt": false,
  "temPorteProprioVideo": false,
  "ativo": true
}
```

**Response — item:**

```json
{
  "id": "guid",
  "codigoTuss": "30715013",
  "descricao": "Herniorrafia inguinal",
  "porte": "6B",
  "porteAnestesico": 4,
  "ehSadt": false,
  "temPorteProprioVideo": false,
  "ativo": true,
  "criadoEm": "2026-05-02T10:00:00Z"
}
```

**Endpoint de importação CSV:**

- Content-Type: `multipart/form-data`, campo `file`
- Formato do CSV (cabeçalho obrigatório, separador `;`):

```
CodigoTuss;Descricao;Porte;PorteAnestesico;EhSadt;TemPorteProprioVideo
30715013;Herniorrafia inguinal;6B;4;false;false
40314340;Eletroencefalograma;;;true;false
```

- Colunas `Porte`, `PorteAnestesico`, `EhSadt`, `TemPorteProprioVideo` são opcionais no CSV — células vazias → null/false
- Upsert: se `CodigoTuss` já existe no tenant, atualiza os demais campos; se não, insere
- Linhas com `CodigoTuss` vazio são ignoradas (não são erro)
- Limite: 10.000 linhas por importação

**Response da importação:**

```json
{
  "inseridos": 42,
  "atualizados": 10,
  "ignorados": 2,
  "erros": []
}
```

Em caso de erro numa linha:

```json
{
  "inseridos": 0,
  "atualizados": 0,
  "ignorados": 0,
  "erros": [{ "linha": 3, "mensagem": "CodigoTuss '99999999' tem mais de 10 caracteres" }]
}
```

Política de erro: processar o máximo possível, coletar erros linha a linha. Não abortar o batch inteiro por uma linha inválida.

---

## Estrutura de arquivos — frontend (`admin-web`)

```
src/app/admin/
└── catalog/
    ├── catalog.routes.ts
    ├── operadoras/
    │   ├── operadora-list/
    │   │   ├── operadora-list.component.ts
    │   │   ├── operadora-list.component.html
    │   │   ├── operadora-list.component.scss
    │   │   └── operadora-list.component.spec.ts
    │   └── operadora-form/
    │       ├── operadora-form.component.ts
    │       ├── operadora-form.component.html
    │       ├── operadora-form.component.scss
    │       └── operadora-form.component.spec.ts
    └── procedimentos/
        ├── procedimento-list/
        │   ├── procedimento-list.component.ts
        │   ├── procedimento-list.component.html
        │   ├── procedimento-list.component.scss
        │   └── procedimento-list.component.spec.ts
        └── procedimento-form/
            ├── procedimento-form.component.ts
            ├── procedimento-form.component.html
            ├── procedimento-form.component.scss
            └── procedimento-form.component.spec.ts
```

**Rotas Angular:**

```
/admin/catalog/operadoras           → OperadoraListComponent  (lazy)
/admin/catalog/operadoras/nova      → OperadoraFormComponent  (lazy)
/admin/catalog/operadoras/:id       → OperadoraFormComponent  (lazy, modo edição)
/admin/catalog/procedimentos        → ProcedimentoListComponent (lazy)
/admin/catalog/procedimentos/novo   → ProcedimentoFormComponent (lazy)
/admin/catalog/procedimentos/:id    → ProcedimentoFormComponent (lazy, modo edição)
```

**Atualizar `admin.routes.ts`** para incluir `catalog.routes.ts` com `loadChildren`.

**Atualizar `AdminShell`** para adicionar links no sidebar:

- Seção "Cadastros" com itens "Operadoras" e "Procedimentos"

---

## UX — telas

### OperadoraList

- Tabela com colunas: Nome, Registro ANS, CNPJ (formatado XX.XXX.XXX/XXXX-XX), Tipo (badge), Status (chip Ativa/Inativa)
- Filtro de busca por nome (debounce 300ms)
- Toggle "Exibir inativas"
- Botão "Nova operadora" → `/admin/catalog/operadoras/nova`
- Click na linha → `/admin/catalog/operadoras/:id`
- Paginação no rodapé

### OperadoraForm (criar/editar)

- Campos: Nome*, Registro ANS, CNPJ, Tipo de cálculo* (select: UNIMED | Sem apuração)
- Toggle "Ativa"
- Botões: "Salvar" / "Cancelar"
- Modo edição: pré-preenche campos, título "Editar operadora"
- Modo criação: título "Nova operadora"
- Validação inline (nome obrigatório, ANS 6 dígitos, CNPJ 14 dígitos)

### ProcedimentoList

- Tabela com colunas: Código TUSS, Descrição, Porte, Porte AN, Flags (chips: SADT, Vídeo próprio), Status
- Campo de busca por código ou descrição (debounce 300ms)
- Toggle "Exibir inativos"
- Botão "Novo procedimento"
- Botão "Importar CSV" → abre modal com upload e prévia do resultado
- Click na linha → form de edição
- Paginação

### ProcedimentoForm (criar/editar)

- Campos: Código TUSS*, Descrição*, Porte, Porte Anestésico, SADT (checkbox), Vídeo próprio (checkbox), Ativo (toggle)
- Tooltips explicativos para "SADT" (urgência não aplica) e "Vídeo próprio" (50% não aplica)
- Botões: "Salvar" / "Cancelar"

### Modal de importação CSV

- Input de arquivo (apenas `.csv`)
- Instrução de formato com link para download de template
- Após upload: exibe resumo (inseridos/atualizados/ignorados/erros)
- Erros listados linha a linha
- Botão "Fechar" após conclusão

---

## Sequência de tasks

As tasks devem ser executadas **nesta ordem**. Cada uma é um prompt isolado para a IA. Todas seguem **TDD**: escrever os testes que falham primeiro, depois implementar o mínimo para passarem, depois refatorar se necessário.

---

### TASK-CAT-01 — Entidades + Scaffold do projeto de testes + Schema tests (Red) ✅

Esta task estabelece a fundação: cria as entidades, configura o EF Core e monta o projeto de testes com os primeiros testes — que falharão até a migration existir.

#### Parte A — Entidades e configurações EF

1. Criar enum `TipoRuleSet` em `App/Catalog/TipoRuleSet.cs`
2. Criar `App/Catalog/Operadora.cs` implementando `ITenantEntity`
3. Criar `App/Catalog/Procedimento.cs` implementando `ITenantEntity`
4. Criar `App/Catalog/Configurations/OperadoraConfiguration.cs`
5. Criar `App/Catalog/Configurations/ProcedimentoConfiguration.cs`
6. Registrar ambas as configurações no `AppDbContext` e adicionar os `DbSet<T>`

Especificações das configurações:

- Nomes de tabela: `operadoras` e `procedimentos` (snake_case)
- `TipoRuleSet` armazenado como string: `.HasConversion<string>()`
- Índices conforme o modelo de dados definido nesta spec
- O global query filter de `TenantId` já aplica via reflexão no `AppDbContext` — não recriar

#### Parte B — Scaffold do projeto `Catalog.Tests` + Schema tests

1. Criar `tests/Catalog.Tests/Catalog.Tests.csproj` (copiar estrutura de `Identity.Tests.csproj`)
2. Adicionar à solution: `dotnet sln add tests/Catalog.Tests/Catalog.Tests.csproj`
3. Copiar `PostgresContainerFixture.cs` de `tests/Identity.Tests/Fixtures/` para `tests/Catalog.Tests/Fixtures/` — sem alterações
4. Criar `tests/Catalog.Tests/Schema/CatalogSchemaTests.cs` com:

```csharp
// Verifica que as tabelas e colunas existem com os tipos e constraints corretos.
// Esses testes FALHARÃO até que a migration seja aplicada (TASK-CAT-02).
[Collection(nameof(PostgresCollection))]
public class CatalogSchemaTests(PostgresContainerFixture db)
{
    [Fact] public async Task Tabela_Operadoras_Existe()
    [Fact] public async Task Tabela_Procedimentos_Existe()
    [Fact] public async Task Operadoras_IndiceUnico_TenantId_Cnpj()
    [Fact] public async Task Procedimentos_IndiceUnico_TenantId_CodigoTuss()
}
```

Usar SQL direto via `db.Connection` para verificar `information_schema.columns` e `pg_indexes` — não usar `AppDbContext` (que exigiria que as tabelas já existissem).

**Estado esperado ao final:** `dotnet build` passa. `dotnet test --filter CatalogSchemaTests` **falha** (tabelas não existem — isso é o Red esperado). Os outros projetos de teste continuam passando.

---

### TASK-CAT-02 — Migration `AddCatalogEntities` (Green dos schema tests) ✅

1. Executar: `dotnet ef migrations add AddCatalogEntities --project App --output-dir Catalog/Migrations`
2. Revisar o arquivo gerado: confirmar tabelas `operadoras` e `procedimentos`, índices únicos `(tenant_id, cnpj)` e `(tenant_id, codigo_tuss)`, e colunas conforme o modelo
3. Aplicar ao banco de dev para validar: `dotnet ef database update --project App`
4. **Não alterar** os testes de schema — eles devem passar sem modificação

**Critério de pronto:** `dotnet test --filter CatalogSchemaTests` passa (Green). `dotnet build` limpo.

---

### TASK-CAT-03 — TDD: `CatalogService` — Operadora ✅

#### Red — escrever os testes primeiro

Criar `tests/Catalog.Tests/Operadora/OperadoraCrudTests.cs` com os seguintes testes. **Escrever todos antes de tocar no service.**

```
CriarOperadora_NomeValido_RetornaOperadoraCriada
CriarOperadora_NomeFaltando_RetornaValidationError
CriarOperadora_CnpjComFormatoInvalido_RetornaValidationError   (ex: "123" — menos de 14 dígitos)
CriarOperadora_CnpjDuplicadoNoMesmoTenant_RetornaConflictError
CriarOperadora_CnpjDuplicadoEmTenantDistinto_Permitido          (isolamento multi-tenant)
CriarOperadora_RegistroAnsComFormatoInvalido_RetornaValidationError  (ex: "12" — menos de 6 dígitos)
ObterOperadoraPorId_NaoEncontrada_RetornaNotFoundError
ObterOperadoraPorId_OperadoraDeOutroTenant_RetornaNotFoundError  (isolamento multi-tenant)
AtualizarOperadora_CnpjDuplicadoNoProprioCadastro_Permitido     (não conflita consigo mesmo)
ListarOperadoras_FiltroNome_RetornaApenasCorrespondentes
ListarOperadoras_FiltroAtiva_RetornaApenasAtivas
ListarOperadoras_NaoRetornaOperadorasDeOutroTenant
ExcluirOperadora_Existente_RemoveDoBanco
ExcluirOperadora_NaoEncontrada_RetornaNotFoundError
```

Confirmar que `dotnet test --filter OperadoraCrudTests` **falha** (service não existe — Red confirmado).

#### Green — implementar o `CatalogService`

Criar `App/Catalog/CatalogService.cs` com os tipos e métodos de Operadora:

```csharp
record ListarOperadorasQuery(string? Nome, bool? Ativa, int Pagina, int ItensPorPagina);
record ListarOperadorasResult(IReadOnlyList<OperadoraDto> Itens, int Total, int Pagina, int ItensPorPagina);
record OperadoraDto(Guid Id, string Nome, string? RegistroAns, string? Cnpj, TipoRuleSet TipoRuleSet, bool Ativa, DateTimeOffset CriadaEm);
record CriarOperadoraCommand(string Nome, string? RegistroAns, string? Cnpj, TipoRuleSet TipoRuleSet);
record AtualizarOperadoraCommand(string Nome, string? RegistroAns, string? Cnpj, TipoRuleSet TipoRuleSet, bool Ativa);

Task<ListarOperadorasResult> ListarAsync(ListarOperadorasQuery query);
Task<Result<OperadoraDto>> ObterPorIdAsync(Guid id);
Task<Result<OperadoraDto>> CriarAsync(CriarOperadoraCommand cmd);
Task<Result<OperadoraDto>> AtualizarAsync(Guid id, AtualizarOperadoraCommand cmd);
Task<Result> ExcluirAsync(Guid id);
```

Regras de negócio:

- `ListarAsync`: global query filter já filtra por tenant; aplicar filtros opcionais; ordenar por `Nome` ASC; paginar
- `CriarAsync`: validar formato de RegistroAns (6 dígitos) e Cnpj (14 dígitos) se informados; checar unicidade de Cnpj no tenant
- `AtualizarAsync`: mesma validação; excluir o próprio `Id` da checagem de unicidade
- `ExcluirAsync`: hard delete simples — `// TODO F3.1: bloquear se houver Guias associadas`
- Retornar `Result<T>` com `ValidationError`, `ConflictError` ou `NotFoundError` conforme o caso

Registrar `CatalogService` no DI em `Program.cs` (scoped).

**Critério de pronto:** `dotnet test --filter OperadoraCrudTests` passa (Green). `dotnet build` limpo.

---

### TASK-CAT-04 — TDD: Endpoints de Operadora ✅

#### Red — escrever os testes primeiro

Criar `tests/Catalog.Tests/Operadora/OperadoraEndpointTests.cs` usando `WebApplicationFactory<Program>`.

**Setup necessário para a fixture de endpoint:**

- Substituir a string de conexão do `AppDbContext` pela do `PostgresContainerFixture` via `builder.ConfigureServices`
- Adicionar um scheme de autenticação de teste (`TestAuthHandler`) que simula um `TenantAdmin` autenticado com `TenantId` fixo, sem passar pelo Google OAuth
- O `TestAuthHandler` deve injetar as mesmas claims que o JWT real injeta (`sub`, `role`, `tenant_id`)

Seguir o padrão do `TestAuthHandler` descrito em [Microsoft docs para integration tests com auth customizada](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests#mock-authentication).

Testes a escrever:

```
GET_Operadoras_SemAutenticacao_Retorna401
GET_Operadoras_ComTenantAdmin_Retorna200ComLista
POST_Operadoras_DadosValidos_Retorna201ComOperadoraCriada
POST_Operadoras_NomeFaltando_Retorna400
POST_Operadoras_CnpjDuplicado_Retorna409
GET_OperadoraPorId_Existente_Retorna200
GET_OperadoraPorId_NaoEncontrada_Retorna404
PUT_Operadora_DadosValidos_Retorna200Atualizado
DELETE_Operadora_Existente_Retorna204
DELETE_Operadora_NaoEncontrada_Retorna404
```

Confirmar Red antes de implementar os endpoints.

#### Green — implementar os endpoints

Criar `App/Catalog/Endpoints/CatalogEndpoints.cs`. Registrar em `Program.cs`:

```csharp
app.MapGroup("/api/v1/admin/operadoras")
   .RequireAuthorization("TenantAccess")
   .MapOperadoraEndpoints();
```

Implementar os 5 endpoints. Seguir **exatamente** o padrão de `App/Identity/Endpoints/AdminEndpoints.cs`:

- Minimal API com `RouteGroupBuilder`
- Request/response records no mesmo arquivo
- Mapeamento manual de `Result<T>` → HTTP status: `ValidationError` → 400, `ConflictError` → 409, `NotFoundError` → 404

**Não implementar** os endpoints de Procedimento nesta task.

**Critério de pronto:** `dotnet test --filter OperadoraEndpointTests` passa. Endpoints aparecem no Swagger.

---

### TASK-CAT-05 — TDD: `CatalogService` — Procedimento + Import CSV

#### Red — escrever os testes primeiro

Criar `tests/Catalog.Tests/Procedimento/ProcedimentoCrudTests.cs`:

```
CriarProcedimento_DadosMinimosValidos_RetornaProcedimentoCriado
CriarProcedimento_CodigoTussFaltando_RetornaValidationError
CriarProcedimento_CodigoTussComMaisDe10Chars_RetornaValidationError
CriarProcedimento_CodigoTussDuplicadoNoMesmoTenant_RetornaConflictError
CriarProcedimento_CodigoTussDuplicadoEmTenantDistinto_Permitido
CriarProcedimento_PorteAnestesico9_RetornaValidationError
CriarProcedimento_PorteAnestesico0_Permitido                    (limite inferior válido)
ObterProcedimentoPorId_DeOutroTenant_RetornaNotFoundError
ListarProcedimentos_BuscaPorCodigoTuss_RetornaCorrespondentes
ListarProcedimentos_BuscaPorDescricao_RetornaCorrespondentes    (case-insensitive)
ListarProcedimentos_NaoRetornaProcedimentosDeOutroTenant
AtualizarProcedimento_AlteraFlags_PersisteCorrentemente
ExcluirProcedimento_NaoEncontrado_RetornaNotFoundError
```

Criar `tests/Catalog.Tests/Procedimento/ProcedimentoCsvImportTests.cs`:

```
ImportarCsv_TodasColunasPresentes_InsereTodas
ImportarCsv_CodigoTussJaExistente_AtualizaCampos                (upsert)
ImportarCsv_CodigoTussJaExistente_NaoAlteraTenantId             (isolamento garantido)
ImportarCsv_LinhaSemCodigoTuss_Ignora
ImportarCsv_PorteAnestesicoInvalido_RegistraErroMasContinua     (batch não aborta)
ImportarCsv_ColunasOpcionaisAusentes_UsaDefaultsFalseNull
ImportarCsv_SeparadorPontoEVirgula_ParseiaCorretamente
ImportarCsv_TenantDistinto_NaoInterfereCadastroExistente
ImportarCsv_Maisde10000Linhas_RetornaErroGenerico
```

Confirmar Red.

#### Green — implementar os métodos de Procedimento no `CatalogService`

Adicionar ao `CatalogService.cs`:

```csharp
record ListarProcedimentosQuery(string? Busca, bool? Ativo, int Pagina, int ItensPorPagina);
record ListarProcedimentosResult(IReadOnlyList<ProcedimentoDto> Itens, int Total, int Pagina, int ItensPorPagina);
record ProcedimentoDto(Guid Id, string CodigoTuss, string Descricao, string? Porte, int? PorteAnestesico, bool EhSadt, bool TemPorteProprioVideo, bool Ativo, DateTimeOffset CriadoEm);
record SalvarProcedimentoCommand(string CodigoTuss, string Descricao, string? Porte, int? PorteAnestesico, bool EhSadt, bool TemPorteProprioVideo, bool Ativo);
record ImportarCsvResult(int Inseridos, int Atualizados, int Ignorados, IReadOnlyList<ErroCsvLinha> Erros);
record ErroCsvLinha(int Linha, string Mensagem);

Task<ListarProcedimentosResult> ListarProcedimentosAsync(ListarProcedimentosQuery query);
Task<Result<ProcedimentoDto>> ObterProcedimentoPorIdAsync(Guid id);
Task<Result<ProcedimentoDto>> CriarProcedimentoAsync(SalvarProcedimentoCommand cmd);
Task<Result<ProcedimentoDto>> AtualizarProcedimentoAsync(Guid id, SalvarProcedimentoCommand cmd);
Task<Result> ExcluirProcedimentoAsync(Guid id);
Task<ImportarCsvResult> ImportarProcedimentosCsvAsync(Stream csvStream);
```

Regras de negócio:

- Busca filtra `CodigoTuss` (startsWith) e `Descricao` (contains), case-insensitive, `OR` entre eles
- Import CSV: separador `;`; cabeçalho case-insensitive; colunas opcionais ausentes → defaults (`false`/`null`); upsert via `ExecuteUpdate` + `AddRange`; erros linha a linha sem abortar; limite 10.000 linhas

**Critério de pronto:** `dotnet test --filter "ProcedimentoCrudTests|ProcedimentoCsvImportTests"` passa. `dotnet build` limpo.

---

### TASK-CAT-06 — TDD: Endpoints de Procedimento

#### Red — escrever os testes primeiro

Criar `tests/Catalog.Tests/Procedimento/ProcedimentoEndpointTests.cs` (reusar a fixture de WebApplicationFactory criada em TASK-CAT-04):

```
GET_Procedimentos_SemAutenticacao_Retorna401
GET_Procedimentos_ComTenantAdmin_Retorna200
POST_Procedimentos_DadosValidos_Retorna201
POST_Procedimentos_CodigoTussDuplicado_Retorna409
GET_ProcedimentoPorId_NaoEncontrado_Retorna404
PUT_Procedimento_AlteraFlags_Retorna200ComNovoValor
DELETE_Procedimento_Existente_Retorna204
POST_ImportarCsv_ArquivoValido_Retorna200ComResumo
POST_ImportarCsv_ExtensaoErrada_Retorna400
POST_ImportarCsv_ArquivoAcima5MB_Retorna400
```

Confirmar Red.

#### Green — implementar os endpoints de Procedimento

Adicionar ao `CatalogEndpoints.cs`:

```csharp
app.MapGroup("/api/v1/admin/procedimentos")
   .RequireAuthorization("TenantAccess")
   .MapProcedimentoEndpoints();
```

Implementar os 6 endpoints (GET lista, GET por id, POST, PUT, DELETE, POST importar-csv).

Para o endpoint de importação:

- Recebe `IFormFile file`
- Validar extensão `.csv` e tamanho máximo 5 MB antes de chamar o service
- Retornar `200 OK` com `ImportarCsvResult` mesmo quando há erros de linha — não é erro HTTP

**Critério de pronto:** `dotnet test --filter ProcedimentoEndpointTests` passa. Todos os endpoints aparecem no Swagger. `dotnet test` passa completo com ≥80% de coverage em `Catalog.Tests`.

---

### TASK-CAT-07 — TDD: Angular — Telas de Operadora

**Pré-requisito:** rodar `pnpm generate-api-client` após TASK-CAT-06 (backend rodando). Se não disponível, criar tipos locais temporários com `// TODO: regenerar após backend`.

#### Red — escrever os specs primeiro

Criar `operadora-list.component.spec.ts`:

```typescript
// renderiza a lista recebida do service mockado
it("exibe uma linha por operadora retornada");
// estado vazio
it('exibe mensagem "Nenhuma operadora cadastrada" quando lista vazia');
// navegação
it("navega para /admin/catalog/operadoras/:id ao clicar na linha");
it('navega para /admin/catalog/operadoras/nova ao clicar em "Nova operadora"');
// filtros
it("chama o service com filtro de nome após debounce de 300ms");
// exclusão
it("chama service.excluir e recarrega a lista após confirmação");
it("não chama service.excluir se o usuário cancelar o confirm()");
```

Criar `operadora-form.component.spec.ts`:

```typescript
// modo criação
it('exibe título "Nova operadora" sem parâmetro de rota');
it("exibe erro de validação no campo nome ao tentar submeter vazio");
it("exibe erro se registroAns não tiver 6 dígitos");
it("exibe erro se cnpj não tiver 14 dígitos");
it("chama service.criar e navega para a lista após salvar com sucesso");
// modo edição
it('exibe título "Editar operadora" com id na rota');
it("pré-preenche os campos com os dados carregados do service");
it("chama service.atualizar ao salvar em modo edição");
```

Confirmar que `pnpm -F admin-web test:ci` **falha** (componentes não existem — Red confirmado).

#### Green — implementar os componentes

1. Criar `src/app/admin/catalog/catalog.routes.ts` com rotas lazy para as 4 telas; stubs vazios para ProcedimentoList e ProcedimentoForm
2. Atualizar `admin.routes.ts` para incluir `catalog.routes.ts` via `loadChildren`
3. Atualizar `AdminShell`: adicionar seção "Cadastros" no sidebar com links "Operadoras" e "Procedimentos"
4. Implementar `OperadoraListComponent` e `OperadoraFormComponent`

Detalhes:

`OperadoraListComponent`:

- Estado via `signal` (lista, loading, filtros, paginação) — sem `BehaviorSubject`
- Campo de busca com `debounceTime(300)`
- Badge de tipo: "UNIMED" / "Sem apuração"; chip status: "Ativa" / "Inativa"
- Excluir: `confirm()` → chamar API → recarregar lista

`OperadoraFormComponent`:

- `ActivatedRoute`: `id` presente = edição, ausente = criação
- `ReactiveFormsModule` com `Validators.required`, `Validators.pattern(/^\d{6}$/)`, `Validators.pattern(/^\d{14}$/)`
- CNPJ exibido formatado (XX.XXX.XXX/XXXX-XX), enviado só dígitos
- Select `TipoRuleSet`: `[{ value: 'Unimed', label: 'UNIMED' }, { value: 'Nulo', label: 'Sem apuração' }]`
- Navegar para a lista após salvar

Estilos: tokens de `STYLES.md` — `var(--color-*)`, `space()`, `@include text-*`. Nunca raw hex.

**Critério de pronto:** `pnpm -F admin-web test:ci` passa (Green). `pnpm -F admin-web lint` com 0 warnings. Telas funcionam no dev server.

---

### TASK-CAT-08 — TDD: Angular — Telas de Procedimento

#### Red — escrever os specs primeiro

Criar `procedimento-list.component.spec.ts`:

```typescript
it("exibe uma linha por procedimento retornado");
it('exibe chip "SADT" apenas quando ehSadt é true');
it('não exibe chip "SADT" quando ehSadt é false');
it('exibe chip "Vídeo próprio" apenas quando temPorteProprioVideo é true');
it("campo de busca chama service com termo após debounce de 300ms");
it('botão "Importar CSV" está presente no DOM');
it("exibe resumo de importação após upload bem-sucedido");
it("exibe erros de linha quando importação retorna erros");
it('botão "Download template" dispara download de arquivo .csv');
```

Criar `procedimento-form.component.spec.ts`:

```typescript
it("exibe erro se codigoTuss ultrapassar 10 caracteres");
it("exibe erro se codigoTuss estiver vazio ao submeter");
it("exibe erro se descricao estiver vazia ao submeter");
it("exibe erro se porteAnestesico for maior que 8");
it('campo "SADT" possui atributo title com texto explicativo');
it('campo "Vídeo próprio" possui atributo title com texto explicativo');
it('toggle "Ativo" reflete o valor do formulário');
it("pré-preenche campos em modo edição");
it("chama service.criar em modo criação e navega para lista");
it("chama service.atualizar em modo edição e navega para lista");
```

Confirmar Red.

#### Green — implementar os componentes

Implementar `ProcedimentoListComponent` e `ProcedimentoFormComponent` substituindo os stubs criados em TASK-CAT-07.

`ProcedimentoListComponent`:

- Colunas: Código TUSS, Descrição (truncada 60 chars + `…`), Porte, Porte AN, Flags, Status
- Chips de flags só quando `true`; busca `OR` por código e descrição (debounce 300ms)
- Importação CSV: `<input type="file" accept=".csv">` oculto, acionado por botão; upload ao selecionar; exibir resumo inline; botão "Download template" gera CSV com 2 linhas de amostra

`ProcedimentoFormComponent`:

- Campos: CodigoTuss*, Descricao*, Porte, PorteAnestesico (0–8), EhSadt (checkbox), TemPorteProprioVideo (checkbox), Ativo (toggle)
- `title` nos checkboxes: `"Exames e diagnósticos — acréscimo de urgência não se aplica"` e `"O código TUSS já contempla videolaparoscopia — acréscimo de 50% não se aplica"`

**Critério de pronto:** `pnpm -F admin-web test:ci` passa (Green). `pnpm -F admin-web lint` e `stylelint` com 0 warnings. Telas funcionam no dev server.

---

## Ordem de execução

```
TASK-CAT-01 ✅ (entidades + scaffold testes + schema tests RED)
      │
      ▼
TASK-CAT-02 ✅ (migration → schema tests GREEN)
      │
      ▼
TASK-CAT-03 ✅ (tests RED → CatalogService Operadora GREEN)
      │
      ▼
TASK-CAT-04 ✅ (tests RED → Endpoints Operadora GREEN)
      │
      ▼
TASK-CAT-05 (tests RED → CatalogService Procedimento + CSV GREEN)
      │
      ▼
TASK-CAT-06 (tests RED → Endpoints Procedimento GREEN)
      │
      ▼  [pnpm generate-api-client]
      │
TASK-CAT-07 (specs RED → Angular Operadora GREEN)
      │
      ▼
TASK-CAT-08 (specs RED → Angular Procedimento GREEN)
```

Cada task só começa depois que os testes da anterior estão passando. O cliente TypeScript (`pnpm generate-api-client`) é o ponto de sincronização entre backend e frontend — rodar após TASK-CAT-06.

---

## Critério de pronto global (F2.2)

- [ ] `dotnet build` passa sem warnings
- [ ] `dotnet test` passa com ≥80% coverage no `Catalog.Tests`
- [ ] Todos os endpoints aparecem no Swagger e respondem corretamente com autenticação
- [ ] Import CSV com 100 linhas funciona em menos de 2 segundos
- [ ] `pnpm -F admin-web test:ci` passa com ≥80% coverage
- [ ] `pnpm -F admin-web lint` e `stylelint` passam com 0 warnings
- [ ] Telas de Operadora e Procedimento funcionam no dev server (criar, listar, editar, excluir)
- [ ] Importação CSV via tela produz resultado correto
- [ ] Multi-tenant: operadora criada no tenant A não aparece no tenant B (verificar com testes)

---

## Decisões tomadas nesta SPEC

| Decisão                            | Escolha                      | Justificativa                                                                          |
| ---------------------------------- | ---------------------------- | -------------------------------------------------------------------------------------- |
| `Procedimento` é por tenant        | `TenantId` presente          | Cada billing company pode ter versão específica da tabela CBHPM do seu Unimed Singular |
| `Porte` como string (não enum)     | `string(4)`                  | CBHPM tem ~17 portes; string evita manutenção de enum quando novos portes surgirem     |
| `TipoRuleSet` como string no banco | `.HasConversion<string>()`   | Legibilidade no banco; facilita debug sem JOIN                                         |
| Import CSV com upsert              | `ExecuteUpdate` + `AddRange` | Permite reimportar tabela completa sem perder dados; D-013                             |
| Hard delete no MVP                 | Sem soft delete              | Sem guias ainda; simplifica; TODO adicionado para bloquear quando F3.1 vier            |
| Separador CSV é `;`                | `;` em vez de `,`            | Planilhas Excel/LibreOffice brasileiro exportam `;` por padrão                         |
| Telas no `admin-web`               | Não no SaaS painel           | Operadoras e procedimentos são dados do tenant, não do SaaS admin                      |

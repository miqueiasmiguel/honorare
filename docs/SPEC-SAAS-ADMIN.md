# SPEC: Painel SaaS Admin

## Contexto

O `admin-web` (Angular SPA em `apps/admin-web/`) serve dois perfis distintos: **SaasAdmin** (opera a plataforma toda) e **TenantAdmin** (opera dentro de um tenant). Atualmente o app tem apenas o shell de auth e um `Dashboard` placeholder. Este spec cobre a construção do painel exclusivo do SaasAdmin para gerenciar tenants.

O backend (`apps/backend/App/Identity/`) já possui `SaasService` e `SaasEndpoints` com os seis endpoints. Alguns serão modificados; nenhum será removido.

**Dependências entre tasks:**

```
TASK-SAAS-01 (backend)  ──┐
TASK-SAAS-02 (backend)  ──┼──► TASK-SAAS-03 (frontend shell) ──► TASK-SAAS-04 (list)
                           │                                  ──► TASK-SAAS-05 (create)
                           │                                  ──► TASK-SAAS-06 (detail)
```

TASK-SAAS-03, 04, 05, 06 dependem que o backend esteja atualizado. As tasks 04, 05 e 06 dependem de 03 (shell com routing).

---

## TASK-SAAS-01 — Backend: criação atômica de tenant + owner ✅ CONCLUÍDA

### Objetivo

Tornar obrigatório informar o TenantOwner ao criar um tenant. Ambos devem ser persistidos na mesma transação de banco. Um tenant sem owner nunca deve existir, nem por um instante.

### Estado atual

`SaasService.CreateTenantAsync` recebe apenas `string name` e cria somente o `Tenant`.
`CreateTenantRequest` (em `SaasEndpoints.cs`) tem apenas `string Name`.
O endpoint `POST /api/v1/saas/tenants/{id}/users` continua existindo para adicionar usuários depois.

### O que mudar

**`SaasEndpoints.cs`** — substituir `CreateTenantRequest`:

```csharp
// remover:
internal sealed record CreateTenantRequest(string Name);

// adicionar:
internal sealed record CreateTenantRequest(
    string TenantName,
    string OwnerEmail);
```

**`SaasService.cs`** — substituir `CreateTenantAsync`:

```csharp
// novo record de retorno (adicionar ao topo do arquivo):
internal sealed record TenantWithOwnerSummary(
    Guid TenantId,
    string TenantName,
    TenantStatus Status,
    DateTimeOffset CreatedAt,
    Guid OwnerId,
    string OwnerEmail);

// nova assinatura:
internal async Task<Result<TenantWithOwnerSummary>> CreateTenantAsync(
    string tenantName, string ownerEmail, CancellationToken ct = default)
```

Lógica interna:

1. Validar `tenantName` não vazio.
2. Validar `ownerEmail` não vazio e formato válido (usar `MailAddress` ou regex simples).
3. Verificar unicidade do e-mail em `_db.Users`.
4. Criar `Tenant` + `ApplicationUser` (role `TenantAdmin` → `TenantId` preenchido, `MedicoId` nulo).
5. `_db.Tenants.Add(tenant)` + `_db.Users.Add(owner)`.
6. `SaveChangesAsync` — única chamada, única transação.
7. Retornar `TenantWithOwnerSummary`.

**`SaasEndpoints.cs`** — atualizar o handler `CreateTenantAsync`:

```csharp
private static async Task<IResult> CreateTenantAsync(
    CreateTenantRequest body, SaasService saasService, CancellationToken ct)
{
    var result = await saasService.CreateTenantAsync(body.TenantName, body.OwnerEmail, ct);
    if (result.IsFailure)
    {
        var statusCode = result.Error is ConflictError
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;
        return Results.Problem(statusCode: statusCode, detail: result.Error!.Message);
    }
    return Results.Created($"/api/v1/saas/tenants/{result.Value!.TenantId}", result.Value);
}
```

### Testes (TDD — escrever antes do código)

Arquivo: `apps/backend/tests/Faturamento.Tests/Identity/SaasServiceCreateTenantTests.cs`

Casos obrigatórios:

- `CreateTenant_WithValidData_CreatesTenantAndOwnerInSameTransaction`
- `CreateTenant_WithEmptyTenantName_ReturnsValidationError`
- `CreateTenant_WithEmptyOwnerEmail_ReturnsValidationError`
- `CreateTenant_WithDuplicateOwnerEmail_ReturnsConflictError`
- `CreateTenant_OwnerHasTenantAdminRole` (verificar `MedicoId == null` e `TenantId` correto)

Usar `PostgresContainerFixture` (container real — sem mocks de banco).

### Arquivos a ler antes de começar

- `apps/backend/App/Identity/SaasService.cs`
- `apps/backend/App/Identity/Endpoints/SaasEndpoints.cs`
- `apps/backend/App/Identity/ApplicationUser.cs`
- `apps/backend/App/Identity/Tenant.cs`
- `apps/backend/App/Identity/AuthService.cs` (para entender `DeriveRole`)
- `apps/backend/tests/Faturamento.Tests/Fixtures/PostgresContainerFixture.cs`
- `C:\Users\mique\.claude\projects\D--Projects-honorare\memory\feedback_slnx_extension.md` (usar `.slnx`, não `.sln`)
- `C:\Users\mique\.claude\projects\D--Projects-honorare\memory\feedback_test_warnings_as_errors.md`
- `C:\Users\mique\.claude\projects\D--Projects-honorare\memory\feedback_shared_postgres_fixture.md`

### Critérios de aceite

- `dotnet build` sem warnings.
- `dotnet test` verde com os 5 casos acima.
- Não há nenhum endpoint ou método que permita criar tenant sem owner.

---

## TASK-SAAS-02 — Backend: enriquecer listagem de tenants com insights ✅ CONCLUÍDA

### Objetivo

`GET /api/v1/saas/tenants` deve retornar, para cada tenant, o número de admins e de médicos cadastrados. Esses dados alimentam a tabela e os cards do painel.

### Estado atual

`TenantSummary` tem: `Id`, `Name`, `Status`, `CreatedAt`.
`ListTenantsAsync` faz `_db.Tenants.ToListAsync()` e projeta sem nenhuma contagem.

### O que mudar

**`SaasService.cs`** — substituir `TenantSummary`:

```csharp
// remover:
internal sealed record TenantSummary(Guid Id, string Name, TenantStatus Status, DateTimeOffset CreatedAt);

// adicionar:
internal sealed record TenantSummary(
    Guid Id,
    string Name,
    TenantStatus Status,
    DateTimeOffset CreatedAt,
    int TotalAdmins,
    int TotalMedicos);
```

**`ListTenantsAsync`** — substituir por uma única query com `GroupJoin` ou subconsultas:

```csharp
internal async Task<IReadOnlyList<TenantSummary>> ListTenantsAsync(CancellationToken ct = default)
{
    return await _db.Tenants
        .Select(t => new TenantSummary(
            t.Id,
            t.Name,
            t.Status,
            t.CreatedAt,
            _db.Users.Count(u => u.TenantId == t.Id && u.MedicoId == null),
            _db.Users.Count(u => u.TenantId == t.Id && u.MedicoId != null)))
        .ToListAsync(ct);
}
```

> Nota: EF Core traduz subconsultas correlacionadas em SQL eficiente. Não usar `.ToList()` seguido de LINQ-to-objects.

Atualizar `UpdateTenantStatusAsync` e outros métodos que retornam `TenantSummary` para incluir os campos novos (buscar contagens ou passar `0` onde não faz sentido — ex.: retorno do PATCH pode usar contagens reais ou omitir, desde que o tipo compile).

### Testes (TDD)

Arquivo: `apps/backend/tests/Faturamento.Tests/Identity/SaasServiceListTenantsTests.cs`

Casos obrigatórios:

- `ListTenants_WithNoTenants_ReturnsEmptyList`
- `ListTenants_ReturnsTenantWithCorrectAdminCount`
- `ListTenants_ReturnsTenantWithCorrectMedicoCount`
- `ListTenants_CountsAreIndependentPerTenant` (dois tenants com contagens diferentes)

### Arquivos a ler antes de começar

- `apps/backend/App/Identity/SaasService.cs` (pós-TASK-SAAS-01)
- `apps/backend/App/Data/AppDbContext.cs`
- Mesmos arquivos de memória da TASK-SAAS-01

### Critérios de aceite

- `dotnet build` sem warnings.
- `dotnet test` verde.
- A query gerada pelo EF Core não faz N+1 (verificar via logs de SQL em test output).

---

## TASK-SAAS-03 — Frontend: shell do SaaS Admin com routing e guard de role ✅ CONCLUÍDA

### Objetivo

Criar a área `/saas` no `admin-web` com layout próprio (sidebar + conteúdo) e protegida exclusivamente para `SaasAdmin`. Redirecionar automaticamente: SaasAdmin → `/saas`, TenantAdmin → `/` (dashboard existente).

### Estado atual

`app.routes.ts` tem: `auth/login`, `auth/callback`, `''` (Dashboard protegido por `authGuard`).
`AuthService` armazena o access token em memória como signal Angular. O token JWT contém o claim `role`.
`Dashboard` é um placeholder vazio.

### O que criar

**`apps/admin-web/src/app/auth/auth.service.ts`** — verificar se já expõe o role. Se não, adicionar computed signal:

```typescript
readonly role = computed(() => {
  const token = this.accessToken();
  if (!token) return null;
  // decodificar payload JWT (atob do segmento central, sem biblioteca externa)
  const payload = JSON.parse(atob(token.split('.')[1]));
  return payload['role'] as string | null;
});
```

**`apps/admin-web/src/app/saas/saas.routes.ts`** — novo arquivo:

```typescript
export const saasRoutes: Routes = [
  {
    path: "",
    component: SaasShell,
    canActivate: [saasGuard],
    children: [
      { path: "tenants", loadComponent: () => import("./tenants/tenant-list").then((m) => m.TenantList) },
      { path: "tenants/:id", loadComponent: () => import("./tenants/tenant-detail").then((m) => m.TenantDetail) },
      { path: "", redirectTo: "tenants", pathMatch: "full" },
    ],
  },
];
```

**`apps/admin-web/src/app/saas/saas.guard.ts`** — novo arquivo:

```typescript
export const saasGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.role() === "SaasAdmin") return true;
  return router.createUrlTree(["/"]);
};
```

**`apps/admin-web/src/app/saas/saas-shell.ts`** — layout com sidebar simples (links: Tenants) e `<router-outlet>`.

**`apps/admin-web/src/app/app.routes.ts`** — adicionar rota `/saas`:

```typescript
{
  path: 'saas',
  canActivate: [authGuard],
  loadChildren: () => import('./saas/saas.routes').then(m => m.saasRoutes),
},
```

**Redirect pós-login** — em `Callback` (`apps/admin-web/src/app/auth/callback/callback.ts`), após armazenar tokens, redirecionar com base no role:

```typescript
const role = this.authService.role();
this.router.navigate([role === "SaasAdmin" ? "/saas" : "/"]);
```

### Testes (TDD)

Arquivo: `apps/admin-web/src/app/saas/saas.guard.spec.ts`

Casos obrigatórios:

- `saasGuard_WhenRoleIsSaasAdmin_AllowsActivation`
- `saasGuard_WhenRoleIsTenantAdmin_RedirectsToRoot`
- `saasGuard_WhenNotAuthenticated_RedirectsToRoot`

Arquivo: `apps/admin-web/src/app/auth/auth.service.spec.ts` — adicionar caso:

- `role_DerivesCorrectlyFromJwtPayload`

### Arquivos a ler antes de começar

- `apps/admin-web/src/app/auth/auth.service.ts`
- `apps/admin-web/src/app/auth/auth.guard.ts`
- `apps/admin-web/src/app/auth/callback/callback.ts`
- `apps/admin-web/src/app/app.routes.ts`
- `apps/admin-web/src/app/app.config.ts`
- `C:\Users\mique\.claude\projects\D--Projects-honorare\memory\feedback_angular_test_setup_deprecations.md`
- `C:\Users\mique\.claude\projects\D--Projects-honorare\memory\feedback_angular_vite_plugin_removed.md`

### Critérios de aceite

- `pnpm -F admin-web test:ci` verde.
- `pnpm -F admin-web lint` sem warnings.
- Acessar `/saas` como TenantAdmin redireciona para `/`.
- Acessar `/saas` sem token redireciona para `/auth/login`.

---

## TASK-SAAS-04 — Frontend: página de listagem de tenants

### Objetivo

Tela principal do SaaS admin: cards de resumo (totais) + tabela com todos os tenants.

### Estado atual

`GET /api/v1/saas/tenants` (pós-TASK-SAAS-02) retorna `TenantSummary[]` com `id`, `name`, `status`, `createdAt`, `totalAdmins`, `totalMedicos`.

O `api-contracts` (`packages/api-contracts/`) é gerado — verificar se já reflete o novo contrato antes de usar os tipos. Se não, usar tipos locais temporários até regenerar.

### O que criar

**`apps/admin-web/src/app/saas/tenants/tenant-list.ts`**

Estrutura da tela:

1. **Cards de resumo** (linha superior):
   - Total de tenants Ativos
   - Total de tenants Suspensos
   - Total de médicos cadastrados (soma de `totalMedicos` de todos tenants)

2. **Tabela** (colunas):
   - Nome do tenant
   - Status (badge colorido: Ativo = verde, Suspenso = amarelo, Cancelado = vermelho)
   - Admins (número)
   - Médicos (número)
   - Criado em (data formatada `dd/MM/yyyy`)
   - Ações: botão "Ver" (navega para `/saas/tenants/:id`) + botão "Novo Tenant" (abre modal)

3. **Modal de criação** — componente inline na mesma rota (não rota separada). Campos:
   - Nome do tenant (obrigatório)
   - E-mail do owner (obrigatório, formato e-mail)
   - Botões: Cancelar / Criar

**`apps/admin-web/src/app/saas/saas.service.ts`** — serviço Angular que encapsula as chamadas HTTP:

```typescript
@Injectable({ providedIn: 'root' })
export class SaasService {
  private readonly http = inject(HttpClient);

  listTenants(): Observable<TenantSummary[]> { ... }
  createTenant(payload: CreateTenantPayload): Observable<TenantWithOwnerSummary> { ... }
  updateTenantStatus(tenantId: string, status: TenantStatus): Observable<TenantSummary> { ... }
  listTenantUsers(tenantId: string): Observable<UserSummary[]> { ... }
  createUser(tenantId: string, payload: CreateUserPayload): Observable<UserSummary> { ... }
  updateUserStatus(tenantId: string, userId: string, isActive: boolean): Observable<void> { ... }
}
```

Definir os tipos localmente em `apps/admin-web/src/app/saas/saas.types.ts` (não depender do `api-contracts` gerado ainda).

### Testes (TDD)

Arquivo: `apps/admin-web/src/app/saas/saas.service.spec.ts`

Casos obrigatórios:

- `listTenants_CallsCorrectEndpoint`
- `createTenant_PostsPayloadAndReturnsCreated`
- `updateTenantStatus_PatchesCorrectEndpoint`

Arquivo: `apps/admin-web/src/app/saas/tenants/tenant-list.spec.ts`

Casos obrigatórios:

- `TenantList_RendersCardWithTotalAtivos`
- `TenantList_RendersTenantRowsFromService`
- `TenantList_OpenModalOnClickNovo`
- `TenantList_SubmitFormCallsCreateTenantAndRefreshesTable`
- `TenantList_ShowsValidationErrorWhenEmailInvalid`

### Arquivos a ler antes de começar

- `apps/admin-web/src/app/saas/saas.routes.ts` (criado em TASK-SAAS-03)
- `apps/admin-web/src/app/auth/auth.interceptor.ts` (para entender como o Bearer é injetado)
- Memórias de Angular (mesmas da TASK-SAAS-03)

### Critérios de aceite

- `pnpm -F admin-web test:ci` verde.
- `pnpm -F admin-web lint` sem warnings.
- A tabela renderiza corretamente com dados mockados nos testes.
- O modal fecha e a tabela é recarregada após criação bem-sucedida.

---

## TASK-SAAS-05 — Frontend: página de detalhe do tenant + gerenciamento de usuários

### Objetivo

Tela acessível via `/saas/tenants/:id`. Mostra os dados do tenant, permite alterar o status e gerencia os usuários daquele tenant.

### Estado atual

`GET /api/v1/saas/tenants/:id/users` retorna `UserSummary[]` com `id`, `email`, `role`, `isActive`, `createdAt`, `medicoId`.
`PATCH /api/v1/saas/tenants/:id/status` altera o status.
`POST /api/v1/saas/tenants/:id/users` cria usuário (TenantAdmin ou Medico).
`PATCH /api/v1/saas/tenants/:id/users/:userId/status` ativa/desativa.

`SaasService` Angular (criado em TASK-SAAS-04) já tem todos os métodos necessários.

### O que criar

**`apps/admin-web/src/app/saas/tenants/tenant-detail.ts`**

Estrutura da tela:

1. **Header do tenant**:
   - Nome do tenant
   - Badge de status (mesmo padrão da listagem)
   - Botão de ação condicional: se `Ativo` → "Suspender"; se `Suspenso` → "Reativar" / "Cancelar"; se `Cancelado` → sem ação
   - Confirmação antes de executar qualquer mudança de status (dialog nativo `confirm()` — sem lib de modal)

2. **Tabela de usuários** (colunas):
   - E-mail
   - Role (badge: Admin = azul, Médico = roxo)
   - Status (Ativo / Inativo com toggle)
   - Criado em

3. **Botão "Adicionar usuário"** — abre modal inline com campos:
   - E-mail (obrigatório)
   - Role: seleção entre `TenantAdmin` e `Medico`
   - Se `Medico`: campo `MedicoId` (UUID, obrigatório)

4. **Navegação**: breadcrumb simples "Tenants / [nome do tenant]" com link de volta.

### Lógica de status do tenant

```
Ativo    → pode ir para Suspenso ou Cancelado
Suspenso → pode ir para Ativo ou Cancelado
Cancelado → estado final, sem transições
```

Desabilitar botões que violem essa máquina de estados.

### Testes (TDD)

Arquivo: `apps/admin-web/src/app/saas/tenants/tenant-detail.spec.ts`

Casos obrigatórios:

- `TenantDetail_RendersNameAndStatus`
- `TenantDetail_ShowsSuspenderButtonWhenAtivo`
- `TenantDetail_HidesAcoesWhenCancelado`
- `TenantDetail_ToggleUserStatus_CallsUpdateUserStatus`
- `TenantDetail_AddUser_SubmitCallsCreateUser`
- `TenantDetail_AddUser_MedicoIdFieldAppearsOnlyWhenRoleIsMedico`

### Arquivos a ler antes de começar

- `apps/admin-web/src/app/saas/saas.service.ts` (criado em TASK-SAAS-04)
- `apps/admin-web/src/app/saas/saas.types.ts` (criado em TASK-SAAS-04)
- `apps/admin-web/src/app/saas/saas.routes.ts` (criado em TASK-SAAS-03)
- Memórias de Angular (mesmas anteriores)

### Critérios de aceite

- `pnpm -F admin-web test:ci` verde.
- `pnpm -F admin-web lint` sem warnings.
- Botões de status respeitam a máquina de estados (testado via spec).
- Toggle de usuário chama o endpoint correto (testado via spec).

---

## Convenções transversais

### Backend

- Sempre usar `TreatWarningsAsErrors` — nenhum `#pragma warning disable` sem comentário explicando o motivo.
- Testes de integração usam `PostgresContainerFixture` — nunca mockar o banco.
- Não assumir banco vazio em testes de lista — criar os dados que o teste precisa.
- Usar `.slnx`, não `.sln` (ex: `dotnet test apps/backend/Honorare.slnx`).

### Frontend

- Componentes usam `inline template` (não arquivos `.html` separados) — obrigatório para o plugin Vite do Angular.
- `BrowserTestingModule` (não `BrowserDynamicTestingModule` legado) nos testes.
- `provideAppInitializer` (não `APP_INITIALIZER` legado).
- `window.location` deve ser mockado nos testes que disparam navegação OAuth.
- Coverage mínimo: 80% em todos os arquivos novos.
- SCSS segue BEM, sem `color-named`, sem `!important`.

### Ordem de execução recomendada

```
TASK-SAAS-01 → TASK-SAAS-02 → TASK-SAAS-03 → TASK-SAAS-04 → TASK-SAAS-05
```

As duas primeiras podem rodar em paralelo se houver duas sessões disponíveis.

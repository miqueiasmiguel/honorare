# SPEC F2.4 — Acesso do Prestador (criação de usuário via formulário)

**Pré-requisito:** F2.3 concluído (entidade `Prestador` e CRUD existentes).
**Pós-condição:** Ao cadastrar um prestador com e-mail, um `ApplicationUser` com role `Medico` é criado automaticamente. O médico poderá então fazer login com Google usando aquele e-mail. O TenantAdmin gerencia tudo dentro do próprio formulário de prestador — sem depender do SaaS admin.

---

## Decisão de design

### Onde fica o e-mail?

O campo `EmailAcesso` é armazenado na própria entidade `Prestador`. Isso permite:

- exibir o e-mail no formulário de prestador sem query cross-context;
- indicar visualmente se o prestador já tem acesso ao portal (badge na lista);
- manter imutabilidade controlada pelo próprio aggregate.

O `ApplicationUser.Email` espelha esse valor. A ligação é feita via `ApplicationUser.MedicoId = Prestador.Id`.

### Imutabilidade do e-mail

O e-mail só pode ser definido na criação. Após salvo, não pode ser alterado — nem via UI, nem via API. Isso evita que o TenantAdmin redirecione o acesso de um médico sem o conhecimento do mesmo.

Consequência no contrato de API: `POST /api/v1/admin/prestadores` aceita `emailAcesso?`; `PUT /api/v1/admin/prestadores/{id}` **não** aceita `emailAcesso`.

### Atomicidade

A criação do `Prestador` e do `ApplicationUser` ocorre em uma única transação (`SaveChangesAsync`). Se a criação do usuário falhar (e.g. e-mail duplicado), o prestador **não** é persistido.

### Exclusão

Se o prestador possui um `ApplicationUser` associado, a exclusão também remove o usuário — desde que não haja guias associadas (a guarda de guias já existe). A remoção do usuário é hard-delete pois ele nunca chegou a fazer login (Google nunca associou `GoogleId`).

Se o prestador tiver `GoogleId` associado (ou seja, o médico já fez login ao menos uma vez), a exclusão do prestador **bloqueia** com 409. Isso preserva auditabilidade.

### Desativação

Quando o prestador é desativado (`Ativo = false` via PUT), o `ApplicationUser` vinculado também é desativado (`IsActive = false`) na mesma transação. Quando reativado, o usuário também é reativado.

---

## Modelo de dados

```
Prestador (existente) +=
  EmailAcesso   string?   nullable, imutável após definido
  SetEmailAcesso(string email)  → só executa se EmailAcesso == null

ApplicationUser (existente — sem mudança de schema)
  MedicoId   Guid?   já existe; aponta para Prestador.Id
```

Nenhuma migration é necessária se `EmailAcesso` for adicionada como coluna nullable com default `NULL` na tabela `prestadores`.

**Migration:** `AddEmailAcessoPrestador` — adiciona `email_acesso varchar(256) NULL` na tabela `prestadores`.

---

## Endpoints

Mudanças nos endpoints existentes de `/api/v1/admin/prestadores`:

```
POST  /api/v1/admin/prestadores
      Body: { nome, registroProfissional?, emailAcesso? }
      → Se emailAcesso presente:
          - Valida formato de e-mail
          - Valida que não está em uso em ApplicationUsers (409 se duplicado)
          - Cria Prestador + ApplicationUser em uma transação
      → Resposta inclui emailAcesso e temUsuario: true

GET   /api/v1/admin/prestadores
      Resposta (por item): += emailAcesso?, temUsuario: bool

GET   /api/v1/admin/prestadores/{id}
      Resposta: += emailAcesso?, temUsuario: bool

PUT   /api/v1/admin/prestadores/{id}
      Body: { nome, registroProfissional?, ativo }
      → emailAcesso ausente do body (ignorado mesmo se enviado)
      → Se ativo muda de true→false: também desativa ApplicationUser vinculado
      → Se ativo muda de false→true: também reativa ApplicationUser vinculado

DELETE /api/v1/admin/prestadores/{id}
      → Guarda existente: 409 se possui guias
      → Nova guarda: 409 se ApplicationUser.GoogleId != null
        (médico já fez login — preservar auditabilidade)
      → Caso contrário: remove ApplicationUser (se existir) + Prestador na mesma transação
```

---

## Arquivos-chave

```
App/Catalog/
  Prestador.cs                          ← += EmailAcesso, SetEmailAcesso()
  CatalogService.cs                     ← CriarPrestadorAsync recebe emailAcesso?
                                           AtualizarPrestadorAsync propaga ativo→user
                                           ExcluirPrestadorAsync: nova guarda + remove user
  Migrations/AddEmailAcessoPrestador/   ← nova migration + .editorconfig

tests/Catalog.Tests/
  Prestador/PrestadorCrudTests.cs       ← estender com novos casos

apps/admin-web/src/app/admin/catalog/prestadores/
  prestador-form/
    prestador-form.component.ts         ← += campo emailAcesso (só em criação)
    prestador-form.component.html       ← += input email + label condicional
    prestador-form.component.spec.ts    ← += novos testes
  prestador-list/
    prestador-list.component.ts         ← += badge "Com acesso" / "Sem acesso"
    prestador-list.component.html       ← += coluna/badge
    prestador-list.component.spec.ts    ← += novos testes
```

---

## TASK-UP-01 — Backend: EmailAcesso em Prestador + criação de usuário [x] concluída

**TDD: testes → entidade → service → migration → build.**

### O que fazer

#### 1. `Prestador.cs`

Adicionar campo e método:

```csharp
public string? EmailAcesso { get; private set; }

internal Result SetEmailAcesso(string email)
{
    if (EmailAcesso is not null)
    {
        return Result.Fail(new ConflictError("E-mail de acesso já definido para este prestador."));
    }
    EmailAcesso = email.Trim().ToLowerInvariant();
    return Result.Ok();
}
```

#### 2. `PrestadorConfiguration.cs`

Adicionar:

```csharp
builder.Property(p => p.EmailAcesso).HasMaxLength(256);
```

#### 3. Migration `AddEmailAcessoPrestador`

Coluna `email_acesso varchar(256) NULL` na tabela `prestadores`. Incluir `.editorconfig` na pasta de migrations.

#### 4. `CatalogService.cs`

Ajustar tipos de comando (split de `SalvarPrestadorCommand`):

```csharp
internal sealed record CriarPrestadorCommand(
    string Nome, string? RegistroProfissional, string? EmailAcesso);

internal sealed record AtualizarPrestadorCommand(
    string Nome, string? RegistroProfissional, bool Ativo);
```

Ajustar `PrestadorDto`:

```csharp
internal sealed record PrestadorDto(
    Guid Id, string Nome, string? RegistroProfissional,
    bool Ativo, DateTimeOffset CriadoEm,
    string? EmailAcesso, bool TemUsuario);
```

Ajustar `CriarPrestadorAsync`:

```csharp
internal async Task<Result<PrestadorDto>> CriarPrestadorAsync(
    CriarPrestadorCommand cmd, CancellationToken ct = default)
{
    // 1. Validar nome (já existe)
    // 2. Se emailAcesso presente:
    //    a. Validar formato
    //    b. Checar duplicidade em _db.Users
    // 3. Criar Prestador
    // 4. Se emailAcesso presente: chamar prestador.SetEmailAcesso(email),
    //    criar ApplicationUser.Create(email, tenantId, medicoId: prestador.Id)
    //    e adicionar ao _db.Users
    // 5. SaveChangesAsync (atômico)
}
```

Ajustar `AtualizarPrestadorAsync` para aceitar `AtualizarPrestadorCommand` e propagar ativo→user:

```csharp
// Após prestador.Atualizar(...):
var user = await _db.Users.FirstOrDefaultAsync(
    u => u.MedicoId == prestador.Id, ct);
if (user is not null)
{
    if (cmd.Ativo) user.Activate(); else user.Deactivate();
}
await _db.SaveChangesAsync(ct);
```

Ajustar `ExcluirPrestadorAsync` com nova guarda:

```csharp
var user = await _db.Users.FirstOrDefaultAsync(u => u.MedicoId == id, ct);
if (user?.GoogleId is not null)
{
    return Result.Fail(new ConflictError(
        "Prestador possui usuário que já acessou o sistema. Desative-o em vez de excluir."));
}
// Remove user (se existir) + prestador — SaveChangesAsync atômico
if (user is not null) _db.Users.Remove(user);
_db.Prestadores.Remove(prestador);
await _db.SaveChangesAsync(ct);
```

Ajustar `ToDto` de `Prestador`:

```csharp
private static PrestadorDto ToDto(Prestador p, bool temUsuario) =>
    new(p.Id, p.Nome, p.RegistroProfissional, p.Ativo, p.CriadoEm, p.EmailAcesso, temUsuario);
```

Para o `ListarPrestadoresAsync`, buscar os `MedicoId`s que possuem usuário em uma única query e fazer match:

```csharp
var ids = itens.Select(p => (Guid?)p.Id).ToHashSet();
var comUsuario = await _db.Users
    .Where(u => u.MedicoId != null && ids.Contains(u.MedicoId))
    .Select(u => u.MedicoId!.Value)
    .ToHashSetAsync(ct);
// projetar com temUsuario = comUsuario.Contains(p.Id)
```

#### 5. `CatalogEndpoints.cs`

- Trocar `SalvarPrestadorCommand` → `CriarPrestadorCommand` no `POST`
- Trocar `SalvarPrestadorCommand` → `AtualizarPrestadorCommand` no `PUT`

### Testes (`PrestadorCrudTests.cs`) — estender com:

```
[Fact] CriarPrestador_SemEmail_NaoCriaUsuario
  Criar prestador sem emailAcesso → Users sem registro com MedicoId do prestador

[Fact] CriarPrestador_ComEmail_CriaUsuario
  Criar prestador com emailAcesso válido → ApplicationUser criado com MedicoId correto, role derivada = Medico

[Fact] CriarPrestador_EmailDuplicado_Retorna409
  Criar dois prestadores com mesmo email → segundo retorna ConflictError

[Fact] CriarPrestador_EmailInvalido_Retorna400
  emailAcesso = "nao-e-email" → ValidationError

[Fact] AtualizarPrestador_NaoAlteraEmail
  Criar prestador com email, atualizar nome → EmailAcesso inalterado

[Fact] AtualizarPrestador_DesativarPrestador_DesativaUsuario
  Criar prestador com email (user IsActive = true), atualizar Ativo = false → user.IsActive = false

[Fact] AtualizarPrestador_ReativarPrestador_ReativaUsuario
  Prestador inativo com user inativo → Ativo = true → user.IsActive = true

[Fact] ExcluirPrestador_SemUsuario_Remove
  Prestador sem email → excluir → removido

[Fact] ExcluirPrestador_ComUsuario_SemGoogleId_RemoveAmbos
  Prestador com email, user sem GoogleId → excluir → Prestador e User removidos

[Fact] ExcluirPrestador_ComUsuario_ComGoogleId_Retorna409
  Prestador com email, user com GoogleId → excluir → ConflictError

[Fact] ListarPrestadores_TemUsuario_Correto
  Criar dois prestadores (um com email, outro sem) → TemUsuario reflete corretamente
```

**Critério de pronto:** `dotnet test` passa; `dotnet build` limpo; migration aplicada.

---

## TASK-UP-02 — Frontend: campo e-mail no formulário de prestador

**TDD: testes Vitest → componentes → build.**

### O que fazer

#### 1. `catalog.types.ts`

Atualizar `PrestadorDto` e separar tipos de payload:

```ts
export interface PrestadorDto {
  id: string;
  nome: string;
  registroProfissional: string | null;
  ativo: boolean;
  criadoEm: string;
  emailAcesso: string | null; // novo
  temUsuario: boolean; // novo
}

export interface CriarPrestadorPayload {
  nome: string;
  registroProfissional: string | null;
  emailAcesso: string | null; // novo — só em criação
}

export interface AtualizarPrestadorPayload {
  nome: string;
  registroProfissional: string | null;
  ativo: boolean;
  // emailAcesso ausente intencionalmente
}
```

#### 2. `CatalogService` Angular

- `criarPrestador(payload: CriarPrestadorPayload)` — tipo do payload atualizado
- `atualizarPrestador(id: string, payload: AtualizarPrestadorPayload)` — tipo separado
- Nenhuma mudança de URL.

#### 3. `PrestadorFormComponent`

**Criação:**

- Adicionar campo `emailAcesso` ao `FormGroup` (nonNullable `''`, validator `email` do Angular se preenchido)
- Campo visível e editável somente quando `!modoEdicao`
- Payload para `criarPrestador`: `emailAcesso: raw.emailAcesso || null`

**Edição:**

- Campo `emailAcesso` exibido como texto somente-leitura (não faz parte do FormGroup)
- Rótulo: "E-mail de acesso" + valor ou "Sem acesso ao portal" (cinza)
- Se `temUsuario`, exibir badge "Com acesso ao portal" (verde)

#### 4. `PrestadorListComponent`

- Adicionar coluna/badge "Acesso" na tabela
- `temUsuario = true` → badge verde "Com acesso"
- `temUsuario = false` → sem badge (ou texto cinza "—")

#### 5. Regenerar cliente OpenAPI

```bash
pnpm generate-api-client
```

### Testes

`prestador-form.component.spec.ts` — estender com:

```
[it] modo criação exibe campo emailAcesso editável
[it] modo criação envia emailAcesso preenchido no payload
[it] modo criação envia null quando emailAcesso vazio
[it] modo edição não exibe campo emailAcesso editável
[it] modo edição exibe emailAcesso como texto somente-leitura
[it] modo edição exibe badge "Com acesso" quando temUsuario = true
[it] modo edição exibe "Sem acesso ao portal" quando temUsuario = false
```

`prestador-list.component.spec.ts` — estender com:

```
[it] exibe badge "Com acesso" para prestador com temUsuario = true
[it] não exibe badge para prestador com temUsuario = false
```

**Critério de pronto:** `pnpm -F admin-web test:ci` passa; `pnpm -F admin-web lint` sem warnings; fluxo criação → login do médico funciona end-to-end.

---

## Resumo de entregáveis por task

| Task  | Backend | Frontend | Migration               | Testes                                    |
| ----- | ------- | -------- | ----------------------- | ----------------------------------------- |
| UP-01 | ✓       | —        | AddEmailAcessoPrestador | PrestadorCrudTests (11 novos casos)       |
| UP-02 | —       | ✓        | —                       | prestador-form.spec + prestador-list.spec |

**Após UP-02:** atualizar `PROXIMOS_PASSOS.md` marcando F2.4 como ✅.

---
name: spec-writer
description: Cria um SPEC.md de implementação dividido em tasks independentes, cada uma executável em uma sessão isolada via /tdd-task (implementa → limpa contexto → próxima task). Use SEMPRE que o usuário quiser planejar uma feature, quebrar um trabalho grande em etapas, "escrever um spec", "criar um plano de implementação", ou organizar uma feature em tasks sequenciais — mesmo que ele não diga a palavra "spec" explicitamente. Também use quando o trabalho for grande demais para uma sessão só e precisar ser fatiado.
---

# Spec Writer

Produz um **SPEC.md** que outra sessão (sem o seu contexto atual) consegue implementar task por task, gastando o mínimo de tokens. Cada task é executada via `/tdd-task <spec> <task-id>`, depois o contexto é limpo, e a próxima task roda do zero.

O leitor de cada task é uma IA **fria**: não viu esta conversa, não explorou o código, não sabe quais arquivos importam. Cada token que ela gasta procurando arquivo, relendo algo que já estava no contexto, ou explorando um padrão que você poderia ter colado, é desperdício. **O trabalho desta skill é eliminar esse desperdício antecipadamente** — você explora o código UMA vez agora (enquanto escreve o spec) para que N sessões futuras não precisem explorar nada.

## Princípio central: o spec carrega o contexto, não a sessão

Numa sessão de implementação, o orçamento de tokens vai embora em três coisas: (1) ler arquivos pra entender o padrão, (2) procurar onde algo está, (3) reler coisas que já estavam no contexto. Um bom spec ataca os três:

| Desperdício na sessão fria                        | Como o spec elimina                                                 |
| ------------------------------------------------- | ------------------------------------------------------------------- |
| "deixa eu procurar onde isso é feito"             | A task lista `arquivo:linhas` exatos, já localizados por você agora |
| "deixa eu ler o serviço inteiro pra ver o padrão" | A task **cola** o trecho-padrão (5–15 linhas) inline                |
| Reler CLAUDE.md / MEMORY.md / specs anteriores    | A task declara explicitamente "já está no contexto, não releia"     |
| Explorar pra descobrir o que é "pronto"           | A task tem critérios de aceite como checklist objetivo              |
| Carregar tasks vizinhas pra entender dependências | Cada task é **autossuficiente**: tudo que precisa está nela         |

A regra de ouro: **se a IA fria precisar abrir um arquivo que você não listou, ou procurar algo que você poderia ter colado, o spec falhou.** Prefira colar um trecho de 10 linhas a mandar ler um arquivo de 300.

## Fluxo para escrever o spec

### 1. Entender a feature

Confirme o objetivo, o escopo e o critério de "pronto" geral. Se algo for ambíguo e mudar a decomposição, pergunte agora — não no meio das tasks.

### 2. Explorar o código UMA vez (agora)

Esse é o investimento que paga as N sessões futuras. Localize:

- Os arquivos que serão criados/modificados.
- Os **padrões existentes** que as tasks devem imitar (um serviço parecido, um componente parecido, um teste parecido). Anote `arquivo:linhas` e copie os trechos curtos que vai colar nas tasks.
- As armadilhas conhecidas (cheque o MEMORY.md já carregado — ex.: NG0701, `[selected]` vs `[value]`, escala `space()`, namespace de teste que sombreia classe).

Use o agente `Explore` se a varredura for ampla; você só precisa das conclusões (`arquivo:linhas` + trechos), não dos arquivos inteiros.

### 3. Decompor em tasks

Cada task deve caber confortavelmente numa sessão e respeitar:

- **Uma unidade coesa de trabalho** — tipicamente uma camada vertical fina (ex.: entidade+migration; depois serviço; depois endpoint; depois UI). Granularidade alvo: o que um humano commitaria de uma vez.
- **Ordem por dependência** — segue a direção do projeto (`Reporting → Faturamento → Catalog → Identity` no backend; backend antes de regenerar o client TS antes do frontend).
- **Independência de leitura** — a task N não pode exigir que você "lembre" da task N-1. Se a task N precisa de um tipo criado na N-1, **cole a assinatura** desse tipo na task N. A sessão fria não terá o contexto da N-1.
- **TDD** — toda task começa pelo teste (red), depois código mínimo (green). Espelha o `/tdd-task`.

### 4. Escrever o SPEC.md

Use o template abaixo. Salve em `docs/specs/<feature>.md` (crie a pasta se não existir) ou onde o usuário pedir.

## Template do SPEC.md

````markdown
# SPEC: <Nome da feature>

> **Como executar:** uma task por sessão. Rode `/tdd-task docs/specs/<feature>.md <TASK-ID>`,
> deixe a sessão implementar+commitar, limpe o contexto (`/clear`), rode a próxima.
> CLAUDE.md e MEMORY.md já entram no contexto de cada sessão — as tasks NÃO os repetem.
> **O checkbox `[ ]`/`[x]` é a única memória entre sessões:** uma sessão fria se orienta
> só por ele. Comece pela primeira task ainda `[ ]`; nunca refaça uma já marcada `[x]`.

## Objetivo

<2–4 linhas: o que a feature faz e por que. O "pronto" geral.>

## Contexto compartilhado (válido para todas as tasks)

<Só o que TODA task precisa e que não está no CLAUDE.md. Ex.: nome do bounded context,
nome da nova tabela, convenção de rota. Mantenha curtíssimo — duplicado em toda sessão.>

## Tasks

### TASK-<FEAT>-01 — <título imperativo curto>

- [ ] pendente

**Objetivo:** <uma frase>

**Já no contexto (NÃO reler):** CLAUDE.md, MEMORY.md.

**Ler (só isto):**

- `caminho/Arquivo.cs:120-160` — padrão de serviço a imitar
- `caminho/OutroTeste.cs:1-40` — formato de teste

**Criar/Editar:**

- `caminho/NovoArquivo.cs` (novo)
- `caminho/Existente.cs` (editar: <o quê>)

**Padrão a seguir (colado para evitar leitura):**

```csharp
// trecho real de caminho/Arquivo.cs:120-135 — replique esta forma
public async Task<X> FazerAlgoAsync(Guid id) { ... }
```

**Testes (red primeiro):**

- `Deve <comportamento esperado> quando <cenário>`
- `Deve rejeitar quando <condição inválida>`

**Aceite (checklist objetivo):**

- [ ] `dotnet build apps/backend/Honorare.slnx` sem warnings
- [ ] novos testes passam; `dotnet test` verde
- [ ] <invariante específico da task>

**Commit:** `feat(<scope>): <descrição> (TASK-<FEAT>-01)`

---

### TASK-<FEAT>-02 — <título>

- [ ] pendente

**Depende de:** TASK-<FEAT>-01 (tipo `X` já existe — assinatura abaixo, não precisa abrir).

```csharp
public sealed record X(Guid Id, string Nome);
```

<...mesma estrutura...>

---

## Checklist final

- [ ] TASK-<FEAT>-01
- [ ] TASK-<FEAT>-02
````

## Regras para tasks econômicas em tokens

1. **Localize, não mande procurar.** Sempre `arquivo:linhas`. Nunca "veja como os outros serviços fazem".
2. **Cole o padrão, não mande lê-lo.** Um trecho de 5–15 linhas inline custa menos que abrir o arquivo e ler 300. Para arquivos grandes, colar é quase sempre mais barato.
3. **Liste o mínimo de arquivos.** Cada arquivo em "Ler" é um custo. Se não é estritamente necessário para esta task, corte.
4. **Declare o que NÃO reler.** CLAUDE.md e MEMORY.md já estão no contexto. Specs anteriores e tasks vizinhas idem. Diga isso explicitamente — a sessão fria não sabe.
5. **Carregue dependências por valor, não por referência.** Se a task precisa de algo criado numa task anterior, cole a assinatura/contrato na própria task. Nunca "use o tipo da TASK-01".
6. **Critérios de aceite como checklist verificável** — comandos exatos (`dotnet build ...`, `pnpm -F admin-web test:ci`) e invariantes objetivos. Isso encerra a task sem exploração extra "só pra ter certeza".
7. **Antecipe as armadilhas conhecidas** do MEMORY.md na task onde elas vão morder, inline. Ex.: numa task de `<select>` Angular, lembre `[selected]` não `[value]`; numa task de migration, lembre o `.editorconfig` suprimindo IDE0005/IDE0161/CA1515/CA1861.
8. **Uma task = um commit.** O formato do commit é parte da task.

## Pareamento com /tdd-task

Este spec é o input do comando `/tdd-task <spec> <task-id>`, que já existe no projeto. Esse comando: lê só a seção da task, faz red→green, commita com staged explícito por arquivo, e marca `[ ]`→`[x]`. Escreva as tasks assumindo exatamente esse executor — é por isso que cada task tem testes, aceite e formato de commit próprios.

## Antes de entregar

- Releia o spec com olhos de IA fria: para cada task, pergunte "consigo fazer isto sem abrir nenhum arquivo além dos listados?". Se não, adicione o `arquivo:linhas` ou cole o trecho que falta.
- Confira que a ordem das tasks respeita as dependências e a direção do projeto.
- Confirme que nenhuma task depende de "lembrar" de outra — só de contratos colados.

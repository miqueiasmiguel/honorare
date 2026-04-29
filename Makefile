.DEFAULT_GOAL := help

COMPOSE      := docker compose -f infra/docker-compose.yml
BACKEND_SLN  := apps/backend/Honorare.slnx
BACKEND_APP  := apps/backend/App
PG_CONTAINER := honorare-postgres-1
PG_USER      := honorare
PG_DB        := honorare

.PHONY: help
help: ## Mostra esta mensagem de ajuda
	@powershell -NoProfile -ExecutionPolicy Bypass -File tools/make-help.ps1

# ══════════════════════════════════════════════════════════════════════════════
# Setup
# ══════════════════════════════════════════════════════════════════════════════

.PHONY: install
install: ## Instala dependências JS/TS e ativa hooks Husky
	pnpm install

# ══════════════════════════════════════════════════════════════════════════════
# Infra (Docker Compose)
# ══════════════════════════════════════════════════════════════════════════════

.PHONY: up
up: ## Sobe toda a stack Docker (backend + frontend + observabilidade)
	$(COMPOSE) up --build

.PHONY: up-bg
up-bg: ## Sobe a stack Docker em background
	$(COMPOSE) up --build -d

.PHONY: down
down: ## Para e remove os containers
	$(COMPOSE) down

.PHONY: down-v
down-v: ## Para containers e apaga volumes (banco + métricas + logs)
	$(COMPOSE) down -v

.PHONY: infra-up
infra-up: ## Sobe apenas serviços de infra: postgres + observabilidade (sem app)
	$(COMPOSE) up -d postgres otel-collector jaeger prometheus loki grafana

.PHONY: infra-down
infra-down: ## Para apenas os serviços de infra
	$(COMPOSE) stop postgres otel-collector jaeger prometheus loki grafana

.PHONY: logs
logs: ## Exibe logs de todos os containers (tail -f)
	$(COMPOSE) logs -f

.PHONY: logs-backend
logs-backend: ## Exibe logs somente do backend
	$(COMPOSE) logs -f backend

.PHONY: ps
ps: ## Lista status dos containers
	$(COMPOSE) ps

# ══════════════════════════════════════════════════════════════════════════════
# Banco de dados
# ══════════════════════════════════════════════════════════════════════════════

.PHONY: db-reset
db-reset: ## Para containers, apaga volumes do Postgres e reinicia a infra
	@echo Resetando banco de dados...
	$(COMPOSE) down -v --remove-orphans
	$(COMPOSE) up -d postgres
	@echo Aguardando Postgres ficar pronto...
	@powershell -NoProfile -Command "do { Start-Sleep 1 } until ((docker exec $(PG_CONTAINER) pg_isready -U $(PG_USER)) -match 'accepting')"
	@echo Postgres pronto.

.PHONY: db-shell
db-shell: ## Abre psql no container do Postgres
	docker exec -it $(PG_CONTAINER) psql -U $(PG_USER) -d $(PG_DB)

.PHONY: db-migrate
db-migrate: ## Aplica migrations EF Core pendentes (requer infra-up)
	dotnet ef database update \
		--project $(BACKEND_APP) \
		-- --environment Development

.PHONY: db-migration
db-migration: ## Cria nova migration. Uso: make db-migration NAME=NomeDaMigration
ifndef NAME
	$(error Informe o nome da migration: make db-migration NAME=NomeDaMigration)
endif
	dotnet ef migrations add $(NAME) \
		--project $(BACKEND_APP) \
		-- --environment Development

.PHONY: db-migration-remove
db-migration-remove: ## Remove a última migration (somente se não aplicada)
	dotnet ef migrations remove \
		--project $(BACKEND_APP) \
		-- --environment Development

# ══════════════════════════════════════════════════════════════════════════════
# Backend (.NET)
# ══════════════════════════════════════════════════════════════════════════════

.PHONY: build
build: ## Compila o backend (.NET)
	dotnet build $(BACKEND_SLN)

.PHONY: run
run: ## Executa o backend localmente (sem Docker)
	dotnet run --project $(BACKEND_APP)

.PHONY: test
test: ## Roda todos os testes xUnit com coverage
	dotnet test $(BACKEND_SLN)

.PHONY: test-watch
test-watch: ## Roda testes em modo watch
	dotnet watch test --project $(BACKEND_SLN)

.PHONY: test-backend-unit
test-backend-unit: ## Roda apenas testes unitários (sem Testcontainers)
	dotnet test $(BACKEND_SLN) --filter "Category!=Integration"

# ══════════════════════════════════════════════════════════════════════════════
# Frontend — admin-web
# ══════════════════════════════════════════════════════════════════════════════

.PHONY: dev-admin
dev-admin: ## Dev server admin-web (http://localhost:4200/admin/)
	pnpm -F admin-web start

.PHONY: build-admin
build-admin: ## Build de produção admin-web
	pnpm -F admin-web build

.PHONY: test-admin
test-admin: ## Testes Vitest admin-web (watch)
	pnpm -F admin-web test

.PHONY: test-admin-ci
test-admin-ci: ## Testes Vitest admin-web single-run + coverage
	pnpm -F admin-web test:ci

.PHONY: lint-admin
lint-admin: ## ESLint + StyleLint + Prettier check admin-web
	pnpm -F admin-web lint
	pnpm -F admin-web stylelint
	pnpm -F admin-web prettier:check

.PHONY: lint-fix-admin
lint-fix-admin: ## Autofix lint + formato admin-web
	pnpm -F admin-web lint:fix
	pnpm -F admin-web stylelint:fix
	pnpm -F admin-web prettier:fix

# ══════════════════════════════════════════════════════════════════════════════
# Frontend — medico-pwa
# ══════════════════════════════════════════════════════════════════════════════

.PHONY: dev-pwa
dev-pwa: ## Dev server medico-pwa (http://localhost:4201/app/)
	pnpm -F medico-pwa start

.PHONY: build-pwa
build-pwa: ## Build de produção medico-pwa
	pnpm -F medico-pwa build

.PHONY: test-pwa
test-pwa: ## Testes Vitest medico-pwa (watch)
	pnpm -F medico-pwa test

.PHONY: test-pwa-ci
test-pwa-ci: ## Testes Vitest medico-pwa single-run + coverage
	pnpm -F medico-pwa test:ci

.PHONY: lint-pwa
lint-pwa: ## ESLint + StyleLint + Prettier check medico-pwa
	pnpm -F medico-pwa lint
	pnpm -F medico-pwa stylelint
	pnpm -F medico-pwa prettier:check

.PHONY: lint-fix-pwa
lint-fix-pwa: ## Autofix lint + formato medico-pwa
	pnpm -F medico-pwa lint:fix
	pnpm -F medico-pwa stylelint:fix
	pnpm -F medico-pwa prettier:fix

# ══════════════════════════════════════════════════════════════════════════════
# Atalhos combinados
# ══════════════════════════════════════════════════════════════════════════════

.PHONY: test-all
test-all: ## Roda todos os testes (backend + ambos frontends, modo CI)
	dotnet test $(BACKEND_SLN)
	pnpm -F admin-web test:ci
	pnpm -F medico-pwa test:ci

.PHONY: lint-all
lint-all: ## Lint em todos os workspaces
	pnpm -F admin-web lint
	pnpm -F admin-web stylelint
	pnpm -F admin-web prettier:check
	pnpm -F medico-pwa lint
	pnpm -F medico-pwa stylelint
	pnpm -F medico-pwa prettier:check

.PHONY: lint-fix-all
lint-fix-all: ## Autofix lint em todos os workspaces
	pnpm -F admin-web lint:fix
	pnpm -F admin-web stylelint:fix
	pnpm -F admin-web prettier:fix
	pnpm -F medico-pwa lint:fix
	pnpm -F medico-pwa stylelint:fix
	pnpm -F medico-pwa prettier:fix

.PHONY: generate-api-client
generate-api-client: ## Regenera o cliente TypeScript a partir do OpenAPI spec
	pnpm generate-api-client

# ══════════════════════════════════════════════════════════════════════════════
# Observabilidade
# ══════════════════════════════════════════════════════════════════════════════

.PHONY: grafana
grafana: ## Abre Grafana no navegador padrão
	start http://localhost:3000

.PHONY: jaeger
jaeger: ## Abre Jaeger UI no navegador padrão
	start http://localhost:16686

.PHONY: prometheus
prometheus: ## Abre Prometheus no navegador padrão
	start http://localhost:9090

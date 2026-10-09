**Language:** **English** · [Tiếng Việt](00-project-setup.vi.md)

# Phase 0 — Project Setup

**Goal:** create a project skeleton that runs with a **single command**, so every later phase has a stable test environment.

**Input:** the assignment brief and the assumed technology in `README.md`.

**Deliverables:** a Git repo, `docker-compose.yml`, 6 backend projects + 1 minimal frontend, shared configuration, health checks.

---

## Step 0.1 — Initialise Git and the folder structure

**Tasks:**
1. `git init` in the project root.
2. Create a `.gitignore` covering:
   - Java/Maven: `target/`, `*.class`, `.mvn/wrapper/maven-wrapper.jar`
   - Node: `node_modules/`, `dist/`, `.vite/`
   - Environment: `.env`, `*.local`
   - IDE/OS: `.idea/`, `.vscode/`, `*.iml`, `.DS_Store`, `Thumbs.db`
3. Create the folder tree (see `README.md`) — 6 service folders plus `gateway/`, `web/`, `docs/`.
4. Create the root `README.md` (a summary of how to run — finalised in Phase 9).

## Step 0.2 — Write `docker-compose.yml` (infrastructure)

**Tasks:** define the infrastructure containers (no service containers yet — services run via Maven during development):

| Container | Image | Ports (host:container) | Dev credentials | Used for |
|---|---|---|---|---|
| `postgres` | `postgres:16-alpine` | `5432:5432` | user `postgres` / pass `postgres` | Creates 5 databases: `user_db`, `tuition_db`, `payment_db`, `otp_db`, `notification_db` |
| `redis` | `redis:7-alpine` | `6379:6379` | — | OTP TTL, idempotency keys, rate limiting |
| `rabbitmq` | `rabbitmq:3-management` | `5672:5672`, `15672:15672` | user `guest` / pass `guest` (default) | Email queue |
| `mailhog` | `mailhog/mailhog` | `1025:1025` (SMTP), `8025:8025` (UI) | — | Captures email during development |

**Template** (a starting point — refined in Phase 4 once migrations exist):
```yaml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports: ["5432:5432"]
    volumes: [pgdata:/var/lib/postgresql/data]
  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]
  rabbitmq:
    image: rabbitmq:3-management
    ports: ["5672:5672", "15672:15672"]
  mailhog:
    image: mailhog/mailhog
    ports: ["1025:1025", "8025:8025"]
volumes:
  pgdata:
```

**Important note:** create the databases for the 5 services with an `init` script (mount `./docker/initdb/*.sql`) **or** let each service's migration tool create its own schema — pick one approach and document it in the README. Suggestion: each service connects to its own database and runs migrations on startup.

**Check:** `docker compose up -d` → all 4 containers healthy:
```bash
docker compose ps
```

## Step 0.3 — Scaffold the backend services

**Tasks:** for each service (except the gateway), create a backend project with `jar` packaging and group `edu.tdtu`:

| Service | Artifact | Port | Required dependencies |
|---|---|---|---|
| user-service | `user-service` | 8081 | Web, Security, Data JPA, Validation, PostgreSQL, Migration, Actuator, OpenAPI docs, AMQP (if it publishes) |
| tuition-service | `tuition-service` | 8082 | Web, Data JPA, Validation, PostgreSQL, Migration, Actuator, OpenAPI docs |
| payment-service | `payment-service` | 8083 | Web, Data JPA, Validation, PostgreSQL, Migration, Actuator, OpenAPI docs, AMQP, HTTP client (calls other services) |
| otp-service | `otp-service` | 8084 | Web, Data JPA, Validation, PostgreSQL, Migration, Actuator, OpenAPI docs, AMQP, (optional Redis) |
| notification-service | `notification-service` | 8085 | Web, Data JPA, Validation, PostgreSQL, Migration, Actuator, AMQP, Mail |
| gateway | `gateway` | 8080 | Gateway, Security (Reactive), Actuator |

**Notes:**
- `payment-service` calls other services over an HTTP client (not the deprecated `RestTemplate`).
- `gateway` uses a **reactive** stack, not the MVC stack.
- You can scaffold each project with the framework's project generator or your IDE's initializer.

## Step 0.4 — Shared per-service configuration (`application.yml`)

**Tasks:** each service gets an `application.yml` with:
1. Its own **port**, per the table above.
2. A **datasource** pointing at its own database, e.g. user-service:
```yaml
spring:
  datasource:
    url: jdbc:postgresql://localhost:5432/user_db
    username: postgres
    password: postgres
  jpa:
    hibernate.ddl-auto: validate   # schema is managed by migrations
    open-in-view: false
  flyway:
    enabled: true
    locations: classpath:db/migration
```
3. **JWT:** share one secret (dev) or an RSA key pair. Config: `app.jwt.secret`, `app.jwt.expiration-ms` (suggested 30 minutes). `user-service` **issues** tokens; `gateway` **verifies** them.
4. **RabbitMQ** (for services that need it): host `localhost`, port 5672; declare queues/exchanges in code (see Phase 2).
5. **OpenAPI docs:** enable the Swagger UI for each service in dev.
6. **Logging pattern** including `traceId` (MDC) — set up once the gateway forwards an `X-Trace-Id` header.

**Check:** run each service; the log prints "Started … in x seconds" with no DB connection errors.

## Step 0.5 — Health check & `ping` endpoint

**Tasks:** each service returns:
- `GET /actuator/health` → `{"status":"UP"}` (including DB and, where present, RabbitMQ health).
- `GET /api/v1/ping` → `{"service":"user-service","status":"ok","time":"..."}` (used for demo and gateway-routing tests).

**Check:**
```bash
curl http://localhost:8081/api/v1/ping
curl http://localhost:8080/actuator/health   # once the gateway exists
```

## Step 0.6 — Environment variables & minimal frontend

**Tasks:**
1. Create `.env.example` listing: DB credentials, JWT secret, RabbitMQ, ports.
2. Frontend (fully built in Phase 7): create `web/` with a Vite + React app, proxying `/api` → `http://localhost:8080` to avoid CORS during development.
3. Make the first commit with a conventional message, e.g. `chore: scaffold monorepo with docker-compose and backend services`.

---

## Phase 0 completion checklist

- [ ] `git init` + `.gitignore` + correct folder structure
- [ ] `docker compose up -d` → 4 healthy containers
- [ ] 6 backend projects scaffolded with the right dependencies/ports
- [ ] Each service runs, health OK
- [ ] `user_db` … `notification_db` exist (or an explicit auto-create mechanism)
- [ ] Shared JWT secret configured
- [ ] `.env.example`, root README, first commit

**Acceptance criteria:** a single `docker compose up -d`, then start each service; all return `200` at `/actuator/health`; no service throws a connection error on startup.

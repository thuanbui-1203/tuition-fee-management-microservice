**Language:** **English** · [Tiếng Việt](README.vi.md)

# iBanking — Tuition Payment Subsystem · Project Documentation

> Topic: **MIDTERM PROJECT — TUITION PAYMENT SUBSYSTEM OF THE iBanking APP** (TDTU).
> The assignment statement was extracted from a photo of the brief (OCR; see `.reasonix/ocr_text/`).

## Purpose of this folder

The `docs/` folder holds the project documentation in **two groups**:

1. **Implementation plan** — the original step-by-step plan split into **9 phases** (Phase 0–9). Each phase is its own `.md` file describing the goal, inputs, concrete steps, deliverables and acceptance criteria.
2. **Implementation reference** — the current **.NET 10** implementation of three microservices (`tuition-service`, `otp-service`, `notification-service`).

**Status:** three services are implemented in **.NET 10** (tuition, otp, notification); `payment-service`, `user-service`, `gateway` and the frontend are still at the planning stage.

## Implementation reference (current)

| Doc | Contents |
|---|---|
| [`architecture.md`](architecture.md) | Service responsibilities, sync/async communication, saga, outbox, concurrency strategy |
| [`database.md`](database.md) | Per-service schemas, constraints, indexes, migrations |
| [`api.md`](api.md) | Public + internal endpoints, request/response, error codes |
| [`security.md`](security.md) | OTP handling, hashing vs encryption, secrets, TLS, rate limiting |
| [`error-handling.md`](error-handling.md) | Unified error body and error-code table |
| [`testing.md`](testing.md) | Unit / integration / concurrency test strategy and commands |
| [`troubleshooting.md`](troubleshooting.md) | Common failures and how to fix them |
| [`deployment.md`](deployment.md) | Production configuration, migrations, TLS, key rotation |

Root-level entry point: [`../README.md`](../README.md).

## Implementation plan (Phases 0–9)

| # | Phase | File | Main content |
|---|---|---|---|
| 0 | Project setup | `00-project-setup.md` | Git, folder structure, Docker Compose, service scaffolding, shared config |
| 1 | Business & data analysis | `01-analysis-use-case-erd.md` | Use Case Diagram (+ UC-04 spec) and ERD |
| 2 | Microservices architecture | `02-microservices-architecture.md` | Service decomposition, responsibilities, the two communication styles (sync REST + async messaging), architecture diagram |
| 3 | REST API design | `03-rest-api-design.md` | Every endpoint: URI, HTTP method, request, response, status codes |
| 4 | Database design & implementation | `04-database-design.md` | Per-service schema, business constraints, seed data, SQL and/or NoSQL |
| 5 | Service & API implementation | `05-backend-services.md` | Build order per service, tasks, required tests |
| 6 | Transaction & concurrency | `06-transaction-concurrency.md` | The two concurrency scenarios, consistency mechanisms, saga, idempotency, outbox |
| 7 | Web UI | `07-frontend-web.md` | Screens, behaviour, API flows, error mapping |
| 8 | Overall testing | `08-testing.md` | Unit / integration / API / E2E, concurrency-scenario tests |
| 9 | Documentation & demo | `09-documentation-demo.md` | README, report against the 8 requirements, demo script |

(Additional plan files: `10-teamwork-plan.md`, `11-teamwork-6days.md`, `12-teamwork-6days-summary.md`.)

## Technology

| Component | Choice | Notes |
|---|---|---|
| Backend | **.NET 10** (ASP.NET Core, EF Core 10) | Migrated from the original Java 17 + Spring Boot 3.x plan |
| Database | PostgreSQL | One logical database per service |
| Cache / TTL | Redis (optional) | OTP TTL is enforced in PostgreSQL; Redis is an optional extra for rate limiting |
| Message broker | RabbitMQ | Asynchronous email (outbox pattern) |
| Email (dev) | MailHog | Local SMTP catcher: SMTP 1025, UI http://localhost:8025 |
| Build / run | .NET SDK + Docker Compose | `docker compose up` starts the infrastructure |

> Compared with the original plan, only the **tooling** changed; the analysis/design phases (1–3, 6) are unchanged.

## 8 assignment requirements → phase

| Requirement | Phase |
|---|---|
| 1. Business & data analysis (Use Case Diagram, ERD) | Phase 1 |
| 2. Microservices architecture + inter-service communication | Phase 2 |
| 3. REST API (URI, method, request, response, status code) | Phase 3 |
| 4. Database in SQL and/or NoSQL | Phase 4 |
| 5. Service & API implementation covering the business flow | Phase 5 |
| 6. Transaction & concurrency, consistency | Phase 6 |
| 7. Web UI integrated with the API | Phase 7 (+ 8) |
| 8. Documentation & demo | Phase 9 (+ 0) |

## Execution order & dependencies

```
P0 Setup → P1 Analysis (UCD + ERD) → P2 Architecture + P3 REST API → P4 Database
        → P5 Backend (user → tuition → otp → notification → payment → gateway)
        → P6 Transaction & concurrency → P7 Frontend → P8 Testing → P9 Docs & demo
```

**Key dependency rules:**

- Do not start `payment-service` (Phase 5.5) before `user-service`, `tuition-service` and `otp-service` are complete and tested.
- Do not consider Phase 6 done until the two concurrency tests (Scenario A & B) run and pass.
- Each phase must meet its own acceptance criteria before moving on.

## Project-wide conventions

1. **Each service owns its data** — no shared tables; communicate only via API/event.
2. **Naming:** REST uses plurals (`/payments`, `/users/me`); events use past tense (`TransactionSucceeded`); variables/endpoints use `lowerCamelCase`/`kebab-case`.
3. **Unified errors:** every error response is `{ "code", "message", "timestamp", "traceId" }`.
4. **Money:** store as integer (VND) or `DECIMAL(15,2)`; never `double`.
5. **Minimum security:** hash passwords (bcrypt); store only OTP hashes; time-limited JWTs; never expose internal endpoints through the gateway.
6. **Every service** has health checks, OpenAPI/Swagger, and logs with `traceId`.

## Repository structure

```
microservices/
├── backend/                      # .NET 10 services
│   ├── Common/                   # shared cross-cutting library (Microservices.Common)
│   ├── tuition-service/          # port 8082 — students, tuition fees
│   ├── otp-service/              # port 8084 — OTP lifecycle, outbox
│   ├── notification-service/     # port 8085 — email
│   ├── payment-service/          # (planned) saga orchestrator
│   ├── user-service/             # (planned) accounts, login, balance
│   ├── gateway/                  # (planned) API gateway
│   ├── tests/                    # unit + integration test projects
│   └── Microservices.sln
├── frontend/                     # React + Vite (planned)
├── docs/                         # project documentation (this folder)
├── docker-compose.yml
├── .env.example
├── README.md                     # root readme (English, default)
└── README.vi.md                  # root readme (Vietnamese)
```

## Assignment business summary (basis for every phase)

1. Users sign in with `username`/`password`; the system tracks full name, phone, email, available balance and transaction history.
2. The payment screen has three groups: payer (auto-filled, read-only), tuition info (looked up by MSSV), and payment info (balance + amount). Only the **entire** tuition fee can be paid; a payment is valid when the fee exists, is unpaid, and balance ≥ amount.
3. Verification uses an **email OTP** bound to exactly one transaction, expiring in **at most 5 minutes**, and usable **once**.
4. After a valid OTP: re-check → debit → mark the fee paid → save history → send a confirmation email → show the result.
5. Concurrency consistency: (A) many transactions on one account → never overspend; (B) several people paying the same fee (MSSV) → exactly one succeeds.

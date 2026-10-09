# iBanking Tuition Payment — .NET Microservices

Production-quality implementation of three microservices for the **iBanking tuition payment subsystem**, migrated from the original Java/Spring design to **.NET 10**:

| Service | Port | Owns | Role |
|---|---|---|---|
| `tuition-service` | 8082 | `students`, `tuition_fees` | Look up unpaid tuition, atomically claim/release a fee |
| `otp-service` | 8084 | `otp_codes`, `outbox_messages` | OTP lifecycle (issue/verify), rate limiting, outbox → RabbitMQ |
| `notification-service` | 8085 | `email_logs` | Consume email events from RabbitMQ, send via SMTP |

The original design documents remain in [`docs/`](docs/README.md). The services `payment-service` (saga orchestrator), `user-service` and `gateway` are **out of scope** for this .NET implementation and are documented as integration points.

---

## 1. Project overview

The subsystem lets a bank user pay a tuition fee for any student (looked up by `MSSV`). The flow is:

1. `payment-service` (not implemented here) looks up the fee via `tuition-service`.
2. It requests an OTP from `otp-service`, which publishes an `OtpRequested` event (OTP encrypted with AES-256-GCM) through its transactional outbox.
3. `notification-service` consumes the event and emails the OTP.
4. On confirmation, `payment-service` verifies the OTP, then `claim`s the fee (atomic conditional update) and debits the account.
5. A `PaymentSucceeded` event is published (outbox) and emailed by `notification-service`.

Key guarantees enforced in code and in the database:

- A tuition fee can be **paid exactly once** (atomic `UPDATE … WHERE status = 'UNPAID'`).
- An OTP is **cryptographically random**, stored **hashed**, **expires in 5 minutes**, is **one-time use**, is **bound to one transaction**, and **locks after 5 failed attempts**.
- Emails are **never sent twice** (deduplicated by `message_id`).
- Outbox + relay ensure **no event is lost** between a DB commit and the broker publish.

## 2. Architecture

```
Client → payment-service (saga)
              ├── REST ──> tuition-service ──> tuition_db
              ├── REST ──> otp-service ──> otp_db
              │                 └── outbox → RabbitMQ ──> notification-service ──> SMTP
              └── (user-service, out of scope)
```

- **Synchronous**: internal REST (`/internal/**`) with service-to-service authentication.
- **Asynchronous**: RabbitMQ (durable topic exchange + queue + dead-letter), outbox relay in `otp-service`, idempotent consumer in `notification-service`.
- **Database ownership**: each service has its own PostgreSQL database; cross-service references are logical ids only.

See [`docs/architecture.md`](docs/architecture.md) for details.

## 3. Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (LTS; a `global.json` pins the SDK)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for infrastructure and integration tests)
- No local PostgreSQL/RabbitMQ/SMTP installation is required — everything runs in Docker.

## 4. Environment variables

All configuration is strongly typed. Development defaults live in `appsettings.Development.json`; production values must be supplied via environment variables (see [`.env.example`](.env.example)).

| Variable | Used by | Notes |
|---|---|---|
| `ConnectionStrings__TuitionDb` | tuition-service | Npgsql connection string |
| `ConnectionStrings__OtpDb` | otp-service | Npgsql connection string |
| `ConnectionStrings__NotificationDb` | notification-service | Npgsql connection string |
| `Security__InternalApi__ApiKey` | tuition, otp | Shared secret for `/internal/**` auth |
| `Security__EventEncryption__Key` | otp, notification | Base64 AES-256-GCM key (must match on both sides) |
| `Smtp__Host`, `Smtp__Port`, `Smtp__UserName`, `Smtp__Password`, `Smtp__EnableSsl` | notification | SMTP server |
| `RabbitMq__*` | otp, notification | Broker connection |

> In production the internal API key and event-encryption key must be rotated and injected from a secret manager (see [`docs/security.md`](docs/security.md)).

## 5. Local development setup

```bash
# 1. Start infrastructure (PostgreSQL, RabbitMQ, MailHog)
docker compose up -d

# 2. Build
dotnet build backend/Microservices.sln

# 3. Run each service (separate terminals)
dotnet run --project backend/tuition-service/TuitionService.Api
dotnet run --project backend/otp-service/OtpService.Api
dotnet run --project backend/notification-service/NotificationService.Api
```

In `Development`, each service applies its EF Core migrations on startup (`Database:ApplyMigrationsOnStartup = true`).

## 6. Docker setup

`docker-compose.yml` starts three containers:

| Container | Image | Ports | Purpose |
|---|---|---|---|
| `postgres` | `postgres:16-alpine` | 5432 | One database per service (created by `docker/initdb`) |
| `rabbitmq` | `rabbitmq:3-management` | 5672, 15672 | Event bus (UI: guest/guest) |
| `mailhog` | `mailhog/mailhog` | 1025, 8025 | SMTP catcher (UI: http://localhost:8025) |

Redis is intentionally omitted — OTP TTL/rate limiting are enforced in PostgreSQL + an in-memory limiter; add Redis only for multi-instance rate limiting (see [`docs/security.md`](docs/security.md)).

## 7. Database setup

- `docker/initdb/01-create-databases.sql` creates `tuition_db`, `otp_db`, `notification_db` (plus `user_db`/`payment_db` placeholders).
- Each service owns its own database and applies its own migrations.

## 8. Migration

```bash
# Apply migrations explicitly (production):
dotnet ef database update --project backend/tuition-service/TuitionService.Infrastructure
dotnet ef database update --project backend/otp-service/OtpService.Infrastructure
dotnet ef database update --project backend/notification-service/NotificationService.Infrastructure

# Add a new migration (example):
dotnet ef migrations add AddColumn --project backend/tuition-service/TuitionService.Infrastructure -o Persistence/Migrations
```

`dotnet-ef` is installed as a local tool (see `backend/dotnet-tools.json`); run it as `dotnet tool run dotnet-ef …`.

## 9. Running each service

| Service | Command | Swagger |
|---|---|---|
| tuition-service | `dotnet run --project backend/tuition-service/TuitionService.Api` | http://localhost:8082/swagger |
| otp-service | `dotnet run --project backend/otp-service/OtpService.Api` | http://localhost:8084/swagger |
| notification-service | `dotnet run --project backend/notification-service/NotificationService.Api` | http://localhost:8085/swagger |

## 10. Swagger URLs

See the table above. Public APIs are under `/api/v1/**`; internal APIs under `/internal/**` (never routed through a gateway).

## 11. Health-check URLs

Every service exposes:

| Endpoint | Meaning |
|---|---|
| `GET /health/live` | Liveness — process is up (no dependency checks) |
| `GET /health/ready` | Readiness — PostgreSQL (and RabbitMQ where used) reachable |
| `GET /health` | Combined |

## 12. RabbitMQ configuration

- Exchange: `email.events` (topic, durable).
- Queue: `notification.email` (durable) bound to routing keys `OtpRequested` and `PaymentSucceeded`.
- Dead-letter: `email.events.dlx` → `notification.email.dlq`.
- Messages are persistent and published with publisher confirms; the consumer acks/nacks manually with `prefetch=1`.

## 13. SMTP / MailHog configuration

Development uses MailHog (`localhost:1025`, no auth, no TLS). Production SMTP is configured via `Smtp__*` with `Smtp__EnableSsl=true` (STARTTLS). See [`docs/deployment.md`](docs/deployment.md).

## 14. Redis configuration

Not required. The single-instance rate limiter (`FixedWindowRateLimiter`) is the default; swap in a Redis-backed `IRateLimiter` for horizontal scaling.

## 15. Running tests

```bash
# Unit tests (no Docker required)
dotnet test backend/Microservices.sln --filter "Category=Unit"

# Integration tests (require Docker: PostgreSQL/RabbitMQ via Testcontainers)
dotnet test backend/Microservices.sln --filter "Category=Integration"
```

## 16. Running integration tests

Integration tests use **Testcontainers** (real PostgreSQL and RabbitMQ) and include the concurrency suites:

- `ConcurrentClaim_ExactlyOneSucceeds` (tuition, 10 concurrent claims → 1 success).
- `ConcurrentVerify_ExactlyOneSucceeds` (otp, 10 concurrent verifications → 1 success).
- `ConcurrentIssue_LeavesExactlyOneActiveOtp` (otp).

See [`docs/testing.md`](docs/testing.md).

## 17. Example API requests

```bash
# Public lookup
curl "http://localhost:8082/api/v1/tuitions?mssv=521H0001"

# Internal claim (requires the shared internal API key)
curl -X POST "http://localhost:8082/internal/tuitions/10/claim" \
  -H "Content-Type: application/json" \
  -H "X-Internal-Api-Key: dev-internal-key-change-me" \
  -d '{"transactionId":"550e8400-e29b-41d4-a716-446655440000"}'

# Internal OTP issue
curl -X POST "http://localhost:8084/internal/otp/issue" \
  -H "Content-Type: application/json" \
  -H "X-Internal-Api-Key: dev-internal-key-change-me" \
  -H "Idempotency-Key: 11111111-1111-1111-1111-111111111111" \
  -d '{"transactionId":"550e8400-e29b-41d4-a716-446655440000","email":"a@tdtu.edu.vn"}'
```

## 18. Common errors

| Code | HTTP | When |
|---|---|---|
| `INVALID_INPUT` | 400 | Missing/malformed input |
| `UNAUTHENTICATED` | 401 | Missing/wrong internal API key |
| `NOT_FOUND` | 404 | MSSV/fee/OTP not found |
| `FEE_ALREADY_PAID` | 409 | Fee already paid by another transaction |
| `OTP_ALREADY_USED` | 409 | OTP reused |
| `OTP_EXPIRED` | 410 | OTP older than 5 minutes |
| `OTP_LOCKED` | 409 | 5 failed attempts |
| `IDEMPOTENCY_CONFLICT` | 409 | Idempotency key reused with a different payload |
| `RATE_LIMITED` | 429 | Too many issue/verify requests |
| `INTERNAL_ERROR` | 500 | Unexpected failure |
| `DEPENDENCY_DOWN` | 503 | Downstream dependency unavailable |

See [`docs/error-handling.md`](docs/error-handling.md).

## 19. Security considerations

- OTPs are generated with `RandomNumberGenerator` and stored only as PBKDF2 hashes.
- The plaintext OTP travels to the email sender **only** inside the outbox, encrypted with AES-256-GCM.
- `/internal/**` requires a shared API key (constant-time compare); production should use mTLS/short-lived JWTs.
- No secrets in source control; no sensitive values in logs; email bodies are never persisted.

See [`docs/security.md`](docs/security.md).

## 20. Production deployment notes

- Set `Database:ApplyMigrationsOnStartup=false`; run `dotnet ef database update` as a deploy step.
- Supply all secrets via environment variables / secret manager.
- Enable TLS for SMTP (`Smtp__EnableSsl=true`) and SSL for PostgreSQL connections.
- Replace the shared API key with mTLS or short-lived service JWTs.

See [`docs/deployment.md`](docs/deployment.md).

## Repository layout

```
backend/
  Microservices.sln
  Common/Microservices.Common/        # shared errors, results, AES-GCM, middleware, rate limiting
  tuition-service/{Domain,Application,Infrastructure,Api}/
  otp-service/{Domain,Application,Infrastructure,Api}/
  notification-service/{Domain,Application,Infrastructure,Api}/
  tests/{...UnitTests,...IntegrationTests}/   # 6 test projects
docker-compose.yml
docker/initdb/
.env.example
docs/
```

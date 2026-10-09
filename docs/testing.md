# Testing

Two layers, matching the original project's separation: **unit tests** (no infrastructure) and **integration tests** (real PostgreSQL/RabbitMQ via Testcontainers).

## Running

```bash
# Unit tests (no Docker)
dotnet test backend/Microservices.sln --filter "Category=Unit"

# Integration tests (Docker required)
dotnet test backend/Microservices.sln --filter "Category=Integration"
```

> Integration tests use `Testcontainers.PostgreSql`/`Testcontainers.RabbitMq` and require a running Docker engine.

## Unit tests

| Suite | Coverage |
|---|---|
| `TuitionService.UnitTests` | MSSV validation; lookup (found/not-found/no-fee/paid/multiple); claim (success/not-found/already-paid/idempotent); release (success/no-op/wrong-transaction/not-found) |
| `OtpService.UnitTests` | generator (length/digits/uniqueness); PBKDF2 hasher (round-trip, wrong code, salt uniqueness, malformed); verify (correct/incorrect/expired/used/locked/wrong-transaction/5-attempt lock/rate-limit/missing); issue (success/replay/conflict/invalid email) |
| `NotificationService.UnitTests` | content builder (OTP decrypt, payment format, unsupported event, wrong key); processing (dedup, send success, permanent failure, transient retry → dead-letter, invalid recipient) |

Fakes are hand-written in-memory implementations of the repository/port interfaces — no mocking framework required, no database required.

## Integration tests

| Suite | Highlights |
|---|---|
| `TuitionService.IntegrationTests` | migrations apply; `amount > 0` CHECK and `mssv` UNIQUE enforced; **10 concurrent claims → exactly 1 success**; already-paid rejection; idempotent replay; release conflict/owner release; lookup lifecycle |
| `OtpService.IntegrationTests` | migrations apply; atomic mark-used; **10 concurrent verifications → exactly 1 success**; **4 concurrent issues → exactly 1 active OTP**; issue writes an outbox entry whose payload decrypts to a 6-digit OTP |
| `NotificationService.IntegrationTests` | migrations apply; send success persists `SENT`; **duplicate `messageId` is deduplicated at the database** (sender called once) |

## Concurrency tests

Concurrency is proven against **real PostgreSQL** (not an in-memory database, which does not reproduce Postgres locking/constraint behaviour):

- **Scenario B** (multiple payers, one fee): `ConcurrentClaim_ExactlyOneSucceeds` asserts exactly one successful claim and one `PAID` record.
- **OTP one-time use**: `ConcurrentVerify_ExactlyOneSucceeds` asserts exactly one successful verification.
- **OTP single-active invariant**: `ConcurrentIssue_LeavesExactlyOneActiveOtp`.

## What is not covered

- End-to-end UI tests (Playwright) and Postman/Newman API collections remain part of the original plan (`docs/08-testing.md`) and are out of scope for this .NET backend implementation.
- A full `payment-service` saga test is out of scope because that service is not implemented here.

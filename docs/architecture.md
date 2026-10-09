**Language:** **English** · [Tiếng Việt](architecture.vi.md)

# Architecture

This document describes the architecture of the three implemented .NET microservices and how they relate to the original iBanking design.

## System context

```
Client
  |
  v
Payment Service (saga orchestrator — NOT implemented in this repo)
  |-- REST (synchronous) ----------> Tuition Service  (tuition_db)
  |-- REST (synchronous) ----------> OTP Service      (otp_db)
  |-- REST (synchronous) ----------> User Service     (user_db, out of scope)
  |-- outbox + RabbitMQ (async) ---> Notification Service (notification_db) --> SMTP
```

## Service responsibilities and data ownership

| Service | Owns | Synchronous API | Asynchronous role |
|---|---|---|---|
| tuition-service | `students`, `tuition_fees` | `GET /api/v1/tuitions`, `POST /internal/tuitions/{feeId}/claim`, `POST /internal/tuitions/{feeId}/release` | — |
| otp-service | `otp_codes`, `outbox_messages` | `POST /internal/otp/issue`, `POST /internal/otp/verify` | producer of `OtpRequested` (outbox → RabbitMQ) |
| notification-service | `email_logs` | (none — consumer only) | consumer of `OtpRequested` and `PaymentSucceeded` |

Each service has its own database. Cross-service references are **logical identifiers** (e.g. `tuition_fees.paid_transaction_id`, `otp_codes.transaction_id`) — there are no cross-database foreign keys.

## Layering

Every service follows the same clean architecture:

```
Api            HTTP transport, controllers, middleware, health checks, Swagger
Application    use cases, DTOs, validation, interfaces, options
Domain         entities, enums, domain rules (no external dependencies)
Infrastructure EF Core, migrations, repositories, RabbitMQ, SMTP, outbox relay
```

Dependency direction is strictly `Api → Application → Domain` and `Api → Infrastructure → {Application, Domain}`. Controllers contain no business logic; repositories contain no business rules beyond atomic persistence.

## Synchronous communication

Internal endpoints live under `/internal/**` and require service-to-service authentication (a shared API key in dev; see `docs/security.md`). They are never exposed through a gateway.

- **Tuition claim** is an atomic conditional update:
  ```sql
  UPDATE tuition_fees SET status='PAID', paid_transaction_id=@txn, updated_at=@now
  WHERE id=@feeId AND status='UNPAID';
  ```
  A row count of `0` is classified as *not found*, *already paid by another transaction*, or *idempotent replay of the same transaction*.

- **Tuition release** (saga compensation) only succeeds when the fee is currently held by the releasing transaction:
  ```sql
  UPDATE tuition_fees SET status='UNPAID', paid_transaction_id=NULL, updated_at=@now
  WHERE id=@feeId AND status='PAID' AND paid_transaction_id=@txn;
  ```

- **OTP verify** marks the code used atomically:
  ```sql
  UPDATE otp_codes SET status='USED', used_at=@now
  WHERE id=@id AND transaction_id=@txn AND status='ACTIVE' AND expires_at > @now;
  ```

## Asynchronous communication (RabbitMQ)

- Durable topic exchange `email.events`, durable queue `notification.email`, dead-letter exchange/queue.
- Messages are persistent and published with publisher confirms.
- The consumer acks/nacks manually with `prefetch=1`; transient failures retry with bounded exponential backoff, then dead-letter.

### Event envelope

```json
{
  "messageId": "…guid…",
  "eventType": "OtpRequested | PaymentSucceeded",
  "occurredAt": "2026-…Z",
  "correlationId": "…",
  "transactionId": "…",
  "recipient": "a@tdtu.edu.vn",
  "payload": { }
}
```

`messageId` is the deduplication key; the notification consumer enforces uniqueness with a database unique index.

## Outbox pattern

`otp-service` writes the `OtpRequested` event into `outbox_messages` **in the same database transaction** as the OTP insert. A background relay publishes PENDING messages and marks them SENT. If the process crashes between commit and publish, the event is republished later — with the same `messageId`, so the consumer deduplicates it.

## Saga and compensation (integration contract)

The full payment saga is orchestrated by `payment-service` (out of scope). The implemented services expose exactly the primitives it needs:

1. `claim(feeId, transactionId)` — reserve the fee.
2. debit the account (user-service, out of scope).
3. on debit failure → `release(feeId, transactionId)` to compensate.

`release` is safe because it is scoped to the claiming transaction, so a compensating transaction can never release a fee that another transaction has already paid.

## Idempotency

| Operation | Mechanism |
|---|---|
| Tuition claim | `paid_transaction_id` — replaying the same transaction returns success |
| Tuition release | release of an already-UNPAID fee is a no-op |
| OTP issuance | `Idempotency-Key` header → unique `idempotency_key` column |
| OTP verification | one-time-use; retries are resolved by the orchestrator's transaction state |
| Notification consumption | unique `message_id` on `email_logs` |

## Concurrency strategy

- **Scenario B (two users pay the same fee)** — the atomic conditional claim guarantees exactly one success; proven by an integration test with 10 concurrent claims against real PostgreSQL.
- **OTP concurrent verification** — the atomic mark-used guarantees exactly one success.
- **OTP concurrent issuance** — a PostgreSQL advisory lock keyed on `transaction_id` plus a partial unique index (`status='ACTIVE'`) guarantee at most one active OTP per transaction.

## Observability

- Structured logging with `traceId`/`correlationId` (propagated via `X-Correlation-Id`).
- Health checks: `/health/live`, `/health/ready`, `/health`.
- No secrets or sensitive values are ever logged.

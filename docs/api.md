# API reference

All endpoints return JSON. Errors use the unified body described in [`error-handling.md`](error-handling.md). Public APIs are under `/api/v1`; internal APIs are under `/internal` and require the `X-Internal-Api-Key` header.

## tuition-service

### `GET /api/v1/tuitions?mssv={mssv}` — public

Look up a student and their single outstanding (UNPAID) tuition fee.

- Authentication: none required for this subsystem demo (a gateway would enforce a user JWT).
- Parameters: `mssv` (required, 1–20 alphanumeric characters).

**200 OK**

```json
{
  "mssv": "521H0001",
  "studentName": "Trần Thị B",
  "fee": { "feeId": 10, "semester": "2024-2025/HK1", "amount": 8400000.00, "status": "UNPAID" }
}
```

**Errors**

| Status | Code | Meaning |
|---|---|---|
| 400 | `INVALID_INPUT` | MSSV missing or invalid |
| 404 | `NOT_FOUND` | MSSV not found, or student has no tuition fee |
| 409 | `FEE_ALREADY_PAID` | the student's fee is already PAID |

### `POST /internal/tuitions/{feeId}/claim` — internal

Atomically claim an unpaid fee for a transaction.

- Auth: `X-Internal-Api-Key`.
- Body: `{ "transactionId": "uuid" }`.

**200 OK** → `{ "success": true }`

**Errors**: `404 NOT_FOUND`, `409 FEE_ALREADY_PAID` (already paid by another transaction). Replaying the same transaction is idempotent (`200`).

### `POST /internal/tuitions/{feeId}/release` — internal

Release a claimed fee (saga compensation). Only the transaction that currently holds the fee may release it.

- Auth: `X-Internal-Api-Key`.
- Body: `{ "transactionId": "uuid" }`.

**200 OK** → `{ "success": true }`

**Errors**: `404 NOT_FOUND`, `409 RELEASE_CONFLICT` (held by a different transaction). Releasing an already-UNPAID fee is an idempotent no-op.

## otp-service

All OTP endpoints are internal.

### `POST /internal/otp/issue` — internal

Issue a new OTP bound to a transaction and enqueue the OTP email (outbox).

- Auth: `X-Internal-Api-Key`.
- Headers: `Idempotency-Key` (optional, recommended for retries).
- Body: `{ "transactionId": "uuid", "email": "a@tdtu.edu.vn" }`.

**201 Created** (fresh) / **200 OK** (idempotent replay) → `{ "otpId": 42 }`

**Errors**: `400 INVALID_INPUT`, `409 IDEMPOTENCY_CONFLICT` (same key, different transaction), `429 RATE_LIMITED`.

### `POST /internal/otp/verify` — internal

Verify an OTP for a transaction. On success the OTP is atomically marked USED and can never be used again.

- Auth: `X-Internal-Api-Key`.
- Body: `{ "transactionId": "uuid", "otp": "482913" }`.

**200 OK** → `{ "valid": true }`

**Errors**: `400 INVALID_INPUT`, `400 OTP_INCORRECT`, `404 NOT_FOUND` (generic — no OTP for the transaction), `409 OTP_ALREADY_USED`, `409 OTP_LOCKED`, `410 OTP_EXPIRED`, `429 RATE_LIMITED`.

> Verification deliberately returns generic messages; it never reveals whether an OTP exists for a *different* transaction.

## notification-service

No REST business endpoints. It exposes only health checks and consumes events from RabbitMQ:

| Event | Routing key | Email |
|---|---|---|
| `OtpRequested` | `OtpRequested` | OTP email (OTP decrypted from the AES-GCM payload) |
| `PaymentSucceeded` | `PaymentSucceeded` | payment confirmation |

## Health endpoints (all services)

| Endpoint | Meaning |
|---|---|
| `GET /health/live` | liveness (no dependencies) |
| `GET /health/ready` | readiness (PostgreSQL, RabbitMQ where used) |
| `GET /health` | combined |

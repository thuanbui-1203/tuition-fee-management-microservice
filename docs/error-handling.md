**Language:** **English** · [Tiếng Việt](error-handling.vi.md)

# Error handling

Every service returns a single, consistent error body:

```json
{
  "code": "FEE_ALREADY_PAID",
  "message": "The tuition fee has already been paid.",
  "timestamp": "2026-10-08T00:00:00Z",
  "traceId": "a1b2c3…"
}
```

- `traceId` equals the `HttpContext.TraceIdentifier`, populated by the correlation middleware from `X-Correlation-Id` (or generated).
- Business failures are modelled as `Result<T>` values (not exceptions) and mapped by `ControllerResultExtensions`.
- Unhandled exceptions are sanitized into `500 INTERNAL_ERROR` by a last-resort middleware — stack traces, connection strings, SQL and internal exception messages are never returned.

## Error codes

| Code | HTTP | When |
|---|---|---|
| `INVALID_INPUT` | 400 | Missing/malformed input (auto model-state failures included) |
| `UNAUTHENTICATED` | 401 | Missing/wrong internal API key |
| `FORBIDDEN` | 403 | Authorized but not permitted (reserved) |
| `NOT_FOUND` | 404 | Resource not found |
| `FEE_ALREADY_PAID` | 409 | Fee already paid by another transaction |
| `RELEASE_CONFLICT` | 409 | Release attempted by the wrong transaction |
| `OTP_ALREADY_USED` | 409 | OTP already used |
| `OTP_INCORRECT` | 400 | OTP value wrong |
| `OTP_EXPIRED` | 410 | OTP expired (over 5 minutes) |
| `OTP_LOCKED` | 409 | OTP locked after too many failed attempts |
| `IDEMPOTENCY_CONFLICT` | 409 | Idempotency key reused with a different payload |
| `RATE_LIMITED` | 429 | Too many requests |
| `INTERNAL_ERROR` | 500 | Unexpected failure |
| `DEPENDENCY_DOWN` | 503 | Downstream dependency unavailable |

## Validation

Input is validated before business logic executes:

- MSSV: non-empty, ≤ 20 alphanumeric characters.
- `transactionId`: non-empty GUID.
- OTP: exact length (default 6) digits.
- Email: validated format, ≤ 254 chars.
- Amount: `> 0` (domain + database CHECK).

Model-binding failures (e.g. malformed GUIDs) are mapped to the same unified `INVALID_INPUT` body.

## Error mapping philosophy

- **400** — the caller can fix the request.
- **409** — a conflict with the current state (already paid/used/locked/conflict).
- **410** — the resource existed but has expired (OTP).
- **429** — rate limit.
- **5xx** — the service or a dependency failed; do not expose internals.

## Logging of errors

Failures are logged server-side with `traceId`, `errorCode`, `transactionId`, `messageId` and `durationMs` where available — but never with secret or sensitive values.

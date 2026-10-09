**Language:** **English** · [Tiếng Việt](security.vi.md)

# Security

## Authentication & authorization

- **Internal APIs** (`/internal/**`) require a shared service credential in the `X-Internal-Api-Key` header, compared in constant time (`CryptographicOperations.FixedTimeEquals`). Internal endpoints are never exposed through a gateway.
- Production should replace the shared key with **mTLS** or **short-lived service JWTs** — the middleware is isolated so the transport credential can be swapped without touching business code.
- Cross-service identifiers (`transactionId`, `feeId`) are validated and never trusted from an unauthenticated caller. Ownership checks are scoped to the calling transaction (e.g. `release` only for the transaction that claimed the fee).

## Secrets

- No secrets are committed. Base `appsettings.json` carries empty placeholders; development-only values live in `appsettings.Development.json` and are documented as non-production.
- Production secrets come from environment variables or a secret manager. See `.env.example` and `docs/deployment.md`.

## OTP handling

- Generation: `RandomNumberGenerator` (`CryptoSecureOtpGenerator`) — never `Random`, `Guid`, or timestamps.
- Storage: PBKDF2-HMAC-SHA256 with a fresh 128-bit salt per record; stored in `otp_codes.code_hash`. The plaintext OTP is **never** stored.
- Transit: the plaintext OTP travels to the email sender only inside the outbox, encrypted with **AES-256-GCM** (authenticated encryption). The key (`Security:EventEncryption:Key`) is shared only by `otp-service` and `notification-service`.
- Verification: constant-time hash comparison; verification errors are generic (no information disclosure about another transaction's OTP).

## Hashing vs encryption

| Data | Treatment | Why |
|---|---|---|
| OTP at rest (`code_hash`) | **Hashed** (PBKDF2) | one-way verification; must not be reversible |
| OTP in outbox event | **Encrypted** (AES-256-GCM) | must be recoverable so the email can be sent; authenticated to prevent tampering |
| Email bodies | **Not stored** | minimizing sensitive data at rest |

Keys are stored in configuration/secret manager. Rotation: generate a new 32-byte key (`AesGcmCrypto.GenerateKey()`), update both services atomically, and invalidate/re-issue any OTPs encrypted under the old key. Document a retention policy for `email_logs` (metadata only) if auditing is required.

## Email security

- Recipient addresses are validated before sending.
- SMTP uses MailKit; `EnableSsl=true` enables STARTTLS in production.
- Headers are built only from fixed templates + validated data — no user-controlled SMTP headers, so header injection is not possible.
- SMTP credentials are never logged.

## Rate limiting / abuse prevention

- OTP issuance and verification are rate-limited per transaction (configurable `RateLimiting` section).
- The default `FixedWindowRateLimiter` is thread-safe and suitable for a single instance; use a Redis-backed `IRateLimiter` for multi-instance deployments.

## Logging

Sensitive values are never logged: OTPs, OTP hashes, passwords, SMTP credentials, JWT/authorization headers, and connection strings. Logs carry `traceId`/`correlationId`, operation, status and error codes only.

## TLS / production

- SMTP over STARTTLS (production).
- PostgreSQL connections support SSL (add `SSL Mode=Require` to the connection string in production).
- Service-to-service traffic should use mTLS or a network policy in production.

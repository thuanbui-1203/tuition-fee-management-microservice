# Troubleshooting

## PostgreSQL unavailable

- Check `docker compose ps` — is `postgres` healthy?
- Verify the connection string matches (`Host=localhost;Port=5432;…`).
- Check `/health/ready` — it reports PostgreSQL reachability.

## Migration failure

- Run `dotnet ef database update --project backend/<service>/<Service>.Infrastructure` and read the error.
- If a migration is partially applied, check the `__EFMigrationsHistory` table.
- A schema change requires a new migration — the app never auto-generates schema.

## RabbitMQ unavailable

- `docker compose ps` → is `rabbitmq` healthy?
- `notification-service` and `otp-service` reconnect automatically (5s backoff); the outbox keeps retrying, so no event is lost.
- Check `/health/ready` and the dead-letter queue `notification.email.dlq` in the RabbitMQ UI (http://localhost:15672).

## SMTP unavailable / email not sent

- Is MailHog running? Open http://localhost:8025 and check messages.
- Check `email_logs`: `status` transitions (`SENDING → SENT/FAILED`) and `error_code`.
  - `SMTP_AUTH_FAILED` → wrong credentials (permanent).
  - `SMTP_UNAVAILABLE`/`SMTP_TRANSIENT` → SMTP down (transient).
  - `TRANSIENT_EXHAUSTED` → retries exhausted → message in the dead-letter queue.

## OTP email not received

1. Confirm `notification-service` is running and consuming (`notification.email` queue).
2. Confirm `otp-service` relay published the event (`outbox_messages.status = SENT`).
3. Check MailHog. If the OTP payload fails to decrypt (`INVALID_INPUT` in `email_logs`), the `Security:EventEncryption:Key` differs between the two services — set the same value on both.

## Expired OTP

OTP lifetime is `Otp:TtlSeconds` (default 300). An expired code returns `410 OTP_EXPIRED`; re-issue a new OTP for the transaction.

## Duplicate message / duplicate email

This is by design: `email_logs.message_id` is unique, so a redelivered event is acknowledged without sending a second email.

## Database deadlock / concurrency conflict

- Claims and OTP transitions are single atomic `UPDATE` statements with no application-level read-then-write, minimizing deadlock exposure.
- If you still see a transient `40001` (serialization/deadlock), retry the operation with a fresh request.

## Invalid JWT / service-to-service authentication failure

This .NET implementation uses a shared internal API key (not JWTs) for `/internal/**`:

- Missing/wrong `X-Internal-Api-Key` → `401 UNAUTHENTICATED`.
- Ensure `Security:InternalApi:ApiKey` is set and matches on both sides.
- (Original design used gateway-issued JWTs for public user authentication; that is `payment-service`/gateway scope.)

## Consumer restart / service crash

- `otp-service` relay republishes PENDING outbox messages on restart (stable `messageId` → dedup).
- `notification-service` reconnects and reprocesses unacked messages; duplicates are deduplicated.

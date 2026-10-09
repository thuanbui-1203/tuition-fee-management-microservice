# Deployment

## Build & publish

```bash
dotnet publish backend/tuition-service/TuitionService.Api -c Release -o out/tuition-service
dotnet publish backend/otp-service/OtpService.Api -c Release -o out/otp-service
dotnet publish backend/notification-service/NotificationService.Api -c Release -o out/notification-service
```

## Configuration (production)

All values below must be provided via environment variables or a secret manager — never commit them.

| Variable | Required | Notes |
|---|---|---|
| `ConnectionStrings__TuitionDb` | yes | include `SSL Mode=Require` for managed PostgreSQL |
| `ConnectionStrings__OtpDb` | yes | |
| `ConnectionStrings__NotificationDb` | yes | |
| `Security__InternalApi__ApiKey` | yes | replace with mTLS/JWT in hardened environments |
| `Security__EventEncryption__Key` | yes | same value on otp-service and notification-service |
| `Smtp__Host`, `Smtp__Port`, `Smtp__UserName`, `Smtp__Password`, `Smtp__EnableSsl` | yes | `EnableSsl=true` for STARTTLS |
| `RabbitMq__HostName`, `RabbitMq__Port`, `RabbitMq__UserName`, `RabbitMq__Password` | yes | |
| `ASPNETCORE_ENVIRONMENT` | yes | `Production` |

## Database migrations

Set `Database:ApplyMigrationsOnStartup=false` in production and run migrations as an explicit deploy step:

```bash
dotnet ef database update --project backend/tuition-service/TuitionService.Infrastructure --connection "$TuitionDb"
dotnet ef database update --project backend/otp-service/OtpService.Infrastructure --connection "$OtpDb"
dotnet ef database update --project backend/notification-service/NotificationService.Infrastructure --connection "$NotificationDb"
```

## Runtime notes

- `ASPNETCORE_URLS` sets the listen address (defaults: 8082/8084/8085 in dev launch settings).
- Liveness (`/health/live`) must not depend on external services; readiness (`/health/ready`) verifies PostgreSQL/RabbitMQ.
- Use a process supervisor or container orchestrator to restart services; both RabbitMQ consumers and the outbox relay are crash-recoverable.

## TLS

- SMTP: STARTTLS (`Smtp__EnableSsl=true`).
- PostgreSQL: `SSL Mode=Require`.
- Service-to-service: terminate TLS at an ingress/gateway and use mTLS or network policies between services in production.

## Key rotation

- `Security__EventEncryption__Key`: generate a new key (`AesGcmCrypto.GenerateKey()`), deploy to both services in a coordinated rollout, then re-issue any OTPs still pending under the old key.
- `Security__InternalApi__ApiKey`: rotate with a short overlap window so in-flight requests succeed.

## Smoke check

```bash
curl http://localhost:8082/health/ready
curl "http://localhost:8082/api/v1/tuitions?mssv=521H0001"
```

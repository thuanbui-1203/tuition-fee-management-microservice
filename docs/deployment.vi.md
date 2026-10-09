**Ngôn ngữ:** [English](deployment.md) · **Tiếng Việt**

# Triển khai

## Build & publish

```bash
dotnet publish backend/tuition-service/TuitionService.Api -c Release -o out/tuition-service
dotnet publish backend/otp-service/OtpService.Api -c Release -o out/otp-service
dotnet publish backend/notification-service/NotificationService.Api -c Release -o out/notification-service
```

## Cấu hình (production)

Mọi giá trị dưới đây phải được cấp qua biến môi trường hoặc secret manager — không bao giờ commit chúng.

| Biến | Bắt buộc | Ghi chú |
|---|---|---|
| `ConnectionStrings__TuitionDb` | có | thêm `SSL Mode=Require` cho PostgreSQL quản lý |
| `ConnectionStrings__OtpDb` | có | |
| `ConnectionStrings__NotificationDb` | có | |
| `Security__InternalApi__ApiKey` | có | thay bằng mTLS/JWT trong môi trường siết chặt |
| `Security__EventEncryption__Key` | có | cùng giá trị ở otp-service và notification-service |
| `Smtp__Host`, `Smtp__Port`, `Smtp__UserName`, `Smtp__Password`, `Smtp__EnableSsl` | có | `EnableSsl=true` cho STARTTLS |
| `RabbitMq__HostName`, `RabbitMq__Port`, `RabbitMq__UserName`, `RabbitMq__Password` | có | |
| `ASPNETCORE_ENVIRONMENT` | có | `Production` |

## Migration database

Đặt `Database:ApplyMigrationsOnStartup=false` ở production và chạy migration như một bước deploy tường minh:

```bash
dotnet ef database update --project backend/tuition-service/TuitionService.Infrastructure --connection "$TuitionDb"
dotnet ef database update --project backend/otp-service/OtpService.Infrastructure --connection "$OtpDb"
dotnet ef database update --project backend/notification-service/NotificationService.Infrastructure --connection "$NotificationDb"
```

## Lưu ý khi chạy

- `ASPNETCORE_URLS` đặt địa chỉ lắng nghe (mặc định dev: 8082/8084/8085 trong launch settings).
- Liveness (`/health/live`) không được phụ thuộc service ngoài; readiness (`/health/ready`) kiểm tra PostgreSQL/RabbitMQ.
- Dùng process supervisor hoặc orchestrator để restart service; cả consumer RabbitMQ và outbox relay đều phục hồi được sau crash.

## TLS

- SMTP: STARTTLS (`Smtp__EnableSsl=true`).
- PostgreSQL: `SSL Mode=Require`.
- Service-to-service: kết thúc TLS ở ingress/gateway và dùng mTLS hoặc network policy giữa các service ở production.

## Xoay khoá

- `Security__EventEncryption__Key`: sinh khoá mới (`AesGcmCrypto.GenerateKey()`), triển khai tới cả hai service theo một đợt phối hợp, rồi phát hành lại các OTP còn đang chờ dưới khoá cũ.
- `Security__InternalApi__ApiKey`: xoay vòng với một khoảng chồng lấn ngắn để các request đang xử lý vẫn thành công.

## Kiểm tra nhanh (smoke check)

```bash
curl http://localhost:8082/health/ready
curl "http://localhost:8082/api/v1/tuitions?mssv=521H0001"
```

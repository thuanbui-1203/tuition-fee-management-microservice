**Ngôn ngữ:** [English](README.md) · **Tiếng Việt**

# iBanking — Thanh toán học phí · Microservices .NET

Bản triển khai chất lượng production gồm ba microservice cho **phân hệ đóng học phí của ứng dụng iBanking**, đã chuyển từ thiết kế Java/Spring ban đầu sang **.NET 10**:

| Service | Cổng | Sở hữu | Vai trò |
|---|---|---|---|
| `tuition-service` | 8082 | `students`, `tuition_fees` | Tra cứu học phí còn nợ, claim/release một khoản phí theo cách nguyên tử |
| `otp-service` | 8084 | `otp_codes`, `outbox_messages` | Vòng đời OTP (issue/verify), rate limiting, outbox → RabbitMQ |
| `notification-service` | 8085 | `email_logs` | Tiêu thụ sự kiện email từ RabbitMQ, gửi qua SMTP |

Tài liệu thiết kế gốc nằm trong [`docs/`](docs/README.vi.md). Các service `payment-service` (điều phối saga), `user-service` và `gateway` **nằm ngoài phạm vi** bản .NET này và được mô tả như các điểm tích hợp.

---

## 1. Tổng quan dự án

Phân hệ cho phép người dùng ngân hàng thanh toán học phí cho một sinh viên bất kỳ (tra theo `MSSV`). Luồng như sau:

1. `payment-service` (không triển khai ở đây) tra cứu khoản phí qua `tuition-service`.
2. Gọi `otp-service` sinh OTP; service này phát sự kiện `OtpRequested` (OTP mã hoá bằng AES-256-GCM) qua outbox giao dịch.
3. `notification-service` tiêu thụ sự kiện và gửi email chứa OTP.
4. Khi xác nhận, `payment-service` xác thực OTP, `claim` khoản phí (cập nhật có điều kiện nguyên tử) và trừ tiền.
5. Sự kiện `PaymentSucceeded` được phát (outbox) và `notification-service` gửi email xác nhận.

Các bảo đảm chính được ép buộc trong code và ở database:

- Một khoản học phí chỉ có thể **được trả đúng một lần** (`UPDATE … WHERE status = 'UNPAID'` nguyên tử).
- OTP **ngẫu nhiên bằng mật mã**, chỉ lưu ở dạng **hash**, **hết hạn sau 5 phút**, **dùng một lần**, **gắn với một giao dịch**, và **bị khoá sau 5 lần nhập sai**.
- Email **không bao giờ gửi hai lần** (khử trùng theo `message_id`).
- Outbox + relay đảm bảo **không mất sự kiện** giữa lúc commit DB và lúc publish lên broker.

## 2. Kiến trúc

```
Client → payment-service (saga)
              ├── REST ──> tuition-service ──> tuition_db
              ├── REST ──> otp-service ──> otp_db
              │                 └── outbox → RabbitMQ ──> notification-service ──> SMTP
              └── (user-service, ngoài phạm vi)
```

- **Đồng bộ**: REST nội bộ (`/internal/**`) với xác thực service-to-service.
- **Bất đồng bộ**: RabbitMQ (topic exchange bền + queue + dead-letter), outbox relay trong `otp-service`, consumer idempotent trong `notification-service`.
- **Sở hữu dữ liệu**: mỗi service có database PostgreSQL riêng; tham chiếu xuyên service chỉ là id logic.

Xem [`docs/architecture.md`](docs/architecture.md) để biết chi tiết.

## 3. Yêu cầu môi trường

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (LTS; `global.json` ghim SDK)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (cho hạ tầng và test tích hợp)
- Không cần cài PostgreSQL/RabbitMQ/SMTP cục bộ — tất cả chạy trong Docker.

## 4. Biến môi trường

Toàn bộ cấu hình đều strongly-typed. Giá trị mặc định cho dev nằm trong `appsettings.Development.json`; giá trị production phải cấp qua biến môi trường (xem [`.env.example`](.env.example)).

| Biến | Dùng bởi | Ghi chú |
|---|---|---|
| `ConnectionStrings__TuitionDb` | tuition-service | Chuỗi kết nối Npgsql |
| `ConnectionStrings__OtpDb` | otp-service | Chuỗi kết nối Npgsql |
| `ConnectionStrings__NotificationDb` | notification-service | Chuỗi kết nối Npgsql |
| `Security__InternalApi__ApiKey` | tuition, otp | Bí mật chia sẻ cho xác thực `/internal/**` |
| `Security__EventEncryption__Key` | otp, notification | Khoá AES-256-GCM dạng base64 (phải trùng ở cả hai bên) |
| `Smtp__Host`, `Smtp__Port`, `Smtp__UserName`, `Smtp__Password`, `Smtp__EnableSsl` | notification | Máy chủ SMTP |
| `RabbitMq__*` | otp, notification | Kết nối broker |

> Ở production, API key nội bộ và khoá mã hoá sự kiện phải được xoay vòng và cấp từ secret manager (xem [`docs/security.md`](docs/security.md)).

## 5. Thiết lập môi trường phát triển cục bộ

```bash
# 1. Khởi động hạ tầng (PostgreSQL, RabbitMQ, MailHog)
docker compose up -d

# 2. Build
dotnet build backend/Microservices.sln

# 3. Chạy từng service (mỗi terminal riêng)
dotnet run --project backend/tuition-service/TuitionService.Api
dotnet run --project backend/otp-service/OtpService.Api
dotnet run --project backend/notification-service/NotificationService.Api
```

Ở môi trường `Development`, mỗi service tự áp dụng migration EF Core khi khởi động (`Database:ApplyMigrationsOnStartup = true`).

## 6. Thiết lập Docker

`docker-compose.yml` khởi động ba container:

| Container | Image | Cổng | Mục đích |
|---|---|---|---|
| `postgres` | `postgres:16-alpine` | 5432 | Một database cho mỗi service (tạo bởi `docker/initdb`) |
| `rabbitmq` | `rabbitmq:3-management` | 5672, 15672 | Event bus (UI: guest/guest) |
| `mailhog` | `mailhog/mailhog` | 1025, 8025 | Bắt email cục bộ (UI: http://localhost:8025) |

Redis được cố ý bỏ qua — TTL/rate-limit của OTP do PostgreSQL + rate limiter in-memory đảm nhiệm; chỉ thêm Redis khi cần rate-limit cho nhiều instance (xem [`docs/security.md`](docs/security.md)).

## 7. Thiết lập cơ sở dữ liệu

- `docker/initdb/01-create-databases.sql` tạo `tuition_db`, `otp_db`, `notification_db` (kèm `user_db`/`payment_db` làm chỗ giữ chỗ).
- Mỗi service sở hữu database riêng và áp dụng migration của chính nó.

## 8. Migration

```bash
# Áp dụng migration tường minh (production):
dotnet ef database update --project backend/tuition-service/TuitionService.Infrastructure
dotnet ef database update --project backend/otp-service/OtpService.Infrastructure
dotnet ef database update --project backend/notification-service/NotificationService.Infrastructure

# Thêm migration mới (ví dụ):
dotnet ef migrations add AddColumn --project backend/tuition-service/TuitionService.Infrastructure -o Persistence/Migrations
```

`dotnet-ef` được cài như local tool (xem `backend/dotnet-tools.json`); chạy bằng `dotnet tool run dotnet-ef …`.

## 9. Chạy từng service

| Service | Câu lệnh | Swagger |
|---|---|---|
| tuition-service | `dotnet run --project backend/tuition-service/TuitionService.Api` | http://localhost:8082/swagger |
| otp-service | `dotnet run --project backend/otp-service/OtpService.Api` | http://localhost:8084/swagger |
| notification-service | `dotnet run --project backend/notification-service/NotificationService.Api` | http://localhost:8085/swagger |

## 10. Đường dẫn Swagger

Xem bảng trên. API công khai nằm dưới `/api/v1/**`; API nội bộ dưới `/internal/**` (không bao giờ route qua gateway).

## 11. Đường dẫn health check

Mỗi service cung cấp:

| Endpoint | Ý nghĩa |
|---|---|
| `GET /health/live` | Liveness — tiến trình còn sống (không kiểm tra phụ thuộc) |
| `GET /health/ready` | Readiness — PostgreSQL (và RabbitMQ ở nơi dùng) kết nối được |
| `GET /health` | Tổng hợp |

## 12. Cấu hình RabbitMQ

- Exchange: `email.events` (topic, bền).
- Queue: `notification.email` (bền) gắn với routing key `OtpRequested` và `PaymentSucceeded`.
- Dead-letter: `email.events.dlx` → `notification.email.dlq`.
- Message là persistent và publish kèm publisher confirms; consumer ack/nack thủ công với `prefetch=1`.

## 13. Cấu hình SMTP / MailHog

Môi trường dev dùng MailHog (`localhost:1025`, không auth, không TLS). SMTP production cấu hình qua `Smtp__*` với `Smtp__EnableSsl=true` (STARTTLS). Xem [`docs/deployment.md`](docs/deployment.md).

## 14. Cấu hình Redis

Không bắt buộc. Rate limiter một instance (`FixedWindowRateLimiter`) là mặc định; thay bằng `IRateLimiter` dựa trên Redis khi cần mở rộng ngang.

## 15. Chạy test

```bash
# Unit test (không cần Docker)
dotnet test backend/Microservices.sln --filter "Category=Unit"

# Integration test (cần Docker: PostgreSQL/RabbitMQ qua Testcontainers)
dotnet test backend/Microservices.sln --filter "Category=Integration"
```

## 16. Chạy test tích hợp

Test tích hợp dùng **Testcontainers** (PostgreSQL và RabbitMQ thật) và bao gồm các bộ test concurrency:

- `ConcurrentClaim_ExactlyOneSucceeds` (tuition, 10 luồng claim đồng thời → 1 thành công).
- `ConcurrentVerify_ExactlyOneSucceeds` (otp, 10 luồng verify đồng thời → 1 thành công).
- `ConcurrentIssue_LeavesExactlyOneActiveOtp` (otp).

Xem [`docs/testing.md`](docs/testing.md).

## 17. Ví dụ gọi API

```bash
# Tra cứu công khai
curl "http://localhost:8082/api/v1/tuitions?mssv=521H0001"

# Claim nội bộ (cần API key nội bộ)
curl -X POST "http://localhost:8082/internal/tuitions/10/claim" \
  -H "Content-Type: application/json" \
  -H "X-Internal-Api-Key: dev-internal-key-change-me" \
  -d '{"transactionId":"550e8400-e29b-41d4-a716-446655440000"}'

# Issue OTP nội bộ
curl -X POST "http://localhost:8084/internal/otp/issue" \
  -H "Content-Type: application/json" \
  -H "X-Internal-Api-Key: dev-internal-key-change-me" \
  -H "Idempotency-Key: 11111111-1111-1111-1111-111111111111" \
  -d '{"transactionId":"550e8400-e29b-41d4-a716-446655440000","email":"a@tdtu.edu.vn"}'
```

## 18. Lỗi thường gặp

| Mã | HTTP | Khi nào |
|---|---|---|
| `INVALID_INPUT` | 400 | Thiếu/sai định dạng đầu vào |
| `UNAUTHENTICATED` | 401 | Thiếu/sai API key nội bộ |
| `NOT_FOUND` | 404 | Không tìm thấy MSSV/khoản phí/OTP |
| `FEE_ALREADY_PAID` | 409 | Khoản phí đã được giao dịch khác trả |
| `OTP_ALREADY_USED` | 409 | OTP đã dùng lại |
| `OTP_EXPIRED` | 410 | OTP quá 5 phút |
| `OTP_LOCKED` | 409 | Sai 5 lần |
| `IDEMPOTENCY_CONFLICT` | 409 | Dùng lại idempotency key với payload khác |
| `RATE_LIMITED` | 429 | Gửi quá nhiều yêu cầu issue/verify |
| `INTERNAL_ERROR` | 500 | Lỗi không lường trước |
| `DEPENDENCY_DOWN` | 503 | Phụ thuộc phía sau không khả dụng |

Xem [`docs/error-handling.md`](docs/error-handling.md).

## 19. Lưu ý bảo mật

- OTP được sinh bằng `RandomNumberGenerator` và chỉ lưu dưới dạng hash PBKDF2.
- OTP dạng plaintext chỉ đi tới nơi gửi email **bên trong outbox**, mã hoá bằng AES-256-GCM.
- `/internal/**` yêu cầu API key chia sẻ (so sánh constant-time); production nên dùng mTLS/JWT ngắn hạn.
- Không có bí mật trong source control; không log giá trị nhạy cảm; nội dung email không bao giờ được lưu.

Xem [`docs/security.md`](docs/security.md).

## 20. Lưu ý triển khai production

- Đặt `Database:ApplyMigrationsOnStartup=false`; chạy `dotnet ef database update` như một bước deploy.
- Cấp toàn bộ bí mật qua biến môi trường / secret manager.
- Bật TLS cho SMTP (`Smtp__EnableSsl=true`) và SSL cho kết nối PostgreSQL.
- Thay API key chia sẻ bằng mTLS hoặc JWT service ngắn hạn.

Xem [`docs/deployment.md`](docs/deployment.md).

## Cấu trúc repo

```
backend/
  Microservices.sln
  Common/Microservices.Common/        # lỗi/result dùng chung, AES-GCM, middleware, rate limiting
  tuition-service/{Domain,Application,Infrastructure,Api}/
  otp-service/{Domain,Application,Infrastructure,Api}/
  notification-service/{Domain,Application,Infrastructure,Api}/
  tests/{...UnitTests,...IntegrationTests}/   # 6 project test
docker-compose.yml
docker/initdb/
.env.example
docs/
```

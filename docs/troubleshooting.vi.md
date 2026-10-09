**Ngôn ngữ:** [English](troubleshooting.md) · **Tiếng Việt**

# Khắc phục sự cố

## PostgreSQL không khả dụng

- Kiểm tra `docker compose ps` — `postgres` có healthy không?
- Xác nhận connection string khớp (`Host=localhost;Port=5432;…`).
- Kiểm tra `/health/ready` — nó báo cáo khả năng kết nối PostgreSQL.

## Migration thất bại

- Chạy `dotnet ef database update --project backend/<service>/<Service>.Infrastructure` và đọc lỗi.
- Nếu một migration được áp dụng một phần, kiểm tra bảng `__EFMigrationsHistory`.
- Thay đổi schema cần một migration mới — ứng dụng không bao giờ tự sinh schema.

## RabbitMQ không khả dụng

- `docker compose ps` → `rabbitmq` có healthy không?
- `notification-service` và `otp-service` tự kết nối lại (backoff 5s); outbox tiếp tục thử lại nên không mất sự kiện.
- Kiểm tra `/health/ready` và dead-letter queue `notification.email.dlq` trong RabbitMQ UI (http://localhost:15672).

## SMTP không khả dụng / email không được gửi

- MailHog có chạy không? Mở http://localhost:8025 và kiểm tra message.
- Kiểm tra `email_logs`: chuyển trạng thái `status` (`SENDING → SENT/FAILED`) và `error_code`.
  - `SMTP_AUTH_FAILED` → sai credential (vĩnh viễn).
  - `SMTP_UNAVAILABLE`/`SMTP_TRANSIENT` → SMTP down (tạm thời).
  - `TRANSIENT_EXHAUSTED` → hết lượt retry → message nằm trong dead-letter queue.

## Không nhận được email OTP

1. Xác nhận `notification-service` đang chạy và tiêu thụ (queue `notification.email`).
2. Xác nhận relay của `otp-service` đã publish sự kiện (`outbox_messages.status = SENT`).
3. Kiểm tra MailHog. Nếu payload OTP giải mã thất bại (`INVALID_INPUT` trong `email_logs`), `Security:EventEncryption:Key` khác nhau giữa hai service — đặt cùng giá trị ở cả hai.

## OTP hết hạn

Vòng đời OTP là `Otp:TtlSeconds` (mặc định 300). Mã hết hạn trả `410 OTP_EXPIRED`; phát hành OTP mới cho giao dịch.

## Message trùng / email trùng

Đây là thiết kế có chủ đích: `email_logs.message_id` là duy nhất, nên một sự kiện được gửi lại sẽ được ack mà không gửi email lần hai.

## Deadlock database / xung đột concurrency

- Claim và chuyển trạng thái OTP là các câu `UPDATE` nguyên tử đơn lẻ, không đọc-rồi-ghi ở tầng ứng dụng, giúp giảm nguy cơ deadlock.
- Nếu vẫn gặp `40001` tạm thời (serialization/deadlock), thử lại thao tác với một request mới.

## JWT sai / xác thực service-to-service thất bại

Bản .NET này dùng API key chia sẻ (không phải JWT) cho `/internal/**`:

- Thiếu/sai `X-Internal-Api-Key` → `401 UNAUTHENTICATED`.
- Đảm bảo `Security:InternalApi:ApiKey` được đặt và khớp ở cả hai phía.
- (Thiết kế gốc dùng JWT do gateway cấp cho xác thực người dùng công khai; phần đó thuộc phạm vi `payment-service`/gateway.)

## Consumer restart / service crash

- Relay của `otp-service` publish lại các outbox message PENDING khi khởi động lại (`messageId` ổn định → dedup).
- `notification-service` kết nối lại và xử lý lại các message chưa ack; message trùng được khử.

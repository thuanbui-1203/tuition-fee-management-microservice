**Ngôn ngữ:** [English](security.md) · **Tiếng Việt**

# Bảo mật

## Xác thực & phân quyền

- **API nội bộ** (`/internal/**`) yêu cầu một credential service chia sẻ trong header `X-Internal-Api-Key`, so sánh theo thời gian hằng (constant time, `CryptographicOperations.FixedTimeEquals`). Endpoint nội bộ không bao giờ lộ qua gateway.
- Ở production nên thay khoá chia sẻ bằng **mTLS** hoặc **JWT service ngắn hạn** — middleware được tách riêng để có thể thay credential transport mà không đụng tới code nghiệp vụ.
- Các định danh xuyên service (`transactionId`, `feeId`) được kiểm tra và không bao giờ tin tưởng từ caller chưa xác thực. Kiểm tra quyền sở hữu được giới hạn theo giao dịch gọi (ví dụ `release` chỉ cho giao dịch đã claim khoản phí).

## Bí mật

- Không commit bí mật. `appsettings.json` gốc để placeholder rỗng; giá trị chỉ dùng cho dev nằm trong `appsettings.Development.json` và được ghi rõ là không dùng cho production.
- Bí mật production lấy từ biến môi trường hoặc secret manager. Xem `.env.example` và `docs/deployment.md`.

## Xử lý OTP

- Sinh: `RandomNumberGenerator` (`CryptoSecureOtpGenerator`) — không bao giờ dùng `Random`, `Guid` hay timestamp.
- Lưu trữ: PBKDF2-HMAC-SHA256 với salt 128-bit mới cho mỗi bản ghi; lưu ở `otp_codes.code_hash`. OTP plaintext **không bao giờ** được lưu.
- Truyền: OTP plaintext chỉ đi tới nơi gửi email bên trong outbox, mã hoá bằng **AES-256-GCM** (mã hoá có xác thực). Khoá (`Security:EventEncryption:Key`) chỉ được chia sẻ giữa `otp-service` và `notification-service`.
- Xác thực: so sánh hash theo thời gian hằng; lỗi xác thực chung chung (không tiết lộ thông tin về OTP của giao dịch khác).

## Hash vs mã hoá

| Dữ liệu | Xử lý | Vì sao |
|---|---|---|
| OTP khi lưu (`code_hash`) | **Hash** (PBKDF2) | xác thực một chiều; không được đảo ngược |
| OTP trong sự kiện outbox | **Mã hoá** (AES-256-GCM) | phải khôi phục được để gửi email; có xác thực để chống sửa đổi |
| Nội dung email | **Không lưu** | giảm thiểu dữ liệu nhạy cảm khi lưu |

Khoá được lưu trong cấu hình/secret manager. Xoay vòng: sinh khoá 32 byte mới (`AesGcmCrypto.GenerateKey()`), cập nhật cả hai service đồng thời, và vô hiệu hoá/phát hành lại các OTP đang mã hoá dưới khoá cũ. Ghi rõ chính sách lưu trữ cho `email_logs` (chỉ metadata) nếu cần kiểm toán.

## Bảo mật email

- Địa chỉ người nhận được kiểm tra trước khi gửi.
- SMTP dùng MailKit; `EnableSsl=true` bật STARTTLS ở production.
- Header chỉ được dựng từ template cố định + dữ liệu đã kiểm tra — không có header SMTP do người dùng điều khiển, nên không thể chèn header.
- Credential SMTP không bao giờ được log.

## Rate limiting / chống lạm dụng

- Phát hành và xác thực OTP được rate-limit theo từng giao dịch (mục `RateLimiting` cấu hình được).
- `FixedWindowRateLimiter` mặc định an toàn luồng và phù hợp một instance; dùng `IRateLimiter` dựa trên Redis khi triển khai nhiều instance.

## Ghi log

Không bao giờ log giá trị nhạy cảm: OTP, hash OTP, mật khẩu, credential SMTP, header JWT/authorization, và connection string. Log chỉ mang `traceId`/`correlationId`, thao tác, trạng thái và mã lỗi.

## TLS / production

- SMTP qua STARTTLS (production).
- Kết nối PostgreSQL hỗ trợ SSL (thêm `SSL Mode=Require` vào connection string ở production).
- Lưu lượng service-to-service nên dùng mTLS hoặc network policy ở production.

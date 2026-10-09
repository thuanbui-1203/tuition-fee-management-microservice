**Ngôn ngữ:** [English](testing.md) · **Tiếng Việt**

# Kiểm thử

Hai tầng, khớp với cách tách của dự án gốc: **unit test** (không cần hạ tầng) và **integration test** (PostgreSQL/RabbitMQ thật qua Testcontainers).

## Cách chạy

```bash
# Unit test (không cần Docker)
dotnet test backend/Microservices.sln --filter "Category=Unit"

# Integration test (cần Docker)
dotnet test backend/Microservices.sln --filter "Category=Integration"
```

> Integration test dùng `Testcontainers.PostgreSql`/`Testcontainers.RabbitMq` và yêu cầu Docker engine đang chạy.

## Unit test

| Bộ test | Phạm vi |
|---|---|
| `TuitionService.UnitTests` | kiểm tra MSSV; lookup (có/không tìm thấy/không có phí/đã trả/nhiều khoản); claim (thành công/không tìm thấy/đã trả/idempotent); release (thành công/no-op/sai giao dịch/không tìm thấy) |
| `OtpService.UnitTests` | generator (độ dài/chữ số/độ đa dạng); hasher PBKDF2 (round-trip, sai mã, salt duy nhất, sai định dạng); verify (đúng/sai/hết hạn/đã dùng/đã khoá/sai giao dịch/khoá sau 5 lần/rate-limit/thiếu); issue (thành công/replay/xung đột/email sai) |
| `NotificationService.UnitTests` | content builder (giải mã OTP, định dạng thanh toán, sự kiện không hỗ trợ, sai khoá); xử lý (dedup, gửi thành công, lỗi vĩnh viễn, retry tạm thời → dead-letter, người nhận sai) |

Các fake là implementation in-memory viết tay của các interface repository/port — không cần framework mock, không cần database.

## Integration test

| Bộ test | Điểm nổi bật |
|---|---|
| `TuitionService.IntegrationTests` | migration chạy; ràng buộc CHECK `amount > 0` và UNIQUE `mssv` được ép buộc; **10 luồng claim đồng thời → đúng 1 thành công**; từ chối đã trả; replay idempotent; xung đột release/release bởi chủ; vòng đời lookup |
| `OtpService.IntegrationTests` | migration chạy; mark-used nguyên tử; **10 luồng verify đồng thời → đúng 1 thành công**; **4 luồng issue đồng thời → đúng 1 OTP hoạt động**; issue ghi một bản ghi outbox mà payload giải mã ra OTP 6 chữ số |
| `NotificationService.IntegrationTests` | migration chạy; gửi thành công lưu trạng thái `SENT`; **`messageId` trùng được khử ở database** (chỉ gửi một lần) |

## Test concurrency

Concurrency được chứng minh trên **PostgreSQL thật** (không dùng database in-memory, vì nó không tái hiện đúng hành vi khoá/ràng buộc của Postgres):

- **Kịch bản B** (nhiều người trả, một khoản phí): `ConcurrentClaim_ExactlyOneSucceeds` khẳng định đúng một claim thành công và đúng một bản ghi `PAID`.
- **OTP dùng một lần**: `ConcurrentVerify_ExactlyOneSucceeds` khẳng định đúng một lần verify thành công.
- **Bất biến một OTP hoạt động**: `ConcurrentIssue_LeavesExactlyOneActiveOtp`.

## Phần chưa bao phủ

- Test E2E giao diện (Playwright) và bộ sưu tập Postman/Newman vẫn thuộc kế hoạch gốc (`docs/08-testing.md`) và nằm ngoài phạm vi bản backend .NET này.
- Test saga đầy đủ cho `payment-service` nằm ngoài phạm vi vì service đó không được triển khai ở đây.

**Ngôn ngữ:** [English](architecture.md) · **Tiếng Việt**

# Kiến trúc

Tài liệu này mô tả kiến trúc của ba microservice .NET đã triển khai và mối liên hệ với thiết kế iBanking gốc.

## Bối cảnh hệ thống

```
Client
  |
  v
Payment Service (điều phối saga — KHÔNG triển khai trong repo này)
  |-- REST (đồng bộ) -------------------> Tuition Service  (tuition_db)
  |-- REST (đồng bộ) -------------------> OTP Service      (otp_db)
  |-- REST (đồng bộ) -------------------> User Service     (user_db, ngoài phạm vi)
  |-- outbox + RabbitMQ (bất đồng bộ) --> Notification Service (notification_db) --> SMTP
```

## Trách nhiệm service và quyền sở hữu dữ liệu

| Service | Sở hữu | API đồng bộ | Vai trò bất đồng bộ |
|---|---|---|---|
| tuition-service | `students`, `tuition_fees` | `GET /api/v1/tuitions`, `POST /internal/tuitions/{feeId}/claim`, `POST /internal/tuitions/{feeId}/release` | — |
| otp-service | `otp_codes`, `outbox_messages` | `POST /internal/otp/issue`, `POST /internal/otp/verify` | phát `OtpRequested` (outbox → RabbitMQ) |
| notification-service | `email_logs` | (không có — chỉ consumer) | consumer của `OtpRequested` và `PaymentSucceeded` |

Mỗi service có database riêng. Tham chiếu xuyên service là **định danh logic** (ví dụ `tuition_fees.paid_transaction_id`, `otp_codes.transaction_id`) — không có khoá ngoại xuyên database.

## Phân tầng

Mọi service đi theo cùng một kiến trúc clean:

```
Api            transport HTTP, controller, middleware, health check, Swagger
Application    use case, DTO, validation, interface, options
Domain         entity, enum, quy tắc nghiệp vụ (không phụ thuộc ngoài)
Infrastructure EF Core, migration, repository, RabbitMQ, SMTP, outbox relay
```

Chiều phụ thuộc nghiêm ngặt: `Api → Application → Domain` và `Api → Infrastructure → {Application, Domain}`. Controller không chứa nghiệp vụ; repository không chứa quy tắc nghiệp vụ ngoài việc lưu trữ nguyên tử.

## Giao tiếp đồng bộ

Các endpoint nội bộ nằm dưới `/internal/**` và yêu cầu xác thực service-to-service (API key chia sẻ ở dev; xem `docs/security.md`). Chúng không bao giờ được lộ qua gateway.

- **Claim học phí** là một cập nhật có điều kiện nguyên tử:
  ```sql
  UPDATE tuition_fees SET status='PAID', paid_transaction_id=@txn, updated_at=@now
  WHERE id=@feeId AND status='UNPAID';
  ```
  Số dòng = `0` được phân loại là *không tìm thấy*, *đã được giao dịch khác trả*, hoặc *replay idempotent của chính giao dịch đó*.

- **Release học phí** (compensation của saga) chỉ thành công khi khoản phí đang do chính giao dịch release giữ:
  ```sql
  UPDATE tuition_fees SET status='UNPAID', paid_transaction_id=NULL, updated_at=@now
  WHERE id=@feeId AND status='PAID' AND paid_transaction_id=@txn;
  ```

- **Verify OTP** đánh dấu mã đã dùng một cách nguyên tử:
  ```sql
  UPDATE otp_codes SET status='USED', used_at=@now
  WHERE id=@id AND transaction_id=@txn AND status='ACTIVE' AND expires_at > @now;
  ```

## Giao tiếp bất đồng bộ (RabbitMQ)

- Topic exchange bền `email.events`, queue bền `notification.email`, exchange/queue dead-letter.
- Message là persistent và publish kèm publisher confirms.
- Consumer ack/nack thủ công với `prefetch=1`; lỗi tạm thời được retry với exponential backoff có giới hạn, sau đó vào dead-letter.

### Phong bì sự kiện

```json
{
  "messageId": "…guid…",
  "eventType": "OtpRequested | PaymentSucceeded",
  "occurredAt": "2026-…Z",
  "correlationId": "…",
  "transactionId": "…",
  "recipient": "a@tdtu.edu.vn",
  "payload": { }
}
```

`messageId` là khoá khử trùng; consumer của notification ép buộc tính duy nhất bằng một unique index ở database.

## Mẫu Outbox

`otp-service` ghi sự kiện `OtpRequested` vào `outbox_messages` **trong cùng transaction database** với lần chèn OTP. Một relay chạy nền publish các message PENDING và đánh dấu SENT. Nếu tiến trình crash giữa lúc commit và publish, sự kiện được publish lại sau — với cùng `messageId`, nên consumer khử trùng được.

## Saga và compensation (hợp đồng tích hợp)

Toàn bộ saga thanh toán do `payment-service` điều phối (ngoài phạm vi). Các service đã triển khai phơi ra đúng những primitive mà nó cần:

1. `claim(feeId, transactionId)` — giữ chỗ khoản phí.
2. trừ tiền tài khoản (user-service, ngoài phạm vi).
3. nếu trừ tiền thất bại → `release(feeId, transactionId)` để bù trừ.

`release` an toàn vì nó giới hạn theo giao dịch đã claim, nên một giao dịch bù trừ không bao giờ giải phóng được khoản phí mà giao dịch khác đã trả.

## Idempotency

| Thao tác | Cơ chế |
|---|---|
| Claim học phí | `paid_transaction_id` — replay cùng giao dịch trả về thành công |
| Release học phí | release khoản phí đã UNPAID là no-op |
| Phát hành OTP | header `Idempotency-Key` → cột `idempotency_key` duy nhất |
| Xác thực OTP | dùng một lần; retry được xử lý bởi trạng thái giao dịch của orchestrator |
| Tiêu thụ notification | `message_id` duy nhất trên `email_logs` |

## Chiến lược concurrency

- **Kịch bản B (hai người trả cùng một khoản phí)** — claim có điều kiện nguyên tử bảo đảm đúng một thành công; được chứng minh bằng test tích hợp với 10 luồng claim đồng thời trên PostgreSQL thật.
- **Verify OTP đồng thời** — mark-used nguyên tử bảo đảm đúng một thành công.
- **Phát hành OTP đồng thời** — advisory lock của PostgreSQL theo `transaction_id` cộng partial unique index (`status='ACTIVE'`) bảo đảm tối đa một OTP đang hoạt động cho mỗi giao dịch.

## Observability

- Log có cấu trúc kèm `traceId`/`correlationId` (truyền qua `X-Correlation-Id`).
- Health check: `/health/live`, `/health/ready`, `/health`.
- Không bao giờ log bí mật hay giá trị nhạy cảm.

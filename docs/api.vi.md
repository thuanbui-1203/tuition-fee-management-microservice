**Ngôn ngữ:** [English](api.md) · **Tiếng Việt**

# Tham chiếu API

Mọi endpoint trả về JSON. Lỗi dùng body thống nhất mô tả trong [`error-handling.md`](error-handling.md). API công khai nằm dưới `/api/v1`; API nội bộ dưới `/internal` và yêu cầu header `X-Internal-Api-Key`.

## tuition-service

### `GET /api/v1/tuitions?mssv={mssv}` — công khai

Tra cứu một sinh viên và khoản học phí còn nợ (UNPAID) duy nhất của họ.

- Xác thực: không yêu cầu cho demo phân hệ này (trong thực tế gateway sẽ ép buộc JWT người dùng).
- Tham số: `mssv` (bắt buộc, 1–20 ký tự chữ-số).

**200 OK**

```json
{
  "mssv": "521H0001",
  "studentName": "Trần Thị B",
  "fee": { "feeId": 10, "semester": "2024-2025/HK1", "amount": 8400000.00, "status": "UNPAID" }
}
```

**Lỗi**

| Status | Mã | Ý nghĩa |
|---|---|---|
| 400 | `INVALID_INPUT` | Thiếu hoặc sai MSSV |
| 404 | `NOT_FOUND` | Không tìm thấy MSSV, hoặc sinh viên không có học phí |
| 409 | `FEE_ALREADY_PAID` | học phí của sinh viên đã PAID |

### `POST /internal/tuitions/{feeId}/claim` — nội bộ

Claim nguyên tử một khoản phí còn nợ cho một giao dịch.

- Auth: `X-Internal-Api-Key`.
- Body: `{ "transactionId": "uuid" }`.

**200 OK** → `{ "success": true }`

**Lỗi**: `404 NOT_FOUND`, `409 FEE_ALREADY_PAID` (đã được giao dịch khác trả). Replay cùng giao dịch là idempotent (`200`).

### `POST /internal/tuitions/{feeId}/release` — nội bộ

Release một khoản phí đã claim (compensation của saga). Chỉ giao dịch đang giữ khoản phí mới được release.

- Auth: `X-Internal-Api-Key`.
- Body: `{ "transactionId": "uuid" }`.

**200 OK** → `{ "success": true }`

**Lỗi**: `404 NOT_FOUND`, `409 RELEASE_CONFLICT` (đang do giao dịch khác giữ). Release một khoản phí đã UNPAID là no-op idempotent.

## otp-service

Mọi endpoint OTP đều là nội bộ.

### `POST /internal/otp/issue` — nội bộ

Phát hành OTP mới gắn với một giao dịch và đưa email OTP vào hàng đợi (outbox).

- Auth: `X-Internal-Api-Key`.
- Headers: `Idempotency-Key` (tuỳ chọn, nên dùng khi retry).
- Body: `{ "transactionId": "uuid", "email": "a@tdtu.edu.vn" }`.

**201 Created** (mới) / **200 OK** (replay idempotent) → `{ "otpId": 42 }`

**Lỗi**: `400 INVALID_INPUT`, `409 IDEMPOTENCY_CONFLICT` (cùng key, khác giao dịch), `429 RATE_LIMITED`.

### `POST /internal/otp/verify` — nội bộ

Xác thực một OTP cho một giao dịch. Khi thành công, OTP được đánh dấu USED một cách nguyên tử và không thể dùng lại.

- Auth: `X-Internal-Api-Key`.
- Body: `{ "transactionId": "uuid", "otp": "482913" }`.

**200 OK** → `{ "valid": true }`

**Lỗi**: `400 INVALID_INPUT`, `400 OTP_INCORRECT`, `404 NOT_FOUND` (chung chung — không có OTP cho giao dịch), `409 OTP_ALREADY_USED`, `409 OTP_LOCKED`, `410 OTP_EXPIRED`, `429 RATE_LIMITED`.

> Quá trình xác thực cố ý trả message chung chung; nó không bao giờ tiết lộ liệu có OTP cho một giao dịch *khác* hay không.

## notification-service

Không có endpoint nghiệp vụ REST. Nó chỉ phơi health check và tiêu thụ sự kiện từ RabbitMQ:

| Sự kiện | Routing key | Email |
|---|---|---|
| `OtpRequested` | `OtpRequested` | email OTP (OTP được giải mã từ payload AES-GCM) |
| `PaymentSucceeded` | `PaymentSucceeded` | xác nhận thanh toán |

## Endpoint health (mọi service)

| Endpoint | Ý nghĩa |
|---|---|
| `GET /health/live` | liveness (không phụ thuộc) |
| `GET /health/ready` | readiness (PostgreSQL, RabbitMQ ở nơi dùng) |
| `GET /health` | tổng hợp |

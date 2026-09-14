# Phase 3 — Thiết kế REST API

**Mục tiêu:** thiết kế đầy đủ REST API cho mọi chức năng (yêu cầu 3 của đề): xác định **URI**, **HTTP Method**, **Input/Request**, **Output/Response** và **HTTP Status Code** cho từng API.

**Đầu vào:** Phase 1 (UC), Phase 2 (service & giao tiếp).

**Sản phẩm bàn giao:** bảng API hoàn chỉnh (file này) + ví dụ JSON request/response + bảng mã lỗi; triển khai OpenAPI/Swagger ở Phase 5.

---

## 3.1. Quy ước chung

1. **Base path:** mọi API công khai: `/api/v1`. Endpoint nội bộ giữa service: `/internal/**` (chặn ở gateway).
2. **Authentication:** header `Authorization: Bearer <JWT>` do user-service cấp; gateway xác thực. `POST /auth/login` là API duy nhất không cần token.
3. **Idempotency:** mọi API **tạo/ghi** (`POST /payments`, `confirm`) nhận header `Idempotency-Key: <uuid>` — client tự sinh, gửi lại khi retry.
4. **Định dạng ngày giờ:** ISO-8601 (UTC), vd `"2025-09-09T04:00:00Z"`.
5. **Tiền tệ:** số (VND), không dùng float.
6. **Phân trang:** `GET /payments/me?page=0&size=20` → response có `{ content, page, size, totalElements }`.
7. **Response lỗi chuẩn:**
```json
{
  "code": "FEE_ALREADY_PAID",
  "message": "Học phí đã được thanh toán",
  "timestamp": "2025-09-09T04:00:00Z",
  "traceId": "a1b2c3"
}
```

## 3.2. Bảng mã lỗi dùng chung

| HTTP Status | Code (business) | Ý nghĩa | Dùng khi |
|---|---|---|---|
| 200 | — | OK | GET, POST trả kết quả |
| 201 | — | Created | tạo giao dịch |
| 400 | `INVALID_INPUT` | Sai định dạng/thiếu trường | MSSV rỗng, OTP rỗng… |
| 401 | `UNAUTHENTICATED` | Chưa có token / token sai | mọi API trừ login |
| 403 | `FORBIDDEN` | Không đủ quyền | user A xem giao dịch của user B |
| 404 | `NOT_FOUND` | Không tìm thấy | MSSV không tồn tại, paymentId sai |
| 409 | `FEE_ALREADY_PAID` | Xung đột nghiệp vụ — học phí đã trả | tra cứu/thanh toán fee PAID |
| 409 | `OTP_ALREADY_USED` | OTP đã dùng | dùng lại OTP cũ |
| 409 | `IDEMPOTENCY_CONFLICT` | Cùng Idempotency-Key nhưng payload khác | retry sai nội dung |
| 410 | `OTP_EXPIRED` | OTP hết hạn (> 5 phút) | xác nhận với OTP hết hạn |
| 422 | `INSUFFICIENT_BALANCE` | Số dư không đủ | thanh toán/xác nhận khi balance < amount |
| 422 | `TRANSACTION_INVALID_STATE` | Giao dịch ở trạng thái không cho phép | confirm giao dịch FAILED/SUCCESS cũ |
| 429 | `RATE_LIMITED` | Gửi lại OTP quá nhanh | resend OTP |
| 500 | `INTERNAL_ERROR` | Lỗi hệ thống | exception không lường trước |
| 503 | `DEPENDENCY_DOWN` | Service phụ thuộc không khả dụng | payment gọi user/tuition/otp lỗi |

## 3.3. Nhóm Auth (user-service)

**POST `/api/v1/auth/login`** — Đăng nhập (UC-01)
- Request:
```json
{ "username": "nguyenvana", "password": "secret123" }
```
- Response 200:
```json
{
  "token": "eyJhbGciOi...",
  "expiresAt": "2025-09-09T04:30:00Z",
  "user": { "id": 1, "username": "nguyenvana", "fullName": "Nguyễn Văn A", "email": "a@tdtu.edu.vn" }
}
```
- Lỗi: `400 INVALID_INPUT` (thiếu trường), `401 UNAUTHENTICATED` (sai username/password — message "Tên đăng nhập hoặc mật khẩu không đúng").

> Không có API đăng ký (đề không yêu cầu). Seed user trong Phase 4.

## 3.4. Nhóm User/Account (user-service) — UC-02

**GET `/api/v1/users/me`** — hồ sơ người dùng hiện tại
- Response 200:
```json
{ "id": 1, "username": "nguyenvana", "fullName": "Nguyễn Văn A", "phone": "0901234567", "email": "a@tdtu.edu.vn" }
```
- Lỗi: `401`.

**GET `/api/v1/users/me/accounts`** — số dư khả dụng
- Response 200: `{ "balance": 10000000, "currency": "VND" }`
- Lỗi: `401`.

## 3.5. Nhóm Tuition (tuition-service) — UC-03

**GET `/api/v1/tuitions?mssv={mssv}`** — tra cứu học phí còn nợ theo MSSV
- Request: query `mssv`, vd `?mssv=521H0001`
- Response 200 (fee còn UNPAID):
```json
{
  "mssv": "521H0001",
  "studentName": "Trần Thị B",
  "fee": { "feeId": 10, "semester": "2024-2025/HK1", "amount": 8400000, "status": "UNPAID" }
}
```
- Lỗi:
  - `400 INVALID_INPUT` — MSSV rỗng/sai định dạng
  - `404 NOT_FOUND` — "Không tìm thấy MSSV"
  - `409 FEE_ALREADY_PAID` — MSSV tồn tại nhưng học phí đã PAID (message: "Học phí đã được thanh toán") → UI chặn thanh toán.

> Thiết kế quy ước: chỉ trả về khoản UNPAID duy nhất cho MSSV (mỗi MSSV 1 khoản đang nợ trong kịch bản demo). Nếu muốn hỗ trợ nhiều khoản, đổi response thành mảng `fees[]` — ghi chú rõ trong báo cáo.

## 3.6. Nhóm Payment (payment-service) — UC-04, UC-05, UC-06

**POST `/api/v1/payments`** — khởi tạo giao dịch thanh toán (bước 6–7 của UC-04)
- Header: `Authorization`, `Idempotency-Key`
- Request:
```json
{ "mssv": "521H0001" }
```
- Hành vi phía server: tra cứu fee (tuition) → kiểm tra fee UNPAID + balance đủ → tạo `Transaction` (INITIATED→OTP_SENT) → gọi otp-service sinh OTP → gửi email.
- Response 201:
```json
{
  "paymentId": "550e8400-e29b-41d4-a716-446655440000",
  "amount": 8400000,
  "balanceAfterCheck": 10000000,
  "status": "OTP_SENT",
  "message": "Mã OTP đã được gửi đến email của bạn",
  "expiresInSeconds": 300
}
```
- Lỗi:
  - `400 INVALID_INPUT` — thiếu MSSV
  - `404 NOT_FOUND` — MSSV không tồn tại
  - `409 FEE_ALREADY_PAID` — học phí đã trả
  - `422 INSUFFICIENT_BALANCE` — balance < amount
  - `429 RATE_LIMITED` — tạo quá nhiều giao dịch trong thời gian ngắn

**POST `/api/v1/payments/{paymentId}/confirm`** — xác nhận bằng OTP (bước 8–15 UC-04)
- Header: `Authorization`, `Idempotency-Key`
- Request:
```json
{ "otp": "482913" }
```
- Hành vi: otp-service xác thực (đúng giao dịch + còn hạn + chưa dùng) → saga: claim fee → debit → SUCCESS → outbox email → trả kết quả.
- Response 200 (thành công):
```json
{
  "paymentId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "SUCCESS",
  "amount": 8400000,
  "paidAt": "2025-09-09T04:02:11Z",
  "newBalance": 1600000,
  "feeStatus": "PAID",
  "message": "Thanh toán học phí thành công"
}
```
- Lỗi (phải map chính xác từng trường hợp để UI hiện đúng thông báo):
  - `400 INVALID_INPUT` / `OTP_INCORRECT` — sai OTP (message: "Mã OTP không đúng")
  - `409 OTP_ALREADY_USED` — OTP đã dùng
  - `410 OTP_EXPIRED` — OTP hết hạn
  - `422 TRANSACTION_INVALID_STATE` — giao dịch không ở trạng thái chờ OTP
  - `422 INSUFFICIENT_BALANCE` — số dư thay đổi/không đủ tại thời điểm xác nhận
  - `409 FEE_ALREADY_PAID` — fee bị người khác trả trong lúc chờ (Kịch bản B)
  - `503 DEPENDENCY_DOWN` — user/tuition/otp lỗi

**POST `/api/v1/payments/{paymentId}/otp/resend`** *(tuỳ chọn, hỗ trợ A2 — OTP hết hạn)*
- Hành vi: tạo OTP mới cho cùng giao dịch (OTP cũ → EXPIRED), gửi lại email.
- Response 200: `{ "message": "Mã OTP mới đã được gửi", "expiresInSeconds": 300 }`
- Lỗi: `429 RATE_LIMITED` (giới hạn vd 60 giây/lần), `422 TRANSACTION_INVALID_STATE`.

**GET `/api/v1/payments/{paymentId}`** — trạng thái giao dịch
- Response 200: `{ "paymentId", "amount", "status", "createdAt", "completedAt", "mssv", "studentName" }`
- Lỗi: `404`, `403` (không phải chủ giao dịch).

**GET `/api/v1/payments/me?page=0&size=20`** — lịch sử giao dịch (UC-06)
- Response 200:
```json
{
  "content": [
    { "paymentId": "...", "mssv": "521H0001", "studentName": "Trần Thị B",
      "amount": 8400000, "status": "SUCCESS", "createdAt": "..." }
  ],
  "page": 0, "size": 20, "totalElements": 1
}
```
- Lỗi: `401`.

## 3.7. Endpoint nội bộ (`/internal/**` — chỉ service gọi, không qua gateway)

| Method | URI | Callee | Request | Response | Ghi chú |
|---|---|---|---|---|---|
| POST | `/internal/accounts/debit` | user-service | `{ accountId, amount, transactionId }` + `Idempotency-Key` | 200 `{ success: true, newBalance }` | Conditional UPDATE, 0 dòng → 422 `INSUFFICIENT_BALANCE`/409 `CONFLICT` |
| POST | `/internal/tuitions/claim` | tuition-service | `{ feeId, transactionId }` | 200 `{ success: true }` | 0 dòng → 409 `FEE_ALREADY_PAID` |
| POST | `/internal/tuitions/{feeId}/release` | tuition-service | `{ transactionId }` | 200 `{ success: true }` | Compensation: chỉ release nếu `paid_transaction_id` = chính transaction này |
| POST | `/internal/otp/issue` | otp-service | `{ transactionId, email }` | 201 `{ otpId }` | Không trả code trong response nội bộ cũng được — code chỉ qua email |
| POST | `/internal/otp/verify` | otp-service | `{ transactionId, otp }` | 200 `{ valid: true }` | Sai/hết hạn/dùng rồi → 400/410/409 tương ứng |

**Bảo mật nội bộ (ghi chú):** `/internal/**` không route qua gateway; khi deploy thật nên đặt các service trong private network + service token. Trong project SV: gateway chặn mọi path `/internal`, service chỉ gọi nhau qua localhost/network docker.

## 3.8. Ví dụ kiểm thử API bằng curl (để dùng ở Phase 8)

```bash
# 1. Đăng nhập
curl -s -X POST localhost:8080/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"nguyenvana","password":"secret123"}'
# → lấy token

# 2. Tra cứu học phí
curl -s "localhost:8080/api/v1/tuitions?mssv=521H0001" -H "Authorization: Bearer $TOKEN"

# 3. Tạo giao dịch (Idempotency-Key tự sinh uuid)
curl -s -X POST localhost:8080/api/v1/payments \
  -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: $(uuidgen)" \
  -H 'Content-Type: application/json' -d '{"mssv":"521H0001"}'

# 4. Lấy OTP từ MailHog (http://localhost:8025) rồi xác nhận
curl -s -X POST localhost:8080/api/v1/payments/<paymentId>/confirm \
  -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: $(uuidgen)" \
  -H 'Content-Type: application/json' -d '{"otp":"482913"}'
```

## 3.9. Checklist hoàn thành Phase 3

- [ ] Đủ API cho mọi UC (UC-01…UC-06) — không thiếu luồng nào của UC-04
- [ ] Mỗi API có: URI, method, request, response (200/201), đầy đủ status code lỗi
- [ ] Bảng mã lỗi chuẩn + response lỗi thống nhất
- [ ] Idempotency-Key cho mọi API ghi
- [ ] Endpoint `/internal/**` tách biệt, không lộ qua gateway
- [ ] Ví dụ curl chạy thử đúng luồng (kiểm tra ở Phase 8)

**Acceptance criteria:** từ bảng API, lập trình viên (Phase 5) triển khai được mà không cần hỏi lại nghiệp vụ; mọi trường hợp lỗi trong UC-04 (A1–A6) có status code + message tương ứng.

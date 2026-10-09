**Ngôn ngữ:** [English](error-handling.md) · **Tiếng Việt**

# Xử lý lỗi

Mọi service trả về cùng một body lỗi thống nhất:

```json
{
  "code": "FEE_ALREADY_PAID",
  "message": "The tuition fee has already been paid.",
  "timestamp": "2026-10-08T00:00:00Z",
  "traceId": "a1b2c3…"
}
```

- `traceId` bằng `HttpContext.TraceIdentifier`, được middleware correlation điền từ `X-Correlation-Id` (hoặc tự sinh).
- Lỗi nghiệp vụ được mô hình hoá bằng giá trị `Result<T>` (không dùng exception) và được ánh xạ bởi `ControllerResultExtensions`.
- Exception không được xử lý sẽ được làm sạch thành `500 INTERNAL_ERROR` bởi middleware chốt cuối — không bao giờ trả về stack trace, connection string, SQL hay message exception nội bộ.

## Mã lỗi

| Mã | HTTP | Khi nào |
|---|---|---|
| `INVALID_INPUT` | 400 | Thiếu/sai định dạng đầu vào (bao gồm cả lỗi model-state tự động) |
| `UNAUTHENTICATED` | 401 | Thiếu/sai API key nội bộ |
| `FORBIDDEN` | 403 | Đã xác thực nhưng không được phép (dành chỗ) |
| `NOT_FOUND` | 404 | Không tìm thấy tài nguyên |
| `FEE_ALREADY_PAID` | 409 | Khoản phí đã được giao dịch khác trả |
| `RELEASE_CONFLICT` | 409 | Release bởi sai giao dịch |
| `OTP_ALREADY_USED` | 409 | OTP đã được dùng |
| `OTP_INCORRECT` | 400 | Giá trị OTP sai |
| `OTP_EXPIRED` | 410 | OTP hết hạn (quá 5 phút) |
| `OTP_LOCKED` | 409 | OTP bị khoá sau quá nhiều lần nhập sai |
| `IDEMPOTENCY_CONFLICT` | 409 | Dùng lại idempotency key với payload khác |
| `RATE_LIMITED` | 429 | Quá nhiều yêu cầu |
| `INTERNAL_ERROR` | 500 | Lỗi không lường trước |
| `DEPENDENCY_DOWN` | 503 | Phụ thuộc phía sau không khả dụng |

## Validation

Đầu vào được kiểm tra trước khi nghiệp vụ chạy:

- MSSV: không rỗng, ≤ 20 ký tự chữ-số.
- `transactionId`: GUID không rỗng.
- OTP: đúng độ dài (mặc định 6) chữ số.
- Email: định dạng hợp lệ, ≤ 254 ký tự.
- Số tiền: `> 0` (domain + CHECK ở database).

Lỗi model binding (ví dụ GUID sai định dạng) được ánh xạ về cùng body `INVALID_INPUT` thống nhất.

## Triết lý ánh xạ lỗi

- **400** — caller sửa được request.
- **409** — xung đột với trạng thái hiện tại (đã trả/đã dùng/đã khoá/xung đột).
- **410** — tài nguyên từng tồn tại nhưng đã hết hạn (OTP).
- **429** — rate limit.
- **5xx** — service hoặc phụ thuộc lỗi; không lộ chi tiết nội bộ.

## Ghi log lỗi

Lỗi được ghi ở phía server kèm `traceId`, `errorCode`, `transactionId`, `messageId` và `durationMs` khi có — nhưng không bao giờ kèm bí mật hay giá trị nhạy cảm.

**Ngôn ngữ:** [English](README.md) · **Tiếng Việt**

# iBanking — Phân hệ đóng học phí · Tài liệu dự án

> Đề tài: **DỰ ÁN GIỮA KỲ — PHÂN HỆ ĐÓNG HỌC PHÍ CỦA ỨNG DỤNG iBanking** (TDTU).
> Nội dung đề bài được trích xuất từ ảnh chụp đề (đã OCR, xem `.reasonix/ocr_text/`).

## Mục đích thư mục này

Thư mục `docs/` chứa tài liệu dự án theo **hai nhóm**:

1. **Kế hoạch thực hiện** — kế hoạch từng bước ban đầu, chia theo **9 pha** (Phase 0–9). Mỗi pha là một file `.md` riêng: mục tiêu, đầu vào, các bước cụ thể, sản phẩm bàn giao và tiêu chí chấp nhận.
2. **Tài liệu triển khai** — bản triển khai **.NET 10** hiện tại của ba microservice (`tuition-service`, `otp-service`, `notification-service`).

**Trạng thái:** đã triển khai ba service bằng **.NET 10** (tuition, otp, notification); `payment-service`, `user-service`, `gateway` và frontend vẫn ở giai đoạn kế hoạch.

## Tài liệu triển khai (hiện tại)

| Tài liệu | Nội dung |
|---|---|
| [`architecture.md`](architecture.md) | Trách nhiệm từng service, giao tiếp đồng bộ/bất đồng bộ, saga, outbox, chiến lược concurrency |
| [`database.md`](database.md) | Schema từng service, ràng buộc, chỉ mục, migration |
| [`api.md`](api.md) | Endpoint công khai + nội bộ, request/response, mã lỗi |
| [`security.md`](security.md) | Xử lý OTP, hash vs mã hoá, bí mật, TLS, rate limiting |
| [`error-handling.md`](error-handling.md) | Response lỗi thống nhất và bảng mã lỗi |
| [`testing.md`](testing.md) | Chiến lược test đơn vị / tích hợp / concurrency và câu lệnh |
| [`troubleshooting.md`](troubleshooting.md) | Lỗi thường gặp và cách khắc phục |
| [`deployment.md`](deployment.md) | Cấu hình production, migration, TLS, xoay khoá |

Điểm vào cấp gốc: [`../README.vi.md`](../README.vi.md).

## Kế hoạch thực hiện (Phase 0–9)

| # | Phase | File | Nội dung chính |
|---|---|---|---|
| 0 | Thiết lập dự án | `00-project-setup.md` | Git, cấu trúc thư mục, Docker Compose, khởi tạo service, cấu hình chung |
| 1 | Phân tích nghiệp vụ & dữ liệu | `01-analysis-use-case-erd.md` | Use Case Diagram (+ đặc tả UC-04) và ERD |
| 2 | Kiến trúc Microservices | `02-microservices-architecture.md` | Phân rã service, trách nhiệm, 2 phần giao tiếp (REST đồng bộ + messaging bất đồng bộ), sơ đồ kiến trúc |
| 3 | Thiết kế REST API | `03-rest-api-design.md` | Toàn bộ endpoint: URI, HTTP method, request, response, status code |
| 4 | Thiết kế & hiện thực CSDL | `04-database-design.md` | Schema từng service, ràng buộc nghiệp vụ, seed data, SQL và/hoặc NoSQL |
| 5 | Lập trình services & API | `05-backend-services.md` | Thứ tự xây dựng từng service, chi tiết công việc, test cần viết |
| 6 | Transaction & concurrency | `06-transaction-concurrency.md` | 2 kịch bản đồng thời, cơ chế nhất quán, saga, idempotency, outbox |
| 7 | Giao diện Web | `07-frontend-web.md` | Từng màn hình, hành vi, luồng gọi API, ánh xạ lỗi |
| 8 | Kiểm thử tổng thể | `08-testing.md` | Unit / Integration / API / E2E, test 2 kịch bản concurrency |
| 9 | Tài liệu & demo | `09-documentation-demo.md` | README, báo cáo theo 8 yêu cầu đề, kịch bản demo |

(Các file kế hoạch khác: `10-teamwork-plan.md`, `11-teamwork-6days.md`, `12-teamwork-6days-summary.md`.)

## Công nghệ

| Thành phần | Lựa chọn | Ghi chú |
|---|---|---|
| Backend | **.NET 10** (ASP.NET Core, EF Core 10) | Đã chuyển từ Java 17 + Spring Boot 3.x ban đầu |
| Cơ sở dữ liệu | PostgreSQL | Một database logic cho mỗi service |
| Cache / TTL | Redis (tuỳ chọn) | TTL của OTP do PostgreSQL ép buộc; Redis là tuỳ chọn cho rate-limit |
| Message broker | RabbitMQ | Gửi email bất đồng bộ (outbox pattern) |
| Email (dev) | MailHog | Bắt email cục bộ: SMTP 1025, UI http://localhost:8025 |
| Build / chạy | .NET SDK + Docker Compose | `docker compose up` khởi động hạ tầng |

> So với kế hoạch gốc, **chỉ phần công cụ** thay đổi; các bước phân tích/thiết kế (Pha 1–3, 6) giữ nguyên.

## Bản đồ 8 yêu cầu của đề bài → Phase

| Yêu cầu đề bài | Phase thực hiện |
|---|---|
| 1. Phân tích nghiệp vụ & dữ liệu (Use Case Diagram, ERD) | Phase 1 |
| 2. Kiến trúc Microservices + cách giao tiếp giữa các service | Phase 2 |
| 3. REST API (URI, method, request, response, status code) | Phase 3 |
| 4. CSDL bằng SQL và/hoặc NoSQL | Phase 4 |
| 5. Lập trình service & API đủ luồng nghiệp vụ | Phase 5 |
| 6. Transaction & concurrency, tính nhất quán | Phase 6 |
| 7. Giao diện Web tích hợp API | Phase 7 (+ 8) |
| 8. Tài liệu & demo | Phase 9 (+ 0) |

## Thứ tự thực hiện và phụ thuộc

```
P0 Setup → P1 Phân tích (UCD + ERD) → P2 Kiến trúc + P3 REST API → P4 CSDL
        → P5 Backend (user → tuition → otp → notification → payment → gateway)
        → P6 Transaction & concurrency → P7 Frontend → P8 Kiểm thử → P9 Tài liệu & demo
```

**Quy tắc phụ thuộc quan trọng:**

- Không bắt đầu `payment-service` (Phase 5.5) trước khi `user-service`, `tuition-service`, `otp-service` hoàn tất và có test.
- Không kết luận Phase 6 hoàn thành khi 2 test concurrency (Kịch bản A & B) chưa **chạy và đạt**.
- Mỗi phase phải đạt acceptance criteria của chính nó trước khi chuyển phase kế tiếp.

## Quy ước chung xuyên suốt dự án

1. **Mỗi service sở hữu dữ liệu riêng** — không dùng chung bảng giữa các service; giao tiếp chỉ qua API/event.
2. **Đặt tên:** REST theo số nhiều (`/payments`, `/users/me`); event theo thì quá khứ (`TransactionSucceeded`); biến/endpoint dạng `lowerCamelCase`/`kebab-case`.
3. **Lỗi thống nhất:** mọi response lỗi có dạng `{ "code", "message", "timestamp", "traceId" }`.
4. **Đơn vị tiền:** lưu số nguyên (VND, không lưu số thập phân float) hoặc `DECIMAL(15,2)`; không bao giờ dùng `double` cho tiền.
5. **Bảo mật tối thiểu:** mật khẩu hash bcrypt; OTP chỉ lưu hash; JWT có hạn; không để lộ endpoint nội bộ qua gateway.
6. **Mỗi service** có health check, OpenAPI/Swagger, log có `traceId` để dò lỗi xuyên service.

## Cấu trúc thư mục

```
microservices/
├── backend/                      # các service .NET 10
│   ├── Common/                   # thư viện dùng chung (Microservices.Common)
│   ├── tuition-service/          # port 8082 — sinh viên, học phí
│   ├── otp-service/              # port 8084 — OTP lifecycle, outbox
│   ├── notification-service/     # port 8085 — gửi email
│   ├── payment-service/          # (kế hoạch) điều phối saga
│   ├── user-service/             # (kế hoạch) tài khoản, đăng nhập, số dư
│   ├── gateway/                  # (kế hoạch) API gateway
│   ├── tests/                    # project test đơn vị + tích hợp
│   └── Microservices.sln
├── frontend/                     # React + Vite (kế hoạch)
├── docs/                         # tài liệu dự án (chính là thư mục này)
├── docker-compose.yml
├── .env.example
├── README.md                     # readme gốc (tiếng Anh, mặc định)
└── README.vi.md                  # readme gốc (tiếng Việt)
```

## Nguồn dữ liệu đề bài (tóm tắt nghiệp vụ — nền tảng cho mọi phase)

1. Người dùng đăng nhập bằng `username`/`password`; hệ thống quản lý: họ tên, SĐT, email, số dư khả dụng, lịch sử giao dịch.
2. Màn hình thanh toán gồm 3 nhóm thông tin: người nộp tiền (tự động, không sửa), thông tin học phí (tra theo MSSV), thông tin thanh toán (số dư + số tiền). Chỉ thanh toán **toàn bộ** khoản học phí; giao dịch hợp lệ khi học phí tồn tại, chưa thanh toán và số dư ≥ số tiền.
3. Xác thực bằng **OTP gửi email**: gắn với đúng 1 giao dịch, hết hạn **tối đa 5 phút**, dùng thành công **1 lần**.
4. Sau khi OTP hợp lệ: kiểm tra lại → trừ tiền → cập nhật học phí đã thanh toán → lưu lịch sử → gửi email xác nhận → hiển thị kết quả.
5. Tính nhất quán khi xử lý đồng thời: (A) nhiều giao dịch cùng tài khoản → không chi vượt số dư; (B) nhiều người thanh toán cùng một học phí (MSSV) → chỉ thanh toán thành công 1 lần.

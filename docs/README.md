# Kế hoạch chi tiết — Dự án giữa kỳ: Phân hệ đóng học phí của ứng dụng iBanking

> Đề tài: **DỰ ÁN GIỮA KỲ — PHÂN HỆ ĐÓNG HỌC PHÍ CỦA ỨNG DỤNG iBanking** (TDTU).
> Nội dung đề bài được trích xuất từ ảnh chụp đề (đã OCR, xem `.reasonix/ocr_text/`).

## Mục đích tài liệu

Bộ tài liệu này là **kế hoạch thực hiện từng bước** cho toàn bộ dự án, được chia theo **9 pha** (Phase). Mỗi pha là một file `.md` riêng, mô tả: mục tiêu, đầu vào, các bước làm cụ thể, sản phẩm bàn giao (deliverable) và tiêu chí chấp nhận (acceptance criteria).

**Trạng thái:** kế hoạch — **chưa viết code ứng dụng**. Các đoạn `SQL`, `JSON`, `yaml` trong tài liệu chỉ là **mẫu thiết kế** để thực hiện ở giai đoạn sau.

## Danh sách các pha và file tương ứng

| # | Phase | File | Nội dung chính |
|---|---|---|---|
| 0 | Thiết lập dự án | `00-project-setup.md` | Git, cấu trúc thư mục, Docker Compose, khởi tạo các Spring Boot service, cấu hình chung |
| 1 | Phân tích nghiệp vụ & dữ liệu | `01-analysis-use-case-erd.md` | Use Case Diagram (các bước + đặc tả UC-04) và ERD (các bước + bảng thiết kế) |
| 2 | Kiến trúc Microservices | `02-microservices-architecture.md` | Phân rã service, trách nhiệm từng service, 2 phần giao tiếp (REST đồng bộ + messaging bất đồng bộ), sơ đồ kiến trúc |
| 3 | Thiết kế REST API | `03-rest-api-design.md` | Toàn bộ endpoint: URI, HTTP method, Input/Request, Output/Response, HTTP Status Code |
| 4 | Thiết kế & hiện thực CSDL | `04-database-design.md` | Schema từng service, ràng buộc nghiệp vụ, seed data, SQL và/hoặc NoSQL |
| 5 | Lập trình services & API | `05-backend-services.md` | Thứ tự xây dựng từng service, chi tiết công việc, test cần viết |
| 6 | Transaction & concurrency | `06-transaction-concurrency.md` | 2 kịch bản đồng thời, cơ chế đảm bảo nhất quán, saga, idempotency, outbox |
| 7 | Giao diện Web | `07-frontend-web.md` | Từng màn hình, hành vi, luồng gọi API, ánh xạ lỗi |
| 8 | Kiểm thử tổng thể | `08-testing.md` | Unit / Integration / API / E2E, test cho 2 kịch bản concurrency |
| 9 | Tài liệu & demo | `09-documentation-demo.md` | README, báo cáo theo 8 yêu cầu đề, kịch bản demo |

## Công nghệ giả định (mặc định — có thể đổi theo yêu cầu môn học)

| Thành phần | Lựa chọn mặc định | Ghi chú |
|---|---|---|
| Backend | Java 17 + Spring Boot 3.x | Spring Web, Data JPA, Security, Validation, Flyway, springdoc-openapi |
| Cơ sở dữ liệu | PostgreSQL | Một database logic cho mỗi service (cùng 1 instance cho dự án SV) |
| Cache / TTL | Redis | OTP expiry, idempotency key, lock/rate-limit hỗ trợ |
| Message broker | RabbitMQ | Gửi email bất đồng bộ (outbox pattern) |
| API Gateway | Spring Cloud Gateway | Cổng vào duy nhất, kiểm tra JWT, routing |
| Frontend | React + Vite | Có thể thay bằng HTML/JS thuần nếu muốn đơn giản |
| Email (dev) | MailHog | Bắt email cục bộ: SMTP 1025, UI http://localhost:8025 |
| Build / chạy | Maven + Docker Compose | `docker compose up` khởi động toàn bộ |

> Nếu môn học yêu cầu stack khác (Node.js, .NET, MySQL, Kafka…), **chỉ phần công cụ** trong các pha thay đổi; các bước phân tích/thiết kế (Pha 1–3, 6) giữ nguyên.

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
3. **Lỗi thống nhất:** mọi response lỗi có dạng `{ "code", "message", "timestamp", "traceId" }` (xem `03-rest-api-design.md`).
4. **Đơn vị tiền:** lưu số nguyên (VND, không lưu số thập phân float) hoặc `DECIMAL(15,2)`; không bao giờ dùng `double` cho tiền.
5. **Bảo mật tối thiểu:** mật khẩu hash bcrypt; OTP chỉ lưu hash; JWT có hạn; không để lộ endpoint nội bộ qua gateway.
6. **Mỗi service** có health check, OpenAPI/Swagger, log có `traceId` để dò lỗi xuyên service.

## Cấu trúc thư mục (đã tạo)

```
microservices/
├── backend/                  # toàn bộ service backend (Java Spring Boot)
│   ├── gateway/              # Spring Cloud Gateway (port 8080)
│   ├── user-service/         # port 8081 — tài khoản, đăng nhập, ví/số dư
│   ├── tuition-service/      # port 8082 — sinh viên, học phí
│   ├── payment-service/      # port 8083 — điều phối giao dịch (saga)
│   ├── otp-service/          # port 8084 — OTP lifecycle
│   └── notification-service/ # port 8085 — gửi email
├── frontend/                 # React + Vite web app (dev port 5173)
├── docs/                     # tài liệu dự án (chính là thư mục này)
├── docker-compose.yml
├── .env.example
└── README.md
```

## Nguồn dữ liệu đề bài (tóm tắt nghiệp vụ — nền tảng cho mọi phase)

1. Người dùng đăng nhập bằng `username`/`password`; hệ thống quản lý: họ tên, SĐT, email, số dư khả dụng, lịch sử giao dịch.
2. Màn hình thanh toán gồm 3 nhóm thông tin: người nộp tiền (tự động, không sửa), thông tin học phí (tra theo MSSV), thông tin thanh toán (số dư + số tiền). Chỉ thanh toán **toàn bộ** khoản học phí; giao dịch hợp lệ khi học phí tồn tại, chưa thanh toán và số dư ≥ số tiền.
3. Xác thực bằng **OTP gửi email**: gắn với đúng 1 giao dịch, hết hạn **tối đa 5 phút**, dùng thành công **1 lần**.
4. Sau khi OTP hợp lệ: kiểm tra lại → trừ tiền → cập nhật học phí đã thanh toán → lưu lịch sử → gửi email xác nhận → hiển thị kết quả.
5. Tính nhất quán khi xử lý đồng thời: (A) nhiều giao dịch cùng tài khoản → không chi vượt số dư; (B) nhiều người thanh toán cùng một học phí (MSSV) → chỉ thanh toán thành công 1 lần.

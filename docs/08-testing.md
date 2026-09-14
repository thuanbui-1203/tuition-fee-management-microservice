# Phase 8 — Kiểm thử tổng thể (Testing)

**Mục tiêu:** đảm bảo toàn hệ thống đúng theo đặc tả, đặc biệt 2 kịch bản concurrency (Phase 6) và toàn bộ luồng UC-04; cung cấp bằng chứng test cho báo cáo.

**Đầu vào:** Phase 5–7 (backend + frontend đã có).

**Sản phẩm bàn giao:** 4 tầng test (mục 8.1) + Postman collection + báo cáo kết quả test.

---

## 8.1. Chiến lược 4 tầng test

| Tầng | Công cụ | Phạm vi | Chạy bằng |
|---|---|---|---|
| 1. Unit test | JUnit 5 + Mockito | logic service từng service (không cần DB/hạ tầng) | `mvn test` trong từng service |
| 2. Integration test | Spring Boot Test + **Testcontainers** (Postgres, RabbitMQ, Redis) | repository + constraint DB + saga + concurrency | `mvn verify` |
| 3. API test | Postman + Newman (CLI) | mọi endpoint theo Phase 3 (URI, method, status code, response) | `newman run collection.json` |
| 4. E2E UI test | Playwright (tuỳ chọn) hoặc kịch bản thủ công Phase 7.4 | luồng người dùng trên trình duyệt | `npx playwright test` / thủ công |

## 8.2. Tầng 1 — Unit test (mỗi service)

**Danh sách tối thiểu cần có:**

| Service | Test case bắt buộc |
|---|---|
| user-service | login đúng/sai; debit đủ/thiếu tiền; debit trùng idempotency key; parse JWT |
| tuition-service | lookup có fee; lookup fee PAID → ném 409; claim thành công/lần 2 thất bại; release đúng chủ |
| otp-service | issue tạo hash + expires=+5'; verify đúng/sai; dùng lại → 409; hết hạn → 410; quá 5 lần sai → khoá |
| notification-service | map event → email; dedupe message_id trùng |
| payment-service | state machine chuyển trạng thái hợp lệ/không hợp lệ; map exception → mã lỗi chuẩn |
| gateway | filter chặn không token; permit login |

**Quy tắc:** mock repository/WebClient ở tầng này; không cần khởi động Docker.

## 8.3. Tầng 2 — Integration test (Testcontainers)

**Thiết lập chung:** mỗi service integration test khởi động container Postgres (schema do Flyway tạo), service nào cần thì thêm RabbitMQ/Redis.

**Các integration test trọng yếu (đây là bằng chứng chấm điểm):**

1. **Test Kịch bản A** (user-service, xem chi tiết Phase 6.3):
   - Seed account 1.000.000; 10 luồng debit 300.000 đồng thời (CountDownLatch);
   - Assert: thành công ≤ 3; `sum(debited) + finalBalance == 1_000_000`; balance ≥ 0.
2. **Test Kịch bản B** (payment + tuition thật, Phase 6.3):
   - 2 user + 1 fee UNPAID; confirm đồng thời; assert đúng 1 SUCCESS + 1 409; DB chỉ 1 PAID; chỉ 1 user bị trừ.
3. **Test idempotency:** POST /payments 2 lần cùng key → 1 bản ghi; confirm 2 lần cùng key → trừ 1 lần.
4. **Test saga compensation:** mock debit thất bại → fee quay lại UNPAID, transaction FAILED.
5. **Test OTP edge:** sai/hết hạn/dùng lại/giao dịch khác.
6. **Test outbox:** tắt consumer, payment thành công → outbox PENDING; bật relay → publish → SENT; notification nhận và gửi email (MailHog).
7. **Test constraint DB:** câu SQL Phase 4.5 chạy qua JdbcTemplate → mong đợi exception (CHECK/UNIQUE bị vi phạm).

**Lưu ý kỹ thuật:**
- Dùng `@SpringBootTest(webEnvironment = RANDOM_PORT)` + Testcontainers JUnit 5 (`@Testcontainers`).
- Test concurrency chạy trên **DB thật** (không dùng H2 — H2 không mô phỏng đúng hành vi lock/constraint của Postgres).
- Chạy lặp test concurrency 10 lần (`@RepeatedTest(10)`) để loại flaky.

## 8.4. Tầng 3 — API test (Postman/Newman)

**Nội dung collection (đặt `docs/postman/`):**
- Thư mục theo nhóm: `auth`, `users`, `tuitions`, `payments`, `internal`.
- **Biến môi trường:** `baseUrl=http://localhost:8080`, `token` (tự động lấy từ login bằng script), `paymentId`, `idempotencyKey` (script sinh uuid).
- **Assert mỗi request:** status code đúng; `response body` có field bắt buộc (dùng `pm.test` + `pm.expect`).
- **Luồng chính:** login → me → tuitions?mssv → POST payments → (đọc OTP từ MailHog API `GET :8025/api/v2/messages` bằng script) → confirm → assert SUCCESS + newBalance.
- **Luồng lỗi:** MSSV 404; fee PAID 409; thiếu tiền 422; OTP sai 400; OTP hết hạn 410; không token 401.

**Chạy tự động:**
```bash
newman run docs/postman/iBanking_Tuition.postman_collection.json \
  -e docs/postman/local.postman_environment.json --reporters cli,json
```

## 8.5. Tầng 4 — E2E (tuỳ chọn nhưng nên làm)

- Playwright: đăng nhập → thanh toán → nhập OTP (lấy OTP bằng cách đọc API MailHog trong test) → assert màn hình THÀNH CÔNG.
- Nếu không làm Playwright: chạy kịch bản thủ công Phase 7.4 và **quay video/lưu ảnh màn hình** làm bằng chứng.

## 8.6. Tổng hợp bằng chứng test (cho báo cáo)

Tạo file `docs/test-report.md` ghi:
1. Bảng kết quả từng tầng: số test, pass/fail, thời gian.
2. Ảnh chụp terminal khi chạy Test A & B (đã xanh nhiều lần).
3. Ảnh Postman runner (collection xanh).
4. Ảnh MailHog có email OTP + email xác nhận.
5. Nếu có, ảnh Playwright report.

## 8.7. Checklist hoàn thành Phase 8

- [ ] Unit test mỗi service chạy xanh (`mvn test` không fail)
- [ ] Integration test: Test A ×10, Test B ×10 xanh trên Postgres thật
- [ ] Newman chạy collection xanh toàn bộ luồng + luồng lỗi
- [ ] E2E (tự động hoặc thủ công có ảnh) xanh
- [ ] `docs/test-report.md` đã viết với bằng chứng

**Acceptance criteria:** chạy 1 lệnh `mvn verify` ở mỗi service (hoặc script tổng) → tất cả xanh; Test A & B chứng minh được 2 yêu cầu §5; collection Postman xanh trên môi trường mới (docker compose up sạch).

# Phase 9 — Tài liệu, báo cáo & trình diễn (Demo)

**Mục tiêu:** chuẩn bị tài liệu hướng dẫn cài đặt/khởi chạy, báo cáo hoàn chỉnh theo 8 yêu cầu đề và kịch bản demo (yêu cầu 8 của đề).

**Đầu vào:** mọi sản phẩm Phase 0–8 + các diagram file nguồn trong `docs/`.

**Sản phẩm bàn giao:** `README.md` gốc hoàn thiện, `docs/report.md` (hoặc tài liệu Word/PDF), `docs/demo-script.md`, slide thuyết trình (tuỳ chọn).

---

## 9.1. README.md gốc (hướng dẫn cài đặt & khởi chạy)

Viết đầy đủ các phần sau:

1. **Giới thiệu:** 1–2 câu về phân hệ; kèm link tới `docs/`.
2. **Yêu cầu môi trường:** JDK 17+, Maven 3.9+, Node 18+, Docker Desktop (hoặc Docker Engine).
3. **Cách chạy — tối giản 3 bước:**
```bash
docker compose up -d          # Postgres, Redis, RabbitMQ, MailHog
# (tuỳ chọn) tạo database nếu chưa có init script
mvn -f user-service/pom.xml spring-boot:run     # … lặp lại cho từng service
# hoặc: script start-all.sh / start-all.bat chạy lần lượt 6 service
cd web && npm install && npm run dev            # frontend http://localhost:5173
```
4. **Bảng cổng & URL hữu ích:**

| Thành phần | URL |
|---|---|
| Gateway | http://localhost:8080 |
| Swagger từng service | http://localhost:8081/swagger-ui.html … 8085 |
| MailHog UI | http://localhost:8025 |
| RabbitMQ UI | http://localhost:15672 (guest/guest) |

5. **Tài khoản demo:** `nguyenvana` / `secret123` (số dư 10.000.000), `tranthib` / `secret123` (500.000). MSSV demo: `521H0001` (UNPAID), `521H0003` (PAID).
6. **Cách chạy test:** lệnh `mvn test`/`mvn verify` từng service; `newman run` collection; link `docs/08-testing.md`.
7. **Cấu trúc repo** (cây thư mục) + bảng service.

## 9.2. Cấu trúc báo cáo — ánh xạ đúng 8 yêu cầu của đề

> Tạo `docs/report.md` hoặc tài liệu Word theo dàn ý dưới đây; **mỗi phần nộp đúng theo số yêu cầu** của đề.

**Phần 1 — Phân tích nghiệp vụ và dữ liệu** (nguồn: Phase 1)
- 1.1 Mô tả nghiệp vụ tóm tắt (dẫn theo §1–§5 đề).
- 1.2 **Use Case Diagram** (ảnh) + bảng UC + đặc tả UC-04 + ma trận truy vết.
- 1.3 **ERD**: logical (ảnh) + bảng entity/thuộc tính + ràng buộc + ánh xạ ownership theo service.

**Phần 2 — Kiến trúc Microservices** (nguồn: Phase 2)
- 2.1 Nguyên tắc phân rã + bảng service/trách nhiệm/dữ liệu sở hữu.
- 2.2 **Cách thức giao tiếp giữa các service — 2 phần:** (A) đồng bộ REST, (B) bất đồng bộ messaging/event — kèm bảng mapping từng luồng.
- 2.3 **Microservices Architecture Diagram** (ảnh) + chú thích.
- 2.4 Cơ chế nhất quán xuyên service: saga + compensation + outbox (tóm tắt, chi tiết ở Phần 6).

**Phần 3 — REST API** (nguồn: Phase 3)
- 3.1 Quy ước chung + bảng mã lỗi.
- 3.2 Bảng API đầy đủ (URI, method, request, response, status code) — có thể đưa nguyên bảng Phase 3.
- 3.3 Ví dụ JSON request/response cho luồng chính.

**Phần 4 — CSDL (SQL và/hoặc NoSQL)** (nguồn: Phase 4)
- 4.1 Giải thích lựa chọn SQL (PostgreSQL) cho dữ liệu tài chính + NoSQL (Redis) cho TTL/rate-limit.
- 4.2 Migration & schema từng service (hoặc link script).
- 4.3 Seed data + ảnh chạy migration thành công.

**Phần 5 — Lập trình service & API** (nguồn: Phase 5)
- 5.1 Mô tả cách tổ chức code từng service.
- 5.2 Ảnh chụp: service khởi động, Swagger, kết quả Postman luồng chính.
- 5.3 Điểm nổi bật: conditional debit/claim, JWT, outbox.

**Phần 6 — Transaction & concurrency** (nguồn: Phase 6 + 8) — *phần chấm điểm cao*
- 6.1 Trình bày 2 kịch bản §5.
- 6.2 Từng cơ chế + nơi hiện thực (bảng 6.4).
- 6.3 **Bằng chứng:** ảnh kết quả Test A (10 luồng) và Test B (2 luồng) + giải thích kết quả.

**Phần 7 — Giao diện Web** (nguồn: Phase 7)
- 7.1 Ảnh từng màn hình (login, dashboard, thanh toán 3 nhóm, OTP, kết quả, lịch sử).
- 7.2 Mô tả luồng + cách xử lý lỗi.

**Phần 8 — Hướng dẫn cài đặt, khởi chạy và demo** (nguồn: Phase 9)
- 8.1 README (dán từ 9.1).
- 8.2 Kịch bản demo (mục 9.4) + video/ảnh.

## 9.3. Đóng gói mã nguồn (trước khi nộp)

- [ ] Xoá file tạm: `target/`, `node_modules/`, `.env` (giữ `.env.example`), log.
- [ ] Chạy lại từ đầu trên máy sạch theo README để chắc chắn hướng dẫn đúng.
- [ ] Commit cuối cùng; ghi tag (vd `v1.0`); xuất file zip nếu cần nộp.

## 9.4. Kịch bản demo (khoảng 10 phút)

**Chuẩn bị trước (5 phút trước buổi demo):**
- `docker compose up -d` + chạy đủ 6 service + web.
- Reset dữ liệu: chạy lại migration/seed (hoặc `docker compose down -v && up`) để số dư & fee về trạng thái ban đầu.
- Mở sẵn: MailHog (8025), Postman (collection), 2 cửa sổ trình duyệt (A: `nguyenvana`, C: đăng nhập khác).

**Diễn trình (từng bước nói theo):**

| Phút | Nội dung demo | Chuẩn bị sẵn |
|---|---|---|
| 0–1 | Giới thiệu kiến trúc: chỉ trên sơ đồ service nào làm gì, 2 phần giao tiếp | slide/sơ đồ |
| 1–2 | Demo 1 — Đăng nhập `nguyenvana`, xem Dashboard (hồ sơ, số dư 10tr, lịch sử) | trình duyệt A |
| 2–4 | Demo 2 — Vào Thanh toán: thấy 3 nhóm; nhập `521H0001` → hiện SV + 8,4tr; nhập MSSV `521H0003` → "đã thanh toán" (chứng minh chặn) | trình duyệt A |
| 4–6 | Demo 3 — Thanh toán thành công: Xác nhận → OTP dialog đếm ngược → mở MailHog lấy OTP → nhập → THÀNH CÔNG; quay lại Dashboard thấy số dư giảm + lịch sử; MailHog có email xác nhận | MailHog + trình duyệt A |
| 6–7 | Demo 4 — Lỗi OTP: tạo giao dịch khác, nhập OTP sai 1 lần (báo lỗi), để quá hạn hoặc bấm sai nhiều lần (tuỳ) | trình duyệt A |
| 7–8 | Demo 5 — **Kịch bản B (concurrency):** dùng `tranthib` trả `521H0002` đồng thời ở 2 cửa sổ → 1 SUCCESS, 1 "đã được thanh toán"; mở DB/Postman chứng minh fee chỉ PAID 1 lần, chỉ 1 user bị trừ | 2 cửa sổ + Postman |
| 8–9 | Demo 6 — Thiếu tiền: `tranthib` (500k) thanh toán `521H0001` (8,4tr) → nút Xác nhận bị khoá/cảnh báo (hoặc 422 nếu tạo tay) | trình duyệt B |
| 9–10 | Demo 7 — Kiến trúc sống: chứng minh outbox/async — tắt notification-service, thanh toán xong, bật lại → email đến (event không mất). Kết thúc bằng bảng test xanh | terminal + MailHog |

**Phòng ngừa lỗi demo:**
- Luôn có **bản dự phòng dữ liệu reset** (script `reset-demo.sh`).
- Nếu MailHog chậm, dùng API MailHog lấy OTP trong Postman thay vì mở UI.
- Nếu 1 service chết, có sẵn câu lệnh khởi động lại nhanh; không demo tính năng của service đang chết.
- Diễn tập 2 lần trước khi nộp; quay video demo (màn hình + mic) để dự phòng khi giáo viên không xem trực tiếp.

## 9.5. Checklist hoàn thành Phase 9

- [ ] README hoàn chỉnh, chạy lại từ đầu theo README thành công
- [ ] Báo cáo đủ 8 phần đúng số yêu cầu đề, mỗi phần có diagram/ảnh minh hoạ
- [ ] Kịch bản demo đã diễn tập ≥ 2 lần; video demo lưu lại
- [ ] Mã nguồn sạch (không target/node_modules/.env), có tag/zip nộp

**Acceptance criteria:** người khác làm theo README là chạy được toàn hệ thống; báo cáo đủ 8 yêu cầu với bằng chứng (ảnh test concurrency, ảnh màn hình, ảnh email); demo 10 phút chạy trơn tru cả 2 kịch bản §5.

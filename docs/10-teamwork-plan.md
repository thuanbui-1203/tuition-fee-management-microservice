# Phụ lục — Phân công công việc cho nhóm 3 người

> Tài liệu này bổ sung cho bộ kế hoạch Phase 0–9 trong `docs/`. Dùng trực tiếp cho mục **"Phân công công việc"** của báo cáo.

---

## 1. Nguyên tắc chia việc (đọc trước khi phân công)

**Chọn cách chia theo SERVICE (vertical slice), KHÔNG chia theo tầng (layer).**

| Cách chia | Ưu điểm | Nhược điểm |
|---|---|---|
| **Theo service (khuyến nghị)** | Mỗi người sở hữu trọn một miền (service + DB + API + test + màn hình), làm song song được ngay sau khi chốt API contract | Cần chốt contract sớm; cần review chéo |
| Theo tầng (1 người backend, 1 frontend, 1 tài liệu) | Dễ hiểu | Người frontend/tài liệu **bị chặn** cho tới khi backend xong; khó chia điểm công bằng |

**Ba quy tắc nền tảng:**
1. **Contract-first:** chốt API (Phase 3) trước khi ai viết code; muốn đổi field/status code phải cập nhật `docs/03-rest-api-design.md` và báo cả nhóm.
2. **Một chủ sở hữu cho mỗi service/thư mục:** không hai người cùng sửa một service.
3. **Two-person rule cho phần rủi ro:** SQL trừ tiền, claim học phí và luồng saga phải được người khác review.

---

## 2. Phân công theo miền (ownership) — 3 thành viên

Đặt tên trung tính: **Thành viên A**, **Thành viên B**, **Thành viên C**.

| Hạng mục | **A — Danh tính & Tiền** | **B — Học phí & OTP** | **C — Điều phối & Tích hợp** |
|---|---|---|---|
| **Service sở hữu** | `user-service` (đăng nhập/JWT, hồ sơ, số dư, **trừ tiền nguyên tử**), `gateway` (routing, kiểm tra JWT, CORS) | `tuition-service` (tra cứu MSSV, **claim/release học phí**), `otp-service` (sinh/xác thực OTP, TTL, rate-limit), `notification-service` (consumer email, dedupe, DLQ) | `payment-service` (state machine, **saga**, outbox, idempotency, lịch sử), Postman collection, E2E |
| **Database/migration sở hữu** | `users`, `accounts` | `students`, `tuition_fees`, `otp_codes`, `email_logs` | `transactions`, `outbox` |
| **API sở hữu** | `/auth/login`, `/users/me`, `/users/me/accounts` | `/tuitions`, `/internal/otp/*`, `/internal/tuitions/claim|release` | `/payments/*`, `/internal/accounts/debit` (phía gọi), route của gateway |
| **Test concurrency sở hữu** | **Kịch bản A** — nhiều giao dịch cùng tài khoản (không chi vượt số dư) | **Kịch bản B** — nhiều người trả cùng một học phí (chỉ 1 lần) | Idempotency + bù trừ saga (compensation) + outbox |
| **Màn hình UI sở hữu** | Login, Dashboard, History | Payment (3 nhóm thông tin), OTP dialog (đếm ngược 5:00) | Layout + axios/JWT client, Result, tích hợp các màn hình |
| **Phần báo cáo sở hữu** | Yêu cầu 3 (API auth/user), Yêu cầu 4 (schema của mình), Yêu cầu 7 (màn hình của mình) | Yêu cầu 3 (API tuition), Yêu cầu 6 (Kịch bản B), Yêu cầu 7 (màn hình của mình) | Yêu cầu 2 (sơ đồ kiến trúc), Yêu cầu 6 (saga), Yêu cầu 8 (kịch bản demo) |

**Lý do C nhận `payment-service`:** saga chạm cả service của A và B → C cần cái nhìn toàn cục. **Bù lại, C không phải người ráp báo cáo** (giao cho A hoặc B) để cân bằng tải.

---

## 3. Phân công theo từng Phase

| Phase | A | B | C |
|---|---|---|---|
| **0 — Thiết lập** | `docker-compose.yml` (Postgres/Redis/RabbitMQ/MailHog), script tạo DB, `.env.example` | Tạo skeleton 3 service (`tuition`, `otp`, `notification`) + cấu hình `application.yml` | Tạo skeleton `gateway` + `payment-service` + `web/`; thống nhất parent pom/quy ước package |
| **1 — Phân tích (UCD + ERD)** | **Use Case Diagram** + đặc tả UC-04 (luồng chính + A1–A6 + BR1–BR7) | **ERD**: thực thể/thuộc tính/quan hệ/ràng buộc + phân ownership theo service | Xuất `.puml` → PNG, lập ma trận truy vết, review chéo cả hai |
| **2 — Kiến trúc** | Review phần giao tiếp **đồng bộ (REST)**, bảng routing gateway | Sở hữu phần giao tiếp **bất đồng bộ** (queue/event/outbox rules) | Vẽ **Microservices Architecture Diagram**, bảng mapping luồng gọi |
| **3 — REST API** | Chốt quy ước chung + bảng mã lỗi; API auth/user | API tuition + internal OTP | API payment + internal claim/debit; tạo Postman collection khung |
| **4 — CSDL** | Migration + seed `user_db` | Migration + seed `tuition_db`, `otp_db`, `notification_db` | Migration `payment_db`; viết 4 câu SQL kiểm chứng ràng buộc |
| **5 — Backend** | `user-service` + `gateway` + unit test | `tuition` + `otp` + `notification` + unit test | `payment-service`; tích hợp end-to-end, sửa lệch contract |
| **6 — Transaction & concurrency** | Hiện thực + test **Kịch bản A** (chạy lặp ×10) | Hiện thực + test **Kịch bản B** (chạy lặp ×10) | Test bù trừ saga + idempotency + outbox; viết `docs/concurrency.md` |
| **7 — Frontend** | Login, Dashboard, History | Màn hình Payment (3 nhóm) + OTP dialog (countdown, resend) | axios client + interceptor 401, trang Result, nối các màn hình |
| **8 — Kiểm thử** | Unit + integration cho service của mình | Unit + integration cho service của mình | Newman collection + E2E; ráp `docs/test-report.md` |
| **9 — Tài liệu & demo** | Dữ liệu demo + `reset-demo.sh`; **ráp báo cáo cuối** | Diễn tập demo Kịch bản B; chuẩn bị ảnh/evidence | Viết kịch bản demo, kiểm tra sơ đồ; trình bày (luân phiên) |

---

## 4. Đường găng (critical path) và thứ tự Sprint

**Đường găng:** `user-service` + `tuition-service` → `payment-service` (saga) → 2 test concurrency → demo.

| Sprint | Mốc bàn giao | A | B | C |
|---|---|---|---|---|
| **Sprint 1** | Chốt API contract + 2 service lõi chạy được | `user-service` chạy + login/debit | `tuition-service` chạy + lookup/claim | Migration `payment_db` + Postman khung + skeleton `web/` |
| **Sprint 2** | Backend đủ luồng UC-04 | `gateway` (routing + JWT filter) | `otp-service` + `notification-service` (email hiện ở MailHog) | `payment-service` happy path (login → confirm → SUCCESS) |
| **Sprint 3** | **Concurrency xanh (phần chấm điểm cao)** | Kịch bản A: test 10 luồng ×10 lần | Kịch bản B: test 2 luồng ×10 lần | Saga compensation + idempotency + outbox |
| **Sprint 4** | UI + test + báo cáo + demo | Login/Dashboard/History + test | Payment + OTP + test | Tích hợp UI + Newman/E2E + kịch bản demo |

**Điểm chặn cần nhớ:**
- Không ai được bắt đầu `payment-service` trước khi `user-service` và `tuition-service` có API ổn định (C có thể viết trước với mock/stub).
- Ai xong trước thì **review** phần của người khác, không ngồi chờ.

---

## 5. Quy tắc làm việc nhóm

1. **Nhánh Git:** `main` được bảo vệ → mỗi người làm trên `feature/<service>-<task>` → mở PR → **1 người khác review** → merge.
   **Ma trận review chéo:** A review B, B review C, C review A.
2. **Họp sync 10 phút mỗi ngày:** (1) đã xong gì, (2) đang bị chặn gì, (3) có thay đổi contract nào không.
3. **Definition of Done cho mỗi service:** API khớp `docs/03-rest-api-design.md`; unit test xanh; health check OK; không truy cập DB của service khác.
4. **Không sửa file dùng chung tuỳ tiện:** `docker-compose.yml`, `README.md`, `docs/README.md` — mỗi file có **1 chủ sở hữu** (A: docker-compose, B: docs/README, C: README gốc) và phải báo khi đổi.
5. **Commit message rõ ràng:** `feat(user-service): atomic debit`, `test(payment): scenario B concurrency`…
6. **Mọi thay đổi ảnh hưởng nghiệp vụ** (điều kiện hợp lệ của giao dịch, thời hạn OTP, quy tắc 1 lần) phải cập nhật lại đặc tả UC-04 trước khi code.

---

## 6. Cân bằng tải & cách điều chỉnh

| Tình huống | Điều chỉnh |
|---|---|
| **C quá tải** (payment + frontend + tích hợp) | Chuyển **trang History** cho A; chuyển **Postman collection** cho B. C giữ saga + tích hợp |
| **A hoặc B xong sớm** | Nhận thêm test integration, hoặc review + viết phần báo cáo của người đang chậm |
| **Có thành viên yếu hơn** | Giao `notification-service` + màn hình UI + viết test (ít phụ thuộc); ghép cặp (pair) với người làm saga khi làm Phase 6 |
| **Một người vắng dài ngày** | Ưu tiên đường găng: 2 người còn lại giữ `user-service`/`tuition-service`/`payment-service`; phần UI và báo cáo lùi lại |
| **Ai cũng bận tuần cuối** | Chốt trước: người ráp báo cáo (A) và người trình bày (B) — không để tuần cuối mới quyết |

**Tỉ lệ khối lượng tham chiếu:** A ≈ 33%, B ≈ 33%, C ≈ 34% (nhưng C đảm nhiệm "tích hợp" nên **không** giao thêm việc ráp báo cáo).

---

## 7. Ước lượng khối lượng công việc

| Hạng mục | Tỉ lệ | Phân bổ |
|---|---|---|
| Thiết lập + phân tích + thiết kế | ~20% | cả 3 cùng làm, chia theo artifact (mục 3) |
| Hiện thực service | ~35% | A: user + gateway; B: tuition + otp + notification; C: payment |
| Transaction & concurrency (code + chứng minh) | ~15% | A: Kịch bản A; B: Kịch bản B; C: saga/idempotency |
| Frontend | ~15% | chia theo màn hình, C tích hợp |
| Kiểm thử + tài liệu + demo | ~15% | mỗi người viết phần của mình, A ráp báo cáo |

---

## 8. Bảng đóng góp cá nhân (điền để nộp kèm báo cáo)

| Hạng mục | A | B | C |
|---|---|---|---|
| Service đã hiện thực (tên) | user-service, gateway | tuition, otp, notification | payment-service |
| Số API đã hiện thực | 4 | 5 | 6 |
| Test đã viết (số lượng / loại) | … | … | … |
| Màn hình UI đã làm | 3 | 2 | 3 |
| Phần báo cáo phụ trách | YC 3, 4, 7 | YC 3, 6, 7 | YC 2, 6, 8 |
| Tỉ lệ đóng góp tự đánh giá | …% | …% | …% |
| Ký xác nhận | | | |

---

## 9. Checklist theo tuần (dán vào bảng công việc nhóm)

**Tuần 1 — Phân tích & contract**
- [ ] Họp chốt Use Case + ERD (cả 3) → A hoàn tất UCD, B hoàn tất ERD
- [ ] Chốt bảng API Phase 3 (đặc biệt `/internal/**`) — **bắt buộc xong trước khi code**
- [ ] Phase 0: `docker compose up -d` chạy được trên máy cả 3 người

**Tuần 2 — Service lõi**
- [ ] A: login + debit chạy, unit test xanh
- [ ] B: tuition lookup + claim chạy, unit test xanh
- [ ] C: migration payment_db + Postman khung + skeleton web

**Tuần 3 — Hoàn thiện backend**
- [ ] B: otp + notification (email hiện ở MailHog)
- [ ] A: gateway routing + JWT filter
- [ ] C: payment happy path chạy end-to-end

**Tuần 4 — Concurrency (điểm cao)**
- [ ] A: Kịch bản A xanh ×10 lần, lưu ảnh log
- [ ] B: Kịch bản B xanh ×10 lần, lưu ảnh log
- [ ] C: compensation + idempotency + outbox xanh

**Tuần 5 — UI + kiểm thử**
- [ ] A: Login/Dashboard/History
- [ ] B: Payment + OTP dialog
- [ ] C: Result + tích hợp + Newman collection

**Tuần 6 — Báo cáo & demo**
- [ ] A ráp báo cáo đủ 8 yêu cầu + `reset-demo.sh`
- [ ] B + C diễn tập demo ≥ 2 lần (quay video dự phòng)
- [ ] Nộp: mã nguồn sạch (không `target/`, `node_modules/`, `.env`), báo cáo, video demo

---

## 10. Câu mẫu để viết vào báo cáo

> "Nhóm 3 thành viên phân chia công việc theo **miền nghiệp vụ (service)**: Thành viên A phụ trách `user-service` và `gateway` (xác thực, số dư và nghiệp vụ trừ tiền nguyên tử); Thành viên B phụ trách `tuition-service`, `otp-service` và `notification-service` (tra cứu học phí, vòng đời OTP và gửi email); Thành viên C phụ trách `payment-service` (điều phối giao dịch theo mô hình saga) và phần tích hợp. Các thành viên chốt trước **hợp đồng API (API contract)** để làm việc song song, mỗi service có một chủ sở hữu duy nhất và được **review chéo** bởi một thành viên khác trước khi merge. Hai kịch bản đồng thời của đề được phân công: Kịch bản A (nhiều giao dịch trên cùng tài khoản) do A hiện thực và kiểm chứng, Kịch bản B (nhiều người thanh toán cùng một khoản học phí) do B hiện thực và kiểm chứng; C chịu trách nhiệm cơ chế bù trừ của saga và tính idempotency."

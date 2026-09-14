# Phụ lục — Phân công 3 người trong 6 NGÀY

> Tài liệu này là **lịch thi công rút gọn** của `docs/10-teamwork-plan.md` (bản 6 tuần). Dùng khi thời gian chỉ còn **6 ngày**.
> Giả định: mỗi người làm ~6–8 giờ/ngày → tổng ~**18 người-ngày**. Vai trò A/B/C giữ nguyên như tài liệu 10.

---

## 0. Phạm vi 6 ngày — CHỐT NGAY (đây là quyết định quan trọng nhất)

**BẮT BUỘC phải có (đủ điểm):**
1. Luồng UC-04 chạy thật: đăng nhập → tra cứu MSSV → xác nhận → OTP qua email → trừ tiền → học phí PAID → email xác nhận.
2. **2 kịch bản concurrency của đề (§5) có test chạy thật và xanh** (đây là phần chấm điểm cao).
3. Web UI tối thiểu: Login + Dashboard + màn hình Thanh toán (3 nhóm) + OTP dialog + Result.
4. Báo cáo đủ 8 yêu cầu + biểu đồ; demo chạy được.

**CẮT / LÀM GỌN (để kịp 6 ngày):**
| Hạng mục | Quyết định 6 ngày |
|---|---|
| Gửi lại OTP (resend) + rate-limit 429 | **Cắt** (chỉ cần báo lỗi "OTP hết hạn") |
| Phân trang lịch sử giao dịch | Làm đơn giản (trả hết danh sách) |
| Redis | **Tuỳ chọn** — có thể dùng bảng DB cho TTL/idempotency, thêm Redis nếu còn thời gian |
| DLQ, circuit breaker, retry nâng cao | Cấu hình mặc định của Spring (đủ để demo outbox) |
| E2E Playwright tự động | **Thay bằng** kịch bản thủ công + ảnh/video |
| Trailing polish UI (Tailwind, animation) | Bỏ |
| Đăng ký tài khoản | Không làm (đề không yêu cầu) |

> Nếu trễ, xem **mục 6 — thang cắt giảm** để biết bỏ gì tiếp theo mà không mất điểm lõi.

---

## 1. Nguyên tắc trong 6 ngày

1. **Ngày 1 buổi sáng phải CHỐT API contract** (`docs/03-rest-api-design.md`): URI, request, response, status code, tên field của `/internal/**`. Sau đó **không đổi** — nếu đổi phải báo cả nhóm trong sync cuối ngày.
2. **Mỗi service một chủ sở hữu** — không hai người cùng sửa một service.
3. **Sync 15 phút cuối mỗi ngày:** hôm nay xong gì, ngày mai làm gì, ai đang bị chặn.
4. **Mỗi ngày phải "demo được" một thứ** — không để đến ngày 6 mới chạy thử lần đầu.
5. **Làm theo đường găng:** `user-service` + `tuition-service` → `payment-service` → test concurrency → demo.
6. **Commit mỗi ngày** (nhánh `feature/<service>` → PR → 1 người review → merge).

---

## 2. Phân vai (giữ nguyên từ tài liệu 10)

| | **A — Danh tính & Tiền** | **B — Học phí & OTP** | **C — Điều phối & Tích hợp** |
|---|---|---|---|
| Service | `user-service`, `gateway` | `tuition-service`, `otp-service`, `notification-service` | `payment-service` + Postman + tích hợp |
| DB | `users`, `accounts` | `students`, `tuition_fees`, `otp_codes`, `email_logs` | `transactions`, `outbox` |
| Concurrency | **Kịch bản A** (cùng tài khoản, không chi vượt số dư) | **Kịch bản B** (cùng 1 học phí, chỉ trả 1 lần) | Bù trừ saga + idempotency + outbox |
| UI | Login, Dashboard | Payment (3 nhóm), OTP dialog | axios/JWT client, Result |
| **Sơ đồ (diagram)** | **Use Case Diagram** + đặc tả UC-04 | **ERD** (thực thể + ràng buộc) | **Microservices Architecture Diagram** |
| Báo cáo | YC 3, 4, 7 + **ráp báo cáo cuối** | YC 3, 6 (B), 7 | YC 2 (sơ đồ), 6 (saga), 8 (demo) |

---

## 3. LỊCH CHI TIẾT TỪNG NGÀY

### NGÀY 1 — Nền móng + CHỐT CONTRACT

| | Buổi sáng | Buổi chiều | Cuối ngày phải có |
|---|---|---|---|
| **A** | `docker-compose.yml` (Postgres, RabbitMQ, MailHog) + `.env.example`; tạo `user_db` | Skeleton `user-service` + `gateway`; entity `User`/`Account` + migration `V1` | `docker compose up -d` OK; `user-service` + `gateway` chạy, `/actuator/health` = UP |
| **B** | Skeleton `tuition-service`, `otp-service`; migration `students`, `tuition_fees` | Migration `otp_codes`, seed data (2 user, 3 MSSV: 2 UNPAID, 1 PAID); `GET /tuitions?mssv=` trả dữ liệu | Cả 3 service chạy + lookup MSSV trả JSON đúng contract |
| **C** | **Chốt API contract** (bảng Phase 3) — in ra, dán vào nhóm; tạo skeleton `payment-service` | Migration `transactions`, `outbox`; Postman collection khung (login, me, tuitions) | Postman gọi được 2 API đầu; contract đã khoá |

**DoD ngày 1:** `docker compose up -d` + 4 service chạy; contract API **đã chốt và ghi vào `docs/03-rest-api-design.md`**; 3 sơ đồ nháp (UCD/ERD/MSA) đã có trên giấy/file.
**Rủi ro:** chốt contract quá lâu → giới hạn 2 giờ, phần còn lại bổ sung sau nhưng phải báo nhóm.
**Buổi tối ngày 1 (30–45 phút, vẽ nháp — KHÔNG bỏ):** A vẽ nháp **Use Case Diagram + đặc tả UC-04**; B vẽ nháp **ERD** (thực thể + quan hệ + ràng buộc); C vẽ nháp **Microservices Architecture Diagram** dựa trên contract vừa chốt. Đây là nền cho báo cáo YC 1–2 và không cần hoàn hảo — ngày 5 sẽ xuất bản chính thức.

---

### NGÀY 2 — Backend lõi (đường găng)

| | Buổi sáng | Buổi chiều | Cuối ngày phải có |
|---|---|---|---|
| **A** | `POST /auth/login` + JWT (BCrypt) | **Debit nguyên tử**: `UPDATE accounts SET balance=balance-:a WHERE id=:id AND balance>=:a`; API `/users/me`, `/users/me/accounts` | Login trả token; debit đủ/thiếu tiền hoạt động (curl) |
| **B** | `claim`/`release` học phí (conditional UPDATE `status='UNPAID'`) | `otp-service`: issue (hash + `expires_at = now()+5'`) + verify (đúng/còn hạn/1 lần) | Claim 2 lần → lần 2 thất bại; OTP issue/verify chạy (log OTP ra console để test) |
| **C** | `POST /payments` (tạo transaction + gọi lookup + gọi otp issue) | `POST /payments/{id}/confirm` happy path: verify OTP → claim → debit → SUCCESS | Chạy được luồng tới SUCCESS bằng Postman (chưa cần email) |

**DoD ngày 2:** luồng nội bộ chạy hết tới `SUCCESS`: balance giảm đúng, fee = PAID, có bản ghi transaction.
**Điểm chặn:** C chỉ bắt đầu `confirm` khi A xong debit và B xong claim → nếu trễ, C dùng mock/stub để không bị chặn.

---

### NGÀY 3 — Hoàn thiện luồng UC-04 (email + OTP thật)

| | Buổi sáng | Buổi chiều | Cuối ngày phải có |
|---|---|---|---|
| **A** | `gateway`: routing (`/auth`,`/users`→8081; `/tuitions`→8082; `/payments`→8083) + JWT filter + chặn `/internal/**` | Unit test user-service (login, debit, idempotency) + review PR của B | Gọi toàn bộ API **xuyên gateway** OK; request không token → 401 |
| **B** | `notification-service`: consumer RabbitMQ → gửi email qua MailHog; dedupe `message_id` | OTP: chuyển email thật qua queue; unit test otp (sai/hết hạn/dùng lại) | OTP **thật** đến MailHog (http://localhost:8025) |
| **C** | Nối email OTP vào luồng (outbox ở payment: ghi `EmailRequested` + relay publish) | Email xác nhận sau SUCCESS + `GET /payments/me` (lịch sử) | **Chạy trọn UC-04 bằng Postman**: login → payments → đọc OTP ở MailHog → confirm → SUCCESS + email xác nhận |

**DoD ngày 3 (mốc quan trọng nhất):** toàn bộ luồng nghiệp vụ của đề chạy thật end-to-end, có email OTP và email xác nhận trong MailHog.
**Fallback:** nếu queue/outbox trục trặc → tạm gửi email trực tiếp (đồng bộ) để có demo, tối quay lại outbox.

---

### NGÀY 4 — CONCURRENCY (phần chấm điểm cao)

| | Buổi sáng | Buổi chiều | Cuối ngày phải có |
|---|---|---|---|
| **A** | **Kịch bản A**: xác nhận debit dùng 1 câu UPDATE + `CHECK balance>=0`; viết test 10 luồng (`CountDownLatch`), seed số dư 1.000.000, mỗi luồng 300.000 | Chạy test `@RepeatedTest(10)`; lưu log/ảnh; bắt đầu trang Login/Dashboard (React) | Test A xanh ×10 lần: thành công ≤ 3, balance ≥ 0, không mất/không sinh tiền |
| **B** | **Kịch bản B**: test 2 người cùng confirm 1 MSSV (2 transaction khác nhau) | Chạy lặp ×10 lần, lưu log; bắt đầu màn hình Payment (3 nhóm, ô payer read-only) | Test B xanh: đúng 1 SUCCESS + 1 `409 FEE_ALREADY_PAID`; DB chỉ 1 PAID; chỉ 1 user bị trừ tiền |
| **C** | Test bù trừ (debit lỗi → fee quay lại UNPAID) + idempotency (gửi lại cùng key) | Test outbox (tắt notification → payment → bật lại → email đến) + axios client + trang Result | 3 test C xanh; skeleton web gọi được API qua gateway |

**DoD ngày 4:** 2 kịch bản §5 **có bằng chứng chạy thật** (ảnh log x2) — đây là thứ không được phép trượt.
**Nếu trễ:** tạm bỏ frontend chiều ngày 4, dồn sang ngày 5.

---

### NGÀY 5 — Giao diện + kiểm thử + tài liệu

| | Buổi sáng | Buổi chiều | Cuối ngày phải có |
|---|---|---|---|
| **A** | Xong Login + Dashboard (hồ sơ, số dư, lịch sử) | Nối luồng UI → Dashboard sau khi trả tiền; sửa lỗi hiển thị | UI đăng nhập + xem dashboard chạy thật |
| **B** | Màn hình Payment: tra cứu MSSV, cảnh báo thiếu tiền, **nút Xác nhận chỉ bật khi hợp lệ** | OTP dialog: đếm ngược 5:00, map lỗi 400/409/410/422 → thông báo tiếng Việt | Thanh toán được từ đầu tới OTP bằng UI |
| **C** | Trang Result + nối toàn bộ màn hình; hoàn thiện Postman collection (đủ luồng + luồng lỗi) | Chạy Newman; viết `docs/test-report.md`; hoàn thiện **Microservices Architecture Diagram** → xuất PNG | Newman xanh; bộ biểu đồ xuất xong |

**Cuối ngày 5 — xuất bản 3 sơ đồ chính thức (từ nháp ngày 1):** A xuất **Use Case Diagram** PNG; B xuất **ERD** PNG; C xuất **Microservices Architecture Diagram** PNG. Cả 3 dán vào `docs/` và gửi vào nhóm chat để A ráp báo cáo ngày 6.
**DoD ngày 5:** demo được toàn bộ từ UI; có bộ test + **3 sơ đồ PNG chính thức** cho báo cáo.

---

### NGÀY 6 — Đóng gói, báo cáo, diễn tập demo

| | Buổi sáng | Buổi chiều | Cuối ngày phải có |
|---|---|---|---|
| **A** | Ráp báo cáo 8 yêu cầu (dán biểu đồ, ảnh, bảng API); viết README chạy dự án + `reset-demo.sh` | **Diễn tập demo lần 1** toàn nhóm; sửa lỗi phát sinh | Báo cáo hoàn chỉnh; README chạy lại từ đầu được |
| **B** | Viết phần YC 6 (Kịch bản B) + ảnh bằng chứng; kiểm tra lại luồng OTP hết hạn | **Diễn tập demo lần 2** (có bấm giờ) + **quay video dự phòng** | Phần báo cáo của B xong; video demo đã quay |
| **C** | Kiểm tra sơ đồ kiến trúc + kịch bản demo từng phút; dọn mã nguồn (xoá `target/`, `node_modules/`, `.env`) | Chuẩn bị dữ liệu demo sạch (reset DB về trạng thái ban đầu); **đóng gói nộp** | Gói nộp: mã nguồn + báo cáo + video; dữ liệu demo sạch |

**DoD ngày 6:** nộp đủ; demo chạy trơn 2 lần; ai cũng biết phần mình phải nói khi trình bày.

---

## 4. Bảng tóm tắt nhanh (dán vào báo cáo / dán lên tường)

| Ngày | A (user+gateway) | B (tuition+otp+notification) | C (payment+web+tích hợp) | Mốc |
|---|---|---|---|---|
| **1** | docker-compose, user-service skeleton | tuition/otp skeleton, migration, seed | **Chốt contract**, payment skeleton, Postman khung | 4 service chạy, contract khoá |
| **2** | login + JWT + debit nguyên tử | claim/release + OTP issue/verify | create + confirm happy path | Luồng tới SUCCESS |
| **3** | gateway routing + JWT filter + test | notification email + OTP thật + test | outbox + email xác nhận + history | **UC-04 chạy end-to-end** |
| **4** | **Kịch bản A** + UI Login | **Kịch bản B** + UI Payment | compensation + idempotency + outbox + axios/Result | **2 test concurrency xanh** |
| **5** | Dashboard + sửa lỗi UI | OTP dialog + map lỗi | Newman + test-report + xuất diagram | UI chạy full flow |
| **6** | Ráp báo cáo + README + demo lần 1 | Phần báo cáo + demo lần 2 + quay video | Kịch bản demo + dọn mã + đóng gói | **Nộp** |

---

## 5. Phụ thuộc — ai chờ ai

```
Ngày 1:  contract API ─────► mọi người code song song
Ngày 2:  A(debit) ──┐
         B(claim) ──┴──► C(payment confirm)      [C không chờ: dùng stub nếu A/B chậm]
Ngày 3:  B(email+OTP) ──► C(luồng email)          A(gateway) độc lập
Ngày 4:  A(test A), B(test B), C(compensation) ── độc lập, chạy song song
Ngày 5:  A,B(UI) ──► C(tích hợp + Newman)
Ngày 6:  tất cả ──► A ráp báo cáo, C đóng gói
```

**Quy tắc chống chờ:** nếu việc của mình phụ thuộc người khác chưa xong → **tạo stub/mock** và tiếp tục, không ngồi đợi.

---

## 6. Thang cắt giảm nếu trễ (áp dụng theo thứ tự, không mất điểm lõi)

1. **Trễ ngày 3** → bỏ outbox, gửi email đồng bộ từ otp-service/payment-service (nêu trong báo cáo là "phương án đơn giản hoá").
2. **Trễ ngày 4** → giữ **test concurrency** (không được bỏ), cắt phần UI còn lại → demo bằng Postman cho luồng lỗi.
3. **Trễ ngày 5** → bỏ trang History, bỏ Newman tự động (dùng Postman bấm tay), vẫn giữ ảnh chứng minh.
4. **Trễ ngày 6** → ưu tiên: (a) demo chạy được, (b) 2 ảnh test concurrency trong báo cáo, (c) phần còn lại viết ngắn gọn.

**Tuyệt đối không cắt:** 2 kịch bản concurrency (YC 6), luồng UC-04 end-to-end, Use Case Diagram + ERD + Microservices Architecture Diagram.

---

## 7. Checklist cuối mỗi ngày (in ra, tick mỗi tối)

**Ngày 1:** [ ] docker compose up OK · [ ] 4 service health UP · [ ] contract API đã ghi vào docs · [ ] 3 sơ đồ nháp (UCD/ERD/MSA)
**Ngày 2:** [ ] login+JWT · [ ] debit đúng/thiếu tiền · [ ] claim 2 lần chặn được · [ ] luồng tới SUCCESS
**Ngày 3:** [ ] gọi API xuyên gateway · [ ] OTP thật tới MailHog · [ ] email xác nhận · [ ] **UC-04 end-to-end**
**Ngày 4:** [ ] Test A xanh ×10 · [ ] Test B xanh ×10 · [ ] compensation xanh · [ ] idempotency xanh · [ ] outbox xanh
**Ngày 5:** [ ] UI Login/Dashboard · [ ] UI Payment + OTP dialog · [ ] Result + lịch sử · [ ] Newman xanh · [ ] diagram PNG
**Ngày 6:** [ ] báo cáo 8 yêu cầu · [ ] README + reset-demo.sh · [ ] demo ×2 + video · [ ] mã nguồn sạch · [ ] **đã nộp**

---

## 8. Câu mẫu để viết vào báo cáo (mục phân công, bản 6 ngày)

> "Trong 6 ngày thực hiện, nhóm 3 thành viên phân công theo miền nghiệp vụ và chốt **API contract** ngay ngày đầu tiên để làm việc song song. Thành viên A phụ trách `user-service` và `gateway` (xác thực JWT, số dư và nghiệp vụ trừ tiền nguyên tử) đồng thời chịu trách nhiệm **Kịch bản đồng thời A**; Thành viên B phụ trách `tuition-service`, `otp-service`, `notification-service` (tra cứu học phí, vòng đời OTP, gửi email) và **Kịch bản đồng thời B**; Thành viên C phụ trách `payment-service` (điều phối giao dịch theo **mô hình saga**, outbox) cùng phần tích hợp và kịch bản demo. Tiến độ được kiểm soát theo từng ngày với mốc: ngày 3 hoàn tất luồng nghiệp vụ đầu-cuối, ngày 4 hoàn tất kiểm chứng hai kịch bản nhất quán dữ liệu, ngày 5 hoàn tất giao diện và kiểm thử, ngày 6 hoàn tất tài liệu và trình diễn."

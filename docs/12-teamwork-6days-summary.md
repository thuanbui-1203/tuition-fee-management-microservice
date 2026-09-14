# Phân công 3 người trong 6 ngày — Bản tóm tắt

> Bản dịch/tóm tắt tiếng Việt của phần phân công 6 ngày. Bản đầy đủ (chi tiết theo buổi sáng/chiều, sơ đồ phụ thuộc, checklist từng ngày) nằm ở `docs/11-teamwork-6days.md`; bản 6 tuần ở `docs/10-teamwork-plan.md`.

---

## 1. Vai trò (phân theo miền nghiệp vụ — service ownership)

| | **A — Danh tính & Tiền** | **B — Học phí & OTP** | **C — Điều phối & Tích hợp** |
|---|---|---|---|
| **Service** | `user-service`, `gateway` | `tuition-service`, `otp-service`, `notification-service` | `payment-service` + Postman + tích hợp |
| **Database** | `users`, `accounts` | `students`, `tuition_fees`, `otp_codes`, `email_logs` | `transactions`, `outbox` |
| **Chứng minh concurrency** | **Kịch bản A** (cùng tài khoản, không chi vượt số dư) | **Kịch bản B** (cùng 1 học phí, chỉ trả 1 lần) | Bù trừ saga + idempotency + outbox |
| **Giao diện** | Login, Dashboard | Payment (3 nhóm thông tin), OTP dialog | axios/JWT client, Result |
| **Sơ đồ** | **Use Case Diagram + đặc tả UC-04** | **ERD** | **Microservices Architecture Diagram** |

---

## 2. Lịch 6 ngày

### Ngày 1 — Nền móng + CHỐT API contract *(buổi tối: vẽ nháp 3 sơ đồ)*
- **A:** `docker-compose.yml`, skeleton `user-service` → tối: vẽ nháp Use Case Diagram + đặc tả UC-04
- **B:** skeleton `tuition`/`otp`, migration, seed data → tối: vẽ nháp ERD
- **C:** **chốt API contract**, skeleton `payment-service`, khung Postman → tối: vẽ nháp Microservices Architecture Diagram
- ✅ **DoD:** 4 service health UP; contract đã ghi vào `docs/03-rest-api-design.md`; 3 sơ đồ nháp đã có.

### Ngày 2 — Backend lõi
- **A:** đăng nhập + JWT + **trừ tiền nguyên tử** (`UPDATE ... WHERE balance >= :a`)
- **B:** **claim/release học phí** + OTP issue/verify (hash, TTL 5 phút, dùng 1 lần)
- **C:** `POST /payments` + `confirm` happy path (verify → claim → debit → SUCCESS)
- ✅ **DoD:** luồng chạy tới `SUCCESS` bằng Postman.

### Ngày 3 — Hoàn tất luồng UC-04 đầu-cuối
- **A:** gateway routing + JWT filter + chặn `/internal/**` + unit test
- **B:** `notification-service` → email OTP thật hiện ở MailHog + test OTP
- **C:** outbox → email OTP + email xác nhận + lịch sử giao dịch
- ✅ **DoD:** **toàn bộ luồng nghiệp vụ chạy end-to-end với email thật**.

### Ngày 4 — Concurrency (ngày giá trị điểm cao nhất)
- **A:** test **Kịch bản A** (10 luồng × 300.000 trên số dư 1.000.000, chạy lặp ×10)
- **B:** test **Kịch bản B** (2 người trả cùng 1 học phí, chạy lặp ×10)
- **C:** test bù trừ (compensation) + idempotency + outbox; bắt đầu axios/trang Result
- ✅ **DoD:** cả 2 kịch bản §5 xanh, có ảnh/log làm bằng chứng.

### Ngày 5 — Giao diện + kiểm thử + xuất sơ đồ chính thức
- **A:** Login/Dashboard → **xuất ảnh Use Case Diagram**
- **B:** màn hình Payment + OTP dialog (đếm ngược 5:00, map lỗi) → **xuất ảnh ERD**
- **C:** trang Result + tích hợp + Newman + `test-report` → **xuất ảnh Microservices Architecture Diagram**
- ✅ **DoD:** luồng chạy trọn vẹn từ UI; có 3 sơ đồ PNG chính thức + báo cáo kiểm thử.

### Ngày 6 — Đóng gói, báo cáo, diễn tập, nộp
- **A:** ráp báo cáo 8 phần + README + `reset-demo.sh` + diễn tập demo lần 1
- **B:** viết phần báo cáo (YC 6 — Kịch bản B) + diễn tập lần 2 + quay video dự phòng
- **C:** kịch bản demo + dọn mã nguồn (`target/`, `node_modules/`, `.env`) + đóng gói nộp
- ✅ **DoD:** báo cáo + demo + mã nguồn sạch đã nộp.

---

## 3. Nguyên tắc bảo vệ tiến độ

- **Tuyệt đối không cắt:** luồng UC-04 end-to-end, **2 kịch bản concurrency (YC 6)**, và **3 sơ đồ** (Use Case Diagram, ERD, Microservices Architecture Diagram — YC 1–2).
- **Thang cắt giảm khi trễ (theo thứ tự):** bỏ outbox → bỏ các trang UI phụ → bỏ Newman tự động → cuối cùng mới rút gọn phần chữ của báo cáo.
- **Chống chờ nhau:** nếu việc của mình phụ thuộc người khác chưa xong → dùng **stub/mock** và tiếp tục. Phụ thuộc cứng duy nhất: A(debit) + B(claim) → C(confirm) trong Ngày 2.
- **Sync 15 phút cuối mỗi ngày:** hôm nay xong gì → ngày mai làm gì → ai đang bị chặn.
- **Commit mỗi ngày:** nhánh `feature/<service>` → PR → 1 người khác review → merge.

---

## 4. Bảng tóm tắt một dòng mỗi ngày

| Ngày | A | B | C | Mốc |
|---|---|---|---|---|
| **1** | docker-compose, user-service skeleton | tuition/otp skeleton, migration, seed | **Chốt contract**, payment skeleton, Postman khung | 4 service chạy, contract khoá, 3 sơ đồ nháp |
| **2** | login + JWT + debit nguyên tử | claim/release + OTP issue/verify | create + confirm happy path | Luồng tới SUCCESS |
| **3** | gateway routing + JWT filter + test | notification email + OTP thật + test | outbox + email xác nhận + history | **UC-04 end-to-end** |
| **4** | **Kịch bản A** + UI Login | **Kịch bản B** + UI Payment | compensation + idempotency + outbox + axios/Result | **2 test concurrency xanh** |
| **5** | Dashboard + xuất UCD PNG | OTP dialog + xuất ERD PNG | Newman + test-report + xuất MSA PNG | UI chạy full flow + 3 sơ đồ |
| **6** | Ráp báo cáo + README + demo lần 1 | Phần báo cáo + demo lần 2 + quay video | Kịch bản demo + dọn mã + đóng gói | **Nộp** |

# Phase 5 — Lập trình services & API (Backend)

**Mục tiêu:** hiện thực đầy đủ luồng nghiệp vụ (yêu cầu 5 của đề): lập trình các service và REST API theo đúng thiết kế Phase 2–3.

**Đầu vào:** Phase 3 (API), Phase 4 (DB + migration).

**Sản phẩm bàn giao:** 6 backend project chạy được, gọi được theo đúng bảng API Phase 3; test mỗi service.

---

## 5.0. Quy ước chung cho mọi service (làm trước khi code nghiệp vụ)

**Cấu trúc package mỗi service** (`edu.tdtu.<service>`):
```
controller/   → REST controller (chỉ điều phối, không chứa nghiệp vụ)
service/      → business logic + @Transactional
repository/   → Spring Data JPA
entity/       → entity khớp schema Phase 4
dto/          → request/response DTO
exception/    → handler tập trung + ErrorResponse chuẩn (Phase 3.1)
config/       → security, openapi, rabbitmq, webclient
```

**Các việc chuẩn hoá lặp lại cho từng service:**
1. `@RestControllerAdvice` toàn service: bắt `MethodArgumentNotValidException`, `EntityNotFoundException`, nghiệp vụ exception → trả `{code, message, timestamp, traceId}` đúng bảng mã lỗi.
2. Security config: user/tuition/payment/otp/notification chặn toàn bộ trừ `/internal/**` (xác thực nội bộ đơn giản bằng service token hoặc để gateway xử lý public) và permit `/actuator/health`, `/swagger-ui/**`, `/v3/api-docs/**`, `/api/v1/ping`.
3. Log mọi request quan trọng kèm `paymentId`/`transactionId` để demo & gỡ lỗi.
4. Mỗi service có OpenAPI (springdoc): truy cập `http://localhost:<port>/swagger-ui.html`.

> **Lưu ý an toàn:** password dùng BCrypt; OTP chỉ lưu hash (SHA-256 kèm salt hoặc bcrypt); không log OTP/mã thẻ; không trả `password_hash`/`code_hash` trong API.

---

## 5.1. user-service (làm ĐẦU TIÊN — service khác phụ thuộc debit của nó)

**Công việc chi tiết:**
1. Entity `User`, `Account` (khớp `user_db`); repository.
2. **Đăng nhập + JWT:**
   - `POST /auth/login`: tìm user theo username → `BCrypt.matches(password, hash)` → tạo JWT (HS256, secret chung Phase 0, hạn 30 phút, claims: `userId`, `username`).
   - Cấu hình Spring Security permit `/auth/login`, còn lại require token (dùng cho dev; gateway sẽ là nơi xác thực chính khi có gateway).
3. **API hồ sơ:** `GET /users/me`, `GET /users/me/accounts` (lấy userId từ token).
4. **Debit service (QUAN TRỌNG NHẤT — nền tảng BR7):**
   - Method `DebitResult debit(accountId, amount, idempotencyKey, transactionId)`:
     - Kiểm tra idempotency: nếu key đã xử lý → trả kết quả cũ (lưu trong Redis hoặc bảng `debit_keys`).
     - Thực thi 1 câu UPDATE (native query hoặc `@Modifying`):
       `UPDATE accounts SET balance = balance - :amount, version = version + 1, updated_at = now() WHERE id = :accountId AND balance >= :amount`
     - `rows == 1` → thành công (trả `newBalance`); `rows == 0` → kiểm tra lại: tài khoản không tồn tại (404) hay thiếu tiền (422 INSUFFICIENT_BALANCE).
   - Bọc trong `@Transactional`; không dùng read-then-write trong Java (dễ race) — **phải là 1 câu UPDATE điều kiện**.
5. **Tests bắt buộc (JUnit):** login đúng/sai; debit đủ tiền; debit thiếu tiền → 422; debit 2 lần cùng idempotency key → cùng kết quả, chỉ trừ 1 lần; test concurrency (Phase 6 sẽ mở rộng).

## 5.2. tuition-service (làm thứ 2)

**Công việc chi tiết:**
1. Entity `Student`, `TuitionFee`; repository (tìm fee theo `student_id` + `status='UNPAID'`).
2. `GET /tuitions?mssv=` → tìm student theo mssv → tìm fee UNPAID → map DTO; fee PAID → ném `FEE_ALREADY_PAID` (409); không thấy → `NOT_FOUND` (404).
3. **Claim service (BR6):**
   - `claim(feeId, transactionId)`: 1 câu UPDATE
     `UPDATE tuition_fees SET status='PAID', paid_transaction_id=:txn, updated_at=now() WHERE id=:feeId AND status='UNPAID'`
     - `rows==1` → claim thành công; `rows==0` → 409 `FEE_ALREADY_PAID`.
   - `release(feeId, transactionId)`: compensation — chỉ đưa về UNPAID nếu `paid_transaction_id = :txn` (không release nhầm giao dịch khác đã trả thật).
   - *Giải thích trong code comment:* claim + release dùng để Kịch bản B không bao giờ trả 2 lần.
4. **Tests:** lookup có fee UNPAID; lookup fee PAID → 409; claim 2 lần → lần 2 thất bại; release đúng/ sai chủ giao dịch.

## 5.3. otp-service (làm thứ 3)

**Công việc chi tiết:**
1. Entity `OtpCode`; repository với query: tìm ACTIVE theo transaction_id.
2. **Issue OTP:**
   - Sinh mã 6 chữ số ngẫu nhiên (SecureRandom — không dùng `Random`).
   - Lưu: `code_hash = hash(code)`, `expires_at = now() + 5 phút`, `status='ACTIVE'`, `transaction_id`.
   - Unique index (Phase 4) tự chặn OTP ACTIVE thứ 2 — nếu vi phạm (resend), đánh dấu OTP cũ EXPIRED trước khi insert.
   - Ghi event vào outbox/chủ động publish `EmailRequested {to, template:"otp", data:{otp}}` lên RabbitMQ → notification-service gửi. *(Nếu otp-service dùng outbox thì thêm bảng `outbox` — hoặc publish trực tiếp kèm giải thích rủi ro; khuyến nghị outbox để đồng bộ với payment.)*
3. **Verify OTP:**
   - Tìm OTP ACTIVE của transaction → nếu không có/EXPIRED → 410 `OTP_EXPIRED`; so hash → sai → tăng `attempts`, quá 5 lần → EXPIRED, trả 400 `OTP_INCORRECT`; đúng → `status='VERIFIED'` (atomic: `UPDATE otp_codes SET status='VERIFIED' WHERE id=? AND status='ACTIVE'`, rows==0 → 409 `OTP_ALREADY_USED`).
4. **Rate-limit resend:** Redis `INCR` key `otp:resend:<txnId>` TTL 60s → quá 1 lần/phút → 429.
5. **Tests:** issue → verify đúng; sai mã; hết hạn (giả lập thời gian); dùng lại OTP đã VERIFIED → 409; resend bị giới hạn 429.

## 5.4. notification-service (làm thứ 4 — độc lập, có thể làm song song)

**Công việc chi tiết:**
1. RabbitMQ listener queue `email.send` → nhận `EmailRequested`.
2. Dedupe: kiểm tra `message_id` trong `email_logs` (UNIQUE) → trùng thì bỏ qua (ack).
3. Gửi email qua Spring Mail tới MailHog (SMTP localhost:1025, no auth). Template email đơn giản bằng text/văn bản HTML thủ công: `[iBanking] Mã OTP của bạn là 482913 (hiệu lực 5 phút)`; `[iBanking] Thanh toán học phí thành công - Số tiền 8,400,000 VND`.
4. Thành công → `email_logs.status='SENT'`; thất bại → retry tối đa 3 lần có backoff → vẫn lỗi → `FAILED` + ghi `last_error` (demo được "không mất email khi service chết giữa chừng").
5. **Tests:** consume message → email_logs có bản ghi SENT (kiểm tra MailHog API `GET localhost:8025/api/v2/messages`); message trùng → không gửi lần 2.

## 5.5. payment-service (làm CUỐI trong nhóm backend — điều phối viên)

**Công việc chi tiết:**
1. Entity `Transaction` + state machine enum: `INITIATED → OTP_SENT → OTP_VERIFIED → SUCCESS/FAILED`. Mọi thay đổi trạng thái qua 1 method `transitionTo(newState)` ghi log lý do.
2. WebClient beans tới: user-service (debit), tuition-service (lookup/claim/release), otp-service (issue/verify) — base URL từ config.
3. **POST /payments (khởi tạo):**
   - Nhận `Idempotency-Key` → kiểm tra key tồn tại → trả giao dịch cũ (idempotent).
   - Gọi tuition lookup (kiểm tra fee UNPAID + lấy amount) → tạo `Transaction(INITIATED)` → gọi otp issue → chuyển `OTP_SENT` → commit (trong 1 transaction DB).
   - Lỗi business → map exception đúng code (404/409/422); lỗi gọi service → 503.
4. **POST /payments/{id}/confirm (saga — lõi nhất):**
   ```
   @Transactional:
     1. transaction = findById; kiểm tra status == OTP_SENT (khác → 422 TRANSACTION_INVALID_STATE)
     2. otpService.verify(txnId, otp)     // ngoài DB local; lỗi → map 400/409/410
     3. status = OTP_VERIFIED
     4. tuitionService.claim(feeId, txnId)   // 409 FEE_ALREADY_PAID → FAILED
     5. try debit(accountId, amount, idemKey)
        catch INSUFFICIENT_BALANCE/error → COMPENSATE: tuitionService.release(feeId, txnId)
            → status = FAILED → ném 422
     6. status = SUCCESS; completed_at = now()
     7. outbox.insert(TransactionSucceeded {to, txnId, amount, ...})  // CÙNG transaction
   commit → outbox relay publish
   ```
   > Lưu ý: claim ở bước 4 đã đổi fee sang PAID; nếu debit thất bại thì release đưa fee về UNPAID — đúng saga compensation. (Có thể chọn claim "2 pha" để fee chỉ PAID sau debit; nhưng với quy mô này, claim+release là đủ — ghi rõ lựa chọn vào báo cáo.)
5. **Outbox relay:** `@Scheduled` mỗi 1–2s (hoặc transaction event listener): đọc outbox `PENDING` → publish RabbitMQ → đánh `SENT`. (Nếu dùng listener + `TransactionSynchronization` gửi sau commit thì vẫn cần outbox để chịu lỗi crash — khuyến nghị relay định kỳ cho dễ demo.)
6. **GET /payments/me** (phân trang theo Phase 3), **GET /payments/{id}** (kiểm tra chủ sở hữu → 403).
7. **Tests:** happy path full flow (mock hoặc Testcontainers); từng lỗi A1–A6; retry cùng idempotency key → không trừ 2 lần; outbox → sau khi publish, status SENT.

## 5.6. gateway (làm sau cùng của backend)

**Công việc chi tiết:**
1. Routes theo bảng Phase 2.5 (`/api/v1/auth/**`→8081, `/users/**`→8081, `/tuitions/**`→8082, `/payments/**`→8083).
2. Global JWT filter: permit `/api/v1/auth/login`, `/actuator/**`, `/swagger-ui/**`; còn lại verify JWT (cùng secret/key với user-service) → nếu sai/thiếu → 401 JSON chuẩn. Lấy `userId` từ claim chèn header `X-User-Id` cho service downstream.
3. **Chặn `/internal/**`** → 404 (không lộ nội bộ).
4. CORS cho origin frontend (`http://localhost:5173`).
5. Rate-limit đơn giản ở gateway cho `/payments/**` (Redis) — hoặc để otp-service tự lo resend; chọn 1 và ghi rõ.
6. **Tests:** request không token → 401; login → có token → gọi `/users/me` xuyên gateway OK; `/internal/**` → 404.

---

## 5.7. Thứ tự triển khai & kiểm tra liên service (integration)

1. user-service → test độc lập.
2. tuition-service → test độc lập.
3. otp-service → test độc lập (cần RabbitMQ + notification sẵn sàng cho email).
4. notification-service → test độc lập (MailHog).
5. **Tích hợp tay (trước khi code payment):** dùng curl/Postman gọi debit/claim/issue/verify nội bộ để chắc chắn contract đúng.
6. payment-service + gateway → chạy full flow bằng Postman collection Phase 3.8.
7. Sửa mọi khác biệt contract (tên field, mã lỗi) ngay bước này trước khi vào Phase 6.

## 5.8. Checklist hoàn thành Phase 5 (Definition of Done mỗi service)

- [ ] Mọi API trong bảng Phase 3 hiện thực đúng URI/method/request/response/status code
- [ ] Response lỗi đúng định dạng chuẩn + mã lỗi bảng Phase 3.2
- [ ] Debit & claim dùng **conditional UPDATE 1 câu**, không read-then-write
- [ ] OTP chỉ lưu hash; không log OTP
- [ ] Mỗi service có unit test quan trọng nhất chạy xanh
- [ ] Full flow UC-04 chạy được bằng Postman từ login → confirm (email hiện ở MailHog)

**Acceptance criteria:** chạy `docker compose up -d` + 6 service → thực hiện trọn luồng UC-04 bằng Postman thành công (SUCCESS, balance giảm đúng, fee = PAID, email xác nhận trong MailHog); mọi lỗi nghiệp vụ (sai OTP, hết hạn, thiếu tiền, fee đã trả) trả đúng status code.

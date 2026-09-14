# Phase 6 — Transaction & Concurrency (tính nhất quán)

**Mục tiêu:** hiện thực cơ chế xử lý **transaction và concurrency**, chứng minh tính nhất quán dữ liệu trong 2 tình huống đồng thời của đề (§5) — đây là yêu cầu 6 và là phần "nặng" nhất để chấm điểm.

**Đầu vào:** Phase 5 (payment-service đã có saga cơ bản).

**Sản phẩm bàn giao:** các cơ chế (mục 6.2) được hiện thực đúng chỗ + **bộ test chứng minh 2 kịch bản chạy thật** + file `docs/concurrency.md` giải thích.

---

## 6.1. Hai kịch bản bắt buộc (trích §5 đề bài)

| Kịch bản | Mô tả | Hệ quả nếu sai | Mục tiêu nhất quán |
|---|---|---|---|
| **A** — Nhiều giao dịch trên cùng 1 tài khoản | Nhiều giao dịch đồng thời cùng trừ trên một số dư | Chi tiêu **vượt quá số dư khả dụng** | Tổng số tiền trừ không bao giờ > số dư |
| **B** — Nhiều tài khoản thanh toán cùng 1 học phí | Hai người đồng thời thanh toán cho cùng MSSV | Học phí bị thanh toán **2 lần** | Chỉ đúng **1 giao dịch** thành công cho 1 khoản học phí |

## 6.2. Các cơ chế sẽ dùng (giải thích từng cái — viết vào `docs/concurrency.md`)

### 6.2.1. Conditional UPDATE (cập nhật có điều kiện) — chống A và B
- **Debit (user-service):**
```sql
UPDATE accounts
   SET balance = balance - :amount,
       version = version + 1,
       updated_at = now()
 WHERE id = :accountId
   AND balance >= :amount          -- điều kiện: đủ tiền
```
`rows == 0` → không trừ (thiếu tiền / không tồn tại). Câu UPDATE là **nguyên tử** ở mức DB → 2 giao dịch cùng lúc không bao giờ cùng vượt ngưỡng.
- **Claim fee (tuition-service):**
```sql
UPDATE tuition_fees
   SET status = 'PAID',
       paid_transaction_id = :txnId,
       updated_at = now()
 WHERE id = :feeId
   AND status = 'UNPAID'           -- điều kiện: chưa trả
```
`rows == 0` → fee đã bị người khác claim → Kịch bản B tự động chặn.

**Tại sao không dùng kiểu Java "đọc rồi kiểm tra rồi ghi"?** Vì giữa lúc đọc và ghi, một giao dịch khác có thể xen vào (race condition) → sai số dư. Conditional UPDATE gộp kiểm tra + ghi thành 1 thao tác nguyên tử.

### 6.2.2. Optimistic locking (version) — phụ trợ cho A
- Cột `version` trên `accounts`. JPA `@Version` → nếu 2 tiến trình cùng sửa 1 tài khoản, tiến trình sau bị `OptimisticLockException` → trả lỗi "giao dịch xung đột, vui lòng thử lại".
- Dùng **kết hợp** với conditional UPDATE (6.2.1 là lớp chặn chính; version là lớp phòng thủ thứ 2 và giúp UI biết số dư đã đổi).

### 6.2.3. Unique constraint & partial unique index — chống B và trùng lặp
- `transactions.idempotency_key UNIQUE` → retry không tạo giao dịch thứ 2.
- `UNIQUE INDEX otp_codes(transaction_id) WHERE status='ACTIVE'` → chỉ 1 OTP sống/giao dịch.
- Nếu muốn lớp chặn cứng thứ 2 cho B: partial unique index `tuition_fees(paid_transaction_id) WHERE status='PAID'` (mỗi fee chỉ được trỏ tới 1 giao dịch).

### 6.2.4. Idempotency (chống trùng khi retry) — mọi bước ghi
- Client gửi `Idempotency-Key` (Phase 3). payment-service lưu key trong bảng `transactions`; debit (user-service) lưu key đã xử lý (Redis hoặc bảng `debit_keys`).
- Cùng key + cùng payload → trả kết quả cũ (không trừ lần 2). Cùng key + khác payload → 409 `IDEMPOTENCY_CONFLICT`.

### 6.2.5. Saga + compensation — phối hợp nhiều service (A/B đều chạm)
- Vì trừ tiền (user) và đánh dấu học phí (tuition) ở 2 database khác nhau → không có 1 ACID transaction toàn cục → dùng **Saga orchestration** bởi payment-service (đã phác thảo Phase 2.4, 5.5):
  - Bước thuận: `claim fee → debit → SUCCESS + outbox email`.
  - Bước ngược (compensation): nếu `debit` thất bại → gọi `release(feeId, txnId)` đưa fee về UNPAID → giao dịch `FAILED` → không có tiền bị trừ, học phí trả lại trạng thái chưa trả.
- Điều kiện an toàn của release: chỉ release nếu `paid_transaction_id = txnId` của chính giao dịch đang bù trừ (không release nhầm giao dịch khác).

### 6.2.6. Outbox pattern — đảm bảo event không mất (phụ trợ email)
- Ghi event `TransactionSucceeded`/`EmailRequested` vào bảng `outbox` **trong cùng DB transaction** với thay đổi trạng thái → relay publish lên RabbitMQ sau commit. Crash giữa chừng → event vẫn còn trong outbox → publish lại khi service khởi động lại. (Chi tiết Phase 5.5.)

### 6.2.7. (Tham khảo, không bắt buộc) Pessimistic lock
- `SELECT ... FOR UPDATE` trên `accounts` khi đọc số dư để hiển thị. Với quy mô này không cần cho mọi nơi; conditional UPDATE đã đủ. Nhắc trong báo cáo như một phương án thay thế để chứng tỏ hiểu biết.

## 6.3. Thiết kế test cho từng kịch bản (bắt buộc chạy thật — evidence cho báo cáo)

### Test Kịch bản A — không chi vượt số dư
**Cách viết (integration test ở user-service hoặc qua payment):**
- Số dư ban đầu: 1.000.000. Chuẩn bị 10 luồng, mỗi luồng debit 300.000 cùng lúc (dùng `ExecutorService` + `CountDownLatch` để bắn đồng thời).
- Assert:
  1. Số lần thành công ≤ 3 (vì 4×300.000 > 1.000.000);
  2. Số dư cuối ≥ 0;
  3. `sum(amount thành công) + balance cuối == 1.000.000` (không mất tiền, không in tiền).
- Chạy lặp 10 lần để chắc chắn không flaky.

### Test Kịch bản B — học phí chỉ trả 1 lần
**Cách viết (integration test payment-service + tuition-service thật):**
- 2 user khác nhau (A, C), cùng 1 MSSV có fee UNPAID.
- Bắn đồng thời 2 `POST /payments/confirm` (sau khi cả 2 đã có OTP hợp lệ của giao dịch riêng).
- Assert:
  1. Đúng 1 response `SUCCESS`, response còn lại `409 FEE_ALREADY_PAID`;
  2. DB tuition: fee duy nhất 1 bản ghi `PAID`;
  3. Chỉ 1 giao dịch `SUCCESS` trong payment_db cho fee đó;
  4. Chỉ 1 user bị trừ tiền.

### Test idempotency
- Gửi `POST /payments` 2 lần cùng `Idempotency-Key` → trả cùng `paymentId`, DB chỉ 1 bản ghi.
- Gửi `confirm` 2 lần cùng key → chỉ 1 lần SUCCESS/trừ tiền, lần 2 trả kết quả cũ.

### Test saga compensation
- Giả lập debit luôn thất bại (mock user-service trả 422 hoặc số dư không đủ):
  - `confirm` trả 422 `INSUFFICIENT_BALANCE` (hoặc 503 nếu mock lỗi hệ thống);
  - fee trong tuition_db **quay lại UNPAID** (release thành công);
  - transaction = FAILED; không user nào bị trừ tiền.

### Test OTP edge (từ UC-05 / BR3-5)
- OTP đúng → SUCCESS; OTP sai → 400 và đếm attempts; sai 5 lần → khoá; OTP dùng lần 2 → 409; OTP sau 5 phút → 410; OTP của giao dịch X dùng cho giao dịch Y → 400/410.

### Test outbox/async (không mất email)
- Tắt notification-service → thực hiện 1 payment thành công → bật lại notification-service → trong vòng vài giây email xác nhận xuất hiện trong MailHog (outbox PENDING → SENT khi relay chạy lại).

## 6.4. Bảng ánh xạ: kịch bản → cơ chế → nơi code → test

| Kịch bản / yêu cầu | Cơ chế chính | Nơi hiện thực | Test chứng minh |
|---|---|---|---|
| A: nhiều giao dịch 1 tài khoản | conditional UPDATE `balance>=amount` (+ version) | user-service debit | Test A (10 luồng) |
| B: nhiều người trả 1 học phí | conditional UPDATE `status='UNPAID'` + unique | tuition-service claim | Test B (2 luồng) |
| Retry trùng | idempotency_key UNIQUE | payment-service | Test idempotency |
| Lỗi giữa saga | saga compensation (release) | payment-service confirm | Test compensation |
| OTP | hash + expiry + single-use | otp-service | Test OTP edge |
| Không mất email | outbox + relay | payment-service | Test outbox |

## 6.5. Những cạm bẫy cần tránh (kinh nghiệm)

1. **Không** viết debit kiểu: đọc balance → `if (balance >= amt)` → `save(balance-amt)` trong Java — sai vì race (trừ khi có `@Lock(PESSIMISTIC_WRITE)` + FOR UPDATE, mà vẫn chậm hơn conditional UPDATE).
2. **Không** để 2 service ghi chung 1 bảng để "cho dễ transaction" — phá vỡ microservices; dùng saga.
3. **Không** gửi event trước commit (gửi xong rollback → email gửi nhầm) — phải qua outbox.
4. **Không** release fee vô điều kiện — release phải kèm `paid_transaction_id` của chính mình, nếu không sẽ "mở khoá" fee mà giao dịch khác đã trả thật.
5. Test concurrency phải dùng **latch** để các luồng bắn đúng đồng thời, không chạy tuần tự ngẫu nhiên.

## 6.6. Checklist hoàn thành Phase 6

- [ ] Cả 2 kịch bản A & B có test chạy thật, chạy xanh nhiều lần liên tiếp
- [ ] Test idempotency, compensation, OTP edge, outbox đều xanh
- [ ] `docs/concurrency.md` giải thích từng cơ chế + ảnh/chụp kết quả test (bằng chứng cho báo cáo)
- [ ] Không tồn tại code read-then-write cho debit/claim
- [ ] Log mỗi saga có paymentId để trình diễn

**Acceptance criteria:** chạy `mvn test` ở các service liên quan → tất cả test mục 6.3 xanh; chạy lại Test A 10 lần và Test B 10 lần không fail; có thể demo sống 2 kịch bản bằng 2 cửa sổ (Phase 9).

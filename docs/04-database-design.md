# Phase 4 — Thiết kế & hiện thực cơ sở dữ liệu

**Mục tiêu:** thiết kế và hiện thực CSDL của phân hệ **bằng SQL và/hoặc NoSQL phù hợp** (yêu cầu 4 của đề), đảm bảo các ràng buộc nghiệp vụ được ép buộc ngay tại tầng dữ liệu.

**Đầu vào:** Phase 1 (ERD logical + ownership), Phase 2 (mỗi service 1 DB).

**Sản phẩm bàn giao:** script migration (Flyway) cho 5 service + seed data + file `docs/database.md` giải thích lựa chọn SQL/NoSQL.

---

## 4.1. Giải thích lựa chọn SQL và/hoặc NoSQL (viết vào báo cáo)

| Nhu cầu | Chọn | Lý do |
|---|---|---|
| Tiền, số dư, học phí, giao dịch — cần **ACID transaction** và ràng buộc toàn vẹn mạnh | **SQL (PostgreSQL)** | CHECK, UNIQUE, FK, `UPDATE ... WHERE` có điều kiện, version (lock) — là công cụ đúng để chứng minh yêu cầu 6 |
| OTP: cần TTL tự động hết hạn, đếm lần nhập, chống dùng lại | PostgreSQL (bảng `otp_codes` + job/kiểm tra lúc verify) **hoặc** Redis `SETEX` | Lưu PostgreSQL để minh hoạ ràng buộc; **Redis dùng thêm** cho TTL nhanh & rate-limit |
| Rate-limit resend OTP, khoá phân tán, cache idempotency key | **NoSQL: Redis** | Cấu trúc đơn giản, tự hết hạn — đúng chỗ dùng NoSQL |

> **Kết luận trình bày:** dự án dùng **SQL (PostgreSQL) làm nguồn dữ liệu chính** cho mọi thực thể nghiệp vụ vì tính chất tài chính cần ACID; **NoSQL (Redis)** phục vụ các nhu cầu phụ trợ (TTL, rate-limit, khoá) — thể hiện đúng câu "SQL và/hoặc NoSQL phù hợp".

## 4.2. Cấu trúc migration theo service (Flyway)

Mỗi service đặt migration tại `src/main/resources/db/migration/`:

| Service | File | Nội dung |
|---|---|---|
| user-service | `V1__create_users_accounts.sql`, `V2__seed.sql` | bảng users, accounts |
| tuition-service | `V1__create_students_tuition_fees.sql`, `V2__seed.sql` | bảng students, tuition_fees |
| payment-service | `V1__create_transactions_outbox.sql` | bảng transactions, outbox |
| otp-service | `V1__create_otp_codes.sql` | bảng otp_codes |
| notification-service | `V1__create_email_logs.sql` | bảng email_logs |

**Quy tắc:** `spring.jpa.hibernate.ddl-auto=validate` (Phase 0) — schema chỉ do Flyway tạo, Hibernate chỉ kiểm tra khớp entity.

## 4.3. Script migration mẫu (template chi tiết)

**user_db — `V1__create_users_accounts.sql`:**
```sql
CREATE TABLE users (
    id            BIGSERIAL PRIMARY KEY,
    username      VARCHAR(50)  NOT NULL UNIQUE,
    password_hash VARCHAR(100) NOT NULL,
    full_name     VARCHAR(100) NOT NULL,
    phone         VARCHAR(20)  NOT NULL,
    email         VARCHAR(100) NOT NULL UNIQUE,
    created_at    TIMESTAMP    NOT NULL DEFAULT now()
);

CREATE TABLE accounts (
    id         BIGSERIAL PRIMARY KEY,
    user_id    BIGINT        NOT NULL UNIQUE REFERENCES users(id),
    balance    DECIMAL(15,2) NOT NULL DEFAULT 0 CHECK (balance >= 0),  -- BR7
    version    INT           NOT NULL DEFAULT 0,                       -- optimistic lock
    updated_at TIMESTAMP     NOT NULL DEFAULT now()
);
CREATE INDEX idx_accounts_user ON accounts(user_id);
```

**tuition_db — `V1__create_students_tuition_fees.sql`:**
```sql
CREATE TABLE students (
    id         BIGSERIAL PRIMARY KEY,
    mssv       VARCHAR(20)  NOT NULL UNIQUE,
    full_name  VARCHAR(100) NOT NULL,
    class_name VARCHAR(50),
    faculty    VARCHAR(100)
);

CREATE TABLE tuition_fees (
    id                  BIGSERIAL PRIMARY KEY,
    student_id          BIGINT        NOT NULL REFERENCES students(id),
    semester            VARCHAR(20)   NOT NULL,
    amount              DECIMAL(15,2) NOT NULL CHECK (amount > 0),
    status              VARCHAR(10)   NOT NULL DEFAULT 'UNPAID'
                          CHECK (status IN ('UNPAID','PAID')),
    paid_transaction_id UUID,          -- FK logic tới payment-service (không tạo FK vật lý)
    updated_at          TIMESTAMP     NOT NULL DEFAULT now()
);
CREATE INDEX idx_fees_student_status ON tuition_fees(student_id, status);
```

**payment_db — `V1__create_transactions_outbox.sql`:**
```sql
CREATE TABLE transactions (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    idempotency_key  VARCHAR(100) NOT NULL UNIQUE,
    payer_user_id    BIGINT       NOT NULL,   -- FK logic tới user-service
    tuition_fee_id   BIGINT       NOT NULL,   -- FK logic tới tuition-service
    amount           DECIMAL(15,2) NOT NULL CHECK (amount > 0),
    status           VARCHAR(20)  NOT NULL CHECK (status IN
                       ('INITIATED','OTP_SENT','OTP_VERIFIED','SUCCESS','FAILED')),
    created_at       TIMESTAMP    NOT NULL DEFAULT now(),
    completed_at     TIMESTAMP
);
CREATE INDEX idx_txn_payer ON transactions(payer_user_id);
CREATE INDEX idx_txn_status ON transactions(status);

CREATE TABLE outbox (
    id         BIGSERIAL PRIMARY KEY,
    event_type VARCHAR(50) NOT NULL,
    payload    JSONB       NOT NULL,
    status     VARCHAR(10) NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING','SENT')),
    created_at TIMESTAMP   NOT NULL DEFAULT now()
);
CREATE INDEX idx_outbox_status ON outbox(status) WHERE status = 'PENDING';
```

**otp_db — `V1__create_otp_codes.sql`:**
```sql
CREATE TABLE otp_codes (
    id             BIGSERIAL PRIMARY KEY,
    transaction_id UUID         NOT NULL,     -- gắn đúng 1 giao dịch (BR3)
    code_hash      VARCHAR(100) NOT NULL,     -- SHA-256/bcrypt của mã, KHÔNG lưu mã gốc
    expires_at     TIMESTAMP    NOT NULL,     -- = created_at + 5 phút (BR4)
    status         VARCHAR(10)  NOT NULL DEFAULT 'ACTIVE'
                     CHECK (status IN ('ACTIVE','VERIFIED','EXPIRED')),
    attempts       INT          NOT NULL DEFAULT 0,
    created_at     TIMESTAMP    NOT NULL DEFAULT now()
);
CREATE INDEX idx_otp_expires ON otp_codes(expires_at);
-- Chỉ 1 OTP ACTIVE cho 1 giao dịch (BR3 + chống spam):
CREATE UNIQUE INDEX uq_otp_active_txn ON otp_codes(transaction_id) WHERE status = 'ACTIVE';
```

**notification_db — `V1__create_email_logs.sql`:**
```sql
CREATE TABLE email_logs (
    id          BIGSERIAL PRIMARY KEY,
    message_id  VARCHAR(100) NOT NULL UNIQUE,   -- dedupe event lặp
    to_email    VARCHAR(100) NOT NULL,
    subject     VARCHAR(200) NOT NULL,
    status      VARCHAR(10)  NOT NULL DEFAULT 'PENDING'
                  CHECK (status IN ('PENDING','SENT','FAILED')),
    attempts    INT          NOT NULL DEFAULT 0,
    last_error  TEXT,
    created_at  TIMESTAMP    NOT NULL DEFAULT now()
);
```

## 4.4. Seed data mẫu (cho demo — cảnh báo: dùng bcrypt thật khi code)

**user_db `V2__seed.sql`** (password đều là `secret123`, hash sinh bằng BCrypt khi code):
| username | full_name | phone | email | balance |
|---|---|---|---|---|
| `nguyenvana` | Nguyễn Văn A | 0901234567 | `nguyenvana@example.com` | 10.000.000 |
| `tranthib` | Trần Thị B | 0907654321 | `tranthib@example.com` | 500.000 (đủ để demo lỗi INSUFFICIENT_BALANCE với fee 8.400.000) |

**tuition_db `V2__seed.sql`:**
| mssv | full_name | semester | amount | status |
|---|---|---|---|---|
| `521H0001` | Trần Thị B | 2024-2025/HK1 | 8.400.000 | UNPAID |
| `521H0002` | Lê Văn C | 2024-2025/HK1 | 9.200.000 | UNPAID |
| `521H0003` | Phạm Thị D | 2023-2024/HK2 | 7.600.000 | PAID (demo lỗi FEE_ALREADY_PAID) |

> Người dùng A trả học phí cho B (MSSV khác mình) → minh hoạ đúng yêu cầu "thanh toán cho sinh viên khác".

## 4.5. Kiểm chứng ràng buộc bằng SQL (chạy tay 1 lần, chụp kết quả cho báo cáo)

```sql
-- (1) Không thể âm số dư (BR7)
UPDATE accounts SET balance = -1 WHERE id = 1;           -- phải lỗi CHECK

-- (2) Không thể trả fee đã PAID lần 2 (BR6) — câu lệnh claim an toàn:
UPDATE tuition_fees SET status='PAID', paid_transaction_id='...'
 WHERE id = 10 AND status = 'UNPAID';                    -- lần 2 trả về 0 rows

-- (3) Không thể có 2 OTP ACTIVE cho cùng giao dịch (BR3)
INSERT INTO otp_codes (transaction_id, code_hash, expires_at)
VALUES ('txn-1','hash1', now() + interval '5 minutes');  -- lần 2 phải lỗi unique index

-- (4) Không thể trùng idempotency_key
INSERT INTO transactions (id, idempotency_key, ...) VALUES (...) -- lần 2 phải lỗi UNIQUE
```

## 4.6. Những câu hỏi tự kiểm tra khi làm Phase 4

- [ ] Mỗi service chỉ migration đúng database của mình, không migration chéo
- [ ] Entity JPA khớp schema (bật `ddl-auto=validate` để tự phát hiện)
- [ ] Không có FK vật lý xuyên service (chỉ FK logic; giải thích trong báo cáo)
- [ ] Mọi ràng buộc BR1–BR7 đều có chỗ ép buộc ở DB (bảng CHECK/UNIQUE) hoặc ở service (conditional UPDATE)
- [ ] Seed data đủ để chạy 3 luồng demo: thành công, số dư không đủ, học phí đã trả
- [ ] Redis được dùng đúng chỗ (OTP TTL/rate-limit/idempotency) và được giải thích là NoSQL

**Acceptance criteria Phase 4:** chạy `docker compose up -d` + khởi động 5 service → Flyway tạo đủ 8 bảng; chạy 4 câu SQL kiểm chứng ở 4.5 → tất cả vi phạm đều bị DB chặn đúng như thiết kế.

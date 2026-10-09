**Ngôn ngữ:** [English](database.md) · **Tiếng Việt**

# Thiết kế cơ sở dữ liệu

Ba database PostgreSQL, mỗi service một database. Mọi mốc thời gian là `timestamptz` (lưu theo UTC qua `DateTimeOffset`). Tiền lưu dạng `numeric(15,2)` (không bao giờ dùng số thực). Tên cột/bảng dùng snake_case nhờ `EFCore.NamingConventions`.

## tuition_db (tuition-service)

### students

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| id | bigint identity | PK |
| mssv | varchar(20) | NOT NULL, UNIQUE |
| full_name | varchar(100) | NOT NULL |
| class_name | varchar(50) | nullable |
| faculty | varchar(100) | nullable |
| created_at | timestamptz | NOT NULL |
| updated_at | timestamptz | NOT NULL |

### tuition_fees

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| id | bigint identity | PK |
| student_id | bigint | FK → students (cascade) |
| semester | varchar(20) | NOT NULL |
| amount | numeric(15,2) | NOT NULL, CHECK `amount > 0` |
| status | varchar(10) | NOT NULL, CHECK `IN ('UNPAID','PAID')` |
| paid_transaction_id | uuid | nullable (tham chiếu logic tới payment-service) |
| created_at | timestamptz | NOT NULL |
| updated_at | timestamptz | NOT NULL |

Chỉ mục: `mssv` duy nhất; `(student_id, status)`.

## otp_db (otp-service)

### otp_codes

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| id | bigint identity | PK |
| transaction_id | uuid | NOT NULL (tham chiếu logic tới payment-service) |
| code_hash | varchar(200) | NOT NULL (hash PBKDF2; không bao giờ lưu plaintext) |
| expires_at | timestamptz | NOT NULL |
| status | varchar(10) | NOT NULL, CHECK `IN ('ACTIVE','USED','EXPIRED','LOCKED')` |
| attempt_count | int | NOT NULL |
| created_at | timestamptz | NOT NULL |
| used_at | timestamptz | nullable |
| idempotency_key | varchar(64) | nullable, UNIQUE |

Chỉ mục:
- **Partial unique**: `UNIQUE (transaction_id) WHERE status = 'ACTIVE'` — tối đa một OTP đang hoạt động cho mỗi giao dịch.
- `idempotency_key` duy nhất.
- `expires_at` (cho quét dọn/hết hạn).

### outbox_messages

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| id | bigint identity | PK |
| message_id | uuid | NOT NULL, UNIQUE |
| event_type | varchar(50) | NOT NULL |
| routing_key | varchar(50) | NOT NULL |
| recipient | varchar(254) | NOT NULL |
| transaction_id | uuid | nullable |
| correlation_id | varchar(64) | nullable |
| payload | text | NOT NULL (JSON; OTP mã hoá AES-256-GCM) |
| status | varchar(10) | NOT NULL, CHECK `IN ('PENDING','SENT')` |
| attempt_count | int | NOT NULL |
| created_at | timestamptz | NOT NULL |

Chỉ mục: `message_id` duy nhất; partial `(status) WHERE status='PENDING'`.

## notification_db (notification-service)

### email_logs

| Cột | Kiểu | Ràng buộc |
|---|---|---|
| id | bigint identity | PK |
| message_id | varchar(100) | NOT NULL, UNIQUE (khoá khử trùng) |
| event_type | varchar(50) | NOT NULL |
| recipient | varchar(254) | NOT NULL |
| subject | varchar(200) | NOT NULL |
| status | varchar(10) | NOT NULL, CHECK `IN ('PENDING','SENDING','SENT','FAILED')` |
| attempt_count | int | NOT NULL |
| provider_message_id | varchar(200) | nullable |
| error_code | varchar(50) | nullable |
| error_message | text | nullable |
| created_at | timestamptz | NOT NULL |
| sent_at | timestamptz | nullable |

Nội dung email **không** được lưu — chỉ lưu metadata để kiểm toán.

## Bất biến ở tầng database

Các quy tắc nghiệp vụ được ép buộc càng gần dữ liệu càng tốt:

| Bất biến | Cách ép buộc |
|---|---|
| `amount > 0` | ràng buộc CHECK |
| trạng thái fee/otp/email hợp lệ | ràng buộc CHECK |
| `mssv` duy nhất | chỉ mục UNIQUE |
| một OTP hoạt động cho mỗi giao dịch | partial UNIQUE index |
| mỗi khoản phí chỉ claim một lần | UPDATE có điều kiện nguyên tử (`WHERE status='UNPAID'`) |
| mỗi OTP chỉ dùng một lần | UPDATE có điều kiện nguyên tử (`WHERE status='ACTIVE'`) |
| không gửi email trùng | UNIQUE `email_logs.message_id` |
| không phát hành trùng | UNIQUE `otp_codes.idempotency_key` |

## Migration

Migration là EF Core migration (tường minh), áp dụng bằng `Database.Migrate()` (khi khởi động ở dev) hoặc `dotnet ef database update` (production). Không bao giờ dùng `Database.EnsureCreated/EnsureDeleted`.

- `tuition-service`: `InitialCreate` + `SeedDemoData` (seed demo idempotent).
- `otp-service`: `InitialCreate`.
- `notification-service`: `InitialCreate`.

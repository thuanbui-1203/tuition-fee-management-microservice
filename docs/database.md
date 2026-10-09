**Language:** **English** · [Tiếng Việt](database.vi.md)

# Database design

Three PostgreSQL databases, one per service. All timestamps are `timestamptz` (stored in UTC via `DateTimeOffset`). Money is stored as `numeric(15,2)` (never floating point). Column/table names are snake_case via `EFCore.NamingConventions`.

## tuition_db (tuition-service)

### students

| Column | Type | Constraints |
|---|---|---|
| id | bigint identity | PK |
| mssv | varchar(20) | NOT NULL, UNIQUE |
| full_name | varchar(100) | NOT NULL |
| class_name | varchar(50) | nullable |
| faculty | varchar(100) | nullable |
| created_at | timestamptz | NOT NULL |
| updated_at | timestamptz | NOT NULL |

### tuition_fees

| Column | Type | Constraints |
|---|---|---|
| id | bigint identity | PK |
| student_id | bigint | FK → students (cascade) |
| semester | varchar(20) | NOT NULL |
| amount | numeric(15,2) | NOT NULL, CHECK `amount > 0` |
| status | varchar(10) | NOT NULL, CHECK `IN ('UNPAID','PAID')` |
| paid_transaction_id | uuid | nullable (logical ref to payment-service) |
| created_at | timestamptz | NOT NULL |
| updated_at | timestamptz | NOT NULL |

Indexes: unique `mssv`; `(student_id, status)`.

## otp_db (otp-service)

### otp_codes

| Column | Type | Constraints |
|---|---|---|
| id | bigint identity | PK |
| transaction_id | uuid | NOT NULL (logical ref to payment-service) |
| code_hash | varchar(200) | NOT NULL (PBKDF2 hash; plaintext never stored) |
| expires_at | timestamptz | NOT NULL |
| status | varchar(10) | NOT NULL, CHECK `IN ('ACTIVE','USED','EXPIRED','LOCKED')` |
| attempt_count | int | NOT NULL |
| created_at | timestamptz | NOT NULL |
| used_at | timestamptz | nullable |
| idempotency_key | varchar(64) | nullable, UNIQUE |

Indexes:
- **Partial unique**: `UNIQUE (transaction_id) WHERE status = 'ACTIVE'` — at most one active OTP per transaction.
- Unique `idempotency_key`.
- `expires_at` (for cleanup/expiry scans).

### outbox_messages

| Column | Type | Constraints |
|---|---|---|
| id | bigint identity | PK |
| message_id | uuid | NOT NULL, UNIQUE |
| event_type | varchar(50) | NOT NULL |
| routing_key | varchar(50) | NOT NULL |
| recipient | varchar(254) | NOT NULL |
| transaction_id | uuid | nullable |
| correlation_id | varchar(64) | nullable |
| payload | text | NOT NULL (JSON; OTP is AES-256-GCM encrypted) |
| status | varchar(10) | NOT NULL, CHECK `IN ('PENDING','SENT')` |
| attempt_count | int | NOT NULL |
| created_at | timestamptz | NOT NULL |

Indexes: unique `message_id`; partial `(status) WHERE status='PENDING'`.

## notification_db (notification-service)

### email_logs

| Column | Type | Constraints |
|---|---|---|
| id | bigint identity | PK |
| message_id | varchar(100) | NOT NULL, UNIQUE (dedup key) |
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

Email bodies are **not** stored — only metadata for auditing.

## Database-level invariants

Business rules are enforced as close to the data as practical:

| Invariant | Enforcement |
|---|---|
| `amount > 0` | CHECK constraint |
| valid fee/otp/email statuses | CHECK constraints |
| unique `mssv` | UNIQUE index |
| one active OTP per transaction | partial UNIQUE index |
| one claim per fee | atomic conditional UPDATE (`WHERE status='UNPAID'`) |
| one use per OTP | atomic conditional UPDATE (`WHERE status='ACTIVE'`) |
| no duplicate email | UNIQUE `email_logs.message_id` |
| no duplicate issuance | UNIQUE `otp_codes.idempotency_key` |

## Migrations

Migrations are EF Core migrations (explicit), applied with `Database.Migrate()` (dev startup) or `dotnet ef database update` (production). `Database.EnsureCreated/EnsureDeleted` are never used.

- `tuition-service`: `InitialCreate` + `SeedDemoData` (idempotent demo seed).
- `otp-service`: `InitialCreate`.
- `notification-service`: `InitialCreate`.

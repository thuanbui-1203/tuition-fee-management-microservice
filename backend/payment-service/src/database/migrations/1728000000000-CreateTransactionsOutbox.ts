import { MigrationInterface, QueryRunner } from 'typeorm';

/**
 * payment_db — V1: tạo bảng transactions + outbox.
 * Ép ràng buộc nghiệp vụ ngay tại DB:
 *  - idempotency_key UNIQUE  → retry không tạo giao dịch thứ 2
 *  - CHECK status IN (...)   → state machine hợp lệ
 *  - amount CHECK > 0
 *  - last_otp_sent_at        → rate-limit resend OTP bằng conditional UPDATE
 */
export class CreateTransactionsOutbox1728000000000
  implements MigrationInterface
{
  name = 'CreateTransactionsOutbox1728000000000';

  public async up(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(`
      CREATE EXTENSION IF NOT EXISTS "pgcrypto";
    `);

    await queryRunner.query(`
      CREATE TABLE "transactions" (
        "id"               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        "idempotency_key"  VARCHAR(100)  NOT NULL UNIQUE,
        "payer_user_id"    BIGINT        NOT NULL,
        "payer_email"      VARCHAR(100),
        "tuition_fee_id"   BIGINT        NOT NULL,
        "mssv"             VARCHAR(20)   NOT NULL,
        "student_name"     VARCHAR(100)  NOT NULL,
        "semester"         VARCHAR(20),
        "amount"           BIGINT        NOT NULL CHECK ("amount" > 0),
        "status"           VARCHAR(20)   NOT NULL DEFAULT 'INITIATED'
          CHECK ("status" IN ('INITIATED','OTP_SENT','OTP_VERIFIED','SUCCESS','FAILED')),
        "last_otp_sent_at" TIMESTAMPTZ   NOT NULL DEFAULT now(),
        "failure_reason"   TEXT,
        "created_at"       TIMESTAMPTZ   NOT NULL DEFAULT now(),
        "completed_at"     TIMESTAMPTZ
      );
    `);
    await queryRunner.query(
      `CREATE INDEX "idx_txn_payer" ON "transactions" ("payer_user_id");`,
    );
    await queryRunner.query(
      `CREATE INDEX "idx_txn_status" ON "transactions" ("status");`,
    );

    await queryRunner.query(`
      CREATE TABLE "outbox" (
        "id"         BIGSERIAL PRIMARY KEY,
        "message_id" UUID        NOT NULL UNIQUE,
        "event_type" VARCHAR(50) NOT NULL,
        "payload"    JSONB       NOT NULL,
        "status"     VARCHAR(10) NOT NULL DEFAULT 'PENDING'
          CHECK ("status" IN ('PENDING','SENT')),
        "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
        "sent_at"    TIMESTAMPTZ
      );
    `);
    await queryRunner.query(
      `CREATE INDEX "idx_outbox_status" ON "outbox" ("status") WHERE "status" = 'PENDING';`,
    );
  }

  public async down(queryRunner: QueryRunner): Promise<void> {
    await queryRunner.query(`DROP TABLE IF EXISTS "outbox";`);
    await queryRunner.query(`DROP TABLE IF EXISTS "transactions";`);
  }
}

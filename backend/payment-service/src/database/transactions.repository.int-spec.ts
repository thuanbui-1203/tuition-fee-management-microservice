import { DataSource } from 'typeorm';
import {
  PostgreSqlContainer,
  StartedPostgreSqlContainer,
} from '@testcontainers/postgresql';
import { Transaction } from './entities/transaction.entity';
import { OutboxEvent, OutboxStatus } from './entities/outbox-event.entity';
import { TransactionStatus } from './transaction-status.enum';
import { TransactionsRepository } from './transactions.repository';
import { CreateTransactionsOutbox1728000000000 } from './migrations/1728000000000-CreateTransactionsOutbox';

// Integration test chạy trên Postgres thật (Testcontainers), KHÔNG dùng H2/sqlite
// vì cần kiểm chứng CHECK/UNIQUE và conditional UPDATE đúng hành vi Postgres.
jest.setTimeout(120_000);

describe('TransactionsRepository (integration, Testcontainers)', () => {
  let container: StartedPostgreSqlContainer;
  let dataSource: DataSource;
  let repo: TransactionsRepository;

  const baseTxn = () => ({
    idempotencyKey: `key-${Math.random().toString(36).slice(2)}`,
    payerUserId: 1,
    payerEmail: 'a@tdtu.edu.vn',
    tuitionFeeId: 10,
    mssv: '521H0001',
    studentName: 'Trần Thị B',
    semester: '2024-2025/HK1',
    amount: 8_400_000,
    status: TransactionStatus.INITIATED,
  });

  beforeAll(async () => {
    container = await new PostgreSqlContainer('postgres:16-alpine').start();
    dataSource = new DataSource({
      type: 'postgres',
      host: container.getHost(),
      port: container.getPort(),
      username: container.getUsername(),
      password: container.getPassword(),
      database: container.getDatabase(),
      entities: [Transaction, OutboxEvent],
      migrations: [CreateTransactionsOutbox1728000000000],
      synchronize: false,
    });
    await dataSource.initialize();
    await dataSource.runMigrations();
    repo = new TransactionsRepository(
      dataSource.getRepository(Transaction),
      dataSource,
    );
  });

  afterAll(async () => {
    if (dataSource?.isInitialized) await dataSource.destroy();
    if (container) await container.stop();
  });

  it('migration tạo đủ 2 bảng transactions + outbox', async () => {
    const rows = await dataSource.query(
      `SELECT table_name FROM information_schema.tables WHERE table_schema='public' ORDER BY table_name`,
    );
    const names = rows.map((r: { table_name: string }) => r.table_name);
    expect(names).toEqual(expect.arrayContaining(['transactions', 'outbox']));
  });

  it('idempotency_key UNIQUE bị DB chặn khi trùng', async () => {
    const t = baseTxn();
    await repo.save(t);
    await expect(repo.save({ ...baseTxn(), idempotencyKey: t.idempotencyKey }))
      .rejects.toThrow();
  });

  it('CHECK status chặn giá trị không hợp lệ', async () => {
    await expect(
      dataSource.query(
        `INSERT INTO transactions (idempotency_key, payer_user_id, tuition_fee_id, mssv, student_name, amount, status)
         VALUES ('k-bad', 1, 10, '521H0001', 'B', 8400000, 'WRONG_STATE')`,
      ),
    ).rejects.toThrow();
  });

  it('CHECK amount > 0 bị chặn', async () => {
    await expect(
      dataSource.query(
        `INSERT INTO transactions (idempotency_key, payer_user_id, tuition_fee_id, mssv, student_name, amount, status)
         VALUES ('k-amt', 1, 10, '521H0001', 'B', 0, 'INITIATED')`,
      ),
    ).rejects.toThrow();
  });

  it('transition() đổi trạng thái có điều kiện (từ đúng from)', async () => {
    const saved = await repo.save(baseTxn());
    const ok = await repo.transition(
      saved.id,
      TransactionStatus.INITIATED,
      TransactionStatus.OTP_SENT,
    );
    expect(ok).toBe(true);
    // from sai → không đổi
    const again = await repo.transition(
      saved.id,
      TransactionStatus.INITIATED,
      TransactionStatus.OTP_SENT,
    );
    expect(again).toBe(false);
  });

  it('tryStampOtpResend: wrong_state khi không ở OTP_SENT', async () => {
    const saved = await repo.save(baseTxn()); // INITIATED
    expect(await repo.tryStampOtpResend(saved.id)).toBe('wrong_state');
  });

  it('tryStampOtpResend: rate_limited khi vừa gửi (< 60s)', async () => {
    const saved = await repo.save({
      ...baseTxn(),
      status: TransactionStatus.OTP_SENT,
    });
    // last_otp_sent_at = now() mặc định → trong 60s
    expect(await repo.tryStampOtpResend(saved.id)).toBe('rate_limited');
  });

  it('tryStampOtpResend: ok khi đã quá 60s', async () => {
    const saved = await repo.save({
      ...baseTxn(),
      status: TransactionStatus.OTP_SENT,
    });
    await dataSource.query(
      `UPDATE transactions SET last_otp_sent_at = now() - interval '61 seconds' WHERE id = $1`,
      [saved.id],
    );
    expect(await repo.tryStampOtpResend(saved.id)).toBe('ok');
  });

  it('tryStampOtpResend đồng thời: chỉ 1 request trong 5 trả "ok" (chống race)', async () => {
    const saved = await repo.save({
      ...baseTxn(),
      status: TransactionStatus.OTP_SENT,
    });
    // đặt last_otp_sent_at quá 60s để điều kiện cho phép resend
    await dataSource.query(
      `UPDATE transactions SET last_otp_sent_at = now() - interval '61 seconds' WHERE id = $1`,
      [saved.id],
    );
    // Bắn 5 request đồng thời
    const results = await Promise.all(
      Array.from({ length: 5 }, () => repo.tryStampOtpResend(saved.id)),
    );
    const okCount = results.filter((r) => r === 'ok').length;
    expect(okCount).toBe(1); // conditional UPDATE nguyên tử → đúng 1 lần gọi otp.issue
  });

  it('markSuccessWithOutbox: ghi SUCCESS + outbox trong cùng transaction', async () => {
    const saved = await repo.save({
      ...baseTxn(),
      status: TransactionStatus.OTP_VERIFIED,
    });
    const ok = await repo.markSuccessWithOutbox(saved.id, {
      messageId: '550e8400-e29b-41d4-a716-446655440000',
      eventType: 'PaymentSucceeded',
      payload: { paymentId: saved.id, amount: 8_400_000 },
    });
    expect(ok).toBe(true);

    const after = await repo.findById(saved.id);
    expect(after?.status).toBe(TransactionStatus.SUCCESS);
    expect(after?.completedAt).toBeTruthy();

    const outboxCount = await dataSource
      .getRepository(OutboxEvent)
      .count({ where: { status: OutboxStatus.PENDING } });
    expect(outboxCount).toBeGreaterThanOrEqual(1);
  });
});

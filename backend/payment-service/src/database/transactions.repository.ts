import { Injectable } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, DataSource, EntityManager } from 'typeorm';
import { Transaction } from './entities/transaction.entity';
import { OutboxEvent, OutboxStatus } from './entities/outbox-event.entity';
import { TransactionStatus } from './transaction-status.enum';

/**
 * Repository bọc các thao tác conditional-UPDATE nguyên tử cho saga.
 * Mọi chuyển trạng thái quan trọng = 1 câu UPDATE có điều kiện (WHERE status=...),
 * trả về số dòng ảnh hưởng. 0 dòng = bị chặn (race/sai state) — KHÔNG read-then-write.
 */
@Injectable()
export class TransactionsRepository {
  constructor(
    @InjectRepository(Transaction)
    private readonly repo: Repository<Transaction>,
    private readonly dataSource: DataSource,
  ) {}

  findById(id: string): Promise<Transaction | null> {
    return this.repo.findOne({ where: { id } });
  }

  /** Lịch sử giao dịch của 1 user, phân trang theo createdAt mới nhất. */
  findByPayer(
    payerUserId: number,
    page: number,
    size: number,
  ): Promise<[Transaction[], number]> {
    return this.repo.findAndCount({
      where: { payerUserId },
      order: { createdAt: 'DESC' },
      skip: page * size,
      take: size,
    });
  }

  findByIdempotencyKey(key: string): Promise<Transaction | null> {
    return this.repo.findOne({ where: { idempotencyKey: key } });
  }

  save(txn: Partial<Transaction>): Promise<Transaction> {
    return this.repo.save(this.repo.create(txn));
  }

  /**
   * Chuyển trạng thái có điều kiện. Trả true nếu đổi được (1 dòng), false nếu bị chặn.
   */
  async transition(
    id: string,
    from: TransactionStatus,
    to: TransactionStatus,
    extra?: Partial<Pick<Transaction, 'completedAt' | 'failureReason'>>,
  ): Promise<boolean> {
    const result = await this.repo
      .createQueryBuilder()
      .update(Transaction)
      .set({ status: to, ...extra })
      .where('id = :id AND status = :from', { id, from })
      .execute();
    return (result.affected ?? 0) === 1;
  }

  /**
   * Rate-limit resend OTP (60s) bằng conditional UPDATE — không cần Redis.
   * Trả 'ok' nếu cập nhật được (1 dòng); 'wrong_state' nếu status != OTP_SENT;
   * 'rate_limited' nếu status đúng nhưng chưa đủ 60s.
   */
  async tryStampOtpResend(
    id: string,
  ): Promise<'ok' | 'rate_limited' | 'wrong_state'> {
    const result = await this.repo
      .createQueryBuilder()
      .update(Transaction)
      .set({ lastOtpSentAt: () => 'now()' })
      .where(
        `id = :id AND status = :st AND last_otp_sent_at < now() - interval '60 seconds'`,
        { id, st: TransactionStatus.OTP_SENT },
      )
      .execute();

    if ((result.affected ?? 0) === 1) return 'ok';

    // 0 dòng: phân biệt sai state vs còn trong 60s
    const current = await this.findById(id);
    if (!current || current.status !== TransactionStatus.OTP_SENT) {
      return 'wrong_state';
    }
    return 'rate_limited';
  }

  /** Ghi SUCCESS + insert outbox trong CÙNG 1 transaction DB (outbox pattern). */
  async markSuccessWithOutbox(
    id: string,
    event: Pick<OutboxEvent, 'messageId' | 'eventType' | 'payload'>,
  ): Promise<boolean> {
    return this.dataSource.transaction(async (manager: EntityManager) => {
      const result = await manager
        .createQueryBuilder()
        .update(Transaction)
        .set({ status: TransactionStatus.SUCCESS, completedAt: () => 'now()' })
        .where('id = :id AND status = :from', {
          id,
          from: TransactionStatus.OTP_VERIFIED,
        })
        .execute();

      if ((result.affected ?? 0) !== 1) return false;

      const outbox = manager.create(OutboxEvent, {
        messageId: event.messageId,
        eventType: event.eventType,
        payload: event.payload,
        status: OutboxStatus.PENDING,
      });
      await manager.save(OutboxEvent, outbox);
      return true;
    });
  }
}

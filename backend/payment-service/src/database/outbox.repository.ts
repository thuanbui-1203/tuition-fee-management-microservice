import { Injectable } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, DataSource } from 'typeorm';
import { OutboxEvent, OutboxStatus } from './entities/outbox-event.entity';

/**
 * Repository cho outbox relay. Lấy batch PENDING bằng FOR UPDATE SKIP LOCKED
 * để nhiều instance relay chạy song song không publish trùng.
 */
@Injectable()
export class OutboxRepository {
  constructor(
    @InjectRepository(OutboxEvent)
    private readonly repo: Repository<OutboxEvent>,
    private readonly dataSource: DataSource,
  ) {}

  /** Khoá & trả về các event PENDING (bỏ qua dòng đang bị instance khác khoá). */
  async lockPendingBatch(limit = 50): Promise<OutboxEvent[]> {
    return this.dataSource.transaction(async (manager) => {
      const rows: OutboxEvent[] = await manager
        .createQueryBuilder(OutboxEvent, 'o')
        .setLock('pessimistic_write')
        .setOnLocked('skip_locked')
        .where('o.status = :status', { status: OutboxStatus.PENDING })
        .orderBy('o.id', 'ASC')
        .limit(limit)
        .getMany();
      return rows;
    });
  }

  async markSent(id: string): Promise<void> {
    await this.repo
      .createQueryBuilder()
      .update(OutboxEvent)
      .set({ status: OutboxStatus.SENT, sentAt: () => 'now()' })
      .where('id = :id', { id })
      .execute();
  }

  countByStatus(status: OutboxStatus): Promise<number> {
    return this.repo.count({ where: { status } });
  }
}

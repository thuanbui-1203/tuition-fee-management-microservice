import { Injectable, Logger } from '@nestjs/common';
import { Interval } from '@nestjs/schedule';
import { OutboxRepository } from '../database/outbox.repository';
import { RabbitPublisher } from './rabbit.publisher';

/**
 * Outbox relay: mỗi 1s đọc event PENDING (FOR UPDATE SKIP LOCKED) → publish RabbitMQ → SENT.
 * Crash giữa chừng → event vẫn PENDING trong DB → publish lại ở lần chạy sau
 * (đảm bảo "không mất email"; at-least-once, consumer dedupe bằng messageId).
 */
@Injectable()
export class OutboxRelayService {
  private readonly logger = new Logger('OutboxRelay');
  private running = false;

  constructor(
    private readonly outbox: OutboxRepository,
    private readonly publisher: RabbitPublisher,
  ) {}

  @Interval('outbox-relay', 1000)
  async relayTick(): Promise<void> {
    if (this.running) return; // tránh chồng tick nếu lần trước còn chạy
    this.running = true;
    try {
      await this.drainOnce();
    } catch (err) {
      this.logger.error(
        `relay tick lỗi: ${err instanceof Error ? err.message : String(err)}`,
      );
    } finally {
      this.running = false;
    }
  }

  /** Publish 1 batch PENDING. Trả số event đã gửi (dùng cho test). */
  async drainOnce(): Promise<number> {
    const batch = await this.outbox.lockPendingBatch(50);
    let sent = 0;
    for (const event of batch) {
      try {
        await this.publisher.publish(
          event.eventType,
          event.messageId,
          event.payload,
        );
        await this.outbox.markSent(event.id);
        sent += 1;
      } catch (err) {
        // Publish lỗi → giữ PENDING, thử lại tick sau. Không đánh SENT.
        this.logger.warn(
          `publish outbox ${event.id} thất bại, sẽ thử lại: ${
            err instanceof Error ? err.message : String(err)
          }`,
        );
        break; // broker có thể đang down → dừng batch, chờ tick sau
      }
    }
    if (sent > 0) this.logger.log(`relay published ${sent} event(s)`);
    return sent;
  }
}

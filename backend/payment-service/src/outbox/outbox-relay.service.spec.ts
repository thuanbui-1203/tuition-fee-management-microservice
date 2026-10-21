import { OutboxRelayService } from './outbox-relay.service';
import { OutboxRepository } from '../database/outbox.repository';
import { RabbitPublisher } from './rabbit.publisher';
import { OutboxEvent, OutboxStatus } from '../database/entities/outbox-event.entity';

function makeEvent(id: string): OutboxEvent {
  return {
    id,
    messageId: `msg-${id}`,
    eventType: 'PaymentSucceeded',
    payload: { paymentId: `p-${id}` },
    status: OutboxStatus.PENDING,
    createdAt: new Date(),
    sentAt: null,
  };
}

describe('OutboxRelayService.drainOnce', () => {
  let outbox: jest.Mocked<OutboxRepository>;
  let publisher: jest.Mocked<RabbitPublisher>;
  let relay: OutboxRelayService;

  beforeEach(() => {
    outbox = {
      lockPendingBatch: jest.fn(),
      markSent: jest.fn().mockResolvedValue(undefined),
    } as unknown as jest.Mocked<OutboxRepository>;
    publisher = {
      publish: jest.fn().mockResolvedValue(undefined),
    } as unknown as jest.Mocked<RabbitPublisher>;
    relay = new OutboxRelayService(outbox, publisher);
  });

  it('publish toàn bộ batch PENDING rồi markSent', async () => {
    outbox.lockPendingBatch.mockResolvedValue([makeEvent('1'), makeEvent('2')]);
    const sent = await relay.drainOnce();
    expect(sent).toBe(2);
    expect(publisher.publish).toHaveBeenCalledTimes(2);
    expect(publisher.publish).toHaveBeenCalledWith(
      'PaymentSucceeded',
      'msg-1',
      { paymentId: 'p-1' },
    );
    expect(outbox.markSent).toHaveBeenCalledWith('1');
    expect(outbox.markSent).toHaveBeenCalledWith('2');
  });

  it('publish lỗi → KHÔNG markSent, dừng batch (giữ PENDING, thử lại tick sau)', async () => {
    outbox.lockPendingBatch.mockResolvedValue([makeEvent('1'), makeEvent('2')]);
    publisher.publish.mockRejectedValueOnce(new Error('broker down'));
    const sent = await relay.drainOnce();
    expect(sent).toBe(0);
    expect(outbox.markSent).not.toHaveBeenCalled();
  });

  it('batch rỗng → 0 event', async () => {
    outbox.lockPendingBatch.mockResolvedValue([]);
    const sent = await relay.drainOnce();
    expect(sent).toBe(0);
    expect(publisher.publish).not.toHaveBeenCalled();
  });
});

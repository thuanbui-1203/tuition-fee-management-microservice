import { DataSource } from 'typeorm';
import {
  PostgreSqlContainer,
  StartedPostgreSqlContainer,
} from '@testcontainers/postgresql';
import {
  RabbitMQContainer,
  StartedRabbitMQContainer,
} from '@testcontainers/rabbitmq';
import { ConfigService } from '@nestjs/config';
import * as amqp from 'amqplib';
import { Transaction } from '../database/entities/transaction.entity';
import { OutboxEvent, OutboxStatus } from '../database/entities/outbox-event.entity';
import { OutboxRepository } from '../database/outbox.repository';
import { OutboxRelayService } from './outbox-relay.service';
import {
  RabbitPublisher,
  EMAIL_EXCHANGE,
  EMAIL_ROUTING_KEY,
} from './rabbit.publisher';
import { CreateTransactionsOutbox1728000000000 } from '../database/migrations/1728000000000-CreateTransactionsOutbox';

jest.setTimeout(180_000);

describe('Outbox relay (integration: Postgres + RabbitMQ)', () => {
  let pg: StartedPostgreSqlContainer;
  let mq: StartedRabbitMQContainer;
  let dataSource: DataSource;
  let relay: OutboxRelayService;
  let publisher: RabbitPublisher;

  beforeAll(async () => {
    pg = await new PostgreSqlContainer('postgres:16-alpine').start();
    mq = await new RabbitMQContainer('rabbitmq:3-management').start();

    dataSource = new DataSource({
      type: 'postgres',
      host: pg.getHost(),
      port: pg.getPort(),
      username: pg.getUsername(),
      password: pg.getPassword(),
      database: pg.getDatabase(),
      entities: [Transaction, OutboxEvent],
      migrations: [CreateTransactionsOutbox1728000000000],
      synchronize: false,
    });
    await dataSource.initialize();
    await dataSource.runMigrations();

    const amqpUrl = mq.getAmqpUrl();
    const u = new URL(amqpUrl);
    const config = {
      get: (k: string) =>
        ({
          RABBITMQ_HOST: u.hostname,
          RABBITMQ_PORT: Number(u.port),
          RABBITMQ_USER: u.username || 'guest',
          RABBITMQ_PASS: u.password || 'guest',
        })[k],
    } as unknown as ConfigService;

    publisher = new RabbitPublisher(config);
    await publisher.onModuleInit();
    relay = new OutboxRelayService(
      new OutboxRepository(dataSource.getRepository(OutboxEvent), dataSource),
      publisher,
    );
  });

  afterAll(async () => {
    await publisher?.onModuleDestroy();
    if (dataSource?.isInitialized) await dataSource.destroy();
    await mq?.stop();
    await pg?.stop();
  });

  it('PENDING → publish RabbitMQ → SENT, message có trên queue', async () => {
    // Chuẩn bị consumer: bind queue vào exchange để bắt message.
    const amqpUrl = mq.getAmqpUrl();
    const conn = await amqp.connect(amqpUrl);
    const ch = await conn.createChannel();
    await ch.assertExchange(EMAIL_EXCHANGE, 'topic', { durable: true });
    const q = await ch.assertQueue('', { exclusive: true });
    await ch.bindQueue(q.queue, EMAIL_EXCHANGE, EMAIL_ROUTING_KEY);

    const received: amqp.ConsumeMessage[] = [];
    await ch.consume(q.queue, (msg) => msg && received.push(msg), {
      noAck: true,
    });

    // Ghi 1 event PENDING vào outbox.
    const repo = dataSource.getRepository(OutboxEvent);
    const saved = await repo.save(
      repo.create({
        messageId: '550e8400-e29b-41d4-a716-446655440000',
        eventType: 'PaymentSucceeded',
        payload: { paymentId: 'p-1', amount: 8_400_000 },
        status: OutboxStatus.PENDING,
      }),
    );

    const sent = await relay.drainOnce();
    expect(sent).toBe(1);

    // outbox đã chuyển SENT
    const after = await repo.findOneByOrFail({ id: saved.id });
    expect(after.status).toBe(OutboxStatus.SENT);
    expect(after.sentAt).toBeTruthy();

    // message tới queue (chờ ngắn cho consumer)
    await new Promise((r) => setTimeout(r, 300));
    expect(received.length).toBe(1);
    const body = JSON.parse(received[0].content.toString());
    expect(body.paymentId).toBe('p-1');
    expect(received[0].properties.contentType).toBe('application/json');
    expect(received[0].properties.headers?.__TypeId__).toBe('PaymentSucceeded');

    await ch.close();
    await conn.close();
  });
});

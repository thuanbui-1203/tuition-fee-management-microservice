import {
  Injectable,
  Logger,
  OnModuleDestroy,
  OnModuleInit,
} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as amqp from 'amqplib';

export const EMAIL_EXCHANGE = 'email.exchange';
export const EMAIL_ROUTING_KEY = 'email.send';

/**
 * Publisher RabbitMQ qua amqplib. Lazy-connect, tự reconnect khi cần.
 *
 * QUAN TRỌNG (polyglot Node→Spring): message publish kèm
 *  - contentType: 'application/json'
 *  - header '__TypeId__' = tên class để Jackson2JsonMessageConverter (Spring) map đúng.
 * notification-service (B) phải map '__TypeId__' này sang DTO của nó
 * HOẶC cấu hình DefaultJackson2JavaTypeMapper set default type.
 */
@Injectable()
export class RabbitPublisher implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger('RabbitPublisher');
  private connection: amqp.ChannelModel | null = null;
  private channel: amqp.ConfirmChannel | null = null;
  private readonly url: string;

  constructor(config: ConfigService) {
    const host = config.get<string>('RABBITMQ_HOST');
    const port = config.get<number>('RABBITMQ_PORT') ?? 5672;
    const user = config.get<string>('RABBITMQ_USER');
    const pass = config.get<string>('RABBITMQ_PASS');
    this.url = `amqp://${user}:${pass}@${host}:${port}`;
  }

  async onModuleInit(): Promise<void> {
    // Không chặn khởi động nếu RabbitMQ chưa sẵn sàng — relay sẽ thử lại.
    await this.ensureChannel().catch((e) =>
      this.logger.warn(`RabbitMQ chưa sẵn sàng khi khởi động: ${e.message}`),
    );
  }

  async onModuleDestroy(): Promise<void> {
    try {
      await this.channel?.close();
      await this.connection?.close();
    } catch {
      // ignore khi tắt
    }
  }

  private async ensureChannel(): Promise<amqp.ConfirmChannel> {
    if (this.channel) return this.channel;

    const connection = await amqp.connect(this.url);
    connection.on('error', (err) =>
      this.logger.error(`RabbitMQ connection error: ${err.message}`),
    );
    connection.on('close', () => {
      this.logger.warn('RabbitMQ connection closed');
      this.connection = null;
      this.channel = null;
    });

    const channel = await connection.createConfirmChannel();
    await channel.assertExchange(EMAIL_EXCHANGE, 'topic', { durable: true });

    this.connection = connection;
    this.channel = channel;
    this.logger.log('RabbitMQ connected, exchange asserted');
    return channel;
  }

  /**
   * Publish 1 event. Trả true nếu broker ack (persistent).
   * messageId dùng để notification-service dedupe (at-least-once).
   */
  async publish(
    eventType: string,
    messageId: string,
    payload: Record<string, unknown>,
  ): Promise<void> {
    const channel = await this.ensureChannel();
    const body = Buffer.from(JSON.stringify(payload));

    await new Promise<void>((resolve, reject) => {
      channel.publish(
        EMAIL_EXCHANGE,
        EMAIL_ROUTING_KEY,
        body,
        {
          contentType: 'application/json',
          messageId,
          persistent: true,
          headers: {
            // Giúp Spring Jackson2JsonMessageConverter map đúng kiểu.
            __TypeId__: eventType,
            eventType,
          },
        },
        (err) => (err ? reject(err) : resolve()),
      );
    });
  }
}

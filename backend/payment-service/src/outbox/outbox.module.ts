import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { DatabaseModule } from '../database/database.module';
import { RabbitPublisher } from './rabbit.publisher';
import { OutboxRelayService } from './outbox-relay.service';

@Module({
  imports: [ConfigModule, DatabaseModule],
  providers: [RabbitPublisher, OutboxRelayService],
  exports: [RabbitPublisher, OutboxRelayService],
})
export class OutboxModule {}

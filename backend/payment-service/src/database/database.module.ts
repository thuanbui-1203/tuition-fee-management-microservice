import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { Transaction } from './entities/transaction.entity';
import { OutboxEvent } from './entities/outbox-event.entity';
import { TransactionsRepository } from './transactions.repository';
import { OutboxRepository } from './outbox.repository';

@Module({
  imports: [TypeOrmModule.forFeature([Transaction, OutboxEvent])],
  providers: [TransactionsRepository, OutboxRepository],
  exports: [TransactionsRepository, OutboxRepository, TypeOrmModule],
})
export class DatabaseModule {}

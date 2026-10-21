import { ConfigService } from '@nestjs/config';
import { DataSourceOptions } from 'typeorm';
import { Transaction } from './entities/transaction.entity';
import { OutboxEvent } from './entities/outbox-event.entity';

/**
 * Build DataSourceOptions từ ConfigService (NestJS) HOẶC từ process.env (CLI migration).
 * TypeORM 0.3: dùng DataSource, KHÔNG dùng getConnection()/getRepository() global.
 */
export function buildDataSourceOptions(
  get: (key: string) => string | undefined,
): DataSourceOptions {
  return {
    type: 'postgres',
    host: get('POSTGRES_HOST') ?? 'localhost',
    port: Number(get('POSTGRES_PORT') ?? 5432),
    username: get('POSTGRES_USER') ?? 'postgres',
    password: get('POSTGRES_PASSWORD') ?? 'postgres',
    database: get('PAYMENT_DB_NAME') ?? 'payment_db',
    entities: [Transaction, OutboxEvent],
    migrations: [__dirname + '/migrations/*.{ts,js}'],
    // Schema do migration quản lý — KHÔNG để TypeORM tự sync.
    synchronize: false,
    migrationsRun: false,
    logging: ['error', 'warn'],
  };
}

export function dataSourceOptionsFromConfig(
  config: ConfigService,
): DataSourceOptions {
  return buildDataSourceOptions((key) => config.get<string>(key));
}

import 'reflect-metadata';
import { config as loadEnv } from 'dotenv';
import { DataSource } from 'typeorm';
import { buildDataSourceOptions } from './typeorm.config';

// Nạp .env ở gốc repo (CLI chạy ngoài Nest nên tự load).
loadEnv();
loadEnv({ path: __dirname + '/../../../../.env' });

/**
 * DataSource dùng cho TypeORM CLI (migration:run / generate / revert).
 * Nest runtime KHÔNG dùng instance này — nó build options riêng qua ConfigService.
 */
export default new DataSource(
  buildDataSourceOptions((key) => process.env[key]),
);

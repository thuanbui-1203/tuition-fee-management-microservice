import { Module, MiddlewareConsumer, NestModule } from '@nestjs/common';
import { ConfigModule, ConfigService } from '@nestjs/config';
import { TypeOrmModule } from '@nestjs/typeorm';
import { ScheduleModule } from '@nestjs/schedule';
import { validateEnv } from './config/env.validation';
import { dataSourceOptionsFromConfig } from './database/typeorm.config';
import { TraceIdMiddleware } from './common/middleware/trace-id.middleware';
import { HealthModule } from './health/health.module';
import { PaymentsModule } from './payments/payments.module';
import { OutboxModule } from './outbox/outbox.module';

@Module({
  imports: [
    ConfigModule.forRoot({
      isGlobal: true,
      // Nạp .env ở gốc repo (4 cấp lên từ src/).
      envFilePath: ['.env', '../../.env'],
      validate: validateEnv,
    }),
    TypeOrmModule.forRootAsync({
      inject: [ConfigService],
      useFactory: (config: ConfigService) =>
        dataSourceOptionsFromConfig(config),
    }),
    ScheduleModule.forRoot(),
    HealthModule,
    PaymentsModule,
    OutboxModule,
  ],
})
export class AppModule implements NestModule {
  configure(consumer: MiddlewareConsumer): void {
    consumer.apply(TraceIdMiddleware).forRoutes('*');
  }
}

import { plainToInstance } from 'class-transformer';
import {
  IsInt,
  IsNotEmpty,
  IsString,
  MinLength,
  validateSync,
} from 'class-validator';

/**
 * Env validation — FAIL FAST khi thiếu/thiếu an toàn.
 * KHÔNG đặt giá trị fallback cho secret trong code (vd JWT_SECRET ?? 'dev').
 * Nếu biến bắt buộc thiếu, app ném lỗi ngay khi khởi động.
 */
export class EnvConfig {
  @IsInt()
  PAYMENT_SERVICE_PORT = 8083;

  // --- Database ---
  @IsString() @IsNotEmpty() POSTGRES_HOST!: string;
  @IsInt() POSTGRES_PORT = 5432;
  @IsString() @IsNotEmpty() POSTGRES_USER!: string;
  @IsString() @IsNotEmpty() POSTGRES_PASSWORD!: string;
  @IsString() @IsNotEmpty() PAYMENT_DB_NAME!: string;

  // --- JWT (HS256, dùng chung với user-service + gateway) ---
  @IsString()
  @MinLength(32, { message: 'JWT_SECRET phải >= 32 ký tự' })
  JWT_SECRET!: string;

  // --- Downstream services ---
  @IsString() @IsNotEmpty() USER_SERVICE_URL!: string;
  @IsString() @IsNotEmpty() TUITION_SERVICE_URL!: string;
  @IsString() @IsNotEmpty() OTP_SERVICE_URL!: string;

  @IsInt() HTTP_TIMEOUT_CONNECT_MS = 3000;
  @IsInt() HTTP_TIMEOUT_READ_MS = 5000;

  // --- RabbitMQ ---
  @IsString() @IsNotEmpty() RABBITMQ_HOST!: string;
  @IsInt() RABBITMQ_PORT = 5672;
  @IsString() @IsNotEmpty() RABBITMQ_USER!: string;
  @IsString() @IsNotEmpty() RABBITMQ_PASS!: string;
}

const NUMERIC_KEYS = [
  'PAYMENT_SERVICE_PORT',
  'POSTGRES_PORT',
  'HTTP_TIMEOUT_CONNECT_MS',
  'HTTP_TIMEOUT_READ_MS',
  'RABBITMQ_PORT',
];

export function validateEnv(config: Record<string, unknown>): EnvConfig {
  // ép kiểu number cho các biến numeric (env luôn là string)
  const normalized: Record<string, unknown> = { ...config };
  for (const key of NUMERIC_KEYS) {
    if (normalized[key] !== undefined && normalized[key] !== '') {
      normalized[key] = Number(normalized[key]);
    }
  }

  const validated = plainToInstance(EnvConfig, normalized, {
    enableImplicitConversion: false,
  });

  const errors = validateSync(validated, {
    skipMissingProperties: false,
    whitelist: false,
  });

  if (errors.length > 0) {
    const details = errors
      .map((e) => Object.values(e.constraints ?? {}).join(', '))
      .join('\n  - ');
    throw new Error(
      `Cấu hình môi trường không hợp lệ (payment-service):\n  - ${details}`,
    );
  }

  return validated;
}

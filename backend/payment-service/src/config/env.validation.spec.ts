import { validateEnv } from './env.validation';

const base = {
  POSTGRES_HOST: 'localhost',
  POSTGRES_USER: 'postgres',
  POSTGRES_PASSWORD: 'pw',
  PAYMENT_DB_NAME: 'payment_db',
  JWT_SECRET: 'x'.repeat(32),
  USER_SERVICE_URL: 'http://localhost:8081',
  TUITION_SERVICE_URL: 'http://localhost:8082',
  OTP_SERVICE_URL: 'http://localhost:8084',
  RABBITMQ_HOST: 'localhost',
  RABBITMQ_USER: 'guest',
  RABBITMQ_PASS: 'guest',
};

describe('validateEnv', () => {
  it('pass khi đủ biến hợp lệ', () => {
    const cfg = validateEnv({ ...base });
    expect(cfg.JWT_SECRET.length).toBeGreaterThanOrEqual(32);
    expect(cfg.POSTGRES_PORT).toBe(5432); // default
  });

  it('fail-fast khi JWT_SECRET < 32 ký tự', () => {
    expect(() => validateEnv({ ...base, JWT_SECRET: 'short' })).toThrow(
      /JWT_SECRET/,
    );
  });

  it('fail-fast khi thiếu biến bắt buộc', () => {
    const { POSTGRES_HOST, ...rest } = base;
    expect(() => validateEnv({ ...rest })).toThrow(
      /môi trường không hợp lệ/,
    );
  });

  it('ép kiểu number cho biến numeric từ string env', () => {
    const cfg = validateEnv({ ...base, POSTGRES_PORT: '6543' });
    expect(cfg.POSTGRES_PORT).toBe(6543);
  });
});

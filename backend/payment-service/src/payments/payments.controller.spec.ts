import { Test } from '@nestjs/testing';
import { INestApplication, ValidationPipe } from '@nestjs/common';
import request from 'supertest';
import { PaymentsController } from './payments.controller';
import { PaymentsService } from './payments.service';
import { JwtGuard } from '../auth/jwt.guard';
import { AuthedRequest } from '../auth/current-user';
import { GlobalExceptionFilter } from '../common/errors/global-exception.filter';
import { TransactionStatus } from '../database/transaction-status.enum';
import { ErrorCode } from '../common/errors/error-code';

describe('PaymentsController POST /payments', () => {
  let app: INestApplication;
  const service = { create: jest.fn() };

  beforeAll(async () => {
    const moduleRef = await Test.createTestingModule({
      controllers: [PaymentsController],
      providers: [{ provide: PaymentsService, useValue: service }],
    })
      // guard giả: gắn user cố định (không cần token thật trong test controller)
      .overrideGuard(JwtGuard)
      .useValue({
        canActivate: (ctx: any) => {
          const req: AuthedRequest = ctx.switchToHttp().getRequest();
          req.user = { userId: 1, username: 'a', email: 'a@tdtu.edu.vn' };
          return true;
        },
      })
      .compile();

    app = moduleRef.createNestApplication();
    app.useGlobalPipes(
      new ValidationPipe({ whitelist: true, forbidNonWhitelisted: true, transform: true }),
    );
    app.useGlobalFilters(new GlobalExceptionFilter());
    await app.init();
  });

  afterEach(() => jest.clearAllMocks());
  afterAll(async () => await app.close());

  it('201 khi tạo thành công', async () => {
    service.create.mockResolvedValue({
      paymentId: 'txn-1',
      amount: 8_400_000,
      status: TransactionStatus.OTP_SENT,
      message: 'Mã OTP đã được gửi đến email của bạn',
      expiresInSeconds: 300,
    });
    const res = await request(app.getHttpServer())
      .post('/api/v1/payments')
      .set('Idempotency-Key', 'key-1')
      .send({ mssv: '521H0001' });
    expect(res.status).toBe(201);
    expect(res.body.paymentId).toBe('txn-1');
    expect(service.create).toHaveBeenCalledWith(
      expect.objectContaining({ userId: 1 }),
      '521H0001',
      'key-1',
      expect.any(String),
    );
  });

  it('400 khi mssv rỗng (validation)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/payments')
      .set('Idempotency-Key', 'key-1')
      .send({ mssv: '' });
    expect(res.status).toBe(400);
    expect(res.body.code).toBe(ErrorCode.INVALID_INPUT);
  });

  it('400 khi gửi field thừa (forbidNonWhitelisted)', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/payments')
      .set('Idempotency-Key', 'key-1')
      .send({ mssv: '521H0001', hacker: true });
    expect(res.status).toBe(400);
  });
});

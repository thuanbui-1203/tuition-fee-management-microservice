import { Test } from '@nestjs/testing';
import {
  INestApplication,
  ValidationPipe,
  Controller,
  Post,
  Body,
} from '@nestjs/common';
import { IsString, IsNotEmpty } from 'class-validator';
import request from 'supertest';
import { PingController } from './ping.controller';
import { GlobalExceptionFilter } from '../common/errors/global-exception.filter';
import { ApiException } from '../common/errors/api.exception';
import { ErrorCode } from '../common/errors/error-code';

class SampleDto {
  @IsString()
  @IsNotEmpty()
  mssv!: string;
}

@Controller('api/v1/_test')
class SampleController {
  @Post('validate')
  validate(@Body() _dto: SampleDto) {
    return { ok: true };
  }

  @Post('business')
  business() {
    throw new ApiException(ErrorCode.FEE_ALREADY_PAID, 'Học phí đã thanh toán', {
      feeId: 10,
    });
  }
}

describe('Skeleton: ping, health route, error format', () => {
  let app: INestApplication;

  beforeAll(async () => {
    const moduleRef = await Test.createTestingModule({
      controllers: [PingController, SampleController],
    }).compile();

    app = moduleRef.createNestApplication();
    app.useGlobalPipes(
      new ValidationPipe({
        whitelist: true,
        forbidNonWhitelisted: true,
        transform: true,
      }),
    );
    app.useGlobalFilters(new GlobalExceptionFilter());
    await app.init();
  });

  afterAll(async () => {
    await app.close();
  });

  it('GET /api/v1/ping trả service=payment-service, status=ok', async () => {
    const res = await request(app.getHttpServer()).get('/api/v1/ping');
    expect(res.status).toBe(200);
    expect(res.body.service).toBe('payment-service');
    expect(res.body.status).toBe('ok');
    expect(typeof res.body.time).toBe('string');
  });

  it('lỗi validation → body chuẩn { code, message, timestamp, traceId }', async () => {
    const res = await request(app.getHttpServer())
      .post('/api/v1/_test/validate')
      .send({});
    expect(res.status).toBe(400);
    expect(res.body.code).toBe(ErrorCode.INVALID_INPUT);
    expect(typeof res.body.message).toBe('string');
    expect(typeof res.body.timestamp).toBe('string');
    expect(res.body).toHaveProperty('traceId');
  });

  it('ApiException → đúng status + code + details', async () => {
    const res = await request(app.getHttpServer()).post(
      '/api/v1/_test/business',
    );
    expect(res.status).toBe(409);
    expect(res.body.code).toBe(ErrorCode.FEE_ALREADY_PAID);
    expect(res.body.details).toEqual({ feeId: 10 });
  });
});

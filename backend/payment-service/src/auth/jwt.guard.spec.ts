import { Test } from '@nestjs/testing';
import { ConfigService } from '@nestjs/config';
import {
  INestApplication,
  Controller,
  Get,
  UseGuards,
} from '@nestjs/common';
import request from 'supertest';
import * as jwt from 'jsonwebtoken';
import { JwtGuard } from './jwt.guard';
import { CurrentUser, AuthUser } from './current-user';
import { signTestToken } from './test-token.util';
import { GlobalExceptionFilter } from '../common/errors/global-exception.filter';
import { ErrorCode } from '../common/errors/error-code';

const SECRET = 's'.repeat(40);

@Controller('api/v1/payments')
class ProtectedController {
  @Get('me')
  @UseGuards(JwtGuard)
  me(@CurrentUser() user: AuthUser) {
    return { userId: user.userId, email: user.email };
  }
}

describe('JwtGuard (self-verify HS256)', () => {
  let app: INestApplication;

  beforeAll(async () => {
    const moduleRef = await Test.createTestingModule({
      controllers: [ProtectedController],
      providers: [
        JwtGuard,
        {
          provide: ConfigService,
          useValue: { get: (k: string) => (k === 'JWT_SECRET' ? SECRET : undefined) },
        },
      ],
    }).compile();

    app = moduleRef.createNestApplication();
    app.useGlobalFilters(new GlobalExceptionFilter());
    await app.init();
  });

  afterAll(async () => {
    await app.close();
  });

  it('token hợp lệ → 200, đọc userId/email TỪ TOKEN', async () => {
    const token = signTestToken(
      { userId: 7, username: 'nguyenvana', email: 'a@tdtu.edu.vn' },
      SECRET,
    );
    const res = await request(app.getHttpServer())
      .get('/api/v1/payments/me')
      .set('Authorization', `Bearer ${token}`);
    expect(res.status).toBe(200);
    expect(res.body).toEqual({ userId: 7, email: 'a@tdtu.edu.vn' });
  });

  it('thiếu token → 401 UNAUTHENTICATED (format chuẩn)', async () => {
    const res = await request(app.getHttpServer()).get('/api/v1/payments/me');
    expect(res.status).toBe(401);
    expect(res.body.code).toBe(ErrorCode.UNAUTHENTICATED);
    expect(res.body).toHaveProperty('traceId');
  });

  it('token sai chữ ký → 401', async () => {
    const bad = jwt.sign({ sub: 1, email: 'x@y.z' }, 'wrong-secret-wrong-secret-wrong!!', {
      algorithm: 'HS256',
    });
    const res = await request(app.getHttpServer())
      .get('/api/v1/payments/me')
      .set('Authorization', `Bearer ${bad}`);
    expect(res.status).toBe(401);
  });

  it('token hết hạn → 401', async () => {
    const expired = signTestToken(
      { userId: 1, username: 'a', email: 'a@b.c' },
      SECRET,
      -10,
    );
    const res = await request(app.getHttpServer())
      .get('/api/v1/payments/me')
      .set('Authorization', `Bearer ${expired}`);
    expect(res.status).toBe(401);
  });

  it('token thiếu email claim → 401', async () => {
    const noEmail = jwt.sign({ sub: 1, username: 'a' }, SECRET, {
      algorithm: 'HS256',
    });
    const res = await request(app.getHttpServer())
      .get('/api/v1/payments/me')
      .set('Authorization', `Bearer ${noEmail}`);
    expect(res.status).toBe(401);
  });

  it('KHÔNG tin X-User-Id client gửi (chỉ tin token)', async () => {
    const token = signTestToken(
      { userId: 7, username: 'a', email: 'a@tdtu.edu.vn' },
      SECRET,
    );
    const res = await request(app.getHttpServer())
      .get('/api/v1/payments/me')
      .set('Authorization', `Bearer ${token}`)
      .set('X-User-Id', '999'); // giả danh — phải bị bỏ qua
    expect(res.body.userId).toBe(7);
  });
});

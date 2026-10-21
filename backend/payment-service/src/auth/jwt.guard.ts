import { CanActivate, ExecutionContext, Injectable } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as jwt from 'jsonwebtoken';
import { ApiException } from '../common/errors/api.exception';
import { AuthedRequest, AuthUser } from './current-user';

/**
 * Lớp phòng thủ 2 (defense in depth): payment-service TỰ verify JWT HS256
 * bằng cùng secret với user-service/gateway. Dù gateway đã verify + chèn
 * X-User-*, nếu ai gọi thẳng :8083 bỏ qua gateway thì vẫn bị chặn ở đây.
 *
 * Đọc userId/email TỪ TOKEN (không tin header client gửi lên).
 * Claims (docs/03 §3.1b.1): sub=userId, username, email.
 */
@Injectable()
export class JwtGuard implements CanActivate {
  private readonly secret: string;

  constructor(config: ConfigService) {
    // env.validation đã đảm bảo JWT_SECRET tồn tại & >= 32 ký tự (fail-fast).
    this.secret = config.get<string>('JWT_SECRET')!;
  }

  canActivate(context: ExecutionContext): boolean {
    const req = context.switchToHttp().getRequest<AuthedRequest>();
    const token = this.extractToken(req);

    if (!token) {
      throw ApiException.unauthenticated('Thiếu hoặc sai token xác thực');
    }

    let payload: jwt.JwtPayload;
    try {
      payload = jwt.verify(token, this.secret, {
        algorithms: ['HS256'],
      }) as jwt.JwtPayload;
    } catch {
      // KHÔNG log token/chi tiết lỗi (tránh lộ bí mật).
      throw ApiException.unauthenticated('Token không hợp lệ hoặc đã hết hạn');
    }

    const user = this.toAuthUser(payload);
    if (!user) {
      throw ApiException.unauthenticated('Token thiếu thông tin người dùng');
    }

    req.user = user;
    return true;
  }

  private extractToken(req: AuthedRequest): string | null {
    const header = req.headers['authorization'];
    if (typeof header !== 'string') return null;
    const [scheme, value] = header.split(' ');
    if (scheme !== 'Bearer' || !value) return null;
    return value;
  }

  private toAuthUser(payload: jwt.JwtPayload): AuthUser | null {
    const sub = payload.sub;
    const userId =
      typeof sub === 'number' ? sub : sub ? Number(sub) : NaN;
    const username = payload['username'];
    const email = payload['email'];
    if (!Number.isFinite(userId) || typeof email !== 'string') {
      return null;
    }
    return {
      userId,
      username: typeof username === 'string' ? username : '',
      email,
    };
  }
}

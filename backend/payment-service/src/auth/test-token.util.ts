import * as jwt from 'jsonwebtoken';
import { AuthUser } from './current-user';

/**
 * Sinh JWT HS256 hợp lệ — dùng trong TEST và khi dev chưa có gateway/user-service.
 * KHÔNG dùng ở production. Secret truyền vào từ env (không hard-code).
 */
export function signTestToken(
  user: AuthUser,
  secret: string,
  expiresInSec = 1800,
): string {
  return jwt.sign(
    { sub: user.userId, username: user.username, email: user.email },
    secret,
    { algorithm: 'HS256', expiresIn: expiresInSec },
  );
}

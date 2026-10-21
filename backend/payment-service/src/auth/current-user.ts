import { createParamDecorator, ExecutionContext } from '@nestjs/common';
import { Request } from 'express';

/** Thông tin người dùng đã xác thực, gắn vào request bởi JwtGuard. */
export interface AuthUser {
  userId: number;
  username: string;
  email: string;
}

export interface AuthedRequest extends Request {
  user?: AuthUser;
  traceId?: string;
}

/** @CurrentUser() — lấy AuthUser từ request trong controller. */
export const CurrentUser = createParamDecorator(
  (_data: unknown, ctx: ExecutionContext): AuthUser => {
    const req = ctx.switchToHttp().getRequest<AuthedRequest>();
    return req.user as AuthUser;
  },
);

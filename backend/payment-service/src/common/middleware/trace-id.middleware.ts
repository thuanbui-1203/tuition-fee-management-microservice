import { Injectable, NestMiddleware } from '@nestjs/common';
import { NextFunction, Request, Response } from 'express';
import { randomUUID } from 'crypto';

/**
 * Gắn X-Trace-Id cho mỗi request để truy vết xuyên service.
 * Nếu gateway đã truyền X-Trace-Id thì giữ nguyên, ngược lại sinh mới.
 */
@Injectable()
export class TraceIdMiddleware implements NestMiddleware {
  use(req: Request, res: Response, next: NextFunction): void {
    const incoming = req.headers['x-trace-id'];
    const traceId =
      typeof incoming === 'string' && incoming.length > 0
        ? incoming
        : randomUUID();
    req.headers['x-trace-id'] = traceId;
    res.setHeader('X-Trace-Id', traceId);
    next();
  }
}

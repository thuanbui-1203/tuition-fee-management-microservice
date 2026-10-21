import {
  ArgumentsHost,
  Catch,
  ExceptionFilter,
  HttpException,
  HttpStatus,
  Logger,
} from '@nestjs/common';
import { Request, Response } from 'express';
import { ApiException } from './api.exception';
import { ErrorCode } from './error-code';
import { redactToString } from '../logging/redact';

interface ErrorBody {
  code: string;
  message: string;
  timestamp: string;
  traceId: string;
  details?: Record<string, unknown>;
}

/**
 * Chuẩn hoá mọi lỗi về body { code, message, timestamp, traceId, details? } (docs/03 §3.1).
 * - ApiException: dùng code + details sẵn có.
 * - ValidationPipe (BadRequest từ class-validator): map sang INVALID_INPUT.
 * - HttpException khác: map theo status.
 * - Lỗi không lường trước: INTERNAL_ERROR (không lộ stack ra client).
 */
@Catch()
export class GlobalExceptionFilter implements ExceptionFilter {
  private readonly logger = new Logger('ExceptionFilter');

  catch(exception: unknown, host: ArgumentsHost): void {
    const ctx = host.switchToHttp();
    const response = ctx.getResponse<Response>();
    const request = ctx.getRequest<Request>();
    const traceId = (request.headers['x-trace-id'] as string) ?? 'unknown';

    const body = this.toErrorBody(exception, traceId);
    const status = this.toStatus(exception, body.code);

    // Log có redact — không bao giờ in otp/password/authorization.
    if (status >= 500) {
      this.logger.error(
        `[${traceId}] ${request.method} ${request.url} -> ${status} ${body.code}: ${body.message}`,
        exception instanceof Error ? exception.stack : undefined,
      );
    } else {
      this.logger.warn(
        `[${traceId}] ${request.method} ${request.url} -> ${status} ${body.code}` +
          (body.details ? ` details=${redactToString(body.details)}` : ''),
      );
    }

    response.status(status).json(body);
  }

  private toErrorBody(exception: unknown, traceId: string): ErrorBody {
    const timestamp = new Date().toISOString();

    if (exception instanceof ApiException) {
      return {
        code: exception.code,
        message: exception.message,
        timestamp,
        traceId,
        ...(exception.details ? { details: exception.details } : {}),
      };
    }

    if (exception instanceof HttpException) {
      const status = exception.getStatus();
      const res = exception.getResponse();
      // ValidationPipe trả { message: string[] , error, statusCode }
      let message = exception.message;
      if (typeof res === 'object' && res !== null && 'message' in res) {
        const m = (res as { message: unknown }).message;
        message = Array.isArray(m) ? m.join('; ') : String(m);
      }
      return {
        code: this.statusToCode(status),
        message,
        timestamp,
        traceId,
      };
    }

    return {
      code: ErrorCode.INTERNAL_ERROR,
      message: 'Lỗi hệ thống',
      timestamp,
      traceId,
    };
  }

  private toStatus(exception: unknown, code: string): number {
    if (exception instanceof HttpException) return exception.getStatus();
    return HttpStatus.INTERNAL_SERVER_ERROR;
  }

  private statusToCode(status: number): ErrorCode {
    switch (status) {
      case HttpStatus.BAD_REQUEST:
        return ErrorCode.INVALID_INPUT;
      case HttpStatus.UNAUTHORIZED:
        return ErrorCode.UNAUTHENTICATED;
      case HttpStatus.FORBIDDEN:
        return ErrorCode.FORBIDDEN;
      case HttpStatus.NOT_FOUND:
        return ErrorCode.NOT_FOUND;
      case HttpStatus.TOO_MANY_REQUESTS:
        return ErrorCode.RATE_LIMITED;
      case HttpStatus.SERVICE_UNAVAILABLE:
        return ErrorCode.DEPENDENCY_DOWN;
      default:
        return ErrorCode.INTERNAL_ERROR;
    }
  }
}

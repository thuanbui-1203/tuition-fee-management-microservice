import { HttpException } from '@nestjs/common';
import { ErrorCode, ERROR_HTTP_STATUS } from './error-code';

/**
 * Exception nghiệp vụ chuẩn của payment-service.
 * Mang theo mã lỗi (ErrorCode) + details tuỳ chọn (vd remainingAttempts).
 * GlobalExceptionFilter sẽ render ra body { code, message, timestamp, traceId, details? }.
 */
export class ApiException extends HttpException {
  readonly code: ErrorCode;
  readonly details?: Record<string, unknown>;

  constructor(
    code: ErrorCode,
    message: string,
    details?: Record<string, unknown>,
  ) {
    super(message, ERROR_HTTP_STATUS[code]);
    this.code = code;
    this.details = details;
  }

  static invalidInput(message = 'Dữ liệu không hợp lệ') {
    return new ApiException(ErrorCode.INVALID_INPUT, message);
  }

  static notFound(message = 'Không tìm thấy') {
    return new ApiException(ErrorCode.NOT_FOUND, message);
  }

  static forbidden(message = 'Không đủ quyền') {
    return new ApiException(ErrorCode.FORBIDDEN, message);
  }

  static unauthenticated(message = 'Chưa xác thực') {
    return new ApiException(ErrorCode.UNAUTHENTICATED, message);
  }

  static dependencyDown(message = 'Dịch vụ phụ thuộc không khả dụng') {
    return new ApiException(ErrorCode.DEPENDENCY_DOWN, message);
  }
}

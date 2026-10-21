import { Logger } from '@nestjs/common';
import { AxiosError } from 'axios';
import { ApiException } from '../common/errors/api.exception';
import { ErrorCode } from '../common/errors/error-code';

const logger = new Logger('DownstreamClient');

/** Body lỗi chuẩn mà service Spring trả về (docs/03 §3.1). */
interface RemoteErrorBody {
  code?: string;
  message?: string;
  details?: Record<string, unknown>;
}

const KNOWN_CODES = new Set<string>(Object.values(ErrorCode));

/**
 * Map lỗi khi gọi downstream service sang ApiException của payment-service:
 *  - 4xx có body {code,...} nghiệp vụ  → giữ nguyên code (vd FEE_ALREADY_PAID, INSUFFICIENT_BALANCE)
 *  - timeout / lỗi mạng / 5xx          → DEPENDENCY_DOWN (503)
 * `service` chỉ dùng để log, KHÔNG lộ chi tiết nội bộ ra client.
 */
export function mapDownstreamError(
  err: unknown,
  service: string,
): ApiException {
  const axiosErr = err as AxiosError<RemoteErrorBody>;

  // timeout / không kết nối được / không có response
  if (
    axiosErr.code === 'ECONNABORTED' ||
    axiosErr.code === 'ECONNREFUSED' ||
    axiosErr.code === 'ETIMEDOUT' ||
    !axiosErr.response
  ) {
    logger.error(`[${service}] không khả dụng: ${axiosErr.code ?? axiosErr.message}`);
    return ApiException.dependencyDown(
      `Dịch vụ ${service} không khả dụng, vui lòng thử lại`,
    );
  }

  const status = axiosErr.response.status;
  const body = axiosErr.response.data ?? {};

  // 5xx của downstream → coi là dependency down
  if (status >= 500) {
    logger.error(`[${service}] trả ${status}: ${body.message ?? 'lỗi nội bộ'}`);
    return ApiException.dependencyDown(`Dịch vụ ${service} đang lỗi`);
  }

  // 4xx nghiệp vụ: giữ nguyên code nếu nằm trong tập đã biết
  if (body.code && KNOWN_CODES.has(body.code)) {
    return new ApiException(
      body.code as ErrorCode,
      body.message ?? 'Lỗi nghiệp vụ',
      body.details,
    );
  }

  // 4xx không rõ code → INVALID_INPUT giữ message
  logger.warn(`[${service}] trả ${status} code không rõ: ${body.code}`);
  return new ApiException(
    ErrorCode.INVALID_INPUT,
    body.message ?? `Yêu cầu tới ${service} không hợp lệ`,
  );
}

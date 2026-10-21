/**
 * Redact dữ liệu nhạy cảm trước khi log.
 * KHÔNG BAO GIỜ log: otp, password, authorization header, jwt/token, code_hash.
 * Chỉ log paymentId / transactionId là đủ để truy vết.
 */
const SENSITIVE_KEYS = new Set(
  [
    'otp',
    'password',
    'passwordhash',
    'password_hash',
    'authorization',
    'token',
    'jwt',
    'accesstoken',
    'access_token',
    'secret',
    'codehash',
    'code_hash',
    'code',
    'idempotency-key',
    'idempotencykey',
  ].map((k) => k.toLowerCase()),
);

const REDACTED = '***REDACTED***';

export function redact<T>(value: T, _seen = new WeakSet<object>()): T {
  if (value === null || value === undefined) return value;

  if (Array.isArray(value)) {
    return value.map((v) => redact(v, _seen)) as unknown as T;
  }

  if (typeof value === 'object') {
    const obj = value as Record<string, unknown>;
    if (_seen.has(obj)) return '[Circular]' as unknown as T;
    _seen.add(obj);

    const out: Record<string, unknown> = {};
    for (const [key, val] of Object.entries(obj)) {
      if (SENSITIVE_KEYS.has(key.toLowerCase())) {
        out[key] = REDACTED;
      } else {
        out[key] = redact(val, _seen);
      }
    }
    return out as unknown as T;
  }

  return value;
}

/** Trả về chuỗi JSON đã redact — dùng trong logger. */
export function redactToString(value: unknown): string {
  try {
    return JSON.stringify(redact(value));
  } catch {
    return '[unserializable]';
  }
}

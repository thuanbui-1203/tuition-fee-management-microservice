import { ValueTransformer } from 'typeorm';

/**
 * BIGINT của Postgres bị driver 'pg' trả về dạng string (tránh mất chính xác với số > 2^53).
 * Với tiền VND, giá trị thực tế < Number.MAX_SAFE_INTEGER nên chuyển sang number an toàn.
 * Lưu số nguyên VND — KHÔNG dùng float cho tiền (quy ước liên ngôn ngữ docs/03 §3.1b.2).
 */
export const bigintTransformer: ValueTransformer = {
  to: (value?: number | null): number | null | undefined => value,
  from: (value?: string | null): number | null => {
    if (value === null || value === undefined) return null;
    return Number(value);
  },
};

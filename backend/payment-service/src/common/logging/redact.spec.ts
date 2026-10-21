import { redact, redactToString } from './redact';

describe('redact', () => {
  it('ẩn các khoá nhạy cảm (otp, password, authorization, token)', () => {
    const input = {
      otp: '482913',
      password: 'secret123',
      authorization: 'Bearer abc.def',
      token: 'xyz',
      paymentId: 'p-1',
      amount: 8400000,
    };
    const out = redact(input);
    expect(out.otp).toBe('***REDACTED***');
    expect(out.password).toBe('***REDACTED***');
    expect(out.authorization).toBe('***REDACTED***');
    expect(out.token).toBe('***REDACTED***');
    // dữ liệu không nhạy cảm giữ nguyên
    expect(out.paymentId).toBe('p-1');
    expect(out.amount).toBe(8400000);
  });

  it('redact lồng nhau trong object và array', () => {
    const input = {
      user: { email: 'a@b.c', password: 'x' },
      items: [{ code: '111' }, { code: '222' }],
    };
    const out = redact(input);
    expect(out.user.password).toBe('***REDACTED***');
    expect(out.user.email).toBe('a@b.c');
    expect(out.items[0].code).toBe('***REDACTED***');
  });

  it('không phân biệt hoa thường cho tên khoá', () => {
    const out = redact({ Authorization: 'Bearer x', OTP: '1' });
    expect(out.Authorization).toBe('***REDACTED***');
    expect(out.OTP).toBe('***REDACTED***');
  });

  it('xử lý được object vòng (circular) mà không ném lỗi', () => {
    const a: Record<string, unknown> = { name: 'a' };
    a.self = a;
    expect(() => redactToString(a)).not.toThrow();
  });

  it('redactToString trả JSON đã redact', () => {
    const s = redactToString({ password: 'p', keep: 1 });
    expect(s).toContain('***REDACTED***');
    expect(s).toContain('"keep":1');
    expect(s).not.toContain('"p"');
  });
});

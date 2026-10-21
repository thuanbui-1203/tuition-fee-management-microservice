import { PaymentsService } from './payments.service';
import { TransactionsRepository } from '../database/transactions.repository';
import { TuitionClient } from '../clients/tuition.client';
import { UserClient } from '../clients/user.client';
import { OtpClient } from '../clients/otp.client';
import { TransactionStatus } from '../database/transaction-status.enum';
import { ApiException } from '../common/errors/api.exception';
import { ErrorCode } from '../common/errors/error-code';
import { AuthUser } from '../auth/current-user';

const USER: AuthUser = { userId: 1, username: 'a', email: 'a@tdtu.edu.vn' };
const OTHER: AuthUser = { userId: 2, username: 'b', email: 'b@tdtu.edu.vn' };
const TRACE = 'trace-1';

function makeTxn(over: Partial<any> = {}) {
  return {
    id: 'txn-1',
    payerUserId: 1,
    payerEmail: USER.email,
    tuitionFeeId: 10,
    mssv: '521H0001',
    studentName: 'B',
    semester: 'HK1',
    amount: 8_400_000,
    status: TransactionStatus.OTP_SENT,
    lastOtpSentAt: new Date(),
    failureReason: null,
    createdAt: new Date(),
    completedAt: null,
    ...over,
  };
}

describe('PaymentsService.resendOtp', () => {
  let txns: jest.Mocked<TransactionsRepository>;
  let otp: jest.Mocked<OtpClient>;
  let service: PaymentsService;

  beforeEach(() => {
    txns = {
      findById: jest.fn().mockResolvedValue(makeTxn()),
      tryStampOtpResend: jest.fn(),
    } as unknown as jest.Mocked<TransactionsRepository>;
    otp = { issue: jest.fn().mockResolvedValue({ otpId: 'o-2' }) } as unknown as jest.Mocked<OtpClient>;
    const tuition = {} as unknown as jest.Mocked<TuitionClient>;
    const user = {} as unknown as jest.Mocked<UserClient>;
    service = new PaymentsService(txns, tuition, user, otp);
  });

  it('resend hợp lệ (ok) → 200, gọi otp.issue', async () => {
    txns.tryStampOtpResend.mockResolvedValue('ok');
    const res = await service.resendOtp(USER, 'txn-1', TRACE);
    expect(res.expiresInSeconds).toBe(300);
    expect(otp.issue).toHaveBeenCalledWith('txn-1', USER.email, TRACE);
  });

  it('rate_limited → 429, KHÔNG gọi otp.issue', async () => {
    txns.tryStampOtpResend.mockResolvedValue('rate_limited');
    await expect(service.resendOtp(USER, 'txn-1', TRACE)).rejects.toMatchObject({
      code: ErrorCode.RATE_LIMITED,
    });
    expect(otp.issue).not.toHaveBeenCalled();
  });

  it('wrong_state (SUCCESS/FAILED) → 422', async () => {
    txns.tryStampOtpResend.mockResolvedValue('wrong_state');
    await expect(service.resendOtp(USER, 'txn-1', TRACE)).rejects.toMatchObject({
      code: ErrorCode.TRANSACTION_INVALID_STATE,
    });
    expect(otp.issue).not.toHaveBeenCalled();
  });

  it('403 khi không phải chủ giao dịch', async () => {
    await expect(service.resendOtp(OTHER, 'txn-1', TRACE)).rejects.toMatchObject({
      code: ErrorCode.FORBIDDEN,
    });
    expect(txns.tryStampOtpResend).not.toHaveBeenCalled();
  });

  it('404 khi không tìm thấy', async () => {
    txns.findById.mockResolvedValue(null);
    await expect(service.resendOtp(USER, 'x', TRACE)).rejects.toMatchObject({
      code: ErrorCode.NOT_FOUND,
    });
  });

  it('otp.issue lỗi → 503 (giữ OTP_SENT, không đổi status)', async () => {
    txns.tryStampOtpResend.mockResolvedValue('ok');
    otp.issue.mockRejectedValue(ApiException.dependencyDown('otp down'));
    await expect(service.resendOtp(USER, 'txn-1', TRACE)).rejects.toMatchObject({
      code: ErrorCode.DEPENDENCY_DOWN,
    });
  });
});

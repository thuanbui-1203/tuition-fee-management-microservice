import { PaymentsService } from './payments.service';
import { TransactionsRepository } from '../database/transactions.repository';
import { TuitionClient } from '../clients/tuition.client';
import { OtpClient } from '../clients/otp.client';
import { TransactionStatus } from '../database/transaction-status.enum';
import { ApiException } from '../common/errors/api.exception';
import { ErrorCode } from '../common/errors/error-code';
import { AuthUser } from '../auth/current-user';

const USER: AuthUser = {
  userId: 1,
  username: 'nguyenvana',
  email: 'a@tdtu.edu.vn',
};
const TRACE = 'trace-1';

function makeTxn(over: Partial<any> = {}) {
  return {
    id: 'txn-1',
    idempotencyKey: 'key-1',
    payerUserId: 1,
    payerEmail: USER.email,
    tuitionFeeId: 10,
    mssv: '521H0001',
    studentName: 'Trần Thị B',
    semester: 'HK1',
    amount: 8_400_000,
    status: TransactionStatus.INITIATED,
    lastOtpSentAt: new Date(),
    failureReason: null,
    createdAt: new Date(),
    completedAt: null,
    ...over,
  };
}

describe('PaymentsService.create', () => {
  let txns: jest.Mocked<TransactionsRepository>;
  let tuition: jest.Mocked<TuitionClient>;
  let otp: jest.Mocked<OtpClient>;
  let service: PaymentsService;

  beforeEach(() => {
    txns = {
      findByIdempotencyKey: jest.fn(),
      save: jest.fn(),
      transition: jest.fn(),
    } as unknown as jest.Mocked<TransactionsRepository>;
    tuition = { lookup: jest.fn() } as unknown as jest.Mocked<TuitionClient>;
    otp = { issue: jest.fn() } as unknown as jest.Mocked<OtpClient>;
    const user = {} as unknown as jest.Mocked<
      import('../clients/user.client').UserClient
    >;
    service = new PaymentsService(txns, tuition, user, otp);
  });

  it('happy path: lookup UNPAID → tạo INITIATED → otp.issue → OTP_SENT (201)', async () => {
    txns.findByIdempotencyKey.mockResolvedValue(null);
    tuition.lookup.mockResolvedValue({
      mssv: '521H0001',
      studentName: 'Trần Thị B',
      fee: { feeId: 10, semester: 'HK1', amount: 8_400_000, status: 'UNPAID' },
    });
    txns.save.mockResolvedValue(makeTxn());
    otp.issue.mockResolvedValue({ otpId: 'o-1' });
    txns.transition.mockResolvedValue(true);

    const res = await service.create(USER, '521H0001', 'key-1', TRACE);

    expect(res.status).toBe(TransactionStatus.OTP_SENT);
    expect(res.amount).toBe(8_400_000);
    expect(res.expiresInSeconds).toBe(300);
    expect(otp.issue).toHaveBeenCalledWith('txn-1', USER.email, TRACE);
  });

  it('thiếu Idempotency-Key → INVALID_INPUT', async () => {
    await expect(
      service.create(USER, '521H0001', '', TRACE),
    ).rejects.toMatchObject({ code: ErrorCode.INVALID_INPUT });
  });

  it('fee đã PAID → FEE_ALREADY_PAID', async () => {
    txns.findByIdempotencyKey.mockResolvedValue(null);
    tuition.lookup.mockResolvedValue({
      mssv: '521H0001',
      studentName: 'B',
      fee: { feeId: 10, semester: 'HK1', amount: 8_400_000, status: 'PAID' },
    });
    await expect(
      service.create(USER, '521H0001', 'key-1', TRACE),
    ).rejects.toMatchObject({ code: ErrorCode.FEE_ALREADY_PAID });
  });

  it('idempotent: cùng key + cùng mssv → trả giao dịch cũ, KHÔNG tạo mới', async () => {
    txns.findByIdempotencyKey.mockResolvedValue(
      makeTxn({ status: TransactionStatus.OTP_SENT }),
    );
    const res = await service.create(USER, '521H0001', 'key-1', TRACE);
    expect(res.paymentId).toBe('txn-1');
    expect(txns.save).not.toHaveBeenCalled();
    expect(tuition.lookup).not.toHaveBeenCalled();
  });

  it('cùng key + khác mssv → IDEMPOTENCY_CONFLICT', async () => {
    txns.findByIdempotencyKey.mockResolvedValue(makeTxn({ mssv: '521H0002' }));
    await expect(
      service.create(USER, '521H0001', 'key-1', TRACE),
    ).rejects.toMatchObject({ code: ErrorCode.IDEMPOTENCY_CONFLICT });
  });

  it('otp.issue lỗi → transaction FAILED + ném lỗi', async () => {
    txns.findByIdempotencyKey.mockResolvedValue(null);
    tuition.lookup.mockResolvedValue({
      mssv: '521H0001',
      studentName: 'B',
      fee: { feeId: 10, semester: 'HK1', amount: 8_400_000, status: 'UNPAID' },
    });
    txns.save.mockResolvedValue(makeTxn());
    otp.issue.mockRejectedValue(ApiException.dependencyDown('otp down'));
    txns.transition.mockResolvedValue(true);

    await expect(
      service.create(USER, '521H0001', 'key-1', TRACE),
    ).rejects.toMatchObject({ code: ErrorCode.DEPENDENCY_DOWN });

    expect(txns.transition).toHaveBeenCalledWith(
      'txn-1',
      TransactionStatus.INITIATED,
      TransactionStatus.FAILED,
      expect.objectContaining({ failureReason: expect.any(String) }),
    );
  });
});

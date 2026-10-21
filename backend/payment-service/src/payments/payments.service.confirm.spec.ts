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
    idempotencyKey: 'key-1',
    payerUserId: 1,
    payerEmail: USER.email,
    tuitionFeeId: 10,
    mssv: '521H0001',
    studentName: 'Trần Thị B',
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

describe('PaymentsService.confirm (saga + compensation)', () => {
  let txns: jest.Mocked<TransactionsRepository>;
  let tuition: jest.Mocked<TuitionClient>;
  let user: jest.Mocked<UserClient>;
  let otp: jest.Mocked<OtpClient>;
  let service: PaymentsService;

  beforeEach(() => {
    txns = {
      findById: jest.fn(),
      transition: jest.fn().mockResolvedValue(true),
      markSuccessWithOutbox: jest.fn().mockResolvedValue(true),
    } as unknown as jest.Mocked<TransactionsRepository>;
    tuition = {
      claim: jest.fn().mockResolvedValue({ success: true }),
      release: jest.fn().mockResolvedValue({ success: true }),
    } as unknown as jest.Mocked<TuitionClient>;
    user = {
      debit: jest.fn(),
    } as unknown as jest.Mocked<UserClient>;
    otp = {
      verify: jest.fn().mockResolvedValue({ valid: true }),
    } as unknown as jest.Mocked<OtpClient>;
    service = new PaymentsService(txns, tuition, user, otp);
  });

  // ---------- HAPPY PATH (Task 7) ----------
  it('happy path: verify → claim → debit → SUCCESS + outbox', async () => {
    txns.findById
      .mockResolvedValueOnce(makeTxn()) // loadOwned
      .mockResolvedValueOnce(
        makeTxn({ status: TransactionStatus.SUCCESS, completedAt: new Date() }),
      ); // after success
    user.debit.mockResolvedValue({ success: true, newBalance: 1_600_000 });

    const res = await service.confirm(USER, 'txn-1', '482913', TRACE);

    expect(res.status).toBe(TransactionStatus.SUCCESS);
    expect(res.feeStatus).toBe('PAID');
    expect(res.newBalance).toBe(1_600_000);
    expect(otp.verify).toHaveBeenCalledWith('txn-1', '482913', TRACE);
    expect(tuition.claim).toHaveBeenCalledWith(10, 'txn-1', TRACE);
    expect(user.debit).toHaveBeenCalledWith(1, 8_400_000, 'txn-1', 'debit-txn-1', TRACE);
    expect(txns.markSuccessWithOutbox).toHaveBeenCalled();
  });

  // ---------- OWNER / STATE ----------
  it('404 khi không tìm thấy giao dịch', async () => {
    txns.findById.mockResolvedValue(null);
    await expect(service.confirm(USER, 'x', '482913', TRACE)).rejects.toMatchObject({
      code: ErrorCode.NOT_FOUND,
    });
  });

  it('403 khi không phải chủ giao dịch', async () => {
    txns.findById.mockResolvedValue(makeTxn());
    await expect(service.confirm(OTHER, 'txn-1', '482913', TRACE)).rejects.toMatchObject({
      code: ErrorCode.FORBIDDEN,
    });
  });

  it('422 khi status != OTP_SENT', async () => {
    txns.findById.mockResolvedValue(makeTxn({ status: TransactionStatus.SUCCESS }));
    await expect(service.confirm(USER, 'txn-1', '482913', TRACE)).rejects.toMatchObject({
      code: ErrorCode.TRANSACTION_INVALID_STATE,
    });
  });

  // ---------- OTP ERRORS: giữ OTP_SENT, KHÔNG claim/debit ----------
  it('OTP sai (400) → giữ OTP_SENT, không verify-transition, không claim', async () => {
    txns.findById.mockResolvedValue(makeTxn());
    otp.verify.mockRejectedValue(
      new ApiException(ErrorCode.OTP_INCORRECT, 'Sai OTP', { remainingAttempts: 4 }),
    );
    await expect(service.confirm(USER, 'txn-1', '000000', TRACE)).rejects.toMatchObject({
      code: ErrorCode.OTP_INCORRECT,
      details: { remainingAttempts: 4 },
    });
    expect(txns.transition).not.toHaveBeenCalled();
    expect(tuition.claim).not.toHaveBeenCalled();
  });

  it('OTP hết hạn (410) → ném OTP_EXPIRED, không claim', async () => {
    txns.findById.mockResolvedValue(makeTxn());
    otp.verify.mockRejectedValue(
      new ApiException(ErrorCode.OTP_EXPIRED, 'Hết hạn'),
    );
    await expect(service.confirm(USER, 'txn-1', '000000', TRACE)).rejects.toMatchObject({
      code: ErrorCode.OTP_EXPIRED,
    });
    expect(tuition.claim).not.toHaveBeenCalled();
  });

  // ---------- SCENARIO B: claim 409 ----------
  it('claim 409 FEE_ALREADY_PAID → FAILED (Kịch bản B), không debit', async () => {
    txns.findById.mockResolvedValue(makeTxn());
    tuition.claim.mockRejectedValue(
      new ApiException(ErrorCode.FEE_ALREADY_PAID, 'Đã thanh toán'),
    );
    await expect(service.confirm(USER, 'txn-1', '482913', TRACE)).rejects.toMatchObject({
      code: ErrorCode.FEE_ALREADY_PAID,
    });
    expect(user.debit).not.toHaveBeenCalled();
    expect(txns.transition).toHaveBeenCalledWith(
      'txn-1',
      TransactionStatus.OTP_VERIFIED,
      TransactionStatus.FAILED,
      expect.objectContaining({ failureReason: expect.stringContaining('claim') }),
    );
  });

  // ---------- SCENARIO A: debit 422 → compensation ----------
  it('debit 422 INSUFFICIENT_BALANCE → release + FAILED (compensation)', async () => {
    txns.findById.mockResolvedValue(makeTxn());
    user.debit.mockRejectedValue(
      new ApiException(ErrorCode.INSUFFICIENT_BALANCE, 'Thiếu tiền'),
    );
    await expect(service.confirm(USER, 'txn-1', '482913', TRACE)).rejects.toMatchObject({
      code: ErrorCode.INSUFFICIENT_BALANCE,
    });
    expect(tuition.release).toHaveBeenCalledWith(10, 'txn-1', TRACE);
    expect(txns.transition).toHaveBeenCalledWith(
      'txn-1',
      TransactionStatus.OTP_VERIFIED,
      TransactionStatus.FAILED,
      expect.objectContaining({ failureReason: expect.stringContaining('debit') }),
    );
  });

  it('debit INSUFFICIENT_BALANCE KHÔNG retry (lỗi nghiệp vụ)', async () => {
    txns.findById.mockResolvedValue(makeTxn());
    user.debit.mockRejectedValue(
      new ApiException(ErrorCode.INSUFFICIENT_BALANCE, 'Thiếu tiền'),
    );
    await service.confirm(USER, 'txn-1', '482913', TRACE).catch(() => undefined);
    expect(user.debit).toHaveBeenCalledTimes(1);
  });

  // ---------- DEPENDENCY_DOWN: retry same key then release ----------
  it('debit timeout → retry cùng key → vẫn lỗi → release + FAILED + 503', async () => {
    txns.findById.mockResolvedValue(makeTxn());
    user.debit.mockRejectedValue(ApiException.dependencyDown('user down'));
    await expect(service.confirm(USER, 'txn-1', '482913', TRACE)).rejects.toMatchObject({
      code: ErrorCode.DEPENDENCY_DOWN,
    });
    expect(user.debit).toHaveBeenCalledTimes(2); // retry 1 lần cùng key
    expect(tuition.release).toHaveBeenCalled();
  });

  it('debit lần 1 timeout, lần 2 OK → SUCCESS (retry thành công)', async () => {
    txns.findById
      .mockResolvedValueOnce(makeTxn())
      .mockResolvedValueOnce(
        makeTxn({ status: TransactionStatus.SUCCESS, completedAt: new Date() }),
      );
    user.debit
      .mockRejectedValueOnce(ApiException.dependencyDown('temp'))
      .mockResolvedValueOnce({ success: true, newBalance: 1_600_000 });

    const res = await service.confirm(USER, 'txn-1', '482913', TRACE);
    expect(res.status).toBe(TransactionStatus.SUCCESS);
    expect(user.debit).toHaveBeenCalledTimes(2);
    expect(tuition.release).not.toHaveBeenCalled();
  });

  it('OTP_VERIFIED transition thất bại (double-confirm) → 422', async () => {
    txns.findById.mockResolvedValue(makeTxn());
    txns.transition.mockResolvedValueOnce(false); // không chuyển được sang OTP_VERIFIED
    await expect(service.confirm(USER, 'txn-1', '482913', TRACE)).rejects.toMatchObject({
      code: ErrorCode.TRANSACTION_INVALID_STATE,
    });
    expect(tuition.claim).not.toHaveBeenCalled();
  });
});

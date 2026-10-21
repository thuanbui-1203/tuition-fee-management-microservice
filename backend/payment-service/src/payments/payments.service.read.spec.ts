import { PaymentsService } from './payments.service';
import { TransactionsRepository } from '../database/transactions.repository';
import { TuitionClient } from '../clients/tuition.client';
import { UserClient } from '../clients/user.client';
import { OtpClient } from '../clients/otp.client';
import { TransactionStatus } from '../database/transaction-status.enum';
import { ErrorCode } from '../common/errors/error-code';
import { AuthUser } from '../auth/current-user';

const USER: AuthUser = { userId: 1, username: 'a', email: 'a@tdtu.edu.vn' };
const OTHER: AuthUser = { userId: 2, username: 'b', email: 'b@tdtu.edu.vn' };

function makeTxn(over: Partial<any> = {}) {
  return {
    id: 'txn-1',
    idempotencyKey: 'key-1',
    payerUserId: 1,
    payerEmail: USER.email,
    tuitionFeeId: 10,
    mssv: '521H0001',
    studentName: 'B',
    semester: 'HK1',
    amount: 8_400_000,
    status: TransactionStatus.SUCCESS,
    lastOtpSentAt: new Date(),
    failureReason: null,
    createdAt: new Date('2026-01-01T00:00:00Z'),
    completedAt: new Date('2026-01-01T00:01:00Z'),
    ...over,
  };
}

describe('PaymentsService read APIs', () => {
  let txns: jest.Mocked<TransactionsRepository>;
  let service: PaymentsService;

  beforeEach(() => {
    txns = {
      findById: jest.fn(),
      findByPayer: jest.fn(),
    } as unknown as jest.Mocked<TransactionsRepository>;
    service = new PaymentsService(
      txns,
      {} as unknown as jest.Mocked<TuitionClient>,
      {} as unknown as jest.Mocked<UserClient>,
      {} as unknown as jest.Mocked<OtpClient>,
    );
  });

  it('getHistory: map sang PaymentView + metadata phân trang', async () => {
    txns.findByPayer.mockResolvedValue([[makeTxn()], 1]);
    const res = await service.getHistory(USER, 0, 20);
    expect(res.totalElements).toBe(1);
    expect(res.page).toBe(0);
    expect(res.size).toBe(20);
    expect(res.content[0]).toMatchObject({
      paymentId: 'txn-1',
      mssv: '521H0001',
      amount: 8_400_000,
      status: TransactionStatus.SUCCESS,
    });
    expect(txns.findByPayer).toHaveBeenCalledWith(1, 0, 20);
  });

  it('getHistory: chuẩn hoá page/size không hợp lệ về mặc định', async () => {
    txns.findByPayer.mockResolvedValue([[], 0]);
    await service.getHistory(USER, -5, 0);
    expect(txns.findByPayer).toHaveBeenCalledWith(1, 0, 20);
  });

  it('getHistory: giới hạn size tối đa 100', async () => {
    txns.findByPayer.mockResolvedValue([[], 0]);
    await service.getHistory(USER, 1, 999);
    expect(txns.findByPayer).toHaveBeenCalledWith(1, 1, 20);
  });

  it('getHistory: danh sách rỗng → content rỗng, total 0', async () => {
    txns.findByPayer.mockResolvedValue([[], 0]);
    const res = await service.getHistory(USER, 0, 20);
    expect(res.content).toEqual([]);
    expect(res.totalElements).toBe(0);
  });

  it('getOne: trả PaymentView khi là chủ', async () => {
    txns.findById.mockResolvedValue(makeTxn());
    const res = await service.getOne(USER, 'txn-1');
    expect(res.paymentId).toBe('txn-1');
  });

  it('getOne: 403 khi không phải chủ', async () => {
    txns.findById.mockResolvedValue(makeTxn());
    await expect(service.getOne(OTHER, 'txn-1')).rejects.toMatchObject({
      code: ErrorCode.FORBIDDEN,
    });
  });

  it('getOne: 404 khi không tồn tại', async () => {
    txns.findById.mockResolvedValue(null);
    await expect(service.getOne(USER, 'x')).rejects.toMatchObject({
      code: ErrorCode.NOT_FOUND,
    });
  });
});

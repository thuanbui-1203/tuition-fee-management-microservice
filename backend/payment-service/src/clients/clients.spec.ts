import { HttpService } from '@nestjs/axios';
import { ConfigService } from '@nestjs/config';
import axios from 'axios';
import nock from 'nock';
import { TuitionClient } from './tuition.client';
import { UserClient } from './user.client';
import { OtpClient } from './otp.client';
import { ApiException } from '../common/errors/api.exception';
import { ErrorCode } from '../common/errors/error-code';

const USER_URL = 'http://user.test';
const TUITION_URL = 'http://tuition.test';
const OTP_URL = 'http://otp.test';
const TRACE = 'trace-1';

function makeConfig(): ConfigService {
  const map: Record<string, unknown> = {
    USER_SERVICE_URL: USER_URL,
    TUITION_SERVICE_URL: TUITION_URL,
    OTP_SERVICE_URL: OTP_URL,
    HTTP_TIMEOUT_READ_MS: 5000,
  };
  return { get: (k: string) => map[k] } as unknown as ConfigService;
}

describe('Downstream clients (nock)', () => {
  let http: HttpService;
  let tuition: TuitionClient;
  let user: UserClient;
  let otp: OtpClient;

  beforeAll(() => {
    http = new HttpService(axios.create());
    const config = makeConfig();
    tuition = new TuitionClient(http, config);
    user = new UserClient(http, config);
    otp = new OtpClient(http, config);
  });

  afterEach(() => nock.cleanAll());
  afterAll(() => nock.restore());

  // --- 200 OK ---
  it('tuition.lookup trả dữ liệu khi 200', async () => {
    nock(TUITION_URL)
      .get('/api/v1/tuitions')
      .query({ mssv: '521H0001' })
      .reply(200, {
        mssv: '521H0001',
        studentName: 'Trần Thị B',
        fee: { feeId: 10, semester: 'HK1', amount: 8400000, status: 'UNPAID' },
      });
    const res = await tuition.lookup('521H0001', TRACE);
    expect(res.fee.feeId).toBe(10);
    expect(res.fee.amount).toBe(8400000);
  });

  it('user.debit trả newBalance khi 200', async () => {
    nock(USER_URL)
      .post('/internal/accounts/debit', {
        userId: 1,
        amount: 8400000,
        transactionId: 'txn-1',
      })
      .reply(200, { success: true, newBalance: 1600000 });
    const res = await user.debit(1, 8400000, 'txn-1', 'debit-txn-1', TRACE);
    expect(res.newBalance).toBe(1600000);
  });

  // --- 4xx nghiệp vụ: giữ nguyên code ---
  it('tuition.claim 409 FEE_ALREADY_PAID → ApiException giữ code', async () => {
    nock(TUITION_URL)
      .post('/internal/tuitions/claim')
      .reply(409, { code: 'FEE_ALREADY_PAID', message: 'Đã thanh toán' });
    await expect(tuition.claim(10, 'txn-1', TRACE)).rejects.toMatchObject({
      code: ErrorCode.FEE_ALREADY_PAID,
    });
  });

  it('tuition.lookup 404 NOT_FOUND → giữ code', async () => {
    nock(TUITION_URL)
      .get('/api/v1/tuitions')
      .query(true)
      .reply(404, { code: 'NOT_FOUND', message: 'Không tìm thấy MSSV' });
    await expect(tuition.lookup('x', TRACE)).rejects.toMatchObject({
      code: ErrorCode.NOT_FOUND,
    });
  });

  it('user.debit 422 INSUFFICIENT_BALANCE → giữ code', async () => {
    nock(USER_URL)
      .post('/internal/accounts/debit')
      .reply(422, { code: 'INSUFFICIENT_BALANCE', message: 'Thiếu tiền' });
    await expect(
      user.debit(1, 8400000, 'txn-1', 'debit-txn-1', TRACE),
    ).rejects.toMatchObject({ code: ErrorCode.INSUFFICIENT_BALANCE });
  });

  it('otp.verify 400 OTP_INCORRECT giữ details.remainingAttempts', async () => {
    nock(OTP_URL)
      .post('/internal/otp/verify')
      .reply(400, {
        code: 'OTP_INCORRECT',
        message: 'Sai OTP',
        details: { remainingAttempts: 4 },
      });
    await expect(otp.verify('txn-1', '000000', TRACE)).rejects.toMatchObject({
      code: ErrorCode.OTP_INCORRECT,
      details: { remainingAttempts: 4 },
    });
  });

  // --- 5xx → DEPENDENCY_DOWN ---
  it('otp.issue 500 → DEPENDENCY_DOWN', async () => {
    nock(OTP_URL)
      .post('/internal/otp/issue')
      .reply(500, { message: 'boom' });
    await expect(
      otp.issue('txn-1', 'a@tdtu.edu.vn', TRACE),
    ).rejects.toMatchObject({ code: ErrorCode.DEPENDENCY_DOWN });
  });

  // --- timeout → DEPENDENCY_DOWN ---
  it('user.debit timeout → DEPENDENCY_DOWN', async () => {
    nock(USER_URL)
      .post('/internal/accounts/debit')
      .delayConnection(200)
      .reply(200, { success: true, newBalance: 0 });
    // ép timeout ngắn bằng config override
    const fastUser = new UserClient(http, {
      get: (k: string) =>
        ({
          USER_SERVICE_URL: USER_URL,
          HTTP_TIMEOUT_READ_MS: 20,
        })[k],
    } as unknown as ConfigService);
    const err = await fastUser
      .debit(1, 100, 'txn-1', 'debit-txn-1', TRACE)
      .catch((e) => e);
    expect(err).toBeInstanceOf(ApiException);
    expect(err.code).toBe(ErrorCode.DEPENDENCY_DOWN);
  });

  it('tuition.release 200 success', async () => {
    nock(TUITION_URL)
      .post('/internal/tuitions/10/release', { transactionId: 'txn-1' })
      .reply(200, { success: true });
    const res = await tuition.release(10, 'txn-1', TRACE);
    expect(res.success).toBe(true);
  });
});

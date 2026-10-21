import { Injectable, Logger } from '@nestjs/common';
import { randomUUID } from 'crypto';
import { TransactionsRepository } from '../database/transactions.repository';
import { Transaction } from '../database/entities/transaction.entity';
import { TransactionStatus } from '../database/transaction-status.enum';
import { TuitionClient } from '../clients/tuition.client';
import { UserClient } from '../clients/user.client';
import { OtpClient } from '../clients/otp.client';
import { ApiException } from '../common/errors/api.exception';
import { ErrorCode } from '../common/errors/error-code';
import { AuthUser } from '../auth/current-user';
import {
  ConfirmPaymentResponse,
  CreatePaymentResponse,
  PaymentPage,
  PaymentView,
  toPaymentView,
} from './dto/payment-response';

export const OTP_TTL_SECONDS = 300;

/**
 * payments-service — saga orchestrator.
 * Mỗi chuyển trạng thái là 1 local transaction ngắn (conditional UPDATE);
 * KHÔNG giữ DB transaction trong lúc gọi HTTP sang service khác.
 */
@Injectable()
export class PaymentsService {
  private readonly logger = new Logger('PaymentsService');

  constructor(
    private readonly txns: TransactionsRepository,
    private readonly tuition: TuitionClient,
    private readonly user: UserClient,
    private readonly otp: OtpClient,
  ) {}

  /**
   * POST /payments — khởi tạo giao dịch (idempotent theo Idempotency-Key).
   *  - key đã tồn tại + payload khớp → trả giao dịch cũ (idempotent)
   *  - key đã tồn tại + payload khác → 409 IDEMPOTENCY_CONFLICT
   *  - chưa có → lookup fee UNPAID → tạo INITIATED → otp.issue → OTP_SENT
   */
  async create(
    user: AuthUser,
    mssv: string,
    idempotencyKey: string,
    traceId: string,
  ): Promise<CreatePaymentResponse> {
    if (!idempotencyKey) {
      throw ApiException.invalidInput('Thiếu header Idempotency-Key');
    }

    const existing = await this.txns.findByIdempotencyKey(idempotencyKey);
    if (existing) {
      return this.replayCreate(existing, mssv);
    }

    // 1. Tra cứu học phí (phải UNPAID + lấy amount)
    const lookup = await this.tuition.lookup(mssv, traceId);
    if (lookup.fee.status === 'PAID') {
      throw new ApiException(
        ErrorCode.FEE_ALREADY_PAID,
        'Học phí đã được thanh toán',
      );
    }

    // 2. Tạo transaction INITIATED (snapshot thông tin SV)
    let txn: Transaction;
    try {
      txn = await this.txns.save({
        idempotencyKey,
        payerUserId: user.userId,
        payerEmail: user.email,
        tuitionFeeId: lookup.fee.feeId,
        mssv: lookup.mssv,
        studentName: lookup.studentName,
        semester: lookup.fee.semester,
        amount: lookup.fee.amount,
        status: TransactionStatus.INITIATED,
      });
    } catch (err) {
      // Race: 2 request cùng key gần như đồng thời → UNIQUE chặn request thứ 2.
      const retry = await this.txns.findByIdempotencyKey(idempotencyKey);
      if (retry) return this.replayCreate(retry, mssv);
      throw err;
    }

    // 3. Sinh OTP (otp-service tự publish email). Lỗi → giao dịch FAILED, 503.
    try {
      await this.otp.issue(txn.id, user.email, traceId);
    } catch (err) {
      await this.txns.transition(
        txn.id,
        TransactionStatus.INITIATED,
        TransactionStatus.FAILED,
        { failureReason: 'otp.issue failed' },
      );
      throw err;
    }

    // 4. INITIATED → OTP_SENT (conditional)
    const moved = await this.txns.transition(
      txn.id,
      TransactionStatus.INITIATED,
      TransactionStatus.OTP_SENT,
    );
    if (!moved) {
      throw new ApiException(
        ErrorCode.TRANSACTION_INVALID_STATE,
        'Giao dịch không ở trạng thái khởi tạo',
      );
    }

    this.logger.log(`[${traceId}] created payment ${txn.id} (OTP_SENT)`);
    return {
      paymentId: txn.id,
      amount: txn.amount,
      status: TransactionStatus.OTP_SENT,
      message: 'Mã OTP đã được gửi đến email của bạn',
      expiresInSeconds: OTP_TTL_SECONDS,
    };
  }

  /** Trả lại giao dịch cũ cho retry cùng Idempotency-Key (kiểm tra payload khớp). */
  private replayCreate(
    existing: Transaction,
    mssv: string,
  ): CreatePaymentResponse {
    if (existing.mssv !== mssv) {
      throw new ApiException(
        ErrorCode.IDEMPOTENCY_CONFLICT,
        'Idempotency-Key đã dùng cho yêu cầu khác',
      );
    }
    return {
      paymentId: existing.id,
      amount: existing.amount,
      status: existing.status,
      message: 'Mã OTP đã được gửi đến email của bạn',
      expiresInSeconds: OTP_TTL_SECONDS,
    };
  }

  /**
   * POST /payments/{id}/confirm — saga lõi.
   * Mỗi bước là 1 local transaction ngắn; KHÔNG giữ DB txn khi gọi HTTP.
   *
   *  0. load txn; kiểm tra owner (403) + status == OTP_SENT (422)
   *  1. otp.verify → sai/hết hạn/dùng rồi: GIỮ OTP_SENT (cho nhập lại), ném lỗi gốc
   *  2. OTP_SENT → OTP_VERIFIED (conditional)
   *  3. tuition.claim → 409 FEE_ALREADY_PAID → FAILED (Kịch bản B)
   *  4. user.debit (key=debit-<txnId>, idempotent):
   *       422 INSUFFICIENT_BALANCE → COMPENSATE release → FAILED (Kịch bản A)
   *       timeout/5xx → đã retry trong client? không — ta retry 1 lần cùng key →
   *                     vẫn lỗi → COMPENSATE release → FAILED → 503
   *  5. markSuccessWithOutbox: OTP_VERIFIED → SUCCESS + insert outbox (CÙNG txn DB)
   */
  async confirm(
    user: AuthUser,
    paymentId: string,
    otp: string,
    traceId: string,
  ): Promise<ConfirmPaymentResponse> {
    const txn = await this.loadOwned(user, paymentId);

    if (txn.status !== TransactionStatus.OTP_SENT) {
      throw new ApiException(
        ErrorCode.TRANSACTION_INVALID_STATE,
        'Giao dịch không ở trạng thái chờ OTP',
      );
    }

    // 1. Verify OTP — lỗi OTP (400/409/410) GIỮ nguyên OTP_SENT để người dùng nhập lại.
    await this.otp.verify(txn.id, otp, traceId);

    // 2. OTP_SENT → OTP_VERIFIED (chặn double-confirm đồng thời)
    const verified = await this.txns.transition(
      txn.id,
      TransactionStatus.OTP_SENT,
      TransactionStatus.OTP_VERIFIED,
    );
    if (!verified) {
      throw new ApiException(
        ErrorCode.TRANSACTION_INVALID_STATE,
        'Giao dịch đang được xử lý hoặc đã kết thúc',
      );
    }

    // 3. Claim fee (BR6). 409 → fee bị người khác trả → FAILED (Kịch bản B).
    try {
      await this.tuition.claim(txn.tuitionFeeId, txn.id, traceId);
    } catch (err) {
      await this.fail(txn.id, `claim failed: ${this.codeOf(err)}`);
      throw err;
    }

    // 4. Debit (BR7). Idempotency-Key = debit-<txnId>.
    let newBalance: number | undefined;
    try {
      const result = await this.debitWithRetry(txn, traceId);
      newBalance = result.newBalance;
    } catch (err) {
      // COMPENSATE: release fee về UNPAID (chỉ release giao dịch của chính mình).
      await this.compensateRelease(txn, traceId);
      await this.fail(txn.id, `debit failed: ${this.codeOf(err)}`);
      throw err;
    }

    // 5. SUCCESS + outbox trong CÙNG 1 transaction DB.
    const ok = await this.txns.markSuccessWithOutbox(txn.id, {
      messageId: randomUUID(),
      eventType: 'PaymentSucceeded',
      payload: {
        paymentId: txn.id,
        to: txn.payerEmail,
        template: 'payment-success',
        data: {
          txnId: txn.id,
          amount: txn.amount,
          mssv: txn.mssv,
          studentName: txn.studentName,
        },
      },
    });
    if (!ok) {
      // Trạng thái đã đổi bởi luồng khác (hiếm) — coi như xung đột state.
      throw new ApiException(
        ErrorCode.TRANSACTION_INVALID_STATE,
        'Giao dịch không ở trạng thái hợp lệ để hoàn tất',
      );
    }

    const after = await this.txns.findById(txn.id);
    this.logger.log(`[${traceId}] payment ${txn.id} SUCCESS`);
    return {
      paymentId: txn.id,
      status: TransactionStatus.SUCCESS,
      amount: txn.amount,
      paidAt: after?.completedAt ? after.completedAt.toISOString() : null,
      newBalance,
      feeStatus: 'PAID',
      message: 'Thanh toán học phí thành công',
    };
  }

  /**
   * POST /payments/{id}/otp/resend — gửi lại OTP.
   *  - owner check (403)
   *  - rate-limit 60s bằng conditional UPDATE (tryStampOtpResend):
   *      'wrong_state' → 422 (giao dịch SUCCESS/FAILED/không ở OTP_SENT)
   *      'rate_limited' → 429 (chưa đủ 60s)
   *  - otp.issue (otp-service EXPIRE OTP cũ trước khi tạo mới) → lỗi → 503, giữ OTP_SENT
   */
  async resendOtp(
    user: AuthUser,
    paymentId: string,
    traceId: string,
  ): Promise<{ message: string; expiresInSeconds: number }> {
    const txn = await this.loadOwned(user, paymentId);

    const stamp = await this.txns.tryStampOtpResend(txn.id);
    if (stamp === 'wrong_state') {
      throw new ApiException(
        ErrorCode.TRANSACTION_INVALID_STATE,
        'Giao dịch không ở trạng thái chờ OTP',
      );
    }
    if (stamp === 'rate_limited') {
      throw new ApiException(
        ErrorCode.RATE_LIMITED,
        'Vui lòng chờ 60 giây trước khi gửi lại mã',
      );
    }

    // stamp === 'ok' → đã cập nhật last_otp_sent_at, giờ gọi issue.
    await this.otp.issue(txn.id, txn.payerEmail ?? user.email, traceId);

    this.logger.log(`[${traceId}] resent OTP for payment ${txn.id}`);
    return {
      message: 'Mã OTP mới đã được gửi',
      expiresInSeconds: OTP_TTL_SECONDS,
    };
  }

  /**
   * Debit idempotent, retry 1 lần cho lỗi tạm thời (DEPENDENCY_DOWN).
   * Lỗi nghiệp vụ (INSUFFICIENT_BALANCE, CONFLICT) KHÔNG retry — ném ngay.
   */
  private async debitWithRetry(txn: Transaction, traceId: string) {
    const key = `debit-${txn.id}`;
    try {
      return await this.user.debit(
        txn.payerUserId,
        txn.amount,
        txn.id,
        key,
        traceId,
      );
    } catch (err) {
      if (
        err instanceof ApiException &&
        err.code === ErrorCode.DEPENDENCY_DOWN
      ) {
        this.logger.warn(`[${traceId}] debit retry (same key) for ${txn.id}`);
        return await this.user.debit(
          txn.payerUserId,
          txn.amount,
          txn.id,
          key,
          traceId,
        );
      }
      throw err;
    }
  }

  /** Compensation: release fee — nuốt lỗi release để không che lỗi gốc. */
  private async compensateRelease(
    txn: Transaction,
    traceId: string,
  ): Promise<void> {
    try {
      await this.tuition.release(txn.tuitionFeeId, txn.id, traceId);
      this.logger.warn(`[${traceId}] compensated: released fee ${txn.tuitionFeeId}`);
    } catch (relErr) {
      // Release lỗi → ghi log, outbox/relay có thể bù sau; không ném đè lỗi gốc.
      this.logger.error(
        `[${traceId}] COMPENSATION FAILED release fee ${txn.tuitionFeeId}: ${this.codeOf(relErr)}`,
      );
    }
  }

  /** Chuyển về FAILED từ bất kỳ trạng thái đang xử lý (OTP_VERIFIED). */
  private async fail(id: string, reason: string): Promise<void> {
    await this.txns.transition(
      id,
      TransactionStatus.OTP_VERIFIED,
      TransactionStatus.FAILED,
      { failureReason: reason, completedAt: new Date() },
    );
  }

  /** GET /payments/me — lịch sử giao dịch của user hiện tại (phân trang). */
  async getHistory(
    user: AuthUser,
    page: number,
    size: number,
  ): Promise<PaymentPage> {
    const safePage = Number.isFinite(page) && page >= 0 ? Math.floor(page) : 0;
    const safeSize =
      Number.isFinite(size) && size > 0 && size <= 100 ? Math.floor(size) : 20;
    const [rows, total] = await this.txns.findByPayer(
      user.userId,
      safePage,
      safeSize,
    );
    return {
      content: rows.map(toPaymentView),
      page: safePage,
      size: safeSize,
      totalElements: total,
    };
  }

  /** GET /payments/{id} — chi tiết 1 giao dịch (403 nếu không phải chủ). */
  async getOne(user: AuthUser, paymentId: string): Promise<PaymentView> {
    const txn = await this.loadOwned(user, paymentId);
    return toPaymentView(txn);
  }

  /** Load giao dịch + kiểm tra quyền sở hữu (404 / 403). */
  private async loadOwned(
    user: AuthUser,
    paymentId: string,
  ): Promise<Transaction> {
    const txn = await this.txns.findById(paymentId);
    if (!txn) {
      throw ApiException.notFound('Không tìm thấy giao dịch');
    }
    if (txn.payerUserId !== user.userId) {
      throw ApiException.forbidden('Không phải giao dịch của bạn');
    }
    return txn;
  }

  private codeOf(err: unknown): string {
    return err instanceof ApiException ? err.code : 'UNKNOWN';
  }
}

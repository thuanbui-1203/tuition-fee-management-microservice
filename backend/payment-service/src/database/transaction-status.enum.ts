/**
 * State machine giao dịch (docs/02 §2.4):
 * INITIATED → OTP_SENT → OTP_VERIFIED → SUCCESS
 * từ bất kỳ bước nào lỗi → FAILED
 */
export enum TransactionStatus {
  INITIATED = 'INITIATED',
  OTP_SENT = 'OTP_SENT',
  OTP_VERIFIED = 'OTP_VERIFIED',
  SUCCESS = 'SUCCESS',
  FAILED = 'FAILED',
}

export const ALL_TRANSACTION_STATUSES = Object.values(TransactionStatus);

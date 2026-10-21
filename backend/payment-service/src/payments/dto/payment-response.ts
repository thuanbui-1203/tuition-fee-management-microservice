import { Transaction } from '../../database/entities/transaction.entity';
import { TransactionStatus } from '../../database/transaction-status.enum';

/** Response 201 khi tạo giao dịch (POST /payments). */
export interface CreatePaymentResponse {
  paymentId: string;
  amount: number;
  status: TransactionStatus;
  message: string;
  expiresInSeconds: number;
}

/** Response 200 khi confirm thành công. */
export interface ConfirmPaymentResponse {
  paymentId: string;
  status: TransactionStatus;
  amount: number;
  paidAt: string | null;
  newBalance?: number;
  feeStatus: 'PAID';
  message: string;
}

/** Item trong lịch sử giao dịch (GET /payments/me, GET /payments/{id}). */
export interface PaymentView {
  paymentId: string;
  mssv: string;
  studentName: string;
  semester: string | null;
  amount: number;
  status: TransactionStatus;
  createdAt: string;
  completedAt: string | null;
}

/** Trang lịch sử giao dịch (GET /payments/me). */
export interface PaymentPage {
  content: PaymentView[];
  page: number;
  size: number;
  totalElements: number;
}

export function toPaymentView(t: Transaction): PaymentView {
  return {
    paymentId: t.id,
    mssv: t.mssv,
    studentName: t.studentName,
    semester: t.semester,
    amount: t.amount,
    status: t.status,
    createdAt: t.createdAt.toISOString(),
    completedAt: t.completedAt ? t.completedAt.toISOString() : null,
  };
}

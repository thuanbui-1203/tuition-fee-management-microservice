import {
  Column,
  CreateDateColumn,
  Entity,
  Index,
  PrimaryGeneratedColumn,
} from 'typeorm';
import { TransactionStatus } from '../transaction-status.enum';
import { bigintTransformer } from '../bigint.transformer';

/**
 * transactions — giao dịch/lịch sử (payment_db).
 * FK logic (không FK vật lý xuyên database) tới user-service & tuition-service.
 * Lưu snapshot mssv/studentName/semester để trả lịch sử không cần gọi lại tuition.
 */
@Entity('transactions')
@Index('idx_txn_payer', ['payerUserId'])
@Index('idx_txn_status', ['status'])
export class Transaction {
  @PrimaryGeneratedColumn('uuid')
  id!: string;

  @Column({ name: 'idempotency_key', type: 'varchar', length: 100, unique: true })
  idempotencyKey!: string;

  @Column({ name: 'payer_user_id', type: 'bigint', transformer: bigintTransformer })
  payerUserId!: number;

  @Column({ name: 'payer_email', type: 'varchar', length: 100, nullable: true })
  payerEmail!: string | null;

  @Column({ name: 'tuition_fee_id', type: 'bigint', transformer: bigintTransformer })
  tuitionFeeId!: number;

  // --- snapshot từ tuition-service (hiển thị lịch sử) ---
  @Column({ name: 'mssv', type: 'varchar', length: 20 })
  mssv!: string;

  @Column({ name: 'student_name', type: 'varchar', length: 100 })
  studentName!: string;

  @Column({ name: 'semester', type: 'varchar', length: 20, nullable: true })
  semester!: string | null;

  @Column({ type: 'bigint', transformer: bigintTransformer })
  amount!: number;

  @Column({
    type: 'varchar',
    length: 20,
    default: TransactionStatus.INITIATED,
  })
  status!: TransactionStatus;

  @Column({
    name: 'last_otp_sent_at',
    type: 'timestamptz',
    default: () => 'now()',
  })
  lastOtpSentAt!: Date;

  @Column({ name: 'failure_reason', type: 'text', nullable: true })
  failureReason!: string | null;

  @CreateDateColumn({ name: 'created_at', type: 'timestamptz' })
  createdAt!: Date;

  @Column({ name: 'completed_at', type: 'timestamptz', nullable: true })
  completedAt!: Date | null;
}

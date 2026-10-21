import { HttpService } from '@nestjs/axios';
import { Injectable } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { firstValueFrom } from 'rxjs';
import { ClaimResult, TuitionLookupResult } from './dto';
import { mapDownstreamError } from './downstream-error';

@Injectable()
export class TuitionClient {
  private readonly baseUrl: string;
  private readonly timeout: number;

  constructor(
    private readonly http: HttpService,
    config: ConfigService,
  ) {
    this.baseUrl = config.get<string>('TUITION_SERVICE_URL')!;
    this.timeout = config.get<number>('HTTP_TIMEOUT_READ_MS') ?? 5000;
  }

  /** Tra cứu học phí theo MSSV (dùng khi tạo giao dịch). */
  async lookup(mssv: string, traceId: string): Promise<TuitionLookupResult> {
    try {
      const res = await firstValueFrom(
        this.http.get<TuitionLookupResult>(
          `${this.baseUrl}/api/v1/tuitions`,
          {
            params: { mssv },
            timeout: this.timeout,
            headers: { 'X-Trace-Id': traceId },
          },
        ),
      );
      return res.data;
    } catch (err) {
      throw mapDownstreamError(err, 'tuition-service');
    }
  }

  /** Claim fee (BR6): chỉ thành công nếu fee đang UNPAID. */
  async claim(
    feeId: number,
    transactionId: string,
    traceId: string,
  ): Promise<ClaimResult> {
    try {
      const res = await firstValueFrom(
        this.http.post<ClaimResult>(
          `${this.baseUrl}/internal/tuitions/claim`,
          { feeId, transactionId },
          { timeout: this.timeout, headers: { 'X-Trace-Id': traceId } },
        ),
      );
      return res.data;
    } catch (err) {
      throw mapDownstreamError(err, 'tuition-service');
    }
  }

  /** Compensation: release fee về UNPAID (chỉ nếu paid_transaction_id = txn này). */
  async release(
    feeId: number,
    transactionId: string,
    traceId: string,
  ): Promise<ClaimResult> {
    try {
      const res = await firstValueFrom(
        this.http.post<ClaimResult>(
          `${this.baseUrl}/internal/tuitions/${feeId}/release`,
          { transactionId },
          { timeout: this.timeout, headers: { 'X-Trace-Id': traceId } },
        ),
      );
      return res.data;
    } catch (err) {
      throw mapDownstreamError(err, 'tuition-service');
    }
  }
}

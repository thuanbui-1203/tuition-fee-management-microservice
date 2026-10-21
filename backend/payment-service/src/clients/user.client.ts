import { HttpService } from '@nestjs/axios';
import { Injectable } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { firstValueFrom } from 'rxjs';
import { DebitResult } from './dto';
import { mapDownstreamError } from './downstream-error';

@Injectable()
export class UserClient {
  private readonly baseUrl: string;
  private readonly timeout: number;

  constructor(
    private readonly http: HttpService,
    config: ConfigService,
  ) {
    this.baseUrl = config.get<string>('USER_SERVICE_URL')!;
    this.timeout = config.get<number>('HTTP_TIMEOUT_READ_MS') ?? 5000;
  }

  /**
   * Trừ tiền nguyên tử (BR7). Debit theo userId (1 user–1 ví) — docs/03 §3.1b.3.
   * idempotencyKey = 'debit-<transactionId>' để retry không trừ 2 lần.
   * 422 INSUFFICIENT_BALANCE / 409 CONFLICT được mapDownstreamError giữ nguyên code.
   */
  async debit(
    userId: number,
    amount: number,
    transactionId: string,
    idempotencyKey: string,
    traceId: string,
  ): Promise<DebitResult> {
    try {
      const res = await firstValueFrom(
        this.http.post<DebitResult>(
          `${this.baseUrl}/internal/accounts/debit`,
          { userId, amount, transactionId },
          {
            timeout: this.timeout,
            headers: {
              'Idempotency-Key': idempotencyKey,
              'X-Trace-Id': traceId,
            },
          },
        ),
      );
      return res.data;
    } catch (err) {
      throw mapDownstreamError(err, 'user-service');
    }
  }
}

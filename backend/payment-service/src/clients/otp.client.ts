import { HttpService } from '@nestjs/axios';
import { Injectable } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { firstValueFrom } from 'rxjs';
import { OtpIssueResult, OtpVerifyResult } from './dto';
import { mapDownstreamError } from './downstream-error';

@Injectable()
export class OtpClient {
  private readonly baseUrl: string;
  private readonly timeout: number;

  constructor(
    private readonly http: HttpService,
    config: ConfigService,
  ) {
    this.baseUrl = config.get<string>('OTP_SERVICE_URL')!;
    this.timeout = config.get<number>('HTTP_TIMEOUT_READ_MS') ?? 5000;
  }

  /** Sinh OTP cho giao dịch + yêu cầu gửi email (otp-service tự publish event). */
  async issue(
    transactionId: string,
    email: string,
    traceId: string,
  ): Promise<OtpIssueResult> {
    try {
      const res = await firstValueFrom(
        this.http.post<OtpIssueResult>(
          `${this.baseUrl}/internal/otp/issue`,
          { transactionId, email },
          { timeout: this.timeout, headers: { 'X-Trace-Id': traceId } },
        ),
      );
      return res.data;
    } catch (err) {
      throw mapDownstreamError(err, 'otp-service');
    }
  }

  /**
   * Xác thực OTP. Sai/hết hạn/dùng rồi → otp-service trả 400/410/409,
   * mapDownstreamError giữ nguyên code (OTP_INCORRECT/OTP_EXPIRED/OTP_ALREADY_USED)
   * cùng details.remainingAttempts nếu có.
   */
  async verify(
    transactionId: string,
    otp: string,
    traceId: string,
  ): Promise<OtpVerifyResult> {
    try {
      const res = await firstValueFrom(
        this.http.post<OtpVerifyResult>(
          `${this.baseUrl}/internal/otp/verify`,
          { transactionId, otp },
          { timeout: this.timeout, headers: { 'X-Trace-Id': traceId } },
        ),
      );
      return res.data;
    } catch (err) {
      throw mapDownstreamError(err, 'otp-service');
    }
  }
}

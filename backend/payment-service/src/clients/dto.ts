/**
 * DTO cho các cuộc gọi nội bộ (/internal/**) tới user/tuition/otp-service.
 * Khớp bảng 3.7 của docs/03-rest-api-design.md (đã chốt debit theo userId).
 */

// --- tuition-service ---
export interface TuitionLookupResult {
  mssv: string;
  studentName: string;
  fee: {
    feeId: number;
    semester: string;
    amount: number; // số nguyên VND
    status: 'UNPAID' | 'PAID';
  };
}

export interface ClaimResult {
  success: boolean;
}

// --- user-service ---
export interface DebitResult {
  success: boolean;
  newBalance: number;
}

// --- otp-service ---
export interface OtpIssueResult {
  otpId: string;
}

export interface OtpVerifyResult {
  valid: boolean;
}

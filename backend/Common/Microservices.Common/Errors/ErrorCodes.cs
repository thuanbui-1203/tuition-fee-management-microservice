namespace Microservices.Common.Errors;

/// <summary>
/// Canonical business error codes shared across services. These values appear in the
/// <c>code</c> field of the unified error response and must be kept stable.
/// </summary>
public static class ErrorCodes
{
    public const string InvalidInput = "INVALID_INPUT";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string FeeAlreadyPaid = "FEE_ALREADY_PAID";
    public const string ReleaseConflict = "RELEASE_CONFLICT";
    public const string OtpAlreadyUsed = "OTP_ALREADY_USED";
    public const string OtpIncorrect = "OTP_INCORRECT";
    public const string OtpExpired = "OTP_EXPIRED";
    public const string OtpLocked = "OTP_LOCKED";
    public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";
    public const string RateLimited = "RATE_LIMITED";
    public const string InternalError = "INTERNAL_ERROR";
    public const string DependencyDown = "DEPENDENCY_DOWN";
}

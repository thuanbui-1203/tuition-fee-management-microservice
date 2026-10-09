namespace OtpService.Domain.Enums;

/// <summary>
/// OTP lifecycle. The code is hashed at rest; status transitions are enforced by atomic
/// conditional updates so concurrent verifications cannot both succeed.
/// </summary>
public enum OtpStatus
{
    Active,
    Used,
    Expired,
    Locked
}

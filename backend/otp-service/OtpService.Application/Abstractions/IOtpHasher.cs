namespace OtpService.Application.Abstractions;

/// <summary>Hashes and verifies OTPs with a per-record random salt.</summary>
public interface IOtpHasher
{
    string Hash(string otp);

    bool Verify(string otp, string storedHash);
}

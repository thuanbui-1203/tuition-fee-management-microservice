using System.Security.Cryptography;
using OtpService.Application.Abstractions;

namespace OtpService.Application.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 OTP hasher with a fresh 128-bit salt per record and constant-time
/// verification. Suitable for short-lived, high-entropy secrets like OTPs.
/// </summary>
public sealed class Pbkdf2OtpHasher : IOtpHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string otp)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(otp, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string otp, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(otp, salt, Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

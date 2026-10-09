using System.Security.Cryptography;
using OtpService.Application.Abstractions;

namespace OtpService.Application.Security;

/// <summary>
/// Cryptographically secure OTP generator using <see cref="RandomNumberGenerator.GetInt32(int, int)"/>,
/// which avoids the modulo bias of <c>Random</c> and never uses <c>Guid</c> or timestamps.
/// </summary>
public sealed class CryptoSecureOtpGenerator : IOtpGenerator
{
    public string Generate(int length)
    {
        if (length is <= 0 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "OTP length must be between 1 and 12.");
        }

        var digits = new char[length];
        for (var i = 0; i < length; i++)
        {
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return new string(digits);
    }
}

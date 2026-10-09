using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OtpService.Application.Validation;

/// <summary>OTP format validation (digits only, exact length).</summary>
public static partial class OtpValidator
{
    public static bool IsValid([NotNullWhen(true)] string? otp, int length)
    {
        if (string.IsNullOrWhiteSpace(otp) || length <= 0)
        {
            return false;
        }

        return otp.Length == length && DigitsRegex().IsMatch(otp);
    }

    [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex DigitsRegex();
}

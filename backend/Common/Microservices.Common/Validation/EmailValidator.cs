using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Microservices.Common.Validation;

/// <summary>Email address validation shared across services.</summary>
public static partial class EmailValidator
{
    public const int MaxLength = 254;

    public static bool IsValid([NotNullWhen(true)] string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.Length <= MaxLength
        && EmailRegex().IsMatch(email);

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();
}

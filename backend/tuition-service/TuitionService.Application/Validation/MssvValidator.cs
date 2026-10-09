using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TuitionService.Application.Validation;

/// <summary>MSSV format rules shared by every tuition use case.</summary>
public static partial class MssvValidator
{
    public const int MaxLength = 20;

    public static bool IsValid([NotNullWhen(true)] string? mssv) =>
        !string.IsNullOrWhiteSpace(mssv)
        && mssv.Length <= MaxLength
        && AlphanumericRegex().IsMatch(mssv);

    [GeneratedRegex("^[A-Za-z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AlphanumericRegex();
}

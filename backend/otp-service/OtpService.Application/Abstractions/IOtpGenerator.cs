namespace OtpService.Application.Abstractions;

/// <summary>Generates cryptographically secure OTPs (never Random / Guid / timestamp based).</summary>
public interface IOtpGenerator
{
    string Generate(int length);
}

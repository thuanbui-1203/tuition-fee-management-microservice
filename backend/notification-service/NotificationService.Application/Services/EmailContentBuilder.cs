using System.Text.Json;
using Microservices.Common.Errors;
using Microservices.Common.Messaging;
using Microservices.Common.Security;
using Microsoft.Extensions.Options;
using NotificationService.Application.Abstractions;

namespace NotificationService.Application.Services;

/// <summary>
/// Builds OTP and payment-confirmation emails from event payloads. The OTP is decrypted with
/// the shared AES-256-GCM key (never logged, never persisted).
/// </summary>
public sealed class EmailContentBuilder : IEmailContentBuilder
{
    private readonly EventEncryptionOptions _encryptionOptions;

    public EmailContentBuilder(IOptions<EventEncryptionOptions> encryptionOptions)
    {
        _encryptionOptions = encryptionOptions.Value;
    }

    public Result<EmailContent> Build(string eventType, JsonElement payload)
    {
        return eventType switch
        {
            EmailEventTypes.OtpRequested => BuildOtpEmail(payload),
            EmailEventTypes.PaymentSucceeded => BuildPaymentConfirmation(payload),
            _ => Error.InvalidInput("Unsupported email event type.")
        };
    }

    private Result<EmailContent> BuildOtpEmail(JsonElement payload)
    {
        if (!TryGetString(payload, "otp", out var encrypted) ||
            !TryGetInt(payload, "expiresInSeconds", out var expiresInSeconds))
        {
            return Error.InvalidInput("Malformed OTP email payload.");
        }

        string otp;
        try
        {
            otp = AesGcmCrypto.DecryptString(encrypted, _encryptionOptions.KeyBytes);
        }
        catch (Exception)
        {
            return Error.InvalidInput("Unable to decrypt OTP email payload.");
        }

        var minutes = Math.Max(1, expiresInSeconds / 60);
        var text = $"Mã OTP xác nhận thanh toán học phí của bạn là: {otp}. Mã có hiệu lực trong {minutes} phút.";
        var html = $"<p>Mã OTP xác nhận thanh toán học phí của bạn là:</p><h2>{otp}</h2><p>Mã có hiệu lực trong {minutes} phút. Không chia sẻ mã này với bất kỳ ai.</p>";

        return new EmailContent("Mã OTP xác nhận thanh toán học phí", html, text);
    }

    private Result<EmailContent> BuildPaymentConfirmation(JsonElement payload)
    {
        var studentName = GetString(payload, "studentName") ?? string.Empty;
        var mssv = GetString(payload, "mssv") ?? string.Empty;
        var semester = GetString(payload, "semester") ?? string.Empty;
        if (!TryGetDecimal(payload, "amount", out var amount))
        {
            return Error.InvalidInput("Malformed payment confirmation payload.");
        }

        var formatted = $"{amount:N0} VND";
        var text = $"Bạn đã thanh toán học phí thành công: sinh viên {studentName} (MSSV {mssv}), học kỳ {semester}, số tiền {formatted}.";
        var html = $"<p>Bạn đã thanh toán học phí thành công.</p><ul><li>Sinh viên: {studentName} (MSSV {mssv})</li><li>Học kỳ: {semester}</li><li>Số tiền: {formatted}</li></ul>";

        return new EmailContent("Thanh toán học phí thành công", html, text);
    }

    private static bool TryGetString(JsonElement payload, string name, out string value)
    {
        value = string.Empty;
        if (!payload.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrEmpty(value);
    }

    private static string? GetString(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static bool TryGetInt(JsonElement payload, string name, out int value)
    {
        value = 0;
        return payload.TryGetProperty(name, out var element)
               && element.ValueKind == JsonValueKind.Number
               && element.TryGetInt32(out value);
    }

    private static bool TryGetDecimal(JsonElement payload, string name, out decimal value)
    {
        value = 0;
        return payload.TryGetProperty(name, out var element)
               && element.ValueKind == JsonValueKind.Number
               && element.TryGetDecimal(out value);
    }
}

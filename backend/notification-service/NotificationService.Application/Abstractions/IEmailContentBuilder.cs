using System.Text.Json;
using Microservices.Common.Errors;

namespace NotificationService.Application.Abstractions;

/// <summary>Email subject/body produced from an event.</summary>
public sealed record EmailContent(string Subject, string HtmlBody, string TextBody);

/// <summary>Maps an incoming event type + payload to an email. OTP payloads are decrypted here.</summary>
public interface IEmailContentBuilder
{
    Result<EmailContent> Build(string eventType, JsonElement payload);
}

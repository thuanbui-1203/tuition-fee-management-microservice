namespace NotificationService.Application.Abstractions;

/// <summary>A single outbound email.</summary>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);

/// <summary>Result of an SMTP send attempt, distinguishing transient from permanent failures.</summary>
public enum SendEmailOutcome
{
    Sent,
    TransientFailure,
    PermanentFailure
}

public sealed record SendEmailResult(
    SendEmailOutcome Outcome,
    string? ProviderMessageId,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>Sends email via SMTP. Implementations classify failures for retry policy.</summary>
public interface IEmailSender
{
    Task<SendEmailResult> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

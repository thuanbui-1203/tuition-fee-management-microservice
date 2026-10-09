using NotificationService.Domain.Entities;

namespace NotificationService.Application.Abstractions;

/// <summary>Persistence contract for the email audit log.</summary>
public interface IEmailLogRepository
{
    Task<EmailLog?> FindByMessageIdAsync(string messageId, CancellationToken cancellationToken);

    /// <summary>Inserts a new log; returns <c>null</c> when a duplicate message id already exists.</summary>
    Task<EmailLog?> TryCreateAsync(EmailLog log, CancellationToken cancellationToken);

    Task MarkSendingAsync(long id, int attemptCount, CancellationToken cancellationToken);

    Task MarkSentAsync(long id, string? providerMessageId, DateTimeOffset sentAt, CancellationToken cancellationToken);

    Task MarkFailedAsync(long id, string errorCode, string? errorMessage, int attemptCount, CancellationToken cancellationToken);
}

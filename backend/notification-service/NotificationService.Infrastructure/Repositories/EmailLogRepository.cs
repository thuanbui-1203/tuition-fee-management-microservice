using Microsoft.EntityFrameworkCore;
using Npgsql;
using NotificationService.Application.Abstractions;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Repositories;

/// <summary>EF Core implementation of the email audit log persistence contract.</summary>
public sealed class EmailLogRepository : IEmailLogRepository
{
    private readonly NotificationDbContext _db;

    public EmailLogRepository(NotificationDbContext db) => _db = db;

    public Task<EmailLog?> FindByMessageIdAsync(string messageId, CancellationToken cancellationToken) =>
        _db.EmailLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MessageId == messageId, cancellationToken);

    public async Task<EmailLog?> TryCreateAsync(EmailLog log, CancellationToken cancellationToken)
    {
        _db.EmailLogs.Add(log);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return log;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return null;
        }
    }

    public async Task MarkSendingAsync(long id, int attemptCount, CancellationToken cancellationToken) =>
        await _db.EmailLogs
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, EmailStatus.Sending)
                .SetProperty(x => x.AttemptCount, attemptCount), cancellationToken);

    public async Task MarkSentAsync(long id, string? providerMessageId, DateTimeOffset sentAt, CancellationToken cancellationToken) =>
        await _db.EmailLogs
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, EmailStatus.Sent)
                .SetProperty(x => x.ProviderMessageId, providerMessageId)
                .SetProperty(x => x.SentAt, sentAt), cancellationToken);

    public async Task MarkFailedAsync(long id, string errorCode, string? errorMessage, int attemptCount, CancellationToken cancellationToken) =>
        await _db.EmailLogs
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, EmailStatus.Failed)
                .SetProperty(x => x.ErrorCode, errorCode)
                .SetProperty(x => x.ErrorMessage, errorMessage)
                .SetProperty(x => x.AttemptCount, attemptCount), cancellationToken);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

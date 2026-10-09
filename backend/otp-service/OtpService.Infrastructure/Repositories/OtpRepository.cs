using Microservices.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OtpService.Application.Abstractions;
using OtpService.Application.Dtos;
using OtpService.Domain.Entities;
using OtpService.Domain.Enums;
using OtpService.Infrastructure.Persistence;

namespace OtpService.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the OTP persistence contract. Issue is a single transaction
/// serialized by a Postgres advisory lock on the transaction id; verify transitions use
/// atomic conditional updates.
/// </summary>
public sealed class OtpRepository : IOtpRepository
{
    private readonly OtpDbContext _db;

    public OtpRepository(OtpDbContext db) => _db = db;

    public Task<OtpCode?> GetLatestForTransactionAsync(Guid transactionId, CancellationToken cancellationToken) =>
        _db.OtpCodes
            .AsNoTracking()
            .Where(o => o.TransactionId == transactionId)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<OtpCode?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        _db.OtpCodes.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IssueResult> IssueAsync(IssueOtpCommand command, CancellationToken cancellationToken)
    {
        var idempotencyKey = string.IsNullOrWhiteSpace(command.IdempotencyKey) ? null : command.IdempotencyKey;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize concurrent issuances for the same transaction.
            // Note: pass parameters as IEnumerable<object> so the CancellationToken binds to the
            // overload's token parameter — otherwise it is interpreted as a SQL parameter.
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))",
                new object[] { command.TransactionId.ToString("D") },
                cancellationToken);

            if (idempotencyKey is not null)
            {
                var existing = await _db.OtpCodes
                    .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, cancellationToken);
                if (existing is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return existing.TransactionId == command.TransactionId
                        ? new IssueReplayed(existing.Id)
                        : new IssueIdempotencyConflict();
                }
            }

            // Any previous active OTP for this transaction is now superseded.
            await _db.OtpCodes
                .Where(o => o.TransactionId == command.TransactionId && o.Status == OtpStatus.Active)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OtpStatus.Expired), cancellationToken);

            var otp = new OtpCode(
                command.TransactionId,
                command.CodeHash,
                command.ExpiresAt,
                command.Now,
                idempotencyKey);
            _db.OtpCodes.Add(otp);
            await _db.SaveChangesAsync(cancellationToken);

            var outbox = new OutboxMessage(
                Guid.NewGuid(),
                EmailEventTypes.OtpRequested,
                RabbitMqTopology.OtpRequestedRoutingKey,
                command.Recipient,
                command.TransactionId,
                string.IsNullOrWhiteSpace(command.CorrelationId) ? null : command.CorrelationId,
                command.EncryptedPayload,
                command.Now);
            _db.OutboxMessages.Add(outbox);
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new IssueCreated(otp.Id);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken);

            var winner = await _db.OtpCodes
                .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, cancellationToken);
            return winner is null
                ? new IssueIdempotencyConflict()
                : winner.TransactionId == command.TransactionId
                    ? new IssueReplayed(winner.Id)
                    : new IssueIdempotencyConflict();
        }
    }

    public async Task<int> MarkUsedAsync(long id, Guid transactionId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await _db.OtpCodes
            .Where(o => o.Id == id
                        && o.TransactionId == transactionId
                        && o.Status == OtpStatus.Active
                        && o.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, OtpStatus.Used)
                .SetProperty(o => o.UsedAt, now), cancellationToken);

    public async Task<int> IncrementAttemptAsync(long id, int maxAttempts, CancellationToken cancellationToken) =>
        await _db.OtpCodes
            .Where(o => o.Id == id && o.Status == OtpStatus.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.AttemptCount, o => o.AttemptCount + 1)
                .SetProperty(o => o.Status, o => o.AttemptCount + 1 >= maxAttempts ? OtpStatus.Locked : o.Status),
                cancellationToken);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

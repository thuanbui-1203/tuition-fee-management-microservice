using OtpService.Application.Dtos;
using OtpService.Domain.Entities;

namespace OtpService.Application.Abstractions;

/// <summary>
/// Persistence contract for OTPs. The <c>IssueAsync</c> operation is atomic: it serializes
/// concurrent issuances for the same transaction, expires any active OTP, inserts the new OTP
/// and writes the outgoing outbox event in a single database transaction.
/// </summary>
public interface IOtpRepository
{
    Task<OtpCode?> GetLatestForTransactionAsync(Guid transactionId, CancellationToken cancellationToken);

    Task<OtpCode?> GetByIdAsync(long id, CancellationToken cancellationToken);

    Task<IssueResult> IssueAsync(IssueOtpCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically marks an ACTIVE, unexpired OTP as USED. Returns the number of rows updated
    /// (1 on success; 0 when another request won or the OTP is no longer usable).
    /// </summary>
    Task<int> MarkUsedAsync(long id, Guid transactionId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically increments the failed-attempt counter, locking the OTP when it reaches
    /// <paramref name="maxAttempts"/>. Returns the number of rows updated.
    /// </summary>
    Task<int> IncrementAttemptAsync(long id, int maxAttempts, CancellationToken cancellationToken);
}

using TuitionService.Domain.Entities;

namespace TuitionService.Application.Abstractions;

/// <summary>
/// Persistence contract for the tuition aggregate. Implementations live in Infrastructure
/// and must perform claim/release with atomic conditional updates, never read-then-write.
/// </summary>
public interface ITuitionRepository
{
    Task<Student?> GetStudentWithFeesAsync(string mssv, CancellationToken cancellationToken);

    Task<TuitionFee?> GetFeeAsync(long feeId, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically transitions a fee from UNPAID to PAID for the given transaction.
    /// Returns the number of rows updated (1 on success, 0 otherwise).
    /// </summary>
    Task<int> ClaimAsync(long feeId, Guid transactionId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically transitions a fee from PAID back to UNPAID, but only when it is currently
    /// claimed by the given transaction. Returns the number of rows updated.
    /// </summary>
    Task<int> ReleaseAsync(long feeId, Guid transactionId, DateTimeOffset now, CancellationToken cancellationToken);
}

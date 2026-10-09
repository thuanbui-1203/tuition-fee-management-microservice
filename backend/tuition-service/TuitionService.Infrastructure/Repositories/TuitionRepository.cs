using Microsoft.EntityFrameworkCore;
using TuitionService.Application.Abstractions;
using TuitionService.Domain.Entities;
using TuitionService.Domain.Enums;
using TuitionService.Infrastructure.Persistence;

namespace TuitionService.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the tuition persistence contract. Claim and release use
/// <c>ExecuteUpdateAsync</c> so the invariant is enforced by a single atomic SQL UPDATE.
/// </summary>
public sealed class TuitionRepository : ITuitionRepository
{
    private readonly TuitionDbContext _db;

    public TuitionRepository(TuitionDbContext db) => _db = db;

    public Task<Student?> GetStudentWithFeesAsync(string mssv, CancellationToken cancellationToken) =>
        _db.Students
            .Include(s => s.TuitionFees)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Mssv == mssv, cancellationToken);

    public Task<TuitionFee?> GetFeeAsync(long feeId, CancellationToken cancellationToken) =>
        _db.TuitionFees
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == feeId, cancellationToken);

    public async Task<int> ClaimAsync(long feeId, Guid transactionId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await _db.TuitionFees
            .Where(f => f.Id == feeId && f.Status == FeeStatus.Unpaid)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(f => f.Status, FeeStatus.Paid)
                .SetProperty(f => f.PaidTransactionId, transactionId)
                .SetProperty(f => f.UpdatedAt, now), cancellationToken);

    public async Task<int> ReleaseAsync(long feeId, Guid transactionId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await _db.TuitionFees
            .Where(f => f.Id == feeId && f.Status == FeeStatus.Paid && f.PaidTransactionId == transactionId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(f => f.Status, FeeStatus.Unpaid)
                .SetProperty(f => f.PaidTransactionId, (Guid?)null)
                .SetProperty(f => f.UpdatedAt, now), cancellationToken);
}

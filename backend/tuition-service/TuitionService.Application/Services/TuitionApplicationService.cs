using Microservices.Common.Errors;
using Microsoft.Extensions.Logging;
using TuitionService.Application.Abstractions;
using TuitionService.Application.Dtos;
using TuitionService.Application.Validation;
using TuitionService.Domain.Enums;

namespace TuitionService.Application.Services;

/// <summary>
/// Coordinates tuition use cases: look up an unpaid fee by MSSV, atomically claim a fee for a
/// transaction, and release it during saga compensation. Business rules live here; the
/// repository guarantees atomicity at the database layer.
/// </summary>
public sealed class TuitionApplicationService
{
    private readonly ITuitionRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TuitionApplicationService> _logger;

    public TuitionApplicationService(ITuitionRepository repository, TimeProvider timeProvider, ILogger<TuitionApplicationService> logger)
    {
        _repository = repository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Looks up a student's single outstanding (UNPAID) tuition fee.</summary>
    public async Task<Result<TuitionLookupResult>> LookupAsync(string? mssv, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mssv))
        {
            return Error.InvalidInput("MSSV is required.");
        }

        if (!MssvValidator.IsValid(mssv))
        {
            return Error.InvalidInput("MSSV has an invalid format.");
        }

        var student = await _repository.GetStudentWithFeesAsync(mssv, cancellationToken);
        if (student is null)
        {
            return Error.NotFound("Student with the given MSSV was not found.");
        }

        var unpaid = student.TuitionFees
            .Where(f => f.Status == FeeStatus.Unpaid)
            .OrderBy(f => f.CreatedAt)
            .FirstOrDefault();

        if (unpaid is not null)
        {
            return new TuitionLookupResult(
                student.Mssv,
                student.FullName,
                new FeeInfo(unpaid.Id, unpaid.Semester, unpaid.Amount, unpaid.Status.ToString().ToUpperInvariant()));
        }

        if (student.TuitionFees.Any(f => f.Status == FeeStatus.Paid))
        {
            return Error.FeeAlreadyPaid();
        }

        return Error.NotFound("No tuition fee found for this student.");
    }

    /// <summary>
    /// Atomically claims an unpaid fee for a transaction. Guarantees exactly one successful
    /// claim per fee. Replaying the same transaction is idempotent; a different transaction
    /// that reaches a PAID fee receives <see cref="ErrorCodes.FeeAlreadyPaid"/>.
    /// </summary>
    public async Task<Result> ClaimAsync(long feeId, Guid transactionId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        // Bounded retries handle the rare interleaving where a fee is released between a
        // failed conditional update and the classification read.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var affected = await _repository.ClaimAsync(feeId, transactionId, now, cancellationToken);
            if (affected == 1)
            {
                _logger.LogInformation("Fee {FeeId} claimed by transaction {TransactionId}.", feeId, transactionId);
                return Result.Ok();
            }

            var fee = await _repository.GetFeeAsync(feeId, cancellationToken);
            if (fee is null)
            {
                return Error.NotFound("Tuition fee not found.");
            }

            if (fee.Status == FeeStatus.Paid && fee.PaidTransactionId == transactionId)
            {
                _logger.LogInformation("Fee {FeeId} already claimed by {TransactionId} (idempotent replay).", feeId, transactionId);
                return Result.Ok();
            }

            if (fee.Status == FeeStatus.Paid)
            {
                _logger.LogInformation("Fee {FeeId} already paid by another transaction; claim rejected for {TransactionId}.", feeId, transactionId);
                return Error.FeeAlreadyPaid();
            }

            // Fee is UNPAID again (released concurrently) — retry the atomic claim.
        }

        _logger.LogWarning("Fee {FeeId} could not be claimed by {TransactionId} after retries.", feeId, transactionId);
        return Error.FeeAlreadyPaid();
    }

    /// <summary>
    /// Releases a fee during saga compensation. Only succeeds when the fee is currently held
    /// by the releasing transaction. Releasing an already-UNPAID fee is an idempotent no-op.
    /// </summary>
    public async Task<Result> ReleaseAsync(long feeId, Guid transactionId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var affected = await _repository.ReleaseAsync(feeId, transactionId, now, cancellationToken);

        if (affected == 1)
        {
            _logger.LogInformation("Fee {FeeId} released by transaction {TransactionId}.", feeId, transactionId);
            return Result.Ok();
        }

        var fee = await _repository.GetFeeAsync(feeId, cancellationToken);
        if (fee is null)
        {
            return Error.NotFound("Tuition fee not found.");
        }

        if (fee.Status == FeeStatus.Unpaid)
        {
            _logger.LogInformation("Fee {FeeId} already unpaid; release treated as idempotent no-op.", feeId);
            return Result.Ok();
        }

        _logger.LogWarning("Fee {FeeId} is paid by {PaidTransactionId}; release by {TransactionId} rejected.", feeId, fee.PaidTransactionId, transactionId);
        return Error.ReleaseConflict();
    }
}

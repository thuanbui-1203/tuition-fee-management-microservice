using TuitionService.Domain.Enums;

namespace TuitionService.Domain.Entities;

/// <summary>
/// A tuition fee owed by a student. The <c>UNPAID → PAID</c> transition is protected by an
/// atomic conditional database update (see the claim use case); it must never be performed
/// with a read-then-write sequence.
/// </summary>
public sealed class TuitionFee
{
    private TuitionFee()
    {
    }

    public TuitionFee(long studentId, string semester, decimal amount, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(semester))
        {
            throw new ArgumentException("Semester is required.", nameof(semester));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Tuition amount must be greater than zero.");
        }

        StudentId = studentId;
        Semester = semester;
        Amount = amount;
        Status = FeeStatus.Unpaid;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>Reconstitution constructor for persistence and test fixtures.</summary>
    internal TuitionFee(long id, long studentId, string semester, decimal amount, FeeStatus status, Guid? paidTransactionId, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        StudentId = studentId;
        Semester = semester;
        Amount = amount;
        Status = status;
        PaidTransactionId = paidTransactionId;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public long Id { get; private set; }
    public long StudentId { get; private set; }
    public string Semester { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public FeeStatus Status { get; private set; }

    /// <summary>Logical reference to the payment-service transaction that paid this fee.</summary>
    public Guid? PaidTransactionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Student? Student { get; private set; }
}

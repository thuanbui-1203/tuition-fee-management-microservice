using FluentAssertions;
using Microservices.Common.Errors;
using Microsoft.Extensions.Logging.Abstractions;
using TuitionService.Application.Abstractions;
using TuitionService.Application.Services;
using TuitionService.Domain.Entities;
using TuitionService.Domain.Enums;

namespace TuitionService.UnitTests;

[Trait("Category", "Unit")]
public class TuitionApplicationServiceTests
{
    private readonly InMemoryTuitionRepository _repository = new();
    private readonly TuitionApplicationService _service;

    public TuitionApplicationServiceTests()
    {
        _service = new TuitionApplicationService(_repository, TimeProvider.System, NullLogger<TuitionApplicationService>.Instance);
    }

    // ---------- Lookup ----------

    [Fact]
    public async Task Lookup_EmptyMssv_ReturnsInvalidInput()
    {
        var result = await _service.LookupAsync("", CancellationToken.None);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.InvalidInput);
    }

    [Fact]
    public async Task Lookup_InvalidFormat_ReturnsInvalidInput()
    {
        var result = await _service.LookupAsync("52!@", CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.InvalidInput);
    }

    [Fact]
    public async Task Lookup_StudentNotFound_ReturnsNotFound()
    {
        var result = await _service.LookupAsync("521H9999", CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Lookup_StudentWithoutFees_ReturnsNotFound()
    {
        _repository.Seed(Student("521H0001"));
        var result = await _service.LookupAsync("521H0001", CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Lookup_UnpaidFee_ReturnsFee()
    {
        _repository.Seed(Student("521H0001", Fee(10, 1, FeeStatus.Unpaid)));
        var result = await _service.LookupAsync("521H0001", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Fee.FeeId.Should().Be(10);
        result.Value.Fee.Status.Should().Be("UNPAID");
    }

    [Fact]
    public async Task Lookup_AlreadyPaid_ReturnsFeeAlreadyPaid()
    {
        _repository.Seed(Student("521H0001", Fee(10, 1, FeeStatus.Paid, Guid.NewGuid())));
        var result = await _service.LookupAsync("521H0001", CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.FeeAlreadyPaid);
    }

    [Fact]
    public async Task Lookup_MultipleUnpaid_ReturnsOldest()
    {
        var older = Fee(10, 1, FeeStatus.Unpaid, createdAt: DateTimeOffset.UtcNow.AddDays(-5));
        var newer = Fee(11, 1, FeeStatus.Unpaid, createdAt: DateTimeOffset.UtcNow);
        _repository.Seed(Student("521H0001", older, newer));

        var result = await _service.LookupAsync("521H0001", CancellationToken.None);
        result.Value.Fee.FeeId.Should().Be(10);
    }

    // ---------- Claim ----------

    [Fact]
    public async Task Claim_Success()
    {
        _repository.Seed(Student("521H0001", Fee(10, 1, FeeStatus.Unpaid)));
        var txn = Guid.NewGuid();

        var result = await _service.ClaimAsync(10, txn, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var fee = await _repository.GetFeeAsync(10, CancellationToken.None);
        fee!.Status.Should().Be(FeeStatus.Paid);
        fee.PaidTransactionId.Should().Be(txn);
    }

    [Fact]
    public async Task Claim_FeeNotFound_ReturnsNotFound()
    {
        var result = await _service.ClaimAsync(404, Guid.NewGuid(), CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Claim_AlreadyPaidByOther_ReturnsFeeAlreadyPaid()
    {
        var other = Guid.NewGuid();
        _repository.Seed(Student("521H0001", Fee(10, 1, FeeStatus.Paid, other)));

        var result = await _service.ClaimAsync(10, Guid.NewGuid(), CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.FeeAlreadyPaid);
    }

    [Fact]
    public async Task Claim_AlreadyPaidBySameTransaction_IsIdempotent()
    {
        var txn = Guid.NewGuid();
        _repository.Seed(Student("521H0001", Fee(10, 1, FeeStatus.Paid, txn)));

        var result = await _service.ClaimAsync(10, txn, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
    }

    // ---------- Release ----------

    [Fact]
    public async Task Release_Success()
    {
        var txn = Guid.NewGuid();
        _repository.Seed(Student("521H0001", Fee(10, 1, FeeStatus.Paid, txn)));

        var result = await _service.ReleaseAsync(10, txn, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var fee = await _repository.GetFeeAsync(10, CancellationToken.None);
        fee!.Status.Should().Be(FeeStatus.Unpaid);
        fee.PaidTransactionId.Should().BeNull();
    }

    [Fact]
    public async Task Release_NeverClaimed_IsIdempotentNoOp()
    {
        _repository.Seed(Student("521H0001", Fee(10, 1, FeeStatus.Unpaid)));
        var result = await _service.ReleaseAsync(10, Guid.NewGuid(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Release_WrongTransaction_ReturnsReleaseConflict()
    {
        var owner = Guid.NewGuid();
        _repository.Seed(Student("521H0001", Fee(10, 1, FeeStatus.Paid, owner)));

        var result = await _service.ReleaseAsync(10, Guid.NewGuid(), CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.ReleaseConflict);
    }

    [Fact]
    public async Task Release_FeeNotFound_ReturnsNotFound()
    {
        var result = await _service.ReleaseAsync(404, Guid.NewGuid(), CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.NotFound);
    }

    // ---------- helpers ----------

    private static Student Student(string mssv, params TuitionFee[] fees)
    {
        var student = new Student(mssv, "Test Student", "CLASS", "Faculty", DateTimeOffset.UtcNow);
        foreach (var fee in fees)
        {
            student.AddFee(fee);
        }

        return student;
    }

    private static TuitionFee Fee(long id, long studentId, FeeStatus status, Guid? transactionId = null, DateTimeOffset? createdAt = null) =>
        new(id, studentId, "2024-2025/HK1", 8_400_000m, status, transactionId, createdAt ?? DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private sealed class InMemoryTuitionRepository : ITuitionRepository
    {
        private readonly Dictionary<string, Student> _students = new();
        private readonly Dictionary<long, TuitionFee> _fees = new();

        public void Seed(Student student)
        {
            _students[student.Mssv] = student;
            foreach (var fee in student.TuitionFees)
            {
                _fees[fee.Id] = fee;
            }
        }

        public Task<Student?> GetStudentWithFeesAsync(string mssv, CancellationToken ct) =>
            Task.FromResult(_students.TryGetValue(mssv, out var s) ? s : null);

        public Task<TuitionFee?> GetFeeAsync(long feeId, CancellationToken ct) =>
            Task.FromResult(_fees.TryGetValue(feeId, out var f) ? f : null);

        public Task<int> ClaimAsync(long feeId, Guid transactionId, DateTimeOffset now, CancellationToken ct)
        {
            if (!_fees.TryGetValue(feeId, out var fee) || fee.Status != FeeStatus.Unpaid)
            {
                return Task.FromResult(0);
            }

            _fees[feeId] = new TuitionFee(fee.Id, fee.StudentId, fee.Semester, fee.Amount, FeeStatus.Paid, transactionId, fee.CreatedAt, now);
            return Task.FromResult(1);
        }

        public Task<int> ReleaseAsync(long feeId, Guid transactionId, DateTimeOffset now, CancellationToken ct)
        {
            if (!_fees.TryGetValue(feeId, out var fee)
                || fee.Status != FeeStatus.Paid
                || fee.PaidTransactionId != transactionId)
            {
                return Task.FromResult(0);
            }

            _fees[feeId] = new TuitionFee(fee.Id, fee.StudentId, fee.Semester, fee.Amount, FeeStatus.Unpaid, null, fee.CreatedAt, now);
            return Task.FromResult(1);
        }
    }
}

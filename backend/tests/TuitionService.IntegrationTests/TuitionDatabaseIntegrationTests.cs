using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TuitionService.Application.Services;
using TuitionService.Domain.Entities;
using TuitionService.Domain.Enums;
using TuitionService.Infrastructure.Persistence;
using TuitionService.Infrastructure.Repositories;

namespace TuitionService.IntegrationTests;

/// <summary>
/// Integration tests against a real PostgreSQL instance: migrations, database constraints,
/// claim/release semantics, and the critical concurrent-claim guarantee.
/// </summary>
[Trait("Category", "Integration")]
public class TuitionDatabaseIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly DbContextOptions<TuitionDbContext> _options;

    public TuitionDatabaseIntegrationTests(PostgresFixture fixture)
    {
        _options = new DbContextOptionsBuilder<TuitionDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        using var db = new TuitionDbContext(_options);
        db.Database.Migrate();
    }

    private TuitionDbContext CreateContext() => new(_options);

    private TuitionApplicationService CreateService() =>
        new(new TuitionRepository(new TuitionDbContext(_options)), TimeProvider.System, NullLogger<TuitionApplicationService>.Instance);

    private long SeedUnpaidFee() => SeedUnpaidFee($"5{Guid.NewGuid():N}"[..8]);

    private long SeedUnpaidFee(string mssv)
    {
        using var db = new TuitionDbContext(_options);
        var student = new Student(mssv, "Trần Thị B", "CLASS", "FAC", DateTimeOffset.UtcNow);
        db.Students.Add(student);
        db.SaveChanges();
        var fee = new TuitionFee(student.Id, "2024-2025/HK1", 8_400_000m, DateTimeOffset.UtcNow);
        db.TuitionFees.Add(fee);
        db.SaveChanges();
        return fee.Id;
    }

    [Fact]
    public void NegativeAmount_IsRejectedByDatabase()
    {
        using var db = CreateContext();
        var student = new Student("521H1001", "X", null, null, DateTimeOffset.UtcNow);
        db.Students.Add(student);
        db.SaveChanges();

        var act = () => db.Database.ExecuteSqlRaw(
            "INSERT INTO tuition_fees (student_id, semester, amount, status, created_at, updated_at) VALUES ({0}, '2024-2025/HK1', -100, 'UNPAID', now(), now())",
            student.Id);

        act.Should().Throw<PostgresException>()
            .Where(ex => ex.SqlState == PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public void DuplicateMssv_IsRejectedByDatabase()
    {
        using var db = CreateContext();
        db.Database.ExecuteSqlRaw("INSERT INTO students (mssv, full_name, created_at, updated_at) VALUES ('521H2001', 'A', now(), now())");

        var act = () => db.Database.ExecuteSqlRaw("INSERT INTO students (mssv, full_name, created_at, updated_at) VALUES ('521H2001', 'B', now(), now())");

        act.Should().Throw<PostgresException>()
            .Where(ex => ex.SqlState == PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task ConcurrentClaim_ExactlyOneSucceeds()
    {
        var feeId = SeedUnpaidFee();
        var transactions = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();

        var results = await Task.WhenAll(transactions.Select(transactionId => Task.Run(async () =>
        {
            using var db = new TuitionDbContext(_options);
            var service = new TuitionApplicationService(new TuitionRepository(db), TimeProvider.System, NullLogger<TuitionApplicationService>.Instance);
            return await service.ClaimAsync(feeId, transactionId, CancellationToken.None);
        })));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Count(r => r.IsFailure).Should().Be(9);

        using var check = new TuitionDbContext(_options);
        var fee = check.TuitionFees.Single(f => f.Id == feeId);
        fee.Status.Should().Be(FeeStatus.Paid);
        fee.PaidTransactionId.Should().NotBeNull();
    }

    [Fact]
    public async Task Claim_ThenReclaimByOtherTransaction_ReturnsFeeAlreadyPaid()
    {
        var feeId = SeedUnpaidFee();
        var first = await CreateService().ClaimAsync(feeId, Guid.NewGuid(), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        var second = await CreateService().ClaimAsync(feeId, Guid.NewGuid(), CancellationToken.None);
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be(Microservices.Common.Errors.ErrorCodes.FeeAlreadyPaid);
    }

    [Fact]
    public async Task Claim_SameTransactionReplay_IsIdempotent()
    {
        var feeId = SeedUnpaidFee();
        var txn = Guid.NewGuid();

        var first = await CreateService().ClaimAsync(feeId, txn, CancellationToken.None);
        var replay = await CreateService().ClaimAsync(feeId, txn, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        replay.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Release_WrongTransaction_ReturnsReleaseConflict()
    {
        var feeId = SeedUnpaidFee();
        var owner = Guid.NewGuid();
        await CreateService().ClaimAsync(feeId, owner, CancellationToken.None);

        var result = await CreateService().ReleaseAsync(feeId, Guid.NewGuid(), CancellationToken.None);

        result.Error.Code.Should().Be(Microservices.Common.Errors.ErrorCodes.ReleaseConflict);
    }

    [Fact]
    public async Task Release_ByOwner_ReturnsFeeToUnpaid()
    {
        var feeId = SeedUnpaidFee();
        var owner = Guid.NewGuid();
        await CreateService().ClaimAsync(feeId, owner, CancellationToken.None);

        var result = await CreateService().ReleaseAsync(feeId, owner, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        using var db = new TuitionDbContext(_options);
        db.TuitionFees.Single(f => f.Id == feeId).Status.Should().Be(FeeStatus.Unpaid);
    }

    [Fact]
    public async Task Lookup_ReturnsUnpaidFee_AndAlreadyPaidAfterClaim()
    {
        // Use an MSSV that is not present in the SeedDemoData migration (which seeds 521H0001..3).
        var mssv = $"LKP{Guid.NewGuid():N}"[..10];
        var feeId = SeedUnpaidFee(mssv);
        var service = CreateService();

        var lookup = await service.LookupAsync(mssv, CancellationToken.None);
        lookup.IsSuccess.Should().BeTrue();
        lookup.Value.Fee.FeeId.Should().Be(feeId);

        await service.ClaimAsync(feeId, Guid.NewGuid(), CancellationToken.None);

        var after = await CreateService().LookupAsync(mssv, CancellationToken.None);
        after.Error.Code.Should().Be(Microservices.Common.Errors.ErrorCodes.FeeAlreadyPaid);
    }
}

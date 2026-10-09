using System.Text.Json;
using FluentAssertions;
using Microservices.Common.RateLimiting;
using Microservices.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OtpService.Application.Options;
using OtpService.Application.Security;
using OtpService.Application.Services;
using OtpService.Domain.Entities;
using OtpService.Domain.Enums;
using OtpService.Infrastructure.Persistence;
using OtpService.Infrastructure.Repositories;

namespace OtpService.IntegrationTests;

/// <summary>
/// Integration tests against a real PostgreSQL instance: migrations, the one-active-OTP
/// invariant, atomic mark-used, and concurrent verification/issuance.
/// </summary>
[Trait("Category", "Integration")]
public class OtpDatabaseIntegrationTests : IClassFixture<PostgresFixture>
{
    private const string TestKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    private readonly DbContextOptions<OtpDbContext> _options;
    private readonly Pbkdf2OtpHasher _hasher = new();

    public OtpDatabaseIntegrationTests(PostgresFixture fixture)
    {
        _options = new DbContextOptionsBuilder<OtpDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        using var db = new OtpDbContext(_options);
        db.Database.Migrate();
    }

    private OtpApplicationService CreateService()
    {
        var rateOptions = new RateLimitingOptions
        {
            IssueLimitPerTransaction = 1000,
            IssueWindowSeconds = 60,
            VerifyLimitPerTransaction = 1000,
            VerifyWindowSeconds = 60
        };

        return new OtpApplicationService(
            new OtpRepository(new OtpDbContext(_options)),
            new CryptoSecureOtpGenerator(),
            _hasher,
            new FixedWindowRateLimiter(),
            TimeProvider.System,
            Options.Create(new OtpOptions { Length = 6, TtlSeconds = 300, MaxAttempts = 5 }),
            Options.Create(rateOptions),
            Options.Create(new EventEncryptionOptions { Key = TestKey }),
            NullLogger<OtpApplicationService>.Instance);
    }

    private long SeedActiveOtp(Guid transactionId, string code = "482913")
    {
        using var db = new OtpDbContext(_options);
        var otp = new OtpCode(0, transactionId, _hasher.Hash(code), DateTimeOffset.UtcNow.AddMinutes(5), OtpStatus.Active, 0, DateTimeOffset.UtcNow, null, null);
        db.OtpCodes.Add(otp);
        db.SaveChanges();
        return otp.Id;
    }

    [Fact]
    public async Task Verify_CorrectCode_MarksUsed()
    {
        var txn = Guid.NewGuid();
        var id = SeedActiveOtp(txn);

        var result = await CreateService().VerifyAsync(txn, "482913", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        using var db = new OtpDbContext(_options);
        var otp = db.OtpCodes.Single(o => o.Id == id);
        otp.Status.Should().Be(OtpStatus.Used);
        otp.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ConcurrentVerify_ExactlyOneSucceeds()
    {
        var txn = Guid.NewGuid();
        SeedActiveOtp(txn);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => CreateService().VerifyAsync(txn, "482913", CancellationToken.None))));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Count(r => r.IsFailure && r.Error.Code == Microservices.Common.Errors.ErrorCodes.OtpAlreadyUsed).Should().Be(9);
    }

    [Fact]
    public async Task ConcurrentIssue_LeavesExactlyOneActiveOtp()
    {
        var txn = Guid.NewGuid();
        var service = CreateService();

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(i =>
            Task.Run(() => service.IssueAsync(txn, "a@example.com", $"key-{i}", null, CancellationToken.None))));

        results.All(r => r.IsSuccess).Should().BeTrue();

        using var db = new OtpDbContext(_options);
        var activeCount = db.OtpCodes.Count(o => o.TransactionId == txn && o.Status == OtpStatus.Active);
        activeCount.Should().Be(1);
    }

    [Fact]
    public async Task Issue_CreatesOutboxWithEncryptablePayload()
    {
        var txn = Guid.NewGuid();
        var result = await CreateService().IssueAsync(txn, "a@example.com", "key-1", "corr-1", CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        using var db = new OtpDbContext(_options);
        var outbox = db.OutboxMessages.Single(m => m.TransactionId == txn);
        outbox.EventType.Should().Be(Microservices.Common.Messaging.EmailEventTypes.OtpRequested);
        outbox.Status.Should().Be(OutboxStatus.Pending);

        using var doc = JsonDocument.Parse(outbox.Payload);
        var encrypted = doc.RootElement.GetProperty("otp").GetString()!;
        var decrypted = AesGcmCrypto.DecryptString(encrypted, Convert.FromBase64String(TestKey));
        decrypted.Should().MatchRegex("^[0-9]{6}$");
    }
}

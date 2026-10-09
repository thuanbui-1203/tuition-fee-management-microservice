using FluentAssertions;
using Microservices.Common.Errors;
using Microservices.Common.RateLimiting;
using Microservices.Common.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OtpService.Application.Abstractions;
using OtpService.Application.Dtos;
using OtpService.Application.Options;
using OtpService.Application.Security;
using OtpService.Application.Services;
using OtpService.Domain.Entities;
using OtpService.Domain.Enums;

namespace OtpService.UnitTests;

[Trait("Category", "Unit")]
public class OtpApplicationServiceTests
{
    private const string TestKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
    private readonly Pbkdf2OtpHasher _hasher = new();

    private OtpApplicationService CreateService(
        InMemoryOtpRepository repository,
        IRateLimiter? rateLimiter = null,
        OtpOptions? options = null,
        RateLimitingOptions? rateOptions = null)
    {
        return new OtpApplicationService(
            repository,
            new CryptoSecureOtpGenerator(),
            _hasher,
            rateLimiter ?? new FixedWindowRateLimiter(),
            TimeProvider.System,
            Options.Create(options ?? new OtpOptions { Length = 6, TtlSeconds = 300, MaxAttempts = 5 }),
            Options.Create(rateOptions ?? new RateLimitingOptions
            {
                IssueLimitPerTransaction = 1000,
                IssueWindowSeconds = 60,
                VerifyLimitPerTransaction = 1000,
                VerifyWindowSeconds = 60
            }),
            Options.Create(new EventEncryptionOptions { Key = TestKey }),
            NullLogger<OtpApplicationService>.Instance);
    }

    // ---------- Verify ----------

    [Fact]
    public async Task Verify_CorrectOtp_SucceedsAndMarksUsed()
    {
        var repo = new InMemoryOtpRepository();
        var txn = Guid.NewGuid();
        repo.Seed(ActiveOtp(1, txn, _hasher.Hash("482913")));

        var result = await CreateService(repo).VerifyAsync(txn, "482913", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repo.Get(1)!.Status.Should().Be(OtpStatus.Used);
        repo.Get(1)!.UsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Verify_IncorrectOtp_ReturnsOtpIncorrect()
    {
        var repo = new InMemoryOtpRepository();
        var txn = Guid.NewGuid();
        repo.Seed(ActiveOtp(1, txn, _hasher.Hash("482913")));

        var result = await CreateService(repo).VerifyAsync(txn, "999999", CancellationToken.None);

        result.Error.Code.Should().Be(ErrorCodes.OtpIncorrect);
    }

    [Fact]
    public async Task Verify_ExpiredOtp_ReturnsOtpExpired()
    {
        var repo = new InMemoryOtpRepository();
        var txn = Guid.NewGuid();
        repo.Seed(ActiveOtp(1, txn, _hasher.Hash("482913"), expiresAt: DateTimeOffset.UtcNow.AddSeconds(-1)));

        var result = await CreateService(repo).VerifyAsync(txn, "482913", CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.OtpExpired);
    }

    [Fact]
    public async Task Verify_UsedOtp_ReturnsOtpAlreadyUsed()
    {
        var repo = new InMemoryOtpRepository();
        var txn = Guid.NewGuid();
        repo.Seed(new OtpCode(1, txn, _hasher.Hash("482913"), DateTimeOffset.UtcNow.AddMinutes(5), OtpStatus.Used, 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));

        var result = await CreateService(repo).VerifyAsync(txn, "482913", CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.OtpAlreadyUsed);
    }

    [Fact]
    public async Task Verify_LockedOtp_ReturnsOtpLocked()
    {
        var repo = new InMemoryOtpRepository();
        var txn = Guid.NewGuid();
        repo.Seed(new OtpCode(1, txn, _hasher.Hash("482913"), DateTimeOffset.UtcNow.AddMinutes(5), OtpStatus.Locked, 5, DateTimeOffset.UtcNow, null, null));

        var result = await CreateService(repo).VerifyAsync(txn, "482913", CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.OtpLocked);
    }

    [Fact]
    public async Task Verify_OtpFromAnotherTransaction_ReturnsGenericNotFound()
    {
        var repo = new InMemoryOtpRepository();
        var txnA = Guid.NewGuid();
        repo.Seed(ActiveOtp(1, txnA, _hasher.Hash("482913")));

        var result = await CreateService(repo).VerifyAsync(Guid.NewGuid(), "482913", CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Verify_FifthFailedAttempt_LocksOtp()
    {
        var repo = new InMemoryOtpRepository();
        var txn = Guid.NewGuid();
        repo.Seed(new OtpCode(1, txn, _hasher.Hash("482913"), DateTimeOffset.UtcNow.AddMinutes(5), OtpStatus.Active, 4, DateTimeOffset.UtcNow, null, null));

        var result = await CreateService(repo).VerifyAsync(txn, "000000", CancellationToken.None);

        result.Error.Code.Should().Be(ErrorCodes.OtpLocked);
        repo.Get(1)!.Status.Should().Be(OtpStatus.Locked);
    }

    [Fact]
    public async Task Verify_RateLimited_ReturnsRateLimited()
    {
        var repo = new InMemoryOtpRepository();
        var txn = Guid.NewGuid();
        repo.Seed(ActiveOtp(1, txn, _hasher.Hash("482913")));

        var service = CreateService(repo, new FixedWindowRateLimiter(), rateOptions: new RateLimitingOptions
        {
            IssueLimitPerTransaction = 1000,
            IssueWindowSeconds = 60,
            VerifyLimitPerTransaction = 1,
            VerifyWindowSeconds = 60
        });

        await service.VerifyAsync(txn, "999999", CancellationToken.None);
        var second = await service.VerifyAsync(txn, "999999", CancellationToken.None);
        second.Error.Code.Should().Be(ErrorCodes.RateLimited);
    }

    [Fact]
    public async Task Verify_MissingOtp_ReturnsInvalidInput()
    {
        var result = await CreateService(new InMemoryOtpRepository()).VerifyAsync(Guid.NewGuid(), null, CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.InvalidInput);
    }

    // ---------- Issue ----------

    [Fact]
    public async Task Issue_Success_ReturnsCreatedOutcome()
    {
        var repo = new InMemoryOtpRepository();
        var txn = Guid.NewGuid();

        var result = await CreateService(repo).IssueAsync(txn, "a@example.com", "key-1", "corr-1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Replayed.Should().BeFalse();
        repo.Count.Should().Be(1);
    }

    [Fact]
    public async Task Issue_SameKeySameTransaction_IsIdempotentReplay()
    {
        var repo = new InMemoryOtpRepository();
        var txn = Guid.NewGuid();
        var service = CreateService(repo);

        var first = await service.IssueAsync(txn, "a@example.com", "key-1", null, CancellationToken.None);
        var second = await service.IssueAsync(txn, "a@example.com", "key-1", null, CancellationToken.None);

        second.Value.Replayed.Should().BeTrue();
        second.Value.OtpId.Should().Be(first.Value.OtpId);
        repo.Count.Should().Be(1);
    }

    [Fact]
    public async Task Issue_SameKeyDifferentTransaction_ReturnsConflict()
    {
        var repo = new InMemoryOtpRepository();
        var service = CreateService(repo);

        await service.IssueAsync(Guid.NewGuid(), "a@example.com", "key-1", null, CancellationToken.None);
        var second = await service.IssueAsync(Guid.NewGuid(), "a@example.com", "key-1", null, CancellationToken.None);

        second.Error.Code.Should().Be(ErrorCodes.IdempotencyConflict);
    }

    [Fact]
    public async Task Issue_InvalidEmail_ReturnsInvalidInput()
    {
        var result = await CreateService(new InMemoryOtpRepository()).IssueAsync(Guid.NewGuid(), "not-an-email", "k", null, CancellationToken.None);
        result.Error.Code.Should().Be(ErrorCodes.InvalidInput);
    }

    private OtpCode ActiveOtp(long id, Guid txn, string hash, DateTimeOffset? expiresAt = null) =>
        new(id, txn, hash, expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(5), OtpStatus.Active, 0, DateTimeOffset.UtcNow, null, null);

    private sealed class InMemoryOtpRepository : IOtpRepository
    {
        private readonly List<OtpCode> _codes = new();
        private long _nextId = 100;

        public int Count => _codes.Count;

        public void Seed(OtpCode code) => _codes.Add(code);

        public OtpCode? Get(long id) => _codes.FirstOrDefault(c => c.Id == id);

        public Task<OtpCode?> GetLatestForTransactionAsync(Guid transactionId, CancellationToken ct) =>
            Task.FromResult(_codes.Where(c => c.TransactionId == transactionId).OrderByDescending(c => c.CreatedAt).FirstOrDefault());

        public Task<OtpCode?> GetByIdAsync(long id, CancellationToken ct) =>
            Task.FromResult(_codes.FirstOrDefault(c => c.Id == id));

        public Task<int> MarkUsedAsync(long id, Guid transactionId, DateTimeOffset now, CancellationToken ct)
        {
            var index = _codes.FindIndex(c => c.Id == id && c.TransactionId == transactionId && c.Status == OtpStatus.Active && c.ExpiresAt > now);
            if (index < 0)
            {
                return Task.FromResult(0);
            }

            var c = _codes[index];
            _codes[index] = new OtpCode(c.Id, c.TransactionId, c.CodeHash, c.ExpiresAt, OtpStatus.Used, c.AttemptCount, c.CreatedAt, now, c.IdempotencyKey);
            return Task.FromResult(1);
        }

        public Task<int> IncrementAttemptAsync(long id, int maxAttempts, CancellationToken ct)
        {
            var index = _codes.FindIndex(c => c.Id == id && c.Status == OtpStatus.Active);
            if (index < 0)
            {
                return Task.FromResult(0);
            }

            var c = _codes[index];
            var count = c.AttemptCount + 1;
            var status = count >= maxAttempts ? OtpStatus.Locked : OtpStatus.Active;
            _codes[index] = new OtpCode(c.Id, c.TransactionId, c.CodeHash, c.ExpiresAt, status, count, c.CreatedAt, c.UsedAt, c.IdempotencyKey);
            return Task.FromResult(1);
        }

        public Task<IssueResult> IssueAsync(IssueOtpCommand command, CancellationToken ct)
        {
            var existing = _codes.FirstOrDefault(c => c.IdempotencyKey == command.IdempotencyKey);
            if (existing is not null)
            {
                return Task.FromResult<IssueResult>(existing.TransactionId == command.TransactionId
                    ? new IssueReplayed(existing.Id)
                    : new IssueIdempotencyConflict());
            }

            for (var i = 0; i < _codes.Count; i++)
            {
                if (_codes[i].TransactionId == command.TransactionId && _codes[i].Status == OtpStatus.Active)
                {
                    var c = _codes[i];
                    _codes[i] = new OtpCode(c.Id, c.TransactionId, c.CodeHash, c.ExpiresAt, OtpStatus.Expired, c.AttemptCount, c.CreatedAt, c.UsedAt, c.IdempotencyKey);
                }
            }

            var id = _nextId++;
            _codes.Add(new OtpCode(id, command.TransactionId, command.CodeHash, command.ExpiresAt, OtpStatus.Active, 0, command.Now, null, command.IdempotencyKey));
            return Task.FromResult<IssueResult>(new IssueCreated(id));
        }
    }
}

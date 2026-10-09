using System.Text.Json;
using Microservices.Common.Errors;
using Microservices.Common.Json;
using Microservices.Common.RateLimiting;
using Microservices.Common.Security;
using Microservices.Common.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtpService.Application.Abstractions;
using OtpService.Application.Dtos;
using OtpService.Application.Options;
using OtpService.Application.Security;
using OtpService.Application.Validation;
using OtpService.Domain.Enums;

namespace OtpService.Application.Services;

/// <summary>
/// OTP lifecycle use cases: issuance (bound to a transaction, rate-limited, idempotent) and
/// verification (one-time use, attempt-limited, transaction-bound). Never logs OTP values,
/// hashes or any secret material.
/// </summary>
public sealed class OtpApplicationService
{
    private readonly IOtpRepository _repository;
    private readonly IOtpGenerator _generator;
    private readonly IOtpHasher _hasher;
    private readonly IRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;
    private readonly OtpOptions _options;
    private readonly RateLimitingOptions _rateLimitOptions;
    private readonly EventEncryptionOptions _encryptionOptions;
    private readonly ILogger<OtpApplicationService> _logger;

    public OtpApplicationService(
        IOtpRepository repository,
        IOtpGenerator generator,
        IOtpHasher hasher,
        IRateLimiter rateLimiter,
        TimeProvider timeProvider,
        IOptions<OtpOptions> options,
        IOptions<RateLimitingOptions> rateLimitOptions,
        IOptions<EventEncryptionOptions> encryptionOptions,
        ILogger<OtpApplicationService> logger)
    {
        _repository = repository;
        _generator = generator;
        _hasher = hasher;
        _rateLimiter = rateLimiter;
        _timeProvider = timeProvider;
        _options = options.Value;
        _rateLimitOptions = rateLimitOptions.Value;
        _encryptionOptions = encryptionOptions.Value;
        _logger = logger;
    }

    /// <summary>Issues a new OTP for a transaction and enqueues the email via the outbox.</summary>
    public async Task<Result<OtpIssueOutcome>> IssueAsync(
        Guid transactionId,
        string? email,
        string? idempotencyKey,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (transactionId == Guid.Empty)
        {
            return Error.InvalidInput("transactionId is required.");
        }

        if (!EmailValidator.IsValid(email))
        {
            return Error.InvalidInput("A valid recipient email is required.");
        }

        if (!_rateLimiter.IsAllowed($"otp:issue:{transactionId}", _rateLimitOptions.IssueLimitPerTransaction, _rateLimitOptions.IssueWindow))
        {
            return Error.RateLimited("Too many OTP issuance requests. Please try again later.");
        }

        var code = _generator.Generate(_options.Length);
        var codeHash = _hasher.Hash(code);
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddSeconds(_options.TtlSeconds);

        // The plaintext OTP is only ever placed inside the outbox event, encrypted with
        // AES-256-GCM so the notification service (which holds the shared key) can decrypt it.
        var encryptedCode = AesGcmCrypto.EncryptToString(code, _encryptionOptions.KeyBytes);
        var payload = JsonSerializer.Serialize(
            new { otp = encryptedCode, expiresInSeconds = _options.TtlSeconds },
            JsonDefaults.Options);

        var command = new IssueOtpCommand(
            transactionId,
            idempotencyKey,
            codeHash,
            now,
            expiresAt,
            email!,
            correlationId ?? string.Empty,
            payload);

        var result = await _repository.IssueAsync(command, cancellationToken);

        return result switch
        {
            IssueCreated created => new OtpIssueOutcome(created.OtpId, false),
            IssueReplayed replayed => new OtpIssueOutcome(replayed.OtpId, true),
            IssueIdempotencyConflict => Error.IdempotencyConflict(),
            _ => Error.InternalError()
        };
    }

    /// <summary>Verifies an OTP against its transaction using a safe, generic error contract.</summary>
    public async Task<Result<OtpVerificationResult>> VerifyAsync(Guid transactionId, string? otp, CancellationToken cancellationToken)
    {
        if (transactionId == Guid.Empty)
        {
            return Error.InvalidInput("transactionId is required.");
        }

        if (!OtpValidator.IsValid(otp, _options.Length))
        {
            return Error.InvalidInput("OTP is required and must be a numeric code.");
        }

        if (!_rateLimiter.IsAllowed($"otp:verify:{transactionId}", _rateLimitOptions.VerifyLimitPerTransaction, _rateLimitOptions.VerifyWindow))
        {
            return Error.RateLimited("Too many verification attempts. Please try again later.");
        }

        var record = await _repository.GetLatestForTransactionAsync(transactionId, cancellationToken);
        if (record is null)
        {
            // Generic message — do not reveal whether an OTP exists for another transaction.
            return Error.NotFound("OTP not found or invalid.");
        }

        switch (record.Status)
        {
            case OtpStatus.Used:
                return Error.OtpAlreadyUsed();
            case OtpStatus.Locked:
                return Error.OtpLocked("The OTP is locked after too many failed attempts. Please request a new OTP.");
            case OtpStatus.Expired:
                return Error.OtpExpired();
        }

        if (record.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            return Error.OtpExpired();
        }

        if (!_hasher.Verify(otp, record.CodeHash))
        {
            await _repository.IncrementAttemptAsync(record.Id, _options.MaxAttempts, cancellationToken);
            var updated = await _repository.GetByIdAsync(record.Id, cancellationToken);
            return updated?.Status == OtpStatus.Locked
                ? Error.OtpLocked("The OTP is locked after too many failed attempts. Please request a new OTP.")
                : Error.OtpIncorrect();
        }

        var now = _timeProvider.GetUtcNow();
        var affected = await _repository.MarkUsedAsync(record.Id, transactionId, now, cancellationToken);

        if (affected == 1)
        {
            _logger.LogInformation("OTP {OtpId} verified and marked used for transaction {TransactionId}.", record.Id, transactionId);
            return new OtpVerificationResult(true);
        }

        // A concurrent verification won the atomic transition; classify without leaking details.
        var after = await _repository.GetByIdAsync(record.Id, cancellationToken);
        return after?.Status switch
        {
            OtpStatus.Used => Error.OtpAlreadyUsed(),
            OtpStatus.Expired => Error.OtpExpired(),
            OtpStatus.Locked => Error.OtpLocked(),
            _ => Error.OtpAlreadyUsed()
        };
    }
}

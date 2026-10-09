using Microservices.Common.Messaging;
using Microservices.Common.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Options;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.Services;

/// <summary>Outcome of processing a consumed event, driving the consumer's ack/nack decision.</summary>
public enum ProcessOutcome
{
    /// <summary>The event was fully handled (sent, deduplicated, or permanently rejected) — ack.</summary>
    Completed,

    /// <summary>Transient retries were exhausted — route to the dead-letter queue.</summary>
    DeadLetter
}

/// <summary>
/// Processes an email event idempotently: dedupes by message id, builds content, sends with
/// bounded exponential backoff, and records the audit log. Never stores or logs email bodies
/// or OTP values.
/// </summary>
public sealed class EmailProcessingService
{
    private readonly IEmailLogRepository _repository;
    private readonly IEmailSender _sender;
    private readonly IEmailContentBuilder _contentBuilder;
    private readonly TimeProvider _timeProvider;
    private readonly NotificationOptions _options;
    private readonly ILogger<EmailProcessingService> _logger;

    public EmailProcessingService(
        IEmailLogRepository repository,
        IEmailSender sender,
        IEmailContentBuilder contentBuilder,
        TimeProvider timeProvider,
        IOptions<NotificationOptions> options,
        ILogger<EmailProcessingService> logger)
    {
        _repository = repository;
        _sender = sender;
        _contentBuilder = contentBuilder;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProcessOutcome> ProcessAsync(EmailEventMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.MessageId))
        {
            _logger.LogWarning("Discarding email event without a message id.");
            return ProcessOutcome.Completed;
        }

        // Idempotency: the same message id is never emailed twice.
        if (await _repository.FindByMessageIdAsync(message.MessageId, cancellationToken) is not null)
        {
            _logger.LogInformation("Duplicate event {MessageId} skipped.", message.MessageId);
            return ProcessOutcome.Completed;
        }

        if (!EmailValidator.IsValid(message.Recipient))
        {
            await RecordFailedAsync(message, "INVALID_RECIPIENT", "Recipient email is invalid.", cancellationToken);
            return ProcessOutcome.Completed;
        }

        var content = _contentBuilder.Build(message.EventType, message.Payload);
        if (!content.IsSuccess)
        {
            await RecordFailedAsync(message, content.Error.Code, content.Error.Message, cancellationToken);
            return ProcessOutcome.Completed;
        }

        var log = new EmailLog(message.MessageId, message.EventType, message.Recipient, content.Value.Subject, _timeProvider.GetUtcNow());
        var created = await _repository.TryCreateAsync(log, cancellationToken);
        if (created is null)
        {
            // Concurrent duplicate delivery — another instance already owns this message id.
            return ProcessOutcome.Completed;
        }

        var backoffSeconds = _options.InitialBackoffSeconds;
        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            await _repository.MarkSendingAsync(created.Id, attempt, cancellationToken);

            var email = new EmailMessage(message.Recipient, content.Value.Subject, content.Value.HtmlBody, content.Value.TextBody);
            var result = await _sender.SendAsync(email, cancellationToken);

            switch (result.Outcome)
            {
                case SendEmailOutcome.Sent:
                    await _repository.MarkSentAsync(created.Id, result.ProviderMessageId, _timeProvider.GetUtcNow(), cancellationToken);
                    _logger.LogInformation("Email {MessageId} sent to {Recipient}.", message.MessageId, message.Recipient);
                    return ProcessOutcome.Completed;

                case SendEmailOutcome.PermanentFailure:
                    await _repository.MarkFailedAsync(created.Id, result.ErrorCode ?? "SEND_FAILED", result.ErrorMessage, attempt, cancellationToken);
                    _logger.LogWarning("Email {MessageId} permanently failed ({ErrorCode}).", message.MessageId, result.ErrorCode);
                    return ProcessOutcome.Completed;

                case SendEmailOutcome.TransientFailure when attempt == _options.MaxAttempts:
                    await _repository.MarkFailedAsync(created.Id, "TRANSIENT_EXHAUSTED", result.ErrorMessage, attempt, cancellationToken);
                    _logger.LogWarning("Email {MessageId} exhausted retries; routing to dead-letter.", message.MessageId);
                    return ProcessOutcome.DeadLetter;

                default:
                    _logger.LogWarning("Email {MessageId} transient failure (attempt {Attempt}/{Max}); backing off {Seconds}s.",
                        message.MessageId, attempt, _options.MaxAttempts, backoffSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), cancellationToken);
                    backoffSeconds = Math.Min(backoffSeconds * 2, _options.MaxBackoffSeconds);
                    break;
            }
        }

        return ProcessOutcome.DeadLetter;
    }

    private async Task RecordFailedAsync(EmailEventMessage message, string errorCode, string? errorMessage, CancellationToken cancellationToken)
    {
        var eventType = string.IsNullOrWhiteSpace(message.EventType) ? "UNKNOWN" : message.EventType;
        var recipient = string.IsNullOrWhiteSpace(message.Recipient) ? "unknown" : message.Recipient;

        var log = new EmailLog(message.MessageId, eventType, recipient, "—", _timeProvider.GetUtcNow());
        var created = await _repository.TryCreateAsync(log, cancellationToken);
        if (created is not null)
        {
            await _repository.MarkFailedAsync(created.Id, errorCode, errorMessage, 0, cancellationToken);
        }
    }
}

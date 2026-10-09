using System.Text.Json;
using FluentAssertions;
using Microservices.Common.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Options;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.UnitTests;

[Trait("Category", "Unit")]
public class EmailProcessingServiceTests
{
    private static EmailProcessingService CreateService(
        InMemoryEmailLogRepository repository,
        IEmailSender sender,
        NotificationOptions? options = null)
    {
        return new EmailProcessingService(
            repository,
            sender,
            new StubContentBuilder(),
            TimeProvider.System,
            Options.Create(options ?? new NotificationOptions { MaxAttempts = 3, InitialBackoffSeconds = 0, MaxBackoffSeconds = 0 }),
            NullLogger<EmailProcessingService>.Instance);
    }

    private static EmailEventMessage Message(string id, string type, string recipient) =>
        new(id, type, DateTimeOffset.UtcNow, "corr", "txn", recipient, JsonDocument.Parse("{}").RootElement);

    [Fact]
    public async Task Process_DuplicateMessage_IsSkipped()
    {
        var repo = new InMemoryEmailLogRepository();
        repo.Existing.Add(new EmailLog("msg-1", EmailEventTypes.OtpRequested, "a@example.com", "S", DateTimeOffset.UtcNow));
        var sender = new RecordingSender(new SendEmailResult(SendEmailOutcome.Sent, "p1", null, null));

        var outcome = await CreateService(repo, sender).ProcessAsync(Message("msg-1", EmailEventTypes.OtpRequested, "a@example.com"), CancellationToken.None);

        outcome.Should().Be(ProcessOutcome.Completed);
        sender.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Process_SendSuccess_MarksSent()
    {
        var repo = new InMemoryEmailLogRepository();
        var sender = new RecordingSender(new SendEmailResult(SendEmailOutcome.Sent, "p1", null, null));

        var outcome = await CreateService(repo, sender).ProcessAsync(Message("msg-1", EmailEventTypes.PaymentSucceeded, "a@example.com"), CancellationToken.None);

        outcome.Should().Be(ProcessOutcome.Completed);
        repo.LastStatus.Should().Be(EmailStatus.Sent);
        repo.LastProviderMessageId.Should().Be("p1");
    }

    [Fact]
    public async Task Process_PermanentFailure_MarksFailedAndCompletes()
    {
        var repo = new InMemoryEmailLogRepository();
        var sender = new RecordingSender(new SendEmailResult(SendEmailOutcome.PermanentFailure, null, "SMTP_AUTH_FAILED", "bad creds"));

        var outcome = await CreateService(repo, sender).ProcessAsync(Message("msg-1", EmailEventTypes.OtpRequested, "a@example.com"), CancellationToken.None);

        outcome.Should().Be(ProcessOutcome.Completed);
        repo.LastStatus.Should().Be(EmailStatus.Failed);
        repo.LastErrorCode.Should().Be("SMTP_AUTH_FAILED");
        sender.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Process_TransientFailure_RetriesThenDeadLetters()
    {
        var repo = new InMemoryEmailLogRepository();
        var sender = new RecordingSender(new SendEmailResult(SendEmailOutcome.TransientFailure, null, "SMTP_UNAVAILABLE", "down"));

        var outcome = await CreateService(repo, sender).ProcessAsync(Message("msg-1", EmailEventTypes.OtpRequested, "a@example.com"), CancellationToken.None);

        outcome.Should().Be(ProcessOutcome.DeadLetter);
        sender.CallCount.Should().Be(3);
        repo.LastStatus.Should().Be(EmailStatus.Failed);
        repo.LastErrorCode.Should().Be("TRANSIENT_EXHAUSTED");
    }

    [Fact]
    public async Task Process_InvalidRecipient_MarksFailedAndCompletes()
    {
        var repo = new InMemoryEmailLogRepository();
        var sender = new RecordingSender(new SendEmailResult(SendEmailOutcome.Sent, "p1", null, null));

        var outcome = await CreateService(repo, sender).ProcessAsync(Message("msg-1", EmailEventTypes.OtpRequested, "not-an-email"), CancellationToken.None);

        outcome.Should().Be(ProcessOutcome.Completed);
        sender.CallCount.Should().Be(0);
        repo.LastErrorCode.Should().Be("INVALID_RECIPIENT");
    }

    private sealed class StubContentBuilder : IEmailContentBuilder
    {
        public Microservices.Common.Errors.Result<EmailContent> Build(string eventType, JsonElement payload) =>
            new EmailContent("Subject", "<p>Body</p>", "Body");
    }

    private sealed class RecordingSender : IEmailSender
    {
        private readonly SendEmailResult _result;
        public int CallCount { get; private set; }

        public RecordingSender(SendEmailResult result) => _result = result;

        public Task<SendEmailResult> SendAsync(EmailMessage message, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class InMemoryEmailLogRepository : IEmailLogRepository
    {
        public List<EmailLog> Existing { get; } = new();
        private long _nextId = 1;

        public EmailStatus? LastStatus { get; private set; }
        public string? LastProviderMessageId { get; private set; }
        public string? LastErrorCode { get; private set; }

        public Task<EmailLog?> FindByMessageIdAsync(string messageId, CancellationToken ct) =>
            Task.FromResult(Existing.FirstOrDefault(x => x.MessageId == messageId));

        public Task<EmailLog?> TryCreateAsync(EmailLog log, CancellationToken ct)
        {
            var created = new EmailLog(_nextId++, log.MessageId, log.EventType, log.Recipient, log.Subject, EmailStatus.Pending, 0, null, null, null, log.CreatedAt, null);
            Existing.Add(created);
            return Task.FromResult<EmailLog?>(created);
        }

        public Task MarkSendingAsync(long id, int attemptCount, CancellationToken ct)
        {
            LastStatus = EmailStatus.Sending;
            return Task.CompletedTask;
        }

        public Task MarkSentAsync(long id, string? providerMessageId, DateTimeOffset sentAt, CancellationToken ct)
        {
            LastStatus = EmailStatus.Sent;
            LastProviderMessageId = providerMessageId;
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(long id, string errorCode, string? errorMessage, int attemptCount, CancellationToken ct)
        {
            LastStatus = EmailStatus.Failed;
            LastErrorCode = errorCode;
            return Task.CompletedTask;
        }
    }
}

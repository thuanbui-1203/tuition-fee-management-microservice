using System.Text.Json;
using FluentAssertions;
using Microservices.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Options;
using NotificationService.Application.Services;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Repositories;

namespace NotificationService.IntegrationTests;

/// <summary>
/// Integration tests against real PostgreSQL for the idempotent email processing pipeline.
/// </summary>
[Trait("Category", "Integration")]
public class NotificationProcessingIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly DbContextOptions<NotificationDbContext> _options;

    public NotificationProcessingIntegrationTests(PostgresFixture fixture)
    {
        _options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        using var db = new NotificationDbContext(_options);
        db.Database.Migrate();
    }

    private static EmailEventMessage Message(string id) =>
        new(id, EmailEventTypes.PaymentSucceeded, DateTimeOffset.UtcNow, "corr", "txn", "a@example.com", JsonDocument.Parse("{}").RootElement);

    private static EmailProcessingService CreateService(NotificationDbContext db, RecordingSender sender) =>
        new(
            new EmailLogRepository(db),
            sender,
            new StubContentBuilder(),
            TimeProvider.System,
            Options.Create(new NotificationOptions { MaxAttempts = 3, InitialBackoffSeconds = 0, MaxBackoffSeconds = 0 }),
            NullLogger<EmailProcessingService>.Instance);

    [Fact]
    public async Task Process_SendSuccess_PersistsSentLog()
    {
        await using var db = new NotificationDbContext(_options);
        var sender = new RecordingSender(new SendEmailResult(SendEmailOutcome.Sent, "prov-1", null, null));

        var outcome = await CreateService(db, sender).ProcessAsync(Message("msg-1"), CancellationToken.None);

        outcome.Should().Be(ProcessOutcome.Completed);
        var log = await db.EmailLogs.AsNoTracking().SingleAsync(x => x.MessageId == "msg-1");
        log.Status.Should().Be(EmailStatus.Sent);
        log.ProviderMessageId.Should().Be("prov-1");
    }

    [Fact]
    public async Task Process_DuplicateMessage_IsDeduplicatedByDatabase()
    {
        var sender = new RecordingSender(new SendEmailResult(SendEmailOutcome.Sent, "prov-1", null, null));

        await using (var db1 = new NotificationDbContext(_options))
        {
            await CreateService(db1, sender).ProcessAsync(Message("msg-dup"), CancellationToken.None);
        }

        await using (var db2 = new NotificationDbContext(_options))
        {
            var outcome = await CreateService(db2, sender).ProcessAsync(Message("msg-dup"), CancellationToken.None);
            outcome.Should().Be(ProcessOutcome.Completed);
        }

        sender.CallCount.Should().Be(1);
        await using var check = new NotificationDbContext(_options);
        (await check.EmailLogs.CountAsync(x => x.MessageId == "msg-dup")).Should().Be(1);
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
}

using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using NotificationService.Application.Abstractions;
using NotificationService.Application.Options;

namespace NotificationService.Infrastructure.Email;

/// <summary>
/// MailKit SMTP sender. Failures are classified so the retry policy can distinguish
/// transient (timeout/SMTP-down/4xx) from permanent (authentication/5xx) errors. Credentials
/// come only from configuration; they are never logged.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SendEmailResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new SmtpClient();
            var secureOptions = _options.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(_options.Host, _options.Port, secureOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.UserName))
            {
                await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
            }

            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            mime.To.Add(MailboxAddress.Parse(message.To));
            mime.Subject = message.Subject;
            mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

            var response = await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            return new SendEmailResult(SendEmailOutcome.Sent, response, null, null);
        }
        catch (AuthenticationException ex)
        {
            _logger.LogWarning("SMTP authentication failed.");
            return new SendEmailResult(SendEmailOutcome.PermanentFailure, null, "SMTP_AUTH_FAILED", ex.Message);
        }
        catch (SmtpCommandException ex)
        {
            var permanent = (int)ex.StatusCode >= 500;
            _logger.LogWarning("SMTP command failed with status {StatusCode}.", ex.StatusCode);
            return new SendEmailResult(
                permanent ? SendEmailOutcome.PermanentFailure : SendEmailOutcome.TransientFailure,
                null,
                permanent ? "SMTP_REJECTED" : "SMTP_TRANSIENT",
                ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("SMTP transient failure.");
            return new SendEmailResult(SendEmailOutcome.TransientFailure, null, "SMTP_UNAVAILABLE", ex.Message);
        }
    }
}

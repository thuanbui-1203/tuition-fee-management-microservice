namespace NotificationService.Application.Options;

/// <summary>SMTP settings. Credentials are always supplied from configuration/environment.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "no-reply@tdtu.edu.vn";
    public string FromName { get; set; } = "iBanking";
    public bool EnableSsl { get; set; }
}

namespace NotificationService.Domain.Enums;

/// <summary>Email delivery state.</summary>
public enum EmailStatus
{
    Pending,
    Sending,
    Sent,
    Failed
}

namespace TuitionService.Domain.Enums;

/// <summary>
/// Lifecycle of a tuition fee. Only <see cref="Unpaid"/> and <see cref="Paid"/> are valid
/// persisted states; transitions are guarded by conditional updates at the database layer.
/// </summary>
public enum FeeStatus
{
    Unpaid,
    Paid
}

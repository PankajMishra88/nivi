using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Domain.Entities;

public class Notification : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ReminderSourceType SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public bool SendEmail { get; set; }
    public DateTime? EmailSentAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

using Nivi.WealthOS.Api.Domain.Enums;

namespace Nivi.WealthOS.Api.Domain.Entities;

public class Reminder : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public ReminderSourceType SourceType { get; set; }
    public Guid SourceId { get; set; }
    public DateOnly DueDate { get; set; }
    public ReminderStatus Status { get; set; }
    public DateTime? LastNotifiedAtUtc { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

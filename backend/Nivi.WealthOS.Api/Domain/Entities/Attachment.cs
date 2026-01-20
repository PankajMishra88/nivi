namespace Nivi.WealthOS.Api.Domain.Entities;

public class Attachment : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string LinkedObjectType { get; set; } = string.Empty;
    public Guid LinkedObjectId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public Guid UploadedBy { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}

namespace Nivi.WealthOS.Api.Models;

public record AttachmentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedAtUtc);

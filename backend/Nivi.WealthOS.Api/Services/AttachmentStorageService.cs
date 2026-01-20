using Microsoft.Extensions.Options;
using Nivi.WealthOS.Api.Options;

namespace Nivi.WealthOS.Api.Services;

public record StoredAttachment(string StoragePath, string FileName, string ContentType, long SizeBytes);

public class AttachmentStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".docx",
        ".xlsx"
    };

    private const long MaxSizeBytes = 15 * 1024 * 1024;
    private readonly StorageOptions _options;

    public AttachmentStorageService(IOptions<StorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<StoredAttachment> SaveAsync(Guid tenantId, string objectType, Guid objectId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || file.Length > MaxSizeBytes)
        {
            throw new InvalidOperationException("Attachment size exceeds allowed limit.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Attachment type is not allowed.");
        }

        var safeFileName = Path.GetFileName(file.FileName);
        var now = DateTime.UtcNow;
        var directory = Path.Combine(
            _options.AttachmentBasePath,
            tenantId.ToString(),
            now.Year.ToString("0000"),
            now.Month.ToString("00"),
            objectType,
            objectId.ToString());

        Directory.CreateDirectory(directory);

        var fullPath = Path.Combine(directory, safeFileName);
        await using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return new StoredAttachment(fullPath, safeFileName, file.ContentType, file.Length);
    }
}

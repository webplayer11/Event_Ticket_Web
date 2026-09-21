namespace EventGO.Domain.Entities;

public class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string StorageProvider { get; set; } = "Local";

    public string BucketName { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public Guid UploadedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
        = DateTimeOffset.UtcNow;
}
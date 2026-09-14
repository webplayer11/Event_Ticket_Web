using EventGO.Domain.Enums;

namespace EventGO.Domain.Entities;

public class EventDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }

    public Guid StoredFileId { get; set; }

    public EventDocumentType DocumentType { get; set; }
        = EventDocumentType.EventPermit;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
        = DateTimeOffset.UtcNow;
}
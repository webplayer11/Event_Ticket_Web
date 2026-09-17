using EventGO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class EventDocumentConfiguration
    : IEntityTypeConfiguration<EventDocument>
{
    public void Configure(EntityTypeBuilder<EventDocument> builder)
    {
        builder.ToTable("EventDocuments", table =>
        {
            table.HasCheckConstraint(
                "CK_EventDocuments_DocumentType",
                "[DocumentType] IN (1, 2)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentType)
            .HasConversion<int>();

        builder.Property(x => x.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasIndex(x => new { x.EventId, x.StoredFileId })
            .IsUnique();

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(x => x.StoredFileId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
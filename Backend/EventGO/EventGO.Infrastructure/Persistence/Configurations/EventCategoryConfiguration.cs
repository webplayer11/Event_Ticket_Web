using EventGO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class EventCategoryConfiguration
    : IEntityTypeConfiguration<EventCategory>
{
    public void Configure(EntityTypeBuilder<EventCategory> builder)
    {
        builder.ToTable("EventCategories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Slug)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasIndex(x => x.Slug)
            .IsUnique();

        // Thể loại mặc định, khớp danh sách trên form tạo sự kiện.
        // Id cố định để migration và frontend tham chiếu ổn định.
        builder.HasData(
            Category("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b01", "Âm nhạc", "am-nhac"),
            Category("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b02", "Thể thao", "the-thao"),
            Category("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b03", "Hội thảo/Workshop", "hoi-thao-workshop"),
            Category("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b04", "Sân khấu & Nghệ thuật", "san-khau-nghe-thuat"),
            Category("0d6f2a1e-6c1b-4b1e-9a51-1f0a8d6e3b05", "Khác", "khac"));
    }

    private static EventCategory Category(string id, string name, string slug) => new()
    {
        Id = new Guid(id),
        Name = name,
        Slug = slug,
        Description = string.Empty,
        IsActive = true
    };
}
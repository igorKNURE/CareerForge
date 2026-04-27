using CareerForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerForge.Infrastructure.Persistence.Configurations;

public sealed class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.FileName).HasMaxLength(512).IsRequired();
        builder.Property(r => r.RawText).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.StructuredJson).HasColumnType("jsonb");
        builder.Property(r => r.SummaryEmbedding).HasColumnType("vector(768)");
        builder.HasIndex(r => r.UserId);
    }
}

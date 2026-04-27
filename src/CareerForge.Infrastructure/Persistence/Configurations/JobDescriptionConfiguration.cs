using CareerForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerForge.Infrastructure.Persistence.Configurations;

public sealed class JobDescriptionConfiguration : IEntityTypeConfiguration<JobDescription>
{
    public void Configure(EntityTypeBuilder<JobDescription> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Title).HasMaxLength(256).IsRequired();
        builder.Property(j => j.Company).HasMaxLength(256);
        builder.Property(j => j.RawText).IsRequired();
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(j => j.StructuredJson).HasColumnType("jsonb");
        builder.Property(j => j.SummaryEmbedding).HasColumnType("vector(768)");
        builder.HasIndex(j => j.UserId);
    }
}

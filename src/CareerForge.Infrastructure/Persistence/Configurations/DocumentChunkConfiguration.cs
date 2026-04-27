using CareerForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerForge.Infrastructure.Persistence.Configurations;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Text).IsRequired();
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.Embedding).HasColumnType($"vector({DocumentChunk.EmbeddingDimensions})");
        builder.HasIndex(c => new { c.DocumentId, c.Kind });
    }
}

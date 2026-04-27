using System.Text.Json;
using CareerForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerForge.Infrastructure.Persistence.Configurations;

public sealed class QuestionBankEntryConfiguration : IEntityTypeConfiguration<QuestionBankEntry>
{
    public void Configure(EntityTypeBuilder<QuestionBankEntry> builder)
    {
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Text).IsRequired();
        builder.Property(q => q.Category).HasConversion<string>().HasMaxLength(32);
        builder.Property(q => q.Difficulty).HasConversion<string>().HasMaxLength(16);
        builder.Property(q => q.ExpectedFormat).HasConversion<string>().HasMaxLength(32);
        builder.Property(q => q.Source).HasMaxLength(32);

        builder.Property(q => q.SkillTags)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v ?? new List<string>(), (JsonSerializerOptions?)null),
                v => SafeDeserializeStringList(v));

        builder.HasIndex(q => new { q.Difficulty, q.Category });
        builder.HasIndex(q => q.Text);
    }

    private static List<string> SafeDeserializeStringList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.TrimStart().StartsWith('['))
            return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions?)null) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }
}

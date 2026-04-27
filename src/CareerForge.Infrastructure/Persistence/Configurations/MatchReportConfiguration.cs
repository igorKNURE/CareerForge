using System.Text.Json;
using CareerForge.Domain.Entities;
using CareerForge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerForge.Infrastructure.Persistence.Configurations;

public sealed class MatchReportConfiguration : IEntityTypeConfiguration<MatchReport>
{
    public void Configure(EntityTypeBuilder<MatchReport> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.OverallScore).HasPrecision(5, 2);
        builder.Property(m => m.SkillCoverageScore).HasPrecision(5, 2);
        builder.Property(m => m.SemanticSimilarityScore).HasPrecision(5, 2);
        builder.Property(m => m.ExperienceFitScore).HasPrecision(5, 2);

        builder.Property(m => m.Findings)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<MatchFinding>>(v, (JsonSerializerOptions?)null) ?? new List<MatchFinding>());

        ConfigureStringList(builder, m => m.MatchedMustHaveSkills);
        ConfigureStringList(builder, m => m.MissingMustHaveSkills);
        ConfigureStringList(builder, m => m.MatchedNiceToHaveSkills);

        builder.HasIndex(m => new { m.UserId, m.ResumeId, m.JobDescriptionId }).IsUnique();

        builder.HasOne<Resume>()
            .WithMany()
            .HasForeignKey(m => m.ResumeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<JobDescription>()
            .WithMany()
            .HasForeignKey(m => m.JobDescriptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureStringList(
        EntityTypeBuilder<MatchReport> builder,
        System.Linq.Expressions.Expression<Func<MatchReport, List<string>>> selector)
    {
        builder.Property(selector)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v ?? new List<string>(), (JsonSerializerOptions?)null),
                v => SafeDeserializeStringList(v));
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

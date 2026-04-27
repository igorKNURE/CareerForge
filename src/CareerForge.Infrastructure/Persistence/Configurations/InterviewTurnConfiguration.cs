using System.Text.Json;
using CareerForge.Domain.Entities;
using CareerForge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerForge.Infrastructure.Persistence.Configurations;

public sealed class InterviewTurnConfiguration : IEntityTypeConfiguration<InterviewTurn>
{
    public void Configure(EntityTypeBuilder<InterviewTurn> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.QuestionText).IsRequired();
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(32);
        builder.Property(t => t.Difficulty).HasConversion<string>().HasMaxLength(16);
        builder.Property(t => t.ExpectedFormat).HasConversion<string>().HasMaxLength(32);

        builder.Property(t => t.Evaluation)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<AnswerEvaluation>(v, (JsonSerializerOptions?)null));

        builder.HasIndex(t => new { t.SessionId, t.TurnIndex }).IsUnique();

        builder.HasOne<InterviewSession>()
            .WithMany()
            .HasForeignKey(t => t.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

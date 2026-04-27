using CareerForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareerForge.Infrastructure.Persistence.Configurations;

public sealed class InterviewSessionConfiguration : IEntityTypeConfiguration<InterviewSession>
{
    public void Configure(EntityTypeBuilder<InterviewSession> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(256).IsRequired();
        builder.Property(s => s.Language).HasMaxLength(8).IsRequired().HasDefaultValue("en");
        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => new { s.ResumeId, s.JobDescriptionId });

        builder.HasOne<Resume>()
            .WithMany()
            .HasForeignKey(s => s.ResumeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<JobDescription>()
            .WithMany()
            .HasForeignKey(s => s.JobDescriptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

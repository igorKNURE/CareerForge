using CareerForge.Domain.Enums;
using Pgvector;

namespace CareerForge.Domain.Entities;

/// <summary>
/// An uploaded resume along with its parsed text, LLM-extracted summary, and embedding.
/// </summary>
public class Resume
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? StructuredJson { get; set; }
    public Vector? SummaryEmbedding { get; set; }
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

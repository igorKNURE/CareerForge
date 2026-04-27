using CareerForge.Domain.Enums;
using Pgvector;

namespace CareerForge.Domain.Entities;

/// <summary>
/// One embedded slice of a resume or job description, used for similarity retrieval.
/// </summary>
public class DocumentChunk
{
    public const int EmbeddingDimensions = 768;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public DocumentKind Kind { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public int TokenEstimate { get; set; }
    public Vector? Embedding { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

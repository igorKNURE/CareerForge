namespace CareerForge.Domain.Enums;

/// <summary>
/// Stages of the resume / job-description ingestion pipeline.
/// </summary>
public enum ProcessingStatus
{
    Pending,
    Parsing,
    Chunking,
    Embedding,
    Summarizing,
    Done,
    Error,
}

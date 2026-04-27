using CareerForge.Domain.Entities;

namespace CareerForge.Application.Abstractions.Persistence;

/// <summary>
/// Read-only access to entities exposed by list and detail query endpoints. The
/// abstraction isolates query call sites from the writable application context,
/// allowing reads to be routed to a separate replica without changes to consumers.
/// </summary>
public interface IReadOnlyDb
{
    IQueryable<Resume> Resumes { get; }
    IQueryable<JobDescription> JobDescriptions { get; }
    IQueryable<MatchReport> MatchReports { get; }
    IQueryable<InterviewSession> InterviewSessions { get; }
    IQueryable<InterviewTurn> InterviewTurns { get; }
}

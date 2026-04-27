using CareerForge.Application.Abstractions.Persistence;
using CareerForge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CareerForge.Infrastructure.Persistence;

/// <summary>
/// Default <see cref="IReadOnlyDb"/> implementation. Exposes the writable application
/// context's entity sets with change tracking disabled. Replace this registration to
/// route reads to a dedicated replica connection.
/// </summary>
public sealed class AppDbReadOnly(AppDbContext db) : IReadOnlyDb
{
    public IQueryable<Resume> Resumes => db.Resumes.AsNoTracking();
    public IQueryable<JobDescription> JobDescriptions => db.JobDescriptions.AsNoTracking();
    public IQueryable<MatchReport> MatchReports => db.MatchReports.AsNoTracking();
    public IQueryable<InterviewSession> InterviewSessions => db.InterviewSessions.AsNoTracking();
    public IQueryable<InterviewTurn> InterviewTurns => db.InterviewTurns.AsNoTracking();
}

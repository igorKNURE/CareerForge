using CareerForge.Api.Endpoints;
using FluentValidation;

namespace CareerForge.Api.Validators;

public sealed class CreateVacancyValidator : AbstractValidator<JobDescriptionEndpoints.CreateVacancyRequest>
{
    public CreateVacancyValidator()
    {
        RuleFor(x => x.RawText).NotEmpty().MinimumLength(80).MaximumLength(50_000);
        RuleFor(x => x.TitleHint).MaximumLength(256);
    }
}

public sealed class CreateMatchValidator : AbstractValidator<MatchEndpoints.CreateMatchRequest>
{
    public CreateMatchValidator()
    {
        RuleFor(x => x.ResumeId).NotEqual(Guid.Empty);
        RuleFor(x => x.JobDescriptionId).NotEqual(Guid.Empty);
    }
}

public sealed class CreateSessionValidator : AbstractValidator<InterviewEndpoints.CreateSessionRequest>
{
    public CreateSessionValidator()
    {
        RuleFor(x => x.ResumeId).NotEqual(Guid.Empty);
        RuleFor(x => x.JobDescriptionId).NotEqual(Guid.Empty);
        RuleFor(x => x.Name).MaximumLength(256);
    }
}

public sealed class SubmitAnswerValidator : AbstractValidator<InterviewEndpoints.SubmitAnswerRequest>
{
    public SubmitAnswerValidator()
    {
        RuleFor(x => x.AnswerText).NotEmpty().MinimumLength(20).MaximumLength(20_000);
    }
}

public sealed class RenameSessionValidator : AbstractValidator<InterviewEndpoints.RenameSessionRequest>
{
    public RenameSessionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MinimumLength(1).MaximumLength(256);
    }
}

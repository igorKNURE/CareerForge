using CareerForge.Domain.Entities;
using CareerForge.Domain.Enums;

namespace CareerForge.Infrastructure.Interview;

internal static class QuestionBankSeed
{
    public static IReadOnlyList<QuestionBankEntry> All => new[]
    {
        // === Behavioral / Resume warm-ups ===
        Mk("Tell me about yourself and what brought you to engineering.", QuestionCategory.Behavioral, QuestionDifficulty.Easy, AnswerFormat.Freeform, "general"),
        Mk("Why are you interested in this role specifically?", QuestionCategory.Behavioral, QuestionDifficulty.Easy, AnswerFormat.Freeform, "general"),
        Mk("Walk me through the project on your resume you're most proud of.", QuestionCategory.Resume, QuestionDifficulty.Easy, AnswerFormat.StructuredBullets, "general"),
        Mk("Describe a recent technical decision you made — what alternatives did you consider?", QuestionCategory.Behavioral, QuestionDifficulty.Medium, AnswerFormat.STAR, "general"),
        Mk("Tell me about a time you disagreed with a teammate. How did you resolve it?", QuestionCategory.Behavioral, QuestionDifficulty.Medium, AnswerFormat.STAR, "general"),
        Mk("Describe a production incident you helped resolve. What did you learn?", QuestionCategory.Behavioral, QuestionDifficulty.Medium, AnswerFormat.STAR, "general"),

        // === .NET / C# core ===
        Mk("What's the difference between IQueryable<T> and IEnumerable<T>?", QuestionCategory.Technical, QuestionDifficulty.Easy, AnswerFormat.Freeform, "c#", ".net", "linq"),
        Mk("When would you choose a struct over a class in C#?", QuestionCategory.Technical, QuestionDifficulty.Easy, AnswerFormat.Freeform, "c#", ".net"),
        Mk("Explain the difference between Task and ValueTask. When does ValueTask win?", QuestionCategory.Technical, QuestionDifficulty.Medium, AnswerFormat.Freeform, "c#", ".net", "async"),
        Mk("How does async/await work under the hood — what's the state machine doing?", QuestionCategory.Technical, QuestionDifficulty.Medium, AnswerFormat.StepByStep, "c#", ".net", "async"),
        Mk("Explain the .NET dependency injection lifetimes (Singleton, Scoped, Transient). When to use each?", QuestionCategory.Technical, QuestionDifficulty.Easy, AnswerFormat.Freeform, ".net", "aspnetcore", "dependencyinjection"),
        Mk("What's the difference between an abstract class and an interface in C#?", QuestionCategory.Technical, QuestionDifficulty.Easy, AnswerFormat.Freeform, "c#", "oop"),
        Mk("Describe how the .NET garbage collector's generations work.", QuestionCategory.Technical, QuestionDifficulty.Medium, AnswerFormat.StepByStep, ".net", "performance"),
        Mk("What's the difference between Dictionary<TKey,TValue> and ConcurrentDictionary?", QuestionCategory.Technical, QuestionDifficulty.Easy, AnswerFormat.Freeform, "c#", ".net", "concurrency"),
        Mk("Explain ASP.NET Core middleware — how is the pipeline built and executed?", QuestionCategory.Technical, QuestionDifficulty.Medium, AnswerFormat.StepByStep, "aspnetcore", ".net"),
        Mk("What's the SOLID 'L' (Liskov Substitution) principle? Give an example violation.", QuestionCategory.Technical, QuestionDifficulty.Medium, AnswerFormat.Freeform, "oop", "solid", "designpatterns"),
        Mk("Difference between Entity Framework's tracking vs no-tracking queries — when to use each?", QuestionCategory.Technical, QuestionDifficulty.Medium, AnswerFormat.Freeform, "entityframework", ".net", "ef"),
        Mk("What's the n+1 query problem and how would you spot it in EF Core?", QuestionCategory.Technical, QuestionDifficulty.Medium, AnswerFormat.StepByStep, "entityframework", "ef", "performance", "sql"),

        // === SQL / databases ===
        Mk("Explain the difference between INNER JOIN and LEFT JOIN with an example.", QuestionCategory.Technical, QuestionDifficulty.Easy, AnswerFormat.Freeform, "sql", "sqlserver", "postgresql"),
        Mk("What's a database index? When does adding one hurt performance?", QuestionCategory.Technical, QuestionDifficulty.Medium, AnswerFormat.Freeform, "sql", "sqlserver", "postgresql", "performance"),
        Mk("Explain database transaction isolation levels — give a concrete read-anomaly example.", QuestionCategory.Technical, QuestionDifficulty.Hard, AnswerFormat.StepByStep, "sql", "sqlserver", "postgresql", "concurrency"),

        // === System design / architecture ===
        Mk("Walk me through how you'd design a URL shortener for ~10M URLs/month.", QuestionCategory.SystemDesign, QuestionDifficulty.Hard, AnswerFormat.StepByStep, "systemdesign", "architecture"),
        Mk("How would you design an idempotent payment-processing API endpoint?", QuestionCategory.SystemDesign, QuestionDifficulty.Hard, AnswerFormat.StepByStep, "systemdesign", "architecture", "api", "rest"),
        Mk("When would you reach for a message queue vs a pub/sub system? Give a real example for each.", QuestionCategory.SystemDesign, QuestionDifficulty.Medium, AnswerFormat.Freeform, "systemdesign", "architecture", "kafka", "rabbitmq"),
        Mk("Explain CAP theorem and what tradeoffs you'd pick for an order-tracking system.", QuestionCategory.SystemDesign, QuestionDifficulty.Hard, AnswerFormat.StepByStep, "systemdesign", "distributedsystems"),
        Mk("How would you implement a rate limiter for an API — describe two different algorithms.", QuestionCategory.SystemDesign, QuestionDifficulty.Medium, AnswerFormat.StepByStep, "systemdesign", "api", "performance"),

        // === Web / API / general ===
        Mk("What does HTTP idempotency mean? Which methods are idempotent and why does it matter?", QuestionCategory.Technical, QuestionDifficulty.Easy, AnswerFormat.Freeform, "http", "rest", "api"),
        Mk("Explain the difference between authentication and authorization with concrete examples.", QuestionCategory.Technical, QuestionDifficulty.Easy, AnswerFormat.Freeform, "security", "auth", "authentication"),
        Mk("Walk me through how a JWT-based authentication flow works end-to-end.", QuestionCategory.Technical, QuestionDifficulty.Medium, AnswerFormat.StepByStep, "security", "auth", "jwt"),
        Mk("Difference between REST and gRPC — when would you choose gRPC?", QuestionCategory.Technical, QuestionDifficulty.Medium, AnswerFormat.Freeform, "api", "rest", "grpc"),
        Mk("Explain Docker vs a virtual machine, and when each is appropriate.", QuestionCategory.Technical, QuestionDifficulty.Easy, AnswerFormat.Freeform, "docker", "devops", "containers"),

        // === JavaScript / Frontend (in case JD shifts) ===
        Mk("Difference between var, let, and const in JavaScript — give a TDZ example.", QuestionCategory.Technical, QuestionDifficulty.Easy, AnswerFormat.Freeform, "javascript", "js", "typescript"),
        Mk("Explain the JavaScript event loop — what's the difference between microtasks and macrotasks?", QuestionCategory.Technical, QuestionDifficulty.Medium, AnswerFormat.StepByStep, "javascript", "js", "node"),
        Mk("What is a closure in JavaScript? Give a real-world use case where it's the right tool.", QuestionCategory.Technical, QuestionDifficulty.Medium, AnswerFormat.Freeform, "javascript", "js"),
        Mk("Explain React's useEffect: when does it run, and what's the dependency-array gotcha?", QuestionCategory.Technical, QuestionDifficulty.Medium, AnswerFormat.Freeform, "react", "javascript", "frontend"),
    };

    private static QuestionBankEntry Mk(string text, QuestionCategory category, QuestionDifficulty difficulty, AnswerFormat format, params string[] tags)
        => new()
        {
            Text = text,
            Category = category,
            Difficulty = difficulty,
            ExpectedFormat = format,
            SkillTags = tags.ToList(),
            Source = "seed",
        };
}

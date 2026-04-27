using CareerForge.Application.Abstractions.Auth;
using CareerForge.Application.Abstractions.BackgroundJobs;
using CareerForge.Application.Abstractions.Captcha;
using CareerForge.Application.Abstractions.Documents;
using CareerForge.Application.Abstractions.Email;
using CareerForge.Application.Abstractions.Interview;
using CareerForge.Application.Abstractions.JobDescriptions;
using CareerForge.Application.Abstractions.Llm;
using CareerForge.Application.Abstractions.Matching;
using CareerForge.Application.Abstractions.Persistence;
using CareerForge.Application.Abstractions.Resumes;
using CareerForge.Application.Interview;
using CareerForge.Application.JobDescriptions;
using CareerForge.Application.Matching;
using CareerForge.Application.Resumes;
using CareerForge.Infrastructure.Auth;
using CareerForge.Infrastructure.BackgroundJobs;
using CareerForge.Infrastructure.Captcha;
using CareerForge.Infrastructure.Documents;
using CareerForge.Infrastructure.Email;
using CareerForge.Infrastructure.Identity;
using CareerForge.Infrastructure.Llm;
using CareerForge.Infrastructure.Llm.Factories;
using CareerForge.Infrastructure.Interview;
using CareerForge.Infrastructure.Llm.Gemini;
using CareerForge.Infrastructure.Llm.Groq;
using CareerForge.Infrastructure.Llm.Ollama;
using CareerForge.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace CareerForge.Infrastructure;

/// <summary>Composition root for the infrastructure layer (DB, Identity, LLM providers, document parsers).</summary>
public static class DependencyInjection
{
    /// <summary>Registers persistence, auth, LLM, and document services with the host's DI container.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);
        AddAuth(services, configuration);
        AddLlm(services, configuration);
        AddDocuments(services);
        AddBackgroundJobs(services);
        AddEmail(services, configuration);
        AddCaptcha(services, configuration);
        return services;
    }

    /// <summary>
    /// CAPTCHA verification. The Turnstile verifier is always registered; when no
    /// secret key is configured it short-circuits to permit all requests, preserving the
    /// development experience.
    /// </summary>
    private static void AddCaptcha(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TurnstileOptions>(configuration.GetSection(TurnstileOptions.SectionName));
        services.AddHttpClient<ICaptchaVerifier, TurnstileVerifier>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
    }

    /// <summary>
    /// Email transport. Resolved by configuration: <c>Email:Provider</c> selects the
    /// implementation. Unset or unknown values fall back to a no-op log sender, keeping
    /// the development experience identical when no transport is wired.
    /// </summary>
    private static void AddEmail(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<ResendOptions>(configuration.GetSection(ResendOptions.SectionName));

        var provider = configuration[$"{EmailOptions.SectionName}:Provider"]?.ToLowerInvariant();
        if (provider == "resend")
        {
            services.AddHttpClient<IEmailSender, ResendEmailSender>((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<ResendOptions>>().Value;
                client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(15);
            });
        }
        else
        {
            services.AddSingleton<IEmailSender, LogEmailSender>();
        }
    }

    /// <summary>
    /// Registers the in-process background job queue and its hosted consumer. Replace
    /// the queue registration with a distributed implementation when horizontal scaling
    /// requires shared work distribution.
    /// </summary>
    private static void AddBackgroundJobs(IServiceCollection services)
    {
        services.AddSingleton<IBackgroundJobQueue, ChannelBackgroundJobQueue>();
        services.AddHostedService<BackgroundJobHostedService>();
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Default"),
                npgsql => npgsql.UseVector()));

        // Read-only abstraction for query handlers. Default implementation reads from the
        // primary context with change tracking disabled. Replace to route reads to a replica.
        services.AddScoped<IReadOnlyDb, AppDbReadOnly>();
    }

    private static void AddAuth(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddIdentityCore<ApplicationUser>(opts =>
            {
                opts.Password.RequiredLength = 8;
                opts.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
    }

    private static void AddLlm(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.Configure<OllamaOptions>(configuration.GetSection(OllamaOptions.SectionName));
        services.Configure<GroqOptions>(configuration.GetSection(GroqOptions.SectionName));

        services.AddHttpClient(GeminiLlmProvider.ProviderName, (sp, c) =>
            {
                var opts = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
                c.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(120);
            })
            .AddStandardResilienceHandler(o =>
            {
                o.Retry.MaxRetryAttempts = 3;
                o.Retry.UseJitter = true;
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(240);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
                o.Retry.ShouldHandle = static args =>
                {
                    if (args.Outcome.Exception is HttpRequestException)
                        return ValueTask.FromResult(true);
                    if (args.Outcome.Result is HttpResponseMessage r)
                    {
                        var code = (int)r.StatusCode;
                        return ValueTask.FromResult(code >= 500 && code < 600);
                    }
                    return ValueTask.FromResult(false);
                };
            });

        services.AddHttpClient(OllamaLlmProvider.ProviderName, (sp, c) =>
        {
            var opts = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            c.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(120);
        });

        services.AddHttpClient(GroqLlmProvider.ProviderName, (sp, c) =>
            {
                var opts = sp.GetRequiredService<IOptions<GroqOptions>>().Value;
                c.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
                c.Timeout = TimeSpan.FromSeconds(60);
            })
            .AddStandardResilienceHandler(o =>
            {
                o.Retry.MaxRetryAttempts = 2;
                o.Retry.UseJitter = true;
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                o.Retry.ShouldHandle = static args =>
                {
                    if (args.Outcome.Exception is HttpRequestException) return ValueTask.FromResult(true);
                    if (args.Outcome.Result is HttpResponseMessage r)
                    {
                        var code = (int)r.StatusCode;
                        return ValueTask.FromResult(code >= 500 && code < 600);
                    }
                    return ValueTask.FromResult(false);
                };
            });

        services.AddSingleton<ILlmProvider, GeminiLlmProvider>();
        services.AddSingleton<ILlmProvider, OllamaLlmProvider>();
        services.AddSingleton<ILlmProvider, GroqLlmProvider>();
        services.AddSingleton<IEmbeddingProvider, GeminiEmbeddingProvider>();
        services.AddSingleton<IEmbeddingProvider, OllamaEmbeddingProvider>();

        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();
        services.AddSingleton<IEmbeddingProviderFactory, EmbeddingProviderFactory>();
    }

    private static void AddDocuments(IServiceCollection services)
    {
        services.AddSingleton<IDocumentParser, PdfDocumentParser>();
        services.AddSingleton<IDocumentParser, PlainTextDocumentParser>();
        services.AddSingleton<IDocumentParserFactory, DocumentParserFactory>();
        services.AddScoped<IResumeAnalyzer, ResumeAnalyzer>();
        services.AddScoped<IJobDescriptionAnalyzer, JobDescriptionAnalyzer>();
        services.AddScoped<IMatchScorer, MatchScorer>();
        services.AddScoped<IQuestionGenerator, QuestionGenerator>();
        services.AddScoped<IAnswerEvaluator, AnswerEvaluator>();
        services.AddScoped<IQuestionBankSelector, QuestionBankSelector>();
        services.AddHostedService<QuestionBankSeeder>();
    }
}

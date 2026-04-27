using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CareerForge.Api.Common;
using CareerForge.Api.Endpoints;
using CareerForge.Api.Validators;
using CareerForge.Infrastructure;
using CareerForge.Infrastructure.Auth;
using CareerForge.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Sentry telemetry is opt-in via configuration. When no DSN is supplied the SDK is not
// initialised and the host runs without any reporting overhead.
var sentryDsn = builder.Configuration["Sentry:Dsn"]
    ?? Environment.GetEnvironmentVariable("SENTRY_DSN");
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = sentryDsn;
        o.Environment = builder.Environment.EnvironmentName;
        o.SendDefaultPii = false;
        o.TracesSampleRate = 0;
        o.MinimumEventLevel = LogLevel.Warning;
    });
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(opts =>
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

// Per-user rate limiting on LLM-backed endpoints. Buckets are partitioned by the JWT
// subject claim; unauthenticated traffic is partitioned into a single shared bucket.
// Limits are configurable via the RateLimits section.
const string LlmHeavyPolicy = "llm-heavy";
const string LlmInteractivePolicy = "llm-interactive";
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.Headers.RetryAfter = "60";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "Too many requests",
            detail = "Rate limit exceeded. Please retry after the indicated interval.",
            status = 429,
        });
    };

    options.AddPolicy(LlmHeavyPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: PartitionKey(ctx),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimits:HeavyPerHour", 10),
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    options.AddPolicy(LlmInteractivePolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: PartitionKey(ctx),
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimits:InteractivePerMinute", 6),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    static string PartitionKey(HttpContext ctx)
    {
        var sub = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? ctx.User.FindFirst("sub")?.Value;
        return sub ?? "anonymous";
    }
});

const string CorsPolicy = "CareerForgeCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition"));
});

var app = builder.Build();

// Migrations are auto-applied in Development for convenience. In other environments
// they must be applied as an out-of-band release step to prevent races between
// concurrently-starting replicas. The behaviour can be overridden via
// Database:AutoMigrate (true / false).
var autoMigrate = app.Configuration.GetValue<bool?>("Database:AutoMigrate")
    ?? app.Environment.IsDevelopment();
if (autoMigrate)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed on startup");
        throw;
    }
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapResumeEndpoints();
app.MapJobDescriptionEndpoints();
app.MapMatchEndpoints();
app.MapInterviewEndpoints();

if (app.Environment.IsDevelopment())
    app.MapLlmTestEndpoints();

app.Run();

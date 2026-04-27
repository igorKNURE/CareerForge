using System.Diagnostics;
using CareerForge.Application.Abstractions.Llm;

namespace CareerForge.Api.Endpoints;

/// <summary>Developer-only endpoints for smoke-testing LLM and embedding providers from the API.</summary>
public static class LlmTestEndpoints
{
    public static IEndpointRouteBuilder MapLlmTestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/llm").WithTags("LLM (dev)");

        group.MapGet("/providers", (ILlmProviderFactory llm, IEmbeddingProviderFactory emb) =>
            Results.Ok(new
            {
                defaultLlm = llm.GetDefault().Name,
                defaultEmbedding = emb.GetDefault().Name,
                llmProviders = llm.AvailableProviders,
                embeddingProviders = emb.AvailableProviders,
            }));

        group.MapPost("/test", async (LlmTestRequest req, ILlmProviderFactory factory, CancellationToken ct) =>
        {
            var provider = string.IsNullOrWhiteSpace(req.Provider) ? factory.GetDefault() : factory.Get(req.Provider);
            var sw = Stopwatch.StartNew();
            var response = await provider.CompleteAsync(new LlmCompletionRequest(
                Model: req.Model ?? string.Empty,
                SystemPrompt: req.SystemPrompt ?? "You are a helpful assistant. Reply concisely.",
                Messages: new[] { new LlmMessage(LlmRole.User, req.Prompt) }), ct);
            sw.Stop();

            return Results.Ok(new LlmTestResponse(
                provider.Name,
                req.Model ?? "(default)",
                response.Content,
                response.PromptTokens,
                response.CompletionTokens,
                sw.ElapsedMilliseconds));
        });

        group.MapPost("/embed-test", async (EmbedTestRequest req, IEmbeddingProviderFactory factory, CancellationToken ct) =>
        {
            var provider = string.IsNullOrWhiteSpace(req.Provider) ? factory.GetDefault() : factory.Get(req.Provider);
            var sw = Stopwatch.StartNew();
            var embedding = await provider.EmbedAsync(req.Text, ct);
            sw.Stop();

            return Results.Ok(new EmbedTestResponse(
                provider.Name,
                provider.Dimensions,
                embedding.Length,
                embedding.Take(5).ToArray(),
                sw.ElapsedMilliseconds));
        });

        return app;
    }

    public sealed record LlmTestRequest(string Prompt, string? Provider = null, string? Model = null, string? SystemPrompt = null);
    public sealed record LlmTestResponse(string Provider, string Model, string Response, int? PromptTokens, int? CompletionTokens, long DurationMs);
    public sealed record EmbedTestRequest(string Text, string? Provider = null);
    public sealed record EmbedTestResponse(string Provider, int ConfiguredDimensions, int ActualDimensions, float[] FirstFiveValues, long DurationMs);
}

using Microsoft.Net.Http.Headers;

namespace CareerForge.Api.Common;

/// <summary>
/// Endpoint extension methods for emitting Cache-Control response headers. All policies
/// are scoped <c>private</c> so per-user responses are never cached by shared
/// intermediaries.
/// </summary>
public static class CacheControlExtensions
{
    /// <summary>
    /// Adds a <c>Cache-Control: private, max-age=&lt;seconds&gt;, must-revalidate</c> header
    /// to successful (2xx) responses on the route.
    /// </summary>
    public static RouteHandlerBuilder WithBrowserCache(this RouteHandlerBuilder builder, int maxAgeSeconds = 30) =>
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            var result = await next(ctx);
            if (ctx.HttpContext.Response.StatusCode is >= 200 and < 300)
            {
                ctx.HttpContext.Response.Headers[HeaderNames.CacheControl] =
                    $"private, max-age={maxAgeSeconds}, must-revalidate";
            }
            return result;
        });
}

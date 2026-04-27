using CareerForge.Api.Common;
using CareerForge.Api.Validation;
using CareerForge.Application.Abstractions.Auth;
using CareerForge.Application.Auth.Dtos;
using CareerForge.Infrastructure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace CareerForge.Api.Endpoints;

/// <summary>Registration, login, refresh / logout, and password / email confirmation endpoints.</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync).WithValidation<RegisterRequest>();
        group.MapPost("/login", LoginAsync).WithValidation<LoginRequest>();
        group.MapPost("/refresh", RefreshAsync).WithValidation<RefreshRequest>();
        group.MapPost("/logout", LogoutAsync).WithValidation<LogoutRequest>();
        group.MapPost("/forgot-password", ForgotPasswordAsync).WithValidation<ForgotPasswordRequest>();
        group.MapPost("/reset-password", ResetPasswordAsync).WithValidation<ResetPasswordRequest>();
        group.MapPost("/email/send-confirm", SendConfirmAsync).WithValidation<SendEmailConfirmRequest>();
        group.MapPost("/email/confirm", ConfirmEmailAsync).WithValidation<ConfirmEmailRequest>();

        group.MapGet("/me", MeAsync).RequireAuthorization();

        return app;
    }

    private static async Task<Results<Ok<MeResponse>, NotFound>> MeAsync(
        HttpContext ctx,
        UserManager<ApplicationUser> userManager,
        CancellationToken ct)
    {
        var userId = ctx.User.GetUserId();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return TypedResults.NotFound();
        return TypedResults.Ok(new MeResponse(user.Id, user.Email ?? string.Empty, user.DisplayName));
    }

    public sealed record DevTokenResponse(string Message, string? DevToken);

    private static async Task<Ok<DevTokenResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        IHostEnvironment env,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        string? devToken = null;
        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            if (env.IsDevelopment()) devToken = token;
        }
        return TypedResults.Ok(new DevTokenResponse(
            "If the account exists, a reset link has been sent.", devToken));
    }

    private static async Task<Results<NoContent, ValidationProblem>> ResetPasswordAsync(
        ResetPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        IRefreshTokenService refresh,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                { ["token"] = new[] { "Invalid email or token" } });

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return TypedResults.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

        await userManager.UpdateSecurityStampAsync(user);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<DevTokenResponse>> SendConfirmAsync(
        SendEmailConfirmRequest request,
        UserManager<ApplicationUser> userManager,
        IHostEnvironment env,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        string? devToken = null;
        if (user is not null && !user.EmailConfirmed)
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            if (env.IsDevelopment()) devToken = token;
        }
        return TypedResults.Ok(new DevTokenResponse(
            "If the account exists and is unconfirmed, a confirmation link has been sent.", devToken));
    }

    private static async Task<Results<NoContent, ValidationProblem>> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        UserManager<ApplicationUser> userManager,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                { ["token"] = new[] { "Invalid email or token" } });

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
            return TypedResults.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<AuthResponse>, ValidationProblem>> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwt,
        IRefreshTokenService refresh,
        CancellationToken ct)
    {
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();
        var user = new ApplicationUser
        {
            Email = request.Email,
            UserName = request.Email,
            DisplayName = displayName,
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
            return TypedResults.ValidationProblem(errors);
        }
        return TypedResults.Ok(await IssueTokensAsync(user, userManager, jwt, refresh, ct));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwt,
        IRefreshTokenService refresh,
        CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            return TypedResults.Unauthorized();
        return TypedResults.Ok(await IssueTokensAsync(user, userManager, jwt, refresh, ct));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> RefreshAsync(
        RefreshRequest request,
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwt,
        IRefreshTokenService refresh,
        CancellationToken ct)
    {
        var rotated = await refresh.RotateAsync(request.RefreshToken, ct);
        if (rotated is null) return TypedResults.Unauthorized();

        var user = await userManager.FindByIdAsync(rotated.UserId.ToString());
        if (user is null) return TypedResults.Unauthorized();

        var roles = await userManager.GetRolesAsync(user);
        var access = jwt.CreateAccessToken(user.Id, user.Email!, roles);
        return TypedResults.Ok(new AuthResponse(access.Token, access.ExpiresAt, rotated.NewToken, rotated.ExpiresAt));
    }

    private static async Task<NoContent> LogoutAsync(
        LogoutRequest request,
        IRefreshTokenService refresh,
        CancellationToken ct)
    {
        await refresh.RevokeAsync(request.RefreshToken, ct);
        return TypedResults.NoContent();
    }

    private static async Task<AuthResponse> IssueTokensAsync(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwt,
        IRefreshTokenService refresh,
        CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(user);
        var access = jwt.CreateAccessToken(user.Id, user.Email!, roles);
        var refreshToken = await refresh.IssueAsync(user.Id, ct);
        return new AuthResponse(access.Token, access.ExpiresAt, refreshToken.Token, refreshToken.ExpiresAt);
    }
}

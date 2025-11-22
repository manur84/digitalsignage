using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using DigitalSignage.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DigitalSignage.Server.Api.Authentication;

/// <summary>
/// Custom authentication handler for mobile app token validation
/// </summary>
public class MobileAppAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IMobileAppService _mobileAppService;

    public MobileAppAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IMobileAppService mobileAppService)
        : base(options, logger, encoder)
    {
        _mobileAppService = mobileAppService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            // Check if Authorization header exists
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                Logger.LogWarning("Missing Authorization header");
                return AuthenticateResult.Fail("Missing Authorization header");
            }

            var authHeader = Request.Headers["Authorization"].ToString();

            // Validate Bearer token format
            if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogWarning("Invalid Authorization header format: {AuthHeader}", authHeader);
                return AuthenticateResult.Fail("Invalid Authorization header format");
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();

            if (string.IsNullOrWhiteSpace(token))
            {
                Logger.LogWarning("Empty token in Authorization header");
                return AuthenticateResult.Fail("Empty token");
            }

            // Validate token using MobileAppService
            var validation = await _mobileAppService.ValidateTokenAsync2(token);

            if (!validation.IsSuccess)
            {
                Logger.LogWarning("Token validation failed: {Error}", validation.ErrorMessage);
                return AuthenticateResult.Fail($"Invalid token: {validation.ErrorMessage}");
            }

            var appId = validation.Value;

            // Create claims for the authenticated user
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, appId.ToString()),
                new Claim("AppId", appId.ToString()),
                new Claim("TokenType", "MobileApp")
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            Logger.LogDebug("Successfully authenticated mobile app {AppId}", appId);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Authentication error");
            return AuthenticateResult.Fail($"Authentication error: {ex.Message}");
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers["WWW-Authenticate"] = "Bearer";
        Response.StatusCode = 401;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Extension methods for adding mobile app authentication
/// </summary>
public static class MobileAppAuthenticationExtensions
{
    public const string SchemeName = "MobileAppScheme";

    public static AuthenticationBuilder AddMobileAppAuthentication(
        this AuthenticationBuilder builder)
    {
        return builder.AddScheme<AuthenticationSchemeOptions, MobileAppAuthenticationHandler>(
            SchemeName,
            options => { });
    }
}

using System.Diagnostics;
using System.Text;
using ColourBricks.Api.Auth;
using ColourBricks.Api.Authorization;
using ColourBricks.Api.Diagnostics;
using ColourBricks.Api.ErrorHandling;
using ColourBricks.Application;
using ColourBricks.Application.Abstractions;
using ColourBricks.Application.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace ColourBricks.Api;

/// <summary>
/// Registers the API cross-cutting concerns from P0-T03: problem-details error
/// handling, the FluentValidation pipeline, CORS and diagnostics support.
/// </summary>
public static class DependencyInjection
{
    public const string CorsPolicyName = "frontend";

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                ctx.ProblemDetails.Extensions.TryAdd(
                    "traceId",
                    Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);
                ctx.ProblemDetails.Instance ??=
                    $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
            };
        });

        // Order matters: specific handlers run before the catch-all.
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<ProjectAccessExceptionHandler>();
        services.AddExceptionHandler<AlreadyAllocatedExceptionHandler>();
        services.AddExceptionHandler<ConcurrencyExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddValidatorsFromAssemblyContaining<DiagnosticPayloadValidator>();
        services.AddValidatorsFromAssemblyContaining<AssemblyMarker>(); // Application layer

        string[] frontendOrigins =
            configuration.GetSection("Cors:FrontendOrigins").Get<string[]>()
            ?? ["http://localhost:3000"];

        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
            .WithOrigins(frontendOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        services.AddSingleton<DiagnosticWriteCounter>();

        return services;
    }

    /// <summary>
    /// JWT bearer auth that reads the access token from the httpOnly <c>cb_access</c>
    /// cookie instead of the Authorization header (plan.md §9).
    /// </summary>
    public static IServiceCollection AddJwtCookieAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        JwtOptions jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured with at least 32 bytes. "
                + "Set it via user-secrets or the environment; appsettings holds only a dev key.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Cookies.TryGetValue(AuthCookies.AccessTokenCookie, out string? token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        // module.action permissions (plan.md §9).
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization();
        return services;
    }
}

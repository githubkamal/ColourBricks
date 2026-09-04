using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ColourBricks.Api;
using ColourBricks.Api.Idempotency;
using ColourBricks.Api.Security;
using ColourBricks.Api.Validation;
using ColourBricks.Infrastructure;
using ColourBricks.Infrastructure.Identity;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging to console + rolling file, with a per-request correlation id
// (plan.md §3, P0-T03). Levels/overrides come from configuration.
builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        "logs/colourbricks-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14));

builder.Services
    .AddControllers(options => options.Filters.Add<FluentValidationFilter>())
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddJwtCookieAuthentication(builder.Configuration);

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "database");

// P9-T03 — rate limit on report export (login is throttled by the dedicated,
// per-account LoginRateLimitMiddleware). Partitioned by client IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("export", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

app.UseSerilogRequestLogging(options =>
    options.EnrichDiagnosticContext = (diagnostic, httpContext) =>
        diagnostic.Set(
            "TraceId",
            System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier));

// RFC 9457 problem+json for everything unhandled (plan.md §7).
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "ColourBricks API v1"));
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseMiddleware<LoginRateLimitMiddleware>();
app.UseCors(ColourBricks.Api.DependencyInjection.CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

// Idempotency-Key enforcement for [Idempotent] endpoints (plan.md §7).
app.UseMiddleware<IdempotencyMiddleware>();

app.MapControllers();

// Pings the database (plan.md P0-T02).
app.MapHealthChecks("/health");

// Seed the RBAC catalogue + the first Administrator (plan.md P0-T04, P0-T05).
await using (var scope = app.Services.CreateAsyncScope())
{
    try
    {
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();
        await scope.ServiceProvider
            .GetRequiredService<ColourBricks.Infrastructure.Persistence.Seeding.ReferenceDataSeeder>()
            .SeedAsync();
    }
    catch (Exception ex)
    {
        // First run before `dotnet ef database update`, or a startup race. Not fatal.
        app.Logger.LogWarning(ex, "Identity seed skipped: the database is not ready.");
    }
}

app.Run();

// Exposed so integration tests (P0-T09) can use WebApplicationFactory<Program>.
public partial class Program;

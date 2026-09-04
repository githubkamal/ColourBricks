using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P0-T03 — API cross-cutting concerns (plan.md §7).</summary>
public sealed class CrossCuttingTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task ExceptionHandler_ReturnsProblemDetails_WithoutStackTrace()
    {
        HttpClient client = Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/_diagnostics/throw");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        string body = await response.Content.ReadAsStringAsync();
        body.Should().NotContainAny("Deliberate diagnostic failure", "stackTrace", "at ColourBricks");

        using JsonDocument doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("traceId", out JsonElement traceId).Should().BeTrue();
        traceId.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Validation_Failure_Returns400_WithFieldErrors()
    {
        HttpClient client = Factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/_diagnostics/validate",
            new { name = "", quantity = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement errors = doc.RootElement.GetProperty("errors");
        errors.TryGetProperty("Name", out _).Should().BeTrue();
        errors.TryGetProperty("Quantity", out _).Should().BeTrue();
    }

    [Fact]
    public async Task IdempotencyKey_Replay_DoesNotDuplicateWrite()
    {
        const string key = "replay-test";
        await ClearKey(key);

        HttpClient client = Factory.CreateClient();

        (string body, bool replayed) first = await PostIdempotent(client, key);
        (string body, bool replayed) second = await PostIdempotent(client, key);
        (string body, bool replayed) third = await PostIdempotent(client, key);

        first.replayed.Should().BeFalse();
        second.replayed.Should().BeTrue();
        third.replayed.Should().BeTrue();
        second.body.Should().Be(first.body);
        third.body.Should().Be(first.body);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        int rows = await db.IdempotencyRecords.CountAsync(r => r.Key == key);
        rows.Should().Be(1);
    }

    [Fact]
    public async Task IdempotencyKey_Missing_Returns400()
    {
        HttpClient client = Factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync("/api/v1/_diagnostics/idempotent-write", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    private async Task ClearKey(string key)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.IdempotencyRecords.Where(r => r.Key == key).ExecuteDeleteAsync();
    }

    private static async Task<(string Body, bool Replayed)> PostIdempotent(HttpClient client, string key)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/_diagnostics/idempotent-write");
        request.Headers.Add("Idempotency-Key", key);

        HttpResponseMessage response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadAsStringAsync(), response.Headers.Contains("Idempotency-Replayed"));
    }
}

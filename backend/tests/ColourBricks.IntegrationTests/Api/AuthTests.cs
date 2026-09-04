using System.Net;
using System.Net.Http.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.Net.Http.Headers;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P0-T04 — Authentication (plan.md §9, BRD §59).</summary>
public sealed class AuthTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private const string Password = "Sup3r!Secret";

    [Fact]
    public async Task Login_WithValidCredentials_SetsHttpOnlyCookies()
    {
        string email = Email();
        await Factory.CreateUserAsync(email, Password);
        HttpClient client = NewClient();

        HttpResponseMessage response = await Login(client, email, Password);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        IList<SetCookieHeaderValue> cookies = SetCookieHeaderValue.ParseList(
            response.Headers.GetValues("Set-Cookie").ToList());

        SetCookieHeaderValue access = cookies.Single(c => c.Name == "cb_access");
        SetCookieHeaderValue refresh = cookies.Single(c => c.Name == "cb_refresh");

        access.HttpOnly.Should().BeTrue();
        access.SameSite.Should().Be(SameSiteMode.Lax);
        refresh.HttpOnly.Should().BeTrue();
        refresh.Value.ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_InactiveUser_Returns401()
    {
        string email = Email();
        await Factory.CreateUserAsync(email, Password, isActive: false);
        HttpClient client = NewClient();

        HttpResponseMessage response = await Login(client, email, Password);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndInvalidatesPrevious()
    {
        string email = Email();
        await Factory.CreateUserAsync(email, Password);
        HttpClient client = NewClient();

        string refresh1 = RefreshCookie(await Login(client, email, Password))!;

        HttpResponseMessage rotated = await Refresh(client, refresh1);
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        string refresh2 = RefreshCookie(rotated)!;
        refresh2.Should().NotBe(refresh1);

        // The consumed token must no longer work.
        HttpResponseMessage replay = await Refresh(client, refresh1);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ReusedToken_RevokesFamily()
    {
        string email = Email();
        long userId = await Factory.CreateUserAsync(email, Password);
        HttpClient client = NewClient();

        string refresh1 = RefreshCookie(await Login(client, email, Password))!;
        string refresh2 = RefreshCookie(await Refresh(client, refresh1))!;

        // Replay the already-consumed refresh1: reuse detection fires.
        (await Refresh(client, refresh1)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // The whole family is now dead, so the still-fresh refresh2 is rejected too.
        (await Refresh(client, refresh2)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (int active, _) = await Factory.RefreshTokenCountsAsync(userId);
        active.Should().Be(0);
    }

    [Fact]
    public async Task Login_AfterNFailures_LocksAccount()
    {
        string email = Email();
        await Factory.CreateUserAsync(email, Password);
        HttpClient client = NewClient();

        // Auth:MaxFailedAttempts is 3 in the test Factory.
        for (int i = 0; i < 3; i++)
        {
            (await Login(client, email, "wrong-password")).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized);
        }

        // Even the correct password is refused while the lockout stands.
        HttpResponseMessage locked = await Login(client, email, Password);
        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        string body = await locked.Content.ReadAsStringAsync();
        body.Should().Contain("locked");
    }

    private HttpClient NewClient() =>
        Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false,
        });

    private static Task<HttpResponseMessage> Login(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    private static Task<HttpResponseMessage> Refresh(HttpClient client, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"cb_refresh={refreshToken}");
        return client.SendAsync(request);
    }

    private static string? RefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? SetCookieHeaderValue.ParseList(values.ToList())
                .FirstOrDefault(c => c.Name == "cb_refresh" && c.Value.Length > 0)
                ?.Value.ToString()
            : null;

    private static string Email() => $"authtest+{Guid.NewGuid():N}@colourbricks.test";
}

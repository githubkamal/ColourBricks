using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Domain.Ledger;
using ColourBricks.Domain.Parties;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P1-T04 — Subcontractor/team master (BRD §8, §9, §70 rules 8, 9).</summary>
public sealed class TeamTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    /// <summary>Resolves a department by name, creating it if it is not already seeded.</summary>
    private async Task<long> EnsureDepartment(HttpClient client, string name)
    {
        using JsonDocument list = JsonDocument.Parse(
            await (await client.GetAsync("/api/v1/departments?includeInactive=true")).Content.ReadAsStringAsync());
        foreach (JsonElement d in list.RootElement.EnumerateArray())
        {
            if (string.Equals(d.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase))
            {
                return d.GetProperty("id").GetInt64();
            }
        }

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/departments", new { name });
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetInt64();
    }

    private async Task<long> CreateTeam(HttpClient client, string name, long departmentId)
    {
        // confirm=true: "Electrical Team A/B/C" are near-duplicates of each other by
        // edit distance but are genuinely distinct crews (BRD §8).
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/teams?confirm=true", new { name, departmentId });
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetInt64();
    }

    private async Task<long> CreateProject(HttpClient client, string name, string code) =>
        JsonDocument.Parse(await (await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name,
            code,
            status = "Ongoing",
            startDate = "2026-04-01",
            expectedEndDate = "2027-03-31",
            contractValue = 5_000_000m,
            estimatedCost = 4_000_000m,
        })).Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt64();

    [Fact]
    public async Task Team_CanBeLinkedToMultipleProjects_ViaWorkEntries()
    {
        HttpClient client = Client;
        long dept = await EnsureDepartment(client, "Electrical");
        long teamId = await CreateTeam(client, "Electrical Team A", dept);
        long projectA = await CreateProject(client, "Tower A", "CB-2026-801");
        long projectB = await CreateProject(client, "Tower B", "CB-2026-802");

        // The team itself carries no project link (BRD rule 9) — the association is
        // made only through transaction records.
        typeof(Party).GetProperty("ProjectId").Should().BeNull();

        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.LedgerEntries.AddRange(
                Work(teamId, projectA),
                Work(teamId, projectB));
            await db.SaveChangesAsync();
        }

        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            List<long?> projects = await db.LedgerEntries
                .Where(l => l.PartyId == teamId)
                .Select(l => l.ProjectId)
                .Distinct()
                .ToListAsync();

            projects.Should().BeEquivalentTo(new long?[] { projectA, projectB });
        }
    }

    [Fact]
    public async Task ReassignTeam_ToAnotherDepartment_WritesAuditRow()
    {
        HttpClient client = Client;
        long electrical = await EnsureDepartment(client, "Electrical");
        long plumbing = await EnsureDepartment(client, "Plumbing");
        long teamId = await CreateTeam(client, "Crew Nine", electrical);

        HttpResponseMessage get = await client.GetAsync($"/api/v1/teams/{teamId}");
        string stamp = JsonDocument.Parse(await get.Content.ReadAsStringAsync())
            .RootElement.GetProperty("concurrencyStamp").GetString()!;

        HttpResponseMessage moved = await client.PutAsJsonAsync(
            $"/api/v1/teams/{teamId}",
            new { name = "Crew Nine", departmentId = plumbing, isActive = true, concurrencyStamp = stamp });
        moved.EnsureSuccessStatusCode();

        JsonDocument.Parse(await (await client.GetAsync($"/api/v1/teams/{teamId}")).Content.ReadAsStringAsync())
            .RootElement.GetProperty("departmentName").GetString().Should().Be("Plumbing");

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        bool audited = await db.AuditLogs.AnyAsync(a =>
            a.EntityType == "Party"
            && a.RecordId == teamId.ToString()
            && a.Action == "update"
            && a.NewValues!.Contains("DepartmentId"));
        audited.Should().BeTrue();
    }

    [Fact]
    public async Task ListTeams_GroupsByDepartment()
    {
        HttpClient client = Client;
        long electrical = await EnsureDepartment(client, "Electrical");
        long plumbing = await EnsureDepartment(client, "Plumbing");
        await CreateTeam(client, "Electrical Team A", electrical);
        await CreateTeam(client, "Electrical Team B", electrical);
        await CreateTeam(client, "Plumbing Team A", plumbing);

        HttpResponseMessage response = await client.GetAsync("/api/v1/teams");
        response.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var byDept = doc.RootElement.EnumerateArray()
            .GroupBy(t => t.GetProperty("departmentName").GetString())
            .ToDictionary(g => g.Key!, g => g.Count());

        byDept["Electrical"].Should().Be(2);
        byDept["Plumbing"].Should().Be(1);
    }

    private static LedgerEntry Work(long teamId, long projectId) => new()
    {
        EntryDate = new DateOnly(2026, 5, 1),
        ProjectId = projectId,
        PartyId = teamId,
        CategoryId = 1,
        Debit = 25_000m,
        Credit = 0m,
        SourceType = "LabourWork",
        SourceId = 0,
    };
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>
/// P10-T03 — field officers (client request, 2026-09-04): a running, bidirectional
/// balance reusing the vendor payable/advance machinery. Project purchases go through
/// the ordinary vendor-purchases endpoint (no new code); these tests cover the new
/// no-project bill and its netting against an advance.
/// </summary>
public sealed class FieldOfficerExpenseTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 1900;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> FieldOfficer(HttpClient c, string name) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/parties?confirm=true",
            new { name, types = new[] { "FieldOfficer" } })))
        .GetProperty("id").GetInt64();

    private async Task<long> Vendor(HttpClient c, string name) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/parties?confirm=true",
            new { name, types = new[] { "Vendor" } })))
        .GetProperty("id").GetInt64();

    private async Task<long> Project(HttpClient c, string name) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name, code = $"CB-2026-{++_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 10_000_000m, estimatedCost = 8_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> Mode(HttpClient c, string name) =>
        (await Json(await c.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<decimal> OutstandingSummaryTotal(HttpClient c, long partyId) =>
        (await Json(await c.GetAsync($"/api/v1/vendors/{partyId}/outstanding-summary")))
        .GetProperty("total").GetDecimal();

    private async Task<decimal> AdvanceOf(HttpClient c, long partyId) =>
        (await Json(await c.GetAsync($"/api/v1/vendors/{partyId}/outstanding-summary")))
        .GetProperty("advance").GetDecimal();

    private async Task<JsonElement> RecordBill(
        HttpClient c, long officerId, string type, decimal amount, string description = "Bill") =>
        await Json(await c.PostAsJsonAsync("/api/v1/field-officer-expenses", new
        {
            fieldOfficerId = officerId, type, date = "2026-08-10", amount, description,
        }));

    [Fact]
    public async Task FieldOfficerBill_NoProject_ShowsInOutstandingTotal_AndCompanySummary()
    {
        HttpClient c = Admin;
        long officer = await FieldOfficer(c, "FO Basic");
        decimal companyCustomBefore = (await Json(await c.GetAsync("/api/v1/common-expenses/summary")))
            .GetProperty("custom").GetDecimal();

        JsonElement bill = await RecordBill(c, officer, "Custom", 12_000m, "Cement for office shed");
        bill.GetProperty("status").GetString().Should().Be("Active");

        (await OutstandingSummaryTotal(c, officer)).Should().Be(12_000m);
        (await Json(await c.GetAsync("/api/v1/common-expenses/summary")))
            .GetProperty("custom").GetDecimal().Should().Be(companyCustomBefore + 12_000m);
    }

    [Fact]
    public async Task FieldOfficerBill_RejectsPartyThatIsNotAFieldOfficer()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c, "Not An Officer");

        HttpResponseMessage r = await c.PostAsJsonAsync("/api/v1/field-officer-expenses", new
        {
            fieldOfficerId = vendor, type = "Personal", date = "2026-08-10", amount = 1000m,
        });

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FieldOfficerAdvance_ThenNoProjectBill_NetsAutomatically()
    {
        HttpClient c = Admin;
        long officer = await FieldOfficer(c, "FO Advance");
        long cash = await Mode(c, "Cash");

        // Give him 20k float before any bill -> becomes a pure advance (no outstanding yet).
        (await c.PostAsJsonAsync("/api/v1/vendor-payments/allocate", new
        {
            vendorId = officer, date = "2026-08-01", amount = 20_000m, paymentModeId = cash,
        })).EnsureSuccessStatusCode();

        (await AdvanceOf(c, officer)).Should().Be(20_000m);
        (await OutstandingSummaryTotal(c, officer)).Should().Be(0m);

        // He submits a 12k Personal bill against the float he's holding.
        await RecordBill(c, officer, "Personal", 12_000m, "Groceries for site canteen");

        // The advance and the new payable are the same project-less ledger balance,
        // so they net to a smaller advance automatically -- no manual "apply" step.
        (await AdvanceOf(c, officer)).Should().Be(8_000m);
        (await OutstandingSummaryTotal(c, officer)).Should().Be(0m);
    }

    [Fact]
    public async Task FieldOfficerBill_ExceedsAdvance_LeavesRemainderAsOutstandingPayable()
    {
        HttpClient c = Admin;
        long officer = await FieldOfficer(c, "FO Overspend");
        long cash = await Mode(c, "Cash");

        (await c.PostAsJsonAsync("/api/v1/vendor-payments/allocate", new
        {
            vendorId = officer, date = "2026-08-01", amount = 5_000m, paymentModeId = cash,
        })).EnsureSuccessStatusCode();

        await RecordBill(c, officer, "Office", 18_000m, "Office supplies");

        // 5k advance absorbed; 13k remains genuinely owed to him.
        (await AdvanceOf(c, officer)).Should().Be(0m);
        (await OutstandingSummaryTotal(c, officer)).Should().Be(13_000m);
    }

    [Fact]
    public async Task FieldOfficerCanBuyForAProject_ThroughTheOrdinaryVendorPurchaseEndpoint()
    {
        HttpClient c = Admin;
        long officer = await FieldOfficer(c, "FO Project Buyer");
        long project = await Project(c, "FO Project");

        (await c.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId = project, vendorId = officer, date = "2026-08-05", total = 45_000m,
            lines = new[] { new { itemName = "Bricks", quantity = 1000m, unit = "Nos", rate = 45m, taxAmount = 0m } },
        })).EnsureSuccessStatusCode();

        (await OutstandingSummaryTotal(c, officer)).Should().Be(45_000m);
        (await Json(await c.GetAsync($"/api/v1/vendors/{officer}/outstanding-summary")))
            .GetProperty("byProject")[0].GetProperty("outstanding").GetDecimal().Should().Be(45_000m);
    }

    [Fact]
    public async Task FieldOfficerBill_Reversed_RestoresOutstanding()
    {
        HttpClient c = Admin;
        long officer = await FieldOfficer(c, "FO Reverse");

        JsonElement bill = await RecordBill(c, officer, "Savings", 7_000m, "Wrong entry");
        long billId = bill.GetProperty("id").GetInt64();
        (await OutstandingSummaryTotal(c, officer)).Should().Be(7_000m);

        (await c.PostAsJsonAsync($"/api/v1/field-officer-expenses/{billId}/reverse",
            new { reason = "Entered against wrong officer" })).EnsureSuccessStatusCode();

        (await OutstandingSummaryTotal(c, officer)).Should().Be(0m);
    }

    [Fact]
    public async Task MapDebit_FieldOfficerAdvancePlusCustomExpense_PostsBothTargets()
    {
        HttpClient c = Admin;
        long hdfc = (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true")))
            .EnumerateArray().First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();
        long officer = await FieldOfficer(c, "FO Bank Map");

        long tx = await SeedBankTx(hdfc, 30_000m, "NEFT DR FIELD OFFICER FLOAT");

        JsonElement res = await Json(await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/map-debit", new
        {
            allocations = new[]
            {
                new { target = "FieldOfficer", amount = 20_000m, description = "Float for site work", vendorId = (long?)officer, projectId = (long?)null },
                new { target = "Custom", amount = 10_000m, description = "Misc company spend", vendorId = (long?)null, projectId = (long?)null },
            },
        }));

        res.GetProperty("status").GetString().Should().Be("Reconciled");
        (await AdvanceOf(c, officer)).Should().Be(20_000m);
    }

    private async Task<long> SeedBankTx(long accountId, decimal debit, string narration)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ColourBricks.Infrastructure.Persistence.AppDbContext>();
        var batch = new ColourBricks.Domain.Banking.ImportBatch
        {
            AccountId = accountId, FileName = "seed.csv", Status = ColourBricks.Domain.Banking.ImportBatchStatus.Committed,
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        var tx = new ColourBricks.Domain.Banking.BankTransaction
        {
            ImportBatchId = batch.Id, AccountId = accountId, ValueDate = DateOnly.Parse("2026-08-12"),
            Narration = narration, NormalisedNarration = narration.ToUpperInvariant(),
            Debit = debit, Credit = 0m, BankReference = $"FO-{Guid.NewGuid():n}",
            RowHash = Guid.NewGuid().ToString("n"), Status = ColourBricks.Domain.Banking.BankTransactionStatus.Pending,
        };
        db.BankTransactions.Add(tx);
        await db.SaveChangesAsync();
        return tx.Id;
    }
}

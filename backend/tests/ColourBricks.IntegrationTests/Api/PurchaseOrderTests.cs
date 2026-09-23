using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>
/// Client request (2026-09-04): draft a vendor purchase order across projects,
/// price it against the vendor's invoice at submit, and confirm it reaches project
/// expenses and vendor outstanding exactly like an ordinary vendor purchase.
/// </summary>
public sealed class PurchaseOrderTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 4600;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"PO Project {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 9_000_000m, estimatedCost = 7_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> Vendor(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/parties?confirm=true",
            new { name = $"PO Vendor {++_seq}", types = new[] { "Vendor" } }))).GetProperty("id").GetInt64();

    private async Task<long> Cash(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/payment-modes"))).EnumerateArray()
        .First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();

    private async Task<long> Bank(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true"))).EnumerateArray()
        .First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();

    private async Task<decimal> ActualCost(HttpClient c, long projectId) =>
        (await Json(await c.GetAsync($"/api/v1/projects/{projectId}/budget-vs-actual")))
        .GetProperty("actualCost").GetDecimal();

    private async Task<decimal> VendorOutstanding(HttpClient c, long vendorId) =>
        (await Json(await c.GetAsync($"/api/v1/vendors/{vendorId}/outstanding-summary")))
        .GetProperty("total").GetDecimal();

    private async Task<JsonElement> CreateDraft(HttpClient c, long vendorId, params (long ProjectId, decimal Qty)[] lines) =>
        await Json(await c.PostAsJsonAsync("/api/v1/purchase-orders", new
        {
            vendorId,
            orderDate = "2026-08-01",
            lines = lines.Select(l => new
            {
                projectId = l.ProjectId, itemName = "Cement", quantity = l.Qty, unit = "Bag",
            }),
        }));

    [Fact]
    public async Task CreatePurchaseOrder_AsDraft_HasNoLedgerImpact()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long p1 = await Project(c);
        long p2 = await Project(c);

        decimal p1Before = await ActualCost(c, p1);
        decimal vendorBefore = await VendorOutstanding(c, vendor);

        JsonElement po = await CreateDraft(c, vendor, (p1, 10m), (p2, 5m));

        po.GetProperty("status").GetString().Should().Be("Draft");
        po.GetProperty("poNumber").GetString().Should().StartWith("PO-");
        po.GetProperty("total").GetDecimal().Should().Be(0m); // nothing priced yet
        po.GetProperty("lines").EnumerateArray().Should().OnlyContain(l =>
            l.GetProperty("rate").ValueKind == JsonValueKind.Null
            && l.GetProperty("lineTotal").ValueKind == JsonValueKind.Null);

        (await ActualCost(c, p1)).Should().Be(p1Before);
        (await VendorOutstanding(c, vendor)).Should().Be(vendorBefore);
    }

    [Fact]
    public async Task SubmitPurchaseOrder_SplitsByProject_PostsToEachProjectAndVendorOutstanding()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long p1 = await Project(c);
        long p2 = await Project(c);
        decimal p1Before = await ActualCost(c, p1);
        decimal p2Before = await ActualCost(c, p2);
        decimal vendorBefore = await VendorOutstanding(c, vendor);

        long poId = (await CreateDraft(c, vendor, (p1, 10m), (p2, 5m))).GetProperty("id").GetInt64();
        JsonElement afterCreate = await Json(await c.GetAsync($"/api/v1/purchase-orders/{poId}"));
        var lineIds = afterCreate.GetProperty("lines").EnumerateArray()
            .ToDictionary(l => l.GetProperty("projectId").GetInt64(), l => l.GetProperty("id").GetInt64());

        JsonElement submitted = await Json(await c.PostAsJsonAsync($"/api/v1/purchase-orders/{poId}/submit", new
        {
            invoiceNumber = $"INV-{poId}",
            lines = new[]
            {
                new { lineId = lineIds[p1], quantity = 10m, rate = 400m, taxAmount = 0m },  // 4,000
                new { lineId = lineIds[p2], quantity = 5m, rate = 400m, taxAmount = 100m }, // 2,100
            },
        }));

        submitted.GetProperty("status").GetString().Should().Be("Submitted");
        submitted.GetProperty("total").GetDecimal().Should().Be(6_100m);
        submitted.GetProperty("obligationIds").GetArrayLength().Should().Be(2);

        (await ActualCost(c, p1)).Should().Be(p1Before + 4_000m);
        (await ActualCost(c, p2)).Should().Be(p2Before + 2_100m);
        (await VendorOutstanding(c, vendor)).Should().Be(vendorBefore + 6_100m);
    }

    [Fact]
    public async Task SubmitPurchaseOrder_AppearsInProjectExpenseReport()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long p1 = await Project(c);
        long poId = (await CreateDraft(c, vendor, (p1, 3m))).GetProperty("id").GetInt64();
        long lineId = (await Json(await c.GetAsync($"/api/v1/purchase-orders/{poId}")))
            .GetProperty("lines").EnumerateArray().First().GetProperty("id").GetInt64();

        await c.PostAsJsonAsync($"/api/v1/purchase-orders/{poId}/submit", new
        {
            invoiceNumber = $"INV-{poId}",
            lines = new[] { new { lineId, quantity = 3m, rate = 1_000m, taxAmount = 0m } },
        });

        JsonElement report = await Json(await c.GetAsync(
            $"/api/v1/reports/run/project-expense?projectId={p1}&datePreset=ThisFinancialYear&pageSize=200"));
        (report.GetProperty("totals").GetProperty("debit").GetDecimal()
            - report.GetProperty("totals").GetProperty("credit").GetDecimal())
            .Should().Be(3_000m);
    }

    [Fact]
    public async Task SubmitPurchaseOrder_CanCorrectQuantityAgainstTheInvoice()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long p1 = await Project(c);
        long poId = (await CreateDraft(c, vendor, (p1, 10m))).GetProperty("id").GetInt64();
        long lineId = (await Json(await c.GetAsync($"/api/v1/purchase-orders/{poId}")))
            .GetProperty("lines").EnumerateArray().First().GetProperty("id").GetInt64();

        JsonElement submitted = await Json(await c.PostAsJsonAsync($"/api/v1/purchase-orders/{poId}/submit", new
        {
            invoiceNumber = $"INV-{poId}",
            lines = new[] { new { lineId, quantity = 8m, rate = 500m, taxAmount = 0m } }, // invoice says 8, not 10
        }));

        JsonElement line = submitted.GetProperty("lines").EnumerateArray().First();
        line.GetProperty("quantity").GetDecimal().Should().Be(8m);
        submitted.GetProperty("total").GetDecimal().Should().Be(4_000m);
    }

    [Fact]
    public async Task SubmitPurchaseOrder_RequiresEveryLinePriced_Returns400()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long p1 = await Project(c);
        long p2 = await Project(c);
        long poId = (await CreateDraft(c, vendor, (p1, 2m), (p2, 2m))).GetProperty("id").GetInt64();
        long firstLineId = (await Json(await c.GetAsync($"/api/v1/purchase-orders/{poId}")))
            .GetProperty("lines").EnumerateArray().First().GetProperty("id").GetInt64();

        HttpResponseMessage response = await c.PostAsJsonAsync($"/api/v1/purchase-orders/{poId}/submit", new
        {
            invoiceNumber = "INV-partial",
            lines = new[] { new { lineId = firstLineId, quantity = 2m, rate = 100m, taxAmount = 0m } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DraftPurchaseOrder_CanBeEditedThenCancelled_NoObligationsCreated()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long p1 = await Project(c);
        long p2 = await Project(c);
        JsonElement po = await CreateDraft(c, vendor, (p1, 4m));

        JsonElement edited = await Json(await c.PutAsJsonAsync($"/api/v1/purchase-orders/{po.GetProperty("id").GetInt64()}", new
        {
            orderDate = "2026-08-05",
            concurrencyStamp = po.GetProperty("concurrencyStamp").GetString(),
            lines = new[]
            {
                new { projectId = p1, itemName = "Cement", quantity = 4m, unit = "Bag" },
                new { projectId = p2, itemName = "Sand", quantity = 2m, unit = "Ton" },
            },
        }));
        edited.GetProperty("lines").GetArrayLength().Should().Be(2);

        (await c.PostAsync($"/api/v1/purchase-orders/{po.GetProperty("id").GetInt64()}/cancel", null))
            .EnsureSuccessStatusCode();

        JsonElement cancelled = await Json(await c.GetAsync($"/api/v1/purchase-orders/{po.GetProperty("id").GetInt64()}"));
        cancelled.GetProperty("status").GetString().Should().Be("Cancelled");
        cancelled.GetProperty("obligationIds").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task BankPayment_ReducesVendorOutstanding_AfterPoSubmit()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long p1 = await Project(c);
        long cash = await Cash(c);
        long bank = await Bank(c);
        decimal vendorBefore = await VendorOutstanding(c, vendor);

        long poId = (await CreateDraft(c, vendor, (p1, 10m))).GetProperty("id").GetInt64();
        long lineId = (await Json(await c.GetAsync($"/api/v1/purchase-orders/{poId}")))
            .GetProperty("lines").EnumerateArray().First().GetProperty("id").GetInt64();
        await c.PostAsJsonAsync($"/api/v1/purchase-orders/{poId}/submit", new
        {
            invoiceNumber = $"INV-{poId}",
            lines = new[] { new { lineId, quantity = 10m, rate = 1_000m, taxAmount = 0m } }, // 10,000
        });

        (await VendorOutstanding(c, vendor)).Should().Be(vendorBefore + 10_000m);

        (await c.PostAsJsonAsync("/api/v1/vendor-payments", new
        {
            vendorId = vendor, projectId = p1, date = "2026-08-20", amount = 6_000m,
            paymentModeId = cash, accountId = bank,
        })).EnsureSuccessStatusCode();

        // The bank-settled payment reduces outstanding; the remainder is what's still owed.
        (await VendorOutstanding(c, vendor)).Should().Be(vendorBefore + 4_000m);
    }

    // ── Other charges and round-off (client request, 2026-09-23) ─────────────

    [Fact]
    public async Task SubmitPurchaseOrder_OtherChargesAndRoundOff_ReachTheProjectAndVendorOutstanding()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long p1 = await Project(c);
        decimal p1Before = await ActualCost(c, p1);
        decimal vendorBefore = await VendorOutstanding(c, vendor);

        long poId = (await CreateDraft(c, vendor, (p1, 10m))).GetProperty("id").GetInt64();
        long lineId = (await Json(await c.GetAsync($"/api/v1/purchase-orders/{poId}")))
            .GetProperty("lines").EnumerateArray().First().GetProperty("id").GetInt64();

        JsonElement submitted = await Json(await c.PostAsJsonAsync($"/api/v1/purchase-orders/{poId}/submit", new
        {
            invoiceNumber = $"INV-{poId}",
            lines = new[] { new { lineId, quantity = 10m, rate = 400m, taxAmount = 0m } }, // 4,000
            charges = new object[]
            {
                new { chargeType = "Transport", amount = 500m, taxType = "Percentage", taxRate = 18m }, // 590
                new { chargeType = "Handling", amount = 200m, taxAmount = 0m },                         // 200
            },
            roundOff = -0.4m,
        }));

        submitted.GetProperty("chargesSubtotal").GetDecimal().Should().Be(700m);
        submitted.GetProperty("chargesTax").GetDecimal().Should().Be(90m);
        submitted.GetProperty("roundOff").GetDecimal().Should().Be(-0.4m);
        submitted.GetProperty("total").GetDecimal().Should().Be(4_789.6m);
        submitted.GetProperty("charges").GetArrayLength().Should().Be(2);

        // Charges and the round-off are real cost, not decoration — they land on the
        // project's expenses and the vendor's outstanding like the goods do.
        (await ActualCost(c, p1)).Should().Be(p1Before + 4_789.6m);
        (await VendorOutstanding(c, vendor)).Should().Be(vendorBefore + 4_789.6m);
    }

    [Fact]
    public async Task SubmitPurchaseOrder_OtherCharges_SplitAcrossProjectsByItemValue()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long p1 = await Project(c);
        long p2 = await Project(c);
        decimal p1Before = await ActualCost(c, p1);
        decimal p2Before = await ActualCost(c, p2);

        long poId = (await CreateDraft(c, vendor, (p1, 30m), (p2, 10m))).GetProperty("id").GetInt64();
        var lineIds = (await Json(await c.GetAsync($"/api/v1/purchase-orders/{poId}")))
            .GetProperty("lines").EnumerateArray()
            .ToDictionary(l => l.GetProperty("projectId").GetInt64(), l => l.GetProperty("id").GetInt64());

        JsonElement submitted = await Json(await c.PostAsJsonAsync($"/api/v1/purchase-orders/{poId}/submit", new
        {
            invoiceNumber = $"INV-{poId}",
            lines = new[]
            {
                new { lineId = lineIds[p1], quantity = 30m, rate = 100m, taxAmount = 0m }, // 3,000 — 75%
                new { lineId = lineIds[p2], quantity = 10m, rate = 100m, taxAmount = 0m }, // 1,000 — 25%
            },
            charges = new object[] { new { chargeType = "Transport", amount = 400m, taxAmount = 0m } },
        }));

        submitted.GetProperty("total").GetDecimal().Should().Be(4_400m);

        // The ₹400 transport follows the goods: 75/25, and the two shares add back up.
        (await ActualCost(c, p1)).Should().Be(p1Before + 3_300m);
        (await ActualCost(c, p2)).Should().Be(p2Before + 1_100m);
    }

    [Fact]
    public async Task SubmitPurchaseOrder_WithoutChargesOrRoundOff_IsUnchanged()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long p1 = await Project(c);
        decimal p1Before = await ActualCost(c, p1);

        long poId = (await CreateDraft(c, vendor, (p1, 4m))).GetProperty("id").GetInt64();
        long lineId = (await Json(await c.GetAsync($"/api/v1/purchase-orders/{poId}")))
            .GetProperty("lines").EnumerateArray().First().GetProperty("id").GetInt64();

        JsonElement submitted = await Json(await c.PostAsJsonAsync($"/api/v1/purchase-orders/{poId}/submit", new
        {
            invoiceNumber = $"INV-{poId}",
            lines = new[] { new { lineId, quantity = 4m, rate = 250m, taxAmount = 0m } },
        }));

        submitted.GetProperty("charges").GetArrayLength().Should().Be(0);
        submitted.GetProperty("roundOff").GetDecimal().Should().Be(0m);
        submitted.GetProperty("total").GetDecimal().Should().Be(1_000m);
        (await ActualCost(c, p1)).Should().Be(p1Before + 1_000m);
    }

    [Fact]
    public async Task SubmitPurchaseOrder_NegativeCharge_Returns400()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long p1 = await Project(c);
        long poId = (await CreateDraft(c, vendor, (p1, 4m))).GetProperty("id").GetInt64();
        long lineId = (await Json(await c.GetAsync($"/api/v1/purchase-orders/{poId}")))
            .GetProperty("lines").EnumerateArray().First().GetProperty("id").GetInt64();

        HttpResponseMessage response = await c.PostAsJsonAsync($"/api/v1/purchase-orders/{poId}/submit", new
        {
            invoiceNumber = $"INV-{poId}",
            lines = new[] { new { lineId, quantity = 4m, rate = 250m, taxAmount = 0m } },
            charges = new object[] { new { chargeType = "Transport", amount = -50m, taxAmount = 0m } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitPurchaseOrder_RoundOffCancellingTheWholeOrder_Returns400()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long p1 = await Project(c);
        long poId = (await CreateDraft(c, vendor, (p1, 4m))).GetProperty("id").GetInt64();
        long lineId = (await Json(await c.GetAsync($"/api/v1/purchase-orders/{poId}")))
            .GetProperty("lines").EnumerateArray().First().GetProperty("id").GetInt64();

        HttpResponseMessage response = await c.PostAsJsonAsync($"/api/v1/purchase-orders/{poId}/submit", new
        {
            invoiceNumber = $"INV-{poId}",
            lines = new[] { new { lineId, quantity = 4m, rate = 250m, taxAmount = 0m } }, // 1,000
            roundOff = -1_000m,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProjectLedger_VendorPurchaseFromAPo_CarriesThatOrdersId()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long p1 = await Project(c);
        long poId = (await CreateDraft(c, vendor, (p1, 5m))).GetProperty("id").GetInt64();
        long lineId = (await Json(await c.GetAsync($"/api/v1/purchase-orders/{poId}")))
            .GetProperty("lines").EnumerateArray().First().GetProperty("id").GetInt64();

        await c.PostAsJsonAsync($"/api/v1/purchase-orders/{poId}/submit", new
        {
            invoiceNumber = $"INV-{poId}",
            lines = new[] { new { lineId, quantity = 5m, rate = 200m, taxAmount = 0m } },
        });

        JsonElement ledger = await Json(await c.GetAsync(
            $"/api/v1/projects/{p1}/financial-ledger?dateFrom=2026-04-01&dateTo=2027-03-31"));

        // The ledger row for the purchase points back at the order it came from, so
        // the UI can offer "View PO" without a second lookup.
        ledger.GetProperty("lines").EnumerateArray()
            .Where(l => l.GetProperty("sourceType").GetString() == "VendorPurchase")
            .Should().Contain(l => l.GetProperty("purchaseOrderId").GetInt64() == poId);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Domain.Banking;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P4-T06 / P4-T07 / P4-T09 — credit &amp; debit reconciliation and unreconcile.</summary>
public sealed class BankReconcileTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 1300;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c, string name) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name, code = $"CB-2026-{++_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 10_000_000m, estimatedCost = 8_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> Vendor(HttpClient c, string name) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/parties?confirm=true", new { name, types = new[] { "Vendor" } })))
        .GetProperty("id").GetInt64();

    private async Task<long> Client_(HttpClient c, string name) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/parties?confirm=true", new { name, types = new[] { "Client" } })))
        .GetProperty("id").GetInt64();

    private async Task<long> Mode(HttpClient c, string name) =>
        (await Json(await c.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<long> Account(HttpClient c, string name) =>
        (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<decimal> Balance(HttpClient c, long acct) =>
        (await Json(await c.GetAsync($"/api/v1/accounts/{acct}"))).GetProperty("balance").GetDecimal();

    private async Task<decimal> VendorOutstanding(HttpClient c, long v) =>
        (await Json(await c.GetAsync($"/api/v1/vendors/{v}/outstanding"))).GetProperty("outstanding").GetDecimal();

    private async Task<decimal> ProjectIncome(HttpClient c, long p) =>
        (await Json(await c.GetAsync($"/api/v1/projects/{p}/income-total"))).GetProperty("total").GetDecimal();

    private async Task Purchase(HttpClient c, long project, long vendor, decimal amount, string date = "2026-05-01") =>
        (await c.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId = project, vendorId = vendor, date, total = amount,
            lines = new[] { new { itemName = "X", quantity = 1m, unit = "Nos", rate = amount, taxAmount = 0m } },
        })).EnsureSuccessStatusCode();

    private async Task<long> SeedBankTx(long accountId, decimal debit, decimal credit, string narration, string? reference, string date = "2026-05-10")
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = new ImportBatch { AccountId = accountId, FileName = "seed.csv", Status = ImportBatchStatus.Committed };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        var tx = new BankTransaction
        {
            ImportBatchId = batch.Id, AccountId = accountId, ValueDate = DateOnly.Parse(date),
            Narration = narration, NormalisedNarration = narration.ToUpperInvariant(),
            Debit = debit, Credit = credit, BankReference = reference,
            RowHash = Guid.NewGuid().ToString("n"), Status = BankTransactionStatus.Pending,
        };
        db.BankTransactions.Add(tx);
        await db.SaveChangesAsync();
        return tx.Id;
    }

    private async Task<BankTransactionStatus> TxStatus(long id)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.BankTransactions.Where(t => t.Id == id).Select(t => t.Status).FirstAsync();
    }

    // ── P4-T06 credit ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreditReconcile_BrdSection34Example()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long project = await Project(c, "S34 Project A");
        long client = await Client_(c, "ABC Builders");
        long tx = await SeedBankTx(hdfc, 0m, 500_000m, "RTGS ABC BUILDERS ADVANCE", "R500");

        JsonElement res = await Json(await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-credit",
            new { clientId = client, projectId = project, incomeType = "ClientAdvance" }));

        res.GetProperty("settlementCreated").GetBoolean().Should().BeTrue();
        (await ProjectIncome(c, project)).Should().Be(500_000m);
        (await TxStatus(tx)).Should().Be(BankTransactionStatus.Reconciled);
    }

    [Fact]
    public async Task CreditReconcile_MultiProject_Returns400()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long a = await Project(c, "Split A");
        long b = await Project(c, "Split B");
        long client = await Client_(c, "Split Client");
        long tx = await SeedBankTx(hdfc, 0m, 500_000m, "RTGS CLIENT", "R1");

        // projectId as an array is not a shape the endpoint accepts.
        HttpResponseMessage r = await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-credit",
            new { clientId = client, projectId = new[] { a, b } });

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreditReconcile_ToExistingReceipt_DoesNotDuplicate()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long mode = await Mode(c, "Bank Transfer");
        long project = await Project(c, "Existing Receipt P");
        long client = await Client_(c, "Existing Receipt Client");

        long receiptId = (await Json(await c.PostAsJsonAsync("/api/v1/receipts", new
        {
            projectId = project, type = "ClientAdvance", date = "2026-05-05", amount = 300_000m,
            paymentModeId = mode, accountId = hdfc, referenceNo = "R300",
        }))).GetProperty("id").GetInt64();

        decimal incomeBefore = await ProjectIncome(c, project);
        long tx = await SeedBankTx(hdfc, 0m, 300_000m, "RTGS CLIENT ADV", "R300");

        (await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-credit",
            new { clientId = client, projectId = project, existingReceiptId = receiptId })).EnsureSuccessStatusCode();

        (await ProjectIncome(c, project)).Should().Be(incomeBefore); // no second receipt
    }

    [Fact]
    public async Task CreditReconcile_UpdatesAllFiveBalances()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long project = await Project(c, "Five Balances P");
        long client = await Client_(c, "Five Balances Client");

        decimal bankBefore = await Balance(c, hdfc);
        decimal outstandingBefore = (await Json(await c.GetAsync($"/api/v1/projects/{project}/outstanding-summary")))
            .GetProperty("clientReceivable").GetDecimal();
        long tx = await SeedBankTx(hdfc, 0m, 400_000m, "RTGS CLIENT", "R400");

        (await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-credit",
            new { clientId = client, projectId = project })).EnsureSuccessStatusCode();

        (await Balance(c, hdfc)).Should().Be(bankBefore + 400_000m);
        (await ProjectIncome(c, project)).Should().Be(400_000m);
        decimal outstandingAfter = (await Json(await c.GetAsync($"/api/v1/projects/{project}/outstanding-summary")))
            .GetProperty("clientReceivable").GetDecimal();
        outstandingAfter.Should().Be(outstandingBefore - 400_000m);
    }

    [Fact]
    public async Task CreditReconcile_WithoutPermission_Returns403()
    {
        HttpClient admin = Admin;
        long hdfc = await Account(admin, "HDFC");
        long project = await Project(admin, "No Perm P");
        long client = await Client_(admin, "No Perm Client");
        long tx = await SeedBankTx(hdfc, 0m, 100_000m, "RTGS", "R1");

        HttpClient viewer = Factory.CreateClientAs(userId: 2, permissions: "bank_reconciliation.view");
        HttpResponseMessage r = await viewer.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-credit",
            new { clientId = client, projectId = project });

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── P4-T07 debit ─────────────────────────────────────────────────────────

    private async Task<(long Vendor, long A, long B, long C, long D)> Section20(HttpClient c, string tag)
    {
        long v = await Vendor(c, $"S20 {tag}");
        long a = await Project(c, $"S20A {tag}");
        long b = await Project(c, $"S20B {tag}");
        long cc = await Project(c, $"S20C {tag}");
        long d = await Project(c, $"S20D {tag}");
        await Purchase(c, a, v, 25_000m);
        await Purchase(c, b, v, 10_000m);
        await Purchase(c, cc, v, 30_000m);
        await Purchase(c, d, v, 50_000m);
        return (v, a, b, cc, d);
    }

    private static object Alloc(long p, decimal amt) => new { projectId = p, amount = amt };

    [Fact]
    public async Task DebitReconcile_BrdSection35Example()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        (long v, long a, long b, long cc, long d) = await Section20(c, "s35");
        long tx = await SeedBankTx(hdfc, 100_000m, 0m, "NEFT DR-S20", "N100");

        JsonElement res = await Json(await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-debit", new
        {
            vendorId = v,
            allocations = new[] { Alloc(a, 25_000m), Alloc(b, 10_000m), Alloc(cc, 30_000m), Alloc(d, 35_000m) },
        }));

        res.GetProperty("settlementCreated").GetBoolean().Should().BeTrue();
        (await VendorOutstanding(c, v)).Should().Be(15_000m);
        (await TxStatus(tx)).Should().Be(BankTransactionStatus.Reconciled);
    }

    [Fact]
    public async Task DebitReconcile_AllocationMismatch_BlocksReconcile()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        (long v, long a, long b, long cc, long d) = await Section20(c, "mismatch");
        long tx = await SeedBankTx(hdfc, 100_000m, 0m, "NEFT DR", "N1");

        HttpResponseMessage r = await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-debit", new
        {
            vendorId = v,
            allocations = new[] { Alloc(a, 25_000m), Alloc(b, 10_000m), Alloc(cc, 30_000m), Alloc(d, 35_001m) },
        });

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await r.Content.ReadAsStringAsync()).Should().Contain("rule 48");
    }

    [Fact]
    public async Task DebitReconcile_ProposesFifoByProjectOutstanding()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        (long v, long a, long b, long cc, long d) = await Section20(c, "fifo");
        long tx = await SeedBankTx(hdfc, 100_000m, 0m, "NEFT DR", "N1");

        JsonElement p = await Json(await c.GetAsync(
            $"/api/v1/bank-transactions/{tx}/reconciliation-proposal?vendorId={v}"));

        var fifo = p.GetProperty("fifoProposal").EnumerateArray()
            .ToDictionary(x => x.GetProperty("projectId").GetInt64(), x => x.GetProperty("amount").GetDecimal());
        fifo[a].Should().Be(25_000m);
        fifo[b].Should().Be(10_000m);
        fifo[cc].Should().Be(30_000m);
        fifo[d].Should().Be(35_000m);
    }

    [Fact]
    public async Task DebitReconcile_ToExistingPayment_LinksNotDuplicates_NoDoubleCounting_BrdSection37()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long cash = await Mode(c, "Cash");
        long project = await Project(c, "S37 P");
        long v = await Vendor(c, "S37 Vendor");
        await Purchase(c, project, v, 200_000m);

        long paymentId = (await Json(await c.PostAsJsonAsync("/api/v1/vendor-payments", new
        {
            vendorId = v, projectId = project, date = "2026-05-10", amount = 100_000m,
            paymentModeId = cash, accountId = hdfc,
        }))).GetProperty("id").GetInt64();

        decimal outstandingAfterManual = await VendorOutstanding(c, v);
        decimal bankAfterManual = await Balance(c, hdfc);
        long tx = await SeedBankTx(hdfc, 100_000m, 0m, "NEFT DR-S37 VENDOR", "N100");

        JsonElement res = await Json(await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-debit",
            new { existingPaymentId = paymentId }));
        res.GetProperty("settlementCreated").GetBoolean().Should().BeFalse();

        // The manual payment already moved the money — reconciling links, it does not re-post.
        (await VendorOutstanding(c, v)).Should().Be(outstandingAfterManual);
        (await Balance(c, hdfc)).Should().Be(bankAfterManual);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Settlements.CountAsync(s => s.PartyId == v && s.Direction == Domain.Settlements.SettlementDirection.Out))
            .Should().Be(1);
        (await db.BankTransactions.CountAsync(t => t.Id == tx)).Should().Be(1);
    }

    [Fact]
    public async Task DebitReconcile_OneBankRow_ManyAllocations()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        (long v, long a, long b, long cc, long d) = await Section20(c, "onerow");
        long tx = await SeedBankTx(hdfc, 100_000m, 0m, "NEFT DR", "N1");

        (await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-debit", new
        {
            vendorId = v,
            allocations = new[] { Alloc(a, 25_000m), Alloc(b, 10_000m), Alloc(cc, 30_000m), Alloc(d, 35_000m) },
        })).EnsureSuccessStatusCode();

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.BankTransactions.CountAsync(t => t.AccountId == hdfc)).Should().Be(1);
        long sid = await db.ReconciliationLinks.Where(l => l.BankTransactionId == tx).Select(l => l.SettlementId!.Value).FirstAsync();
        (await db.Allocations.CountAsync(x => x.SettlementId == sid && x.ProjectId != null)).Should().Be(4);
    }

    // ── P4-T09 unreconcile ───────────────────────────────────────────────────

    [Fact]
    public async Task Unreconcile_CreatedSettlement_IsReversed()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        (long v, long a, long b, long cc, long d) = await Section20(c, "unrev");
        decimal outstandingBefore = await VendorOutstanding(c, v);
        long tx = await SeedBankTx(hdfc, 100_000m, 0m, "NEFT DR", "N1");

        await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-debit", new
        {
            vendorId = v,
            allocations = new[] { Alloc(a, 25_000m), Alloc(b, 10_000m), Alloc(cc, 30_000m), Alloc(d, 35_000m) },
        });

        (await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/unreconcile", new { reason = "wrong vendor" }))
            .EnsureSuccessStatusCode();

        (await VendorOutstanding(c, v)).Should().Be(outstandingBefore);
        (await TxStatus(tx)).Should().Be(BankTransactionStatus.Pending);
    }

    [Fact]
    public async Task Unreconcile_PreExistingSettlement_IsUntouched()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long cash = await Mode(c, "Cash");
        long project = await Project(c, "Untouched P");
        long v = await Vendor(c, "Untouched Vendor");
        await Purchase(c, project, v, 200_000m);
        long paymentId = (await Json(await c.PostAsJsonAsync("/api/v1/vendor-payments", new
        {
            vendorId = v, projectId = project, date = "2026-05-10", amount = 100_000m, paymentModeId = cash, accountId = hdfc,
        }))).GetProperty("id").GetInt64();
        decimal outstandingLinked = await VendorOutstanding(c, v);
        long tx = await SeedBankTx(hdfc, 100_000m, 0m, "NEFT DR", "N1");
        await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-debit", new { existingPaymentId = paymentId });

        (await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/unreconcile", new { reason = "duplicate row" }))
            .EnsureSuccessStatusCode();

        (await VendorOutstanding(c, v)).Should().Be(outstandingLinked); // payment untouched
        (await TxStatus(tx)).Should().Be(BankTransactionStatus.Pending);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Settlements.Where(s => s.Id == paymentId).Select(s => s.Status).FirstAsync())
            .Should().Be(Domain.Settlements.SettlementStatus.Active);
    }

    [Fact]
    public async Task Unreconcile_WithoutPermission_Returns403()
    {
        HttpClient admin = Admin;
        long hdfc = await Account(admin, "HDFC");
        long project = await Project(admin, "UnrecPerm P");
        long client = await Client_(admin, "UnrecPerm Client");
        long tx = await SeedBankTx(hdfc, 0m, 100_000m, "RTGS", "R1");
        await admin.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-credit",
            new { clientId = client, projectId = project });

        HttpClient viewer = Factory.CreateClientAs(userId: 2, permissions: "bank_reconciliation.view");
        HttpResponseMessage r = await viewer.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/unreconcile",
            new { reason = "x" });

        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unreconcile_WithoutReason_Returns400()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long project = await Project(c, "NoReason P");
        long client = await Client_(c, "NoReason Client");
        long tx = await SeedBankTx(hdfc, 0m, 100_000m, "RTGS", "R1");
        await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-credit",
            new { clientId = client, projectId = project });

        HttpResponseMessage r = await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/unreconcile",
            new { reason = "" });

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reconcile_WritesAuditWithReconciliationFields()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long project = await Project(c, "Audit P");
        long client = await Client_(c, "Audit Client");
        long tx = await SeedBankTx(hdfc, 0m, 250_000m, "RTGS AUDIT CLIENT", "AUDITREF1");
        JsonElement res = await Json(await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-credit",
            new { clientId = client, projectId = project }));
        long sid = res.GetProperty("settlementId").GetInt64();

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.AuditLogs.SingleAsync(x =>
            x.Module == "bank_reconciliation" && x.Action == "reconcile" && x.RecordId == tx.ToString());
        row.Details.Should().NotBeNull();
        row.Details!.Should().Contain("AUDITREF1").And.Contain(sid.ToString());
    }

    [Fact]
    public async Task Unreconcile_LedgerReturnsToPriorState()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        (long v, long a, long b, long cc, long d) = await Section20(c, "ledger");
        decimal bankBefore = await Balance(c, hdfc);
        decimal outstandingBefore = await VendorOutstanding(c, v);
        long tx = await SeedBankTx(hdfc, 100_000m, 0m, "NEFT DR", "N1");

        await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-debit", new
        {
            vendorId = v,
            allocations = new[] { Alloc(a, 25_000m), Alloc(b, 10_000m), Alloc(cc, 30_000m), Alloc(d, 35_000m) },
        });
        (await Balance(c, hdfc)).Should().Be(bankBefore - 100_000m);

        (await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/unreconcile", new { reason = "revert" }))
            .EnsureSuccessStatusCode();

        (await Balance(c, hdfc)).Should().Be(bankBefore);
        (await VendorOutstanding(c, v)).Should().Be(outstandingBefore);
    }

    // ── hold / map-debit split (client request, 2026-09-04) ──────────────────

    private static object Split(string target, decimal amt, string description,
        long? projectId = null, long? vendorId = null) =>
        new { target, amount = amt, description, projectId, vendorId };

    private async Task<decimal> CommonExpensesTotal(HttpClient c, string type)
    {
        JsonElement summary = await Json(await c.GetAsync("/api/v1/common-expenses/summary"));
        return type switch
        {
            "Personal" => summary.GetProperty("personal").GetDecimal(),
            "Office" => summary.GetProperty("office").GetDecimal(),
            "Savings" => summary.GetProperty("savings").GetDecimal(),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    [Fact]
    public async Task Hold_PendingTransaction_MovesToInReview_AndUnholdReturnsItToPending()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long tx = await SeedBankTx(hdfc, 20_000m, 0m, "NEFT DR HOLD", "H1");

        (await c.PostAsync($"/api/v1/bank-transactions/{tx}/hold", null)).EnsureSuccessStatusCode();
        (await TxStatus(tx)).Should().Be(BankTransactionStatus.InReview);

        (await c.PostAsync($"/api/v1/bank-transactions/{tx}/unhold", null)).EnsureSuccessStatusCode();
        (await TxStatus(tx)).Should().Be(BankTransactionStatus.Pending);
    }

    [Fact]
    public async Task Hold_AlreadyReconciled_Returns400()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long project = await Project(c, "Hold Reconciled P");
        long client = await Client_(c, "Hold Reconciled Client");
        long tx = await SeedBankTx(hdfc, 0m, 50_000m, "RTGS", "R1");
        await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/reconcile-credit",
            new { clientId = client, projectId = project });

        (await c.PostAsync($"/api/v1/bank-transactions/{tx}/hold", null)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MapDebit_SplitAcrossVendorProjectAdvanceAndCommonExpenses_PostsEveryTarget()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long project = await Project(c, "Map Split P");
        long vendor = await Vendor(c, "Map Split Vendor");
        await Purchase(c, project, vendor, 30_000m);
        decimal outstandingBefore = await VendorOutstanding(c, vendor);
        decimal personalBefore = await CommonExpensesTotal(c, "Personal");
        decimal officeBefore = await CommonExpensesTotal(c, "Office");

        // 50k debit: 20k against the vendor's project outstanding, 5k as a project-less
        // vendor advance, 15k Personal, 10k Office.
        long tx = await SeedBankTx(hdfc, 50_000m, 0m, "NEFT DR SPLIT", "SPLIT1");
        JsonElement res = await Json(await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/map-debit", new
        {
            allocations = new[]
            {
                Split("Vendor", 20_000m, "Project purchase settlement", projectId: project, vendorId: vendor),
                Split("Vendor", 5_000m, "Advance for next invoice", vendorId: vendor),
                Split("Personal", 15_000m, "Household groceries"),
                Split("Office", 10_000m, "Office rent"),
            },
        }));

        res.GetProperty("status").GetString().Should().Be("Reconciled");
        res.GetProperty("settlementIds").GetArrayLength().Should().Be(1); // one vendor -> one settlement
        res.GetProperty("commonExpenseIds").GetArrayLength().Should().Be(2);

        (await VendorOutstanding(c, vendor)).Should().Be(outstandingBefore - 20_000m);
        (await CommonExpensesTotal(c, "Personal")).Should().Be(personalBefore + 15_000m);
        (await CommonExpensesTotal(c, "Office")).Should().Be(officeBefore + 10_000m);
        (await TxStatus(tx)).Should().Be(BankTransactionStatus.Reconciled);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ReconciliationLinks.CountAsync(l => l.BankTransactionId == tx && l.UnlinkedAtUtc == null))
            .Should().Be(3); // 1 settlement (vendor, both project + advance legs) + 2 common expenses
    }

    [Fact]
    public async Task MapDebit_OnHeldTransaction_DoesNotRequireUnholdFirst()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long tx = await SeedBankTx(hdfc, 5_000m, 0m, "NEFT DR HOLDMAP", "HM1");
        (await c.PostAsync($"/api/v1/bank-transactions/{tx}/hold", null)).EnsureSuccessStatusCode();

        (await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/map-debit", new
        {
            allocations = new[] { Split("Savings", 5_000m, "Move to FD") },
        })).EnsureSuccessStatusCode();

        (await TxStatus(tx)).Should().Be(BankTransactionStatus.Reconciled);
    }

    [Fact]
    public async Task MapDebit_SplitDoesNotSumToBankAmount_Returns400()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long tx = await SeedBankTx(hdfc, 50_000m, 0m, "NEFT DR", "N1");

        HttpResponseMessage r = await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/map-debit", new
        {
            allocations = new[] { Split("Personal", 40_000m, "Short") },
        });

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await r.Content.ReadAsStringAsync()).Should().Contain("rule 48");
    }

    [Fact]
    public async Task MapDebit_LineMissingDescription_Returns400()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long tx = await SeedBankTx(hdfc, 10_000m, 0m, "NEFT DR", "N1");

        HttpResponseMessage r = await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/map-debit", new
        {
            allocations = new[] { Split("Personal", 10_000m, "") },
        });

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unreconcile_MappedSplit_ReversesEveryTargetAndRestoresPriorState()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long project = await Project(c, "Unrev Split P");
        long vendor = await Vendor(c, "Unrev Split Vendor");
        await Purchase(c, project, vendor, 30_000m);
        decimal outstandingBefore = await VendorOutstanding(c, vendor);
        decimal bankBefore = await Balance(c, hdfc);
        decimal personalBefore = await CommonExpensesTotal(c, "Personal");

        long tx = await SeedBankTx(hdfc, 25_000m, 0m, "NEFT DR SPLIT REV", "SPLITREV1");
        await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/map-debit", new
        {
            allocations = new[]
            {
                Split("Vendor", 20_000m, "Project purchase settlement", projectId: project, vendorId: vendor),
                Split("Personal", 5_000m, "Household groceries"),
            },
        });
        (await Balance(c, hdfc)).Should().Be(bankBefore - 25_000m);

        (await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/unreconcile", new { reason = "wrong split" }))
            .EnsureSuccessStatusCode();

        (await VendorOutstanding(c, vendor)).Should().Be(outstandingBefore);
        (await CommonExpensesTotal(c, "Personal")).Should().Be(personalBefore);
        (await Balance(c, hdfc)).Should().Be(bankBefore);
        (await TxStatus(tx)).Should().Be(BankTransactionStatus.Pending);
    }

    // ── bank-charge split (client request, 2026-09-04) ────────────────────────

    private async Task<decimal> ProjectOtherExpenses(HttpClient c, long projectId) =>
        (await Json(await c.GetAsync($"/api/v1/projects/{projectId}/expenses")))
        .EnumerateArray()
        .Where(x => x.GetProperty("bucket").GetString() == "Other Expenses"
            && x.GetProperty("status").GetString() == "Active")
        .Sum(x => x.GetProperty("amount").GetDecimal());

    [Fact]
    public async Task MapDebit_VendorPaymentPlusBankCharge_SplitsExactlyAndAttributesFeeToProject()
    {
        // BRD scenario: pay a vendor ₹10,000 but the bank statement shows ₹10,100
        // because of a ₹100 transfer fee. The vendor's outstanding must drop by exactly
        // ₹10,000 (what was actually owed); the ₹100 fee is a real cost of that project.
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long project = await Project(c, "Bank Charge P");
        long vendor = await Vendor(c, "Bank Charge Vendor");
        await Purchase(c, project, vendor, 30_000m);
        decimal outstandingBefore = await VendorOutstanding(c, vendor);
        decimal otherExpensesBefore = await ProjectOtherExpenses(c, project);

        long tx = await SeedBankTx(hdfc, 10_100m, 0m, "NEFT DR VENDOR + FEE", "FEE1");
        JsonElement res = await Json(await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/map-debit", new
        {
            allocations = new[]
            {
                Split("Vendor", 10_000m, "Vendor settlement", projectId: project, vendorId: vendor),
                Split("BankCharges", 100m, "NEFT transfer fee", projectId: project),
            },
        }));

        res.GetProperty("status").GetString().Should().Be("Reconciled");
        (await VendorOutstanding(c, vendor)).Should().Be(outstandingBefore - 10_000m);
        (await ProjectOtherExpenses(c, project)).Should().Be(otherExpensesBefore + 100m);
        (await TxStatus(tx)).Should().Be(BankTransactionStatus.Reconciled);
    }

    [Fact]
    public async Task MapDebit_BankCharges_RequiresProjectAndForbidsVendor()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long project = await Project(c, "Bank Charge Validation P");
        long vendor = await Vendor(c, "Bank Charge Validation Vendor");
        long tx = await SeedBankTx(hdfc, 100m, 0m, "NEFT DR FEE ONLY", "FEE2");

        HttpResponseMessage noProject = await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/map-debit", new
        {
            allocations = new[] { Split("BankCharges", 100m, "Transfer fee") },
        });
        noProject.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        HttpResponseMessage withVendor = await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/map-debit", new
        {
            allocations = new[] { Split("BankCharges", 100m, "Transfer fee", projectId: project, vendorId: vendor) },
        });
        withVendor.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unreconcile_BankChargeSplit_ReversesTheDirectExpense()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long project = await Project(c, "Bank Charge Rev P");
        long vendor = await Vendor(c, "Bank Charge Rev Vendor");
        await Purchase(c, project, vendor, 30_000m);
        decimal outstandingBefore = await VendorOutstanding(c, vendor);
        decimal otherExpensesBefore = await ProjectOtherExpenses(c, project);
        decimal bankBefore = await Balance(c, hdfc);

        long tx = await SeedBankTx(hdfc, 10_100m, 0m, "NEFT DR VENDOR + FEE REV", "FEEREV1");
        await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/map-debit", new
        {
            allocations = new[]
            {
                Split("Vendor", 10_000m, "Vendor settlement", projectId: project, vendorId: vendor),
                Split("BankCharges", 100m, "NEFT transfer fee", projectId: project),
            },
        });

        (await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/unreconcile", new { reason = "wrong txn" }))
            .EnsureSuccessStatusCode();

        (await VendorOutstanding(c, vendor)).Should().Be(outstandingBefore);
        (await ProjectOtherExpenses(c, project)).Should().Be(otherExpensesBefore);
        (await Balance(c, hdfc)).Should().Be(bankBefore);
        (await TxStatus(tx)).Should().Be(BankTransactionStatus.Pending);
    }
}

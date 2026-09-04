using System;
using System.IO;
using System.Linq;
using System.Text;
using ColourBricks.Application.Banking;
using ColourBricks.Infrastructure.Banking;
using FluentAssertions;

namespace ColourBricks.UnitTests.Banking;

public sealed class BankStatementParserTests
{
    private readonly BankStatementParser _parser = new();

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "statements", name);

    private static BankStatementProfileDto Profile(
        int headerRowIndex, int dateCol, int narrationCol, bool single, string dateFormats,
        string delimiter = ",", int? refCol = null, int? balanceCol = null,
        int? amountCol = null, int? debitCol = null, int? creditCol = null, string debitSign = "Negative") =>
        new(1, 1, "test", headerRowIndex, delimiter, dateCol, narrationCol, refCol, balanceCol,
            single, amountCol, debitCol, creditCol, debitSign, dateFormats);

    private static MemoryStream SbiXlsx() => XlsxFixture.Build(
    [
        ["State Bank of India"],
        ["Account: XXXXXX7890"],
        ["Statement Period 01-May-2026 - 31-May-2026"],
        ["Txn Date", "Description", "Ref/Cheque No", "Amount", "Balance"],
        ["03/04/2026", "NEFT ABC HARDWARE", "SBIN01", "1,00,000.00 Dr", "4,00,000.00"],
        ["10/05/2026", "CLIENT PAYMENT", "SBIN02", "2,50,000.00 Cr", "6,50,000.00"],
        ["15/05/2026", "SERVICE CHARGE", "SBIN03", "118.00 Dr", "6,49,882.00"],
    ]);

    private static BankStatementProfileDto SbiProfile() =>
        Profile(headerRowIndex: 3, dateCol: 0, narrationCol: 1, single: true, dateFormats: "dd/MM/yyyy",
            refCol: 2, balanceCol: 4, amountCol: 3);

    [Fact]
    public void Parse_HdfcCsv_ProducesExpectedRows()
    {
        using FileStream file = File.OpenRead(FixturePath("hdfc-may.csv"));
        BankStatementProfileDto profile = Profile(
            headerRowIndex: 2, dateCol: 0, narrationCol: 1, single: false, dateFormats: "dd/MM/yy",
            refCol: 2, balanceCol: 6, debitCol: 4, creditCol: 5);

        BankStatementParseResult result = _parser.Parse(file, "hdfc-may.csv", profile);

        result.Rows.Should().HaveCount(3);
        result.ErrorRows.Should().Be(0);

        ParsedBankRowInput r1 = result.Rows[0];
        r1.ValueDate.Should().Be(new DateOnly(2026, 5, 1));
        r1.Debit.Should().Be(25_000m);
        r1.Credit.Should().Be(0m);
        r1.BankReference.Should().Be("N123");

        result.Rows[1].Credit.Should().Be(5_00_000m);
        result.Rows[1].Debit.Should().Be(0m);
        result.Rows[2].Debit.Should().Be(12_500.50m);
    }

    [Fact]
    public void Parse_SbiXlsx_ProducesExpectedRows()
    {
        using MemoryStream file = SbiXlsx();
        BankStatementParseResult result = _parser.Parse(file, "sbi-may.xlsx", SbiProfile());

        result.Rows.Should().HaveCount(3);
        result.ErrorRows.Should().Be(0);
        result.Rows[0].Narration.Should().Be("NEFT ABC HARDWARE");
        result.Rows[0].Debit.Should().Be(1_00_000m);
        result.Rows[1].Credit.Should().Be(2_50_000m);
        result.Rows[2].Debit.Should().Be(118m);
    }

    [Fact]
    public void Parse_AmbiguousDate_UsesProfileFormat_NotLocale()
    {
        using MemoryStream file = SbiXlsx();
        BankStatementParseResult result = _parser.Parse(file, "sbi-may.xlsx", SbiProfile());

        // "03/04/2026" with dd/MM/yyyy is 3 April, never 4 March.
        result.Rows[0].ValueDate.Should().Be(new DateOnly(2026, 4, 3));
    }

    [Fact]
    public void Parse_AmountWithCommasAndCrDr_ParsesCorrectly()
    {
        using MemoryStream file = SbiXlsx();
        BankStatementParseResult result = _parser.Parse(file, "sbi-may.xlsx", SbiProfile());

        result.Rows[0].Debit.Should().Be(1_00_000m);   // "1,00,000.00 Dr"
        result.Rows[1].Credit.Should().Be(2_50_000m);  // "2,50,000.00 Cr"
        result.Rows.Should().OnlyContain(r => r.Debit == 0m || r.Credit == 0m);
    }

    [Fact]
    public void Parse_SignedSingleAmountColumn_SplitsToDebitCredit()
    {
        using FileStream file = File.OpenRead(FixturePath("icici-may.csv"));
        BankStatementProfileDto profile = Profile(
            headerRowIndex: 1, dateCol: 0, narrationCol: 1, single: true, dateFormats: "dd-MM-yyyy",
            delimiter: ";", refCol: 2, balanceCol: 4, amountCol: 3, debitSign: "Negative");

        BankStatementParseResult result = _parser.Parse(file, "icici-may.csv", profile);

        result.Rows.Should().HaveCount(3);
        result.Rows[0].Debit.Should().Be(40_000m);   // -40,000.00
        result.Rows[0].Credit.Should().Be(0m);
        result.Rows[1].Credit.Should().Be(3_00_000m); // 3,00,000.00
        result.Rows[2].Debit.Should().Be(1_250m);
    }

    [Fact]
    public void Parse_BadRow_ReportedNotFatal()
    {
        string csv =
            "Date,Narration,Debit,Credit\n"
            + "01/05/2026,GOOD ROW,1000.00,\n"
            + "NOT-A-DATE,BAD ROW,2000.00,\n"
            + "03/05/2026,ANOTHER GOOD ROW,,5000.00\n";
        using var file = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        BankStatementProfileDto profile = Profile(
            headerRowIndex: 0, dateCol: 0, narrationCol: 1, single: false, dateFormats: "dd/MM/yyyy",
            debitCol: 2, creditCol: 3);

        BankStatementParseResult result = _parser.Parse(file, "adhoc.csv", profile);

        result.Rows.Should().HaveCount(3);
        result.ErrorRows.Should().Be(1);

        ParsedBankRowInput bad = result.Rows.Single(r => r.ParseError is not null);
        bad.ValueDate.Should().BeNull();
        bad.RawLine.Should().Contain("BAD ROW");
        result.Rows.Where(r => r.ParseError is null).Should().HaveCount(2);
    }

    [Fact]
    public void Parse_LargeFile_StreamsWithoutOom()
    {
        var sb = new StringBuilder("Date,Narration,Debit,Credit\n");
        for (int i = 1; i <= 10_000; i++)
        {
            sb.Append(CultureFreeDate(i)).Append(",TXN ").Append(i).Append(',');
            sb.Append(i % 2 == 0 ? "1000.00," : ",1000.00").Append('\n');
        }

        using var file = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        BankStatementProfileDto profile = Profile(
            headerRowIndex: 0, dateCol: 0, narrationCol: 1, single: false, dateFormats: "dd/MM/yyyy",
            debitCol: 2, creditCol: 3);

        DateTime start = DateTime.UtcNow;
        BankStatementParseResult result = _parser.Parse(file, "big.csv", profile);

        result.Rows.Should().HaveCount(10_000);
        result.ErrorRows.Should().Be(0);
        (DateTime.UtcNow - start).Should().BeLessThan(TimeSpan.FromSeconds(15));
    }

    private static string CultureFreeDate(int i)
    {
        int day = (i % 28) + 1;
        return $"{day:00}/05/2026";
    }

    [Fact]
    public void DetectColumns_ReturnsHeaderAndSamples()
    {
        using FileStream file = File.OpenRead(FixturePath("hdfc-may.csv"));
        DetectedColumnsDto detected = _parser.DetectColumns(file, "hdfc-may.csv", headerRowIndex: 2);

        detected.Headers.Should().ContainInOrder("Date", "Narration", "Chq/Ref No");
        detected.SampleRows.Should().NotBeEmpty();
        detected.SampleRows[0][1].Should().Contain("ABC HARDWARE");
    }
}

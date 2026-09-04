using System.Globalization;
using System.Text;
using ColourBricks.Application.Banking;
using ColourBricks.Domain.Banking;
using ColourBricks.Domain.Services;
using ExcelDataReader;

namespace ColourBricks.Infrastructure.Banking;

/// <summary>
/// P4-T02 — reads a bank statement (CSV or XLSX) row by row against a saved
/// <see cref="BankStatementProfile"/>. Streams the file; a row that will not parse
/// becomes an error row rather than aborting the batch. Indian date and amount
/// quirks (dd/MM order, lakh grouping, trailing Cr/Dr) are handled here.
/// </summary>
public sealed class BankStatementParser : IBankStatementParser
{
    private const int MaxSampleRows = 5;

    static BankStatementParser() =>
        // ExcelDataReader's default configuration resolves code page 1252, absent on
        // non-Windows and on trimmed runtimes unless this provider is registered.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public DetectedColumnsDto DetectColumns(Stream content, string fileName, int headerRowIndex)
    {
        List<string[]> rows = ReadAllRows(content, fileName, delimiter: ',').Take(headerRowIndex + 1 + MaxSampleRows).ToList();
        if (rows.Count <= headerRowIndex)
        {
            throw new ArgumentException($"The file has no row at header index {headerRowIndex}.");
        }

        string[] headers = rows[headerRowIndex];
        var samples = rows.Skip(headerRowIndex + 1)
            .Select(r => (IReadOnlyList<string>)r.ToList())
            .ToList();

        return new DetectedColumnsDto(headers.ToList(), samples);
    }

    public BankStatementParseResult Parse(Stream content, string fileName, BankStatementProfileDto profile)
    {
        char delimiter = string.IsNullOrEmpty(profile.Delimiter) ? ',' : profile.Delimiter[0];
        string[] formats = profile.DateFormats
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (formats.Length == 0)
        {
            formats = ["dd/MM/yyyy"];
        }

        var rows = new List<ParsedBankRowInput>();
        int dataRowNo = 0;
        int errors = 0;
        int index = -1;

        foreach (string[] cells in ReadAllRows(content, fileName, delimiter))
        {
            index++;
            if (index <= profile.HeaderRowIndex)
            {
                continue;
            }

            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue; // blank trailing line
            }

            dataRowNo++;
            int sourceLineNo = index + 1; // 1-based line number in the file
            string raw = string.Join(delimiter, cells);

            try
            {
                DateOnly date = ParseDate(Cell(cells, profile.DateColumn), formats);
                (decimal debit, decimal credit) = ParseAmounts(cells, profile);
                string narration = Cell(cells, profile.NarrationColumn);
                string? reference = profile.ReferenceColumn is { } rc ? NullIfEmpty(Cell(cells, rc)) : null;
                decimal? balance = profile.BalanceColumn is { } bc && !string.IsNullOrWhiteSpace(Cell(cells, bc))
                    ? ParseDecimal(Cell(cells, bc))
                    : null;

                rows.Add(new ParsedBankRowInput(
                    sourceLineNo, date, narration, Money.Round(debit), Money.Round(credit),
                    balance is { } b ? Money.Round(b) : null, reference));
            }
            catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException or OverflowException)
            {
                errors++;
                rows.Add(new ParsedBankRowInput(
                    sourceLineNo, null, Cell(cells, profile.NarrationColumn), 0m, 0m,
                    ParseError: ex.Message, RawLine: raw));
            }
        }

        return new BankStatementParseResult(rows, dataRowNo, errors);
    }

    // ── row assembly ─────────────────────────────────────────────────────────

    private static (decimal Debit, decimal Credit) ParseAmounts(string[] cells, BankStatementProfileDto profile)
    {
        if (profile.SingleAmountColumn)
        {
            if (profile.AmountColumn is not { } ac)
            {
                throw new FormatException("The profile has no amount column configured.");
            }

            (decimal value, char marker) = ParseSignedAmount(Cell(cells, ac));
            bool debitNegative = !string.Equals(profile.DebitSign, "Positive", StringComparison.OrdinalIgnoreCase);

            return marker switch
            {
                'C' => (0m, Math.Abs(value)),
                'D' => (Math.Abs(value), 0m),
                _ when debitNegative => value < 0m ? (-value, 0m) : (0m, value),
                _ => value > 0m ? (value, 0m) : (0m, -value),
            };
        }

        decimal debit = profile.DebitColumn is { } dc && !string.IsNullOrWhiteSpace(Cell(cells, dc))
            ? Math.Abs(ParseDecimal(Cell(cells, dc)))
            : 0m;
        decimal credit = profile.CreditColumn is { } crc && !string.IsNullOrWhiteSpace(Cell(cells, crc))
            ? Math.Abs(ParseDecimal(Cell(cells, crc)))
            : 0m;
        return (debit, credit);
    }

    private static DateOnly ParseDate(string raw, string[] formats)
    {
        string s = raw.Trim();
        if (DateOnly.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly d))
        {
            return d;
        }

        // XLSX date cells arrive already rendered by ExcelDataReader as ISO.
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
        {
            return DateOnly.FromDateTime(dt);
        }

        throw new FormatException($"'{raw}' is not a date in {string.Join(" or ", formats)}.");
    }

    private static (decimal Value, char Marker) ParseSignedAmount(string raw)
    {
        string s = raw.Trim();
        char marker = '\0';
        if (s.EndsWith("cr", StringComparison.OrdinalIgnoreCase))
        {
            marker = 'C';
            s = s[..^2];
        }
        else if (s.EndsWith("dr", StringComparison.OrdinalIgnoreCase))
        {
            marker = 'D';
            s = s[..^2];
        }

        return (ParseDecimal(s), marker);
    }

    private static decimal ParseDecimal(string raw)
    {
        string s = raw.Trim().Replace("₹", "").Replace(" ", "");
        if (s.Length == 0)
        {
            return 0m;
        }

        const NumberStyles styles = NumberStyles.Number | NumberStyles.AllowParentheses;
        if (decimal.TryParse(s, styles, CultureInfo.InvariantCulture, out decimal value))
        {
            return value;
        }

        throw new FormatException($"'{raw}' is not a valid amount.");
    }

    private static string Cell(string[] cells, int index) => index >= 0 && index < cells.Length ? cells[index].Trim() : "";

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    // ── file readers ─────────────────────────────────────────────────────────

    private static IEnumerable<string[]> ReadAllRows(Stream content, string fileName, char delimiter)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        return fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? ReadXlsxRows(content)
            : ReadCsvRows(content, delimiter);
    }

    private static IEnumerable<string[]> ReadCsvRows(Stream content, char delimiter)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var field = new StringBuilder();
        var record = new List<string>();
        bool inQuotes = false;
        int ch;

        while ((ch = reader.Read()) != -1)
        {
            char c = (char)ch;
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                record.Add(field.ToString());
                field.Clear();
            }
            else if (c is '\r')
            {
                // swallow; a following \n ends the record
            }
            else if (c is '\n')
            {
                record.Add(field.ToString());
                field.Clear();
                yield return record.ToArray();
                record.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            yield return record.ToArray();
        }
    }

    private static IEnumerable<string[]> ReadXlsxRows(Stream content)
    {
        using IExcelDataReader reader = ExcelReaderFactory.CreateOpenXmlReader(content);

        // First sheet only.
        while (reader.Read())
        {
            var cells = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                object? value = reader.GetValue(i);
                cells[i] = value switch
                {
                    null => "",
                    DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                    _ => value.ToString() ?? "",
                };
            }

            yield return cells;
        }
    }
}

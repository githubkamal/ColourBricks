using ColourBricks.Domain.Common;

namespace ColourBricks.Domain.Banking;

/// <summary>Which sign an outflow carries in a single signed amount column. TINYINT.</summary>
public enum AmountSign : byte
{
    /// <summary>A debit is a negative number (the common case).</summary>
    Negative = 1,

    /// <summary>A debit is a positive number (some "withdrawal amount" exports).</summary>
    Positive = 2,
}

/// <summary>
/// A saved per-account recipe for reading one bank's statement export (BRD §31,
/// P4-T02). Indian bank CSV/XLSX exports differ in header position, column order,
/// date format and whether debit/credit are two columns or one signed column, so
/// the layout is captured once (via the mapping wizard) and reused.
/// </summary>
public sealed class BankStatementProfile : BaseEntity
{
    public long AccountId { get; set; }

    public string Name { get; set; } = "";

    /// <summary>0-based index of the row that holds the column headers. Rows above it are skipped.</summary>
    public int HeaderRowIndex { get; set; }

    /// <summary>CSV field delimiter (one character). Ignored for XLSX.</summary>
    public string Delimiter { get; set; } = ",";

    public int DateColumn { get; set; }

    public int NarrationColumn { get; set; }

    public int? ReferenceColumn { get; set; }

    public int? BalanceColumn { get; set; }

    /// <summary>True: one signed <see cref="AmountColumn"/>. False: separate <see cref="DebitColumn"/>/<see cref="CreditColumn"/>.</summary>
    public bool SingleAmountColumn { get; set; }

    public int? AmountColumn { get; set; }

    public int? DebitColumn { get; set; }

    public int? CreditColumn { get; set; }

    public AmountSign DebitSign { get; set; } = AmountSign.Negative;

    /// <summary>One or more <c>DateTime.ParseExact</c> patterns, "|"-separated, tried in order (e.g. <c>dd/MM/yyyy|dd-MM-yy</c>).</summary>
    public string DateFormats { get; set; } = "dd/MM/yyyy";
}

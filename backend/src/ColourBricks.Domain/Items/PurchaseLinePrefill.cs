namespace ColourBricks.Domain.Items;

/// <summary>
/// The values a purchase line takes from an <see cref="Item"/> at the moment it is
/// created. Producing this snapshot — rather than referencing the live master — is
/// what makes historical purchase lines immune to later changes to the item's
/// default rate, tax or unit (plan.md §5.2, BRD §14, P1-T03 acceptance).
/// P2-T02 persists these values on <c>ObligationLine</c>.
/// </summary>
public sealed record PurchaseLinePrefill(
    long ItemId,
    string ItemName,
    string Unit,
    decimal Rate,
    decimal TaxRate)
{
    public static PurchaseLinePrefill From(Item item) =>
        new(item.Id, item.Name, item.Unit, item.DefaultRate, item.TaxRate);
}

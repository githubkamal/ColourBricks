using ColourBricks.Domain.Items;
using FluentAssertions;

namespace ColourBricks.UnitTests.Domain;

public class ItemTests
{
    [Fact]
    public void ItemDefaultRateChange_DoesNotAffectHistoricalLines()
    {
        var cement = new Item
        {
            Id = 1,
            Name = "Cement",
            NormalisedName = "cement",
            Unit = "Bag",
            DefaultRate = 400m,
            TaxRate = 18m,
        };

        // A purchase line entered today snapshots the item's defaults.
        PurchaseLinePrefill historicalLine = PurchaseLinePrefill.From(cement);

        // The master's default rate is revised later.
        cement.DefaultRate = 500m;
        cement.TaxRate = 12m;
        cement.Unit = "Ton";

        // The already-entered line keeps the values it was created with.
        historicalLine.Rate.Should().Be(400m);
        historicalLine.TaxRate.Should().Be(18m);
        historicalLine.Unit.Should().Be("Bag");
        historicalLine.ItemId.Should().Be(1);
    }

    [Fact]
    public void PurchaseLinePrefill_From_CopiesTheItemsCurrentDefaults()
    {
        var pipe = new Item
        {
            Id = 7,
            Name = "PVC Pipe",
            NormalisedName = "pvc pipe",
            Unit = "Nos",
            DefaultRate = 250m,
            TaxRate = 18m,
        };

        PurchaseLinePrefill.From(pipe).Should().Be(
            new PurchaseLinePrefill(7, "PVC Pipe", "Nos", 250m, 18m));
    }
}

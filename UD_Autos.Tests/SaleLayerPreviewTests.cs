using UD_Autos.Services;

namespace UD_Autos.Tests;

public class SaleLayerPreviewTests
{
    private static readonly SaleStockLayer[] TwoCosts =
    [
        new(10, 30_000),
        new(20, 40_000)
    ];

    [Fact]
    public void OneLayer_ReportsOldestStockAndCost()
    {
        var slices = SaleLayerPreview.Plan([new SaleStockLayer(10, 30_000)], 4);

        var slice = Assert.Single(slices);
        Assert.Equal(4, slice.Quantity);
        Assert.Equal(30_000, slice.UnitCost);
        Assert.Equal("Oldest stock: 10 left. Cost Rs 30,000.", SaleLayerPreview.Describe(slices, 10, true));
        Assert.Equal("Oldest stock: 10 left.", SaleLayerPreview.Describe(slices, 10, false));
    }

    [Fact]
    public void QuantityCrossingTwoCosts_SplitsTenThenFive()
    {
        var slices = SaleLayerPreview.Plan(TwoCosts, 15);

        Assert.Equal(2, slices.Count);
        Assert.Equal(10, slices[0].Quantity);
        Assert.Equal(30_000, slices[0].UnitCost);
        Assert.Equal(5, slices[1].Quantity);
        Assert.Equal(40_000, slices[1].UnitCost);
        Assert.Equal(
            "This quantity uses 10 at Rs 30,000 and 5 at Rs 40,000. Add a second line if those should have different sale prices.",
            SaleLayerPreview.Describe(slices, 10, true));
        Assert.Equal(
            "This quantity uses 10 and 5. Add a second line if those should have different sale prices.",
            SaleLayerPreview.Describe(slices, 10, false));
    }

    [Fact]
    public void SkipFirstTen_UsesTheLaterCostOnly()
    {
        var slices = SaleLayerPreview.Plan(TwoCosts, 5, skip: 10);

        Assert.Single(slices);
        Assert.Equal(5, slices[0].Quantity);
        Assert.Equal(40_000, slices[0].UnitCost);
        Assert.Equal(20, SaleLayerPreview.OldestAvailable(TwoCosts, skip: 10));
        Assert.Equal("Oldest stock: 20 left. Cost Rs 40,000.", SaleLayerPreview.Describe(slices, 20, true));
    }
}

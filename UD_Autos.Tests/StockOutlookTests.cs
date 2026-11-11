using UD_Autos.Services;

namespace UD_Autos.Tests;

public class StockOutlookTests
{
    [Fact]
    public void SummarizeStock_UsesAverageCost_AndWholesaleBeforeRetail()
    {
        var outlook = DashboardService.SummarizeStock(
        [
            new DashboardService.StockOutlookRow(10, 8, 5, 20, 25),
            new DashboardService.StockOutlookRow(4, null, 10, 0, 15),
            new DashboardService.StockOutlookRow(0, 50, 50, 80, 90)
        ]);

        Assert.Equal(14m, outlook.Quantity);
        Assert.Equal(120m, outlook.Investment);
        Assert.Equal(260m, outlook.Outcome);
        Assert.Equal(140m, outlook.Profit);
    }
}

using UD_Autos.Services;

namespace UD_Autos.Tests;

public class AccountingMathTests
{
    [Fact]
    public void AllocateByBasis_LastLineAbsorbsRounding()
    {
        var shares = AccountingMath.AllocateByBasis(100.00m, new[] { 1m, 1m, 1m });
        Assert.Equal(3, shares.Length);
        Assert.Equal(100.00m, shares.Sum());
        Assert.Equal(33.33m, shares[0]);
        Assert.Equal(33.33m, shares[1]);
        Assert.Equal(33.34m, shares[2]);
    }

    [Theory]
    [InlineData(0, "0–29")]
    [InlineData(29, "0–29")]
    [InlineData(30, "30–59")]
    [InlineData(60, "60–89")]
    [InlineData(90, "90–119")]
    [InlineData(120, "120+")]
    [InlineData(200, "120+")]
    public void AgingBucketLabel_MatchesClientBuckets(int days, string expected)
        => Assert.Equal(expected, AccountingMath.AgingBucketLabel(days));

    [Fact]
    public void AllocateFifo_PaysOldestFirst()
    {
        var open = new List<(int InvoiceId, decimal Outstanding)>
        {
            (1, 40m),
            (2, 80m),
            (3, 50m)
        };
        var alloc = AccountingMath.AllocateFifo(100m, open);
        Assert.Equal(2, alloc.Count);
        Assert.Equal((1, 40m), alloc[0]);
        Assert.Equal((2, 60m), alloc[1]);
    }

    [Fact]
    public void AgingBucketDays_UsesCalendarDates()
    {
        var age = AccountingMath.AgingBucketDays(new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));
        Assert.Equal(31, age);
    }
}

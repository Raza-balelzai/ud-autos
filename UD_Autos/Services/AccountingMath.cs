namespace UD_Autos.Services;

/// <summary>Pure accounting helpers used by services and unit tests.</summary>
public static class AccountingMath
{
    /// <summary>
    /// Allocate <paramref name="total"/> across basis weights; last line absorbs rounding so sum equals total.
    /// </summary>
    public static decimal[] AllocateByBasis(decimal total, IReadOnlyList<decimal> bases)
    {
        if (bases.Count == 0) return Array.Empty<decimal>();
        var basisTotal = bases.Sum();
        if (basisTotal <= 0) throw new ArgumentException("Basis total must be positive.", nameof(bases));

        var shares = new decimal[bases.Count];
        decimal allocated = 0;
        for (var i = 0; i < bases.Count; i++)
        {
            if (i == bases.Count - 1)
                shares[i] = Math.Round(total - allocated, 2);
            else
            {
                shares[i] = Math.Round(total * (bases[i] / basisTotal), 2);
                allocated += shares[i];
            }
        }
        return shares;
    }

    public static int AgingBucketDays(DateTime invoiceDate, DateTime asOf)
        => Math.Max(0, (int)(asOf.Date - invoiceDate.Date).TotalDays);

    public static string AgingBucketLabel(int ageDays) => ageDays switch
    {
        < 30 => "0–29",
        < 60 => "30–59",
        < 90 => "60–89",
        < 120 => "90–119",
        _ => "120+"
    };

    /// <summary>FIFO: apply payment across outstanding invoices oldest-first.</summary>
    public static List<(int InvoiceId, decimal Amount)> AllocateFifo(
        decimal paymentAmount, IReadOnlyList<(int InvoiceId, decimal Outstanding)> openInvoicesOldestFirst)
    {
        var result = new List<(int, decimal)>();
        var remaining = Math.Round(paymentAmount, 2);
        foreach (var inv in openInvoicesOldestFirst)
        {
            if (remaining <= 0) break;
            if (inv.Outstanding <= 0) continue;
            var take = Math.Min(inv.Outstanding, remaining);
            result.Add((inv.InvoiceId, take));
            remaining = Math.Round(remaining - take, 2);
        }
        return result;
    }
}

using System.Globalization;

namespace UD_Autos.Services;

public readonly record struct SaleStockLayer(decimal Available, decimal UnitCost);

public readonly record struct SaleLayerSlice(decimal Quantity, decimal UnitCost);

public static class SaleLayerPreview
{
    private static readonly CultureInfo MoneyCulture = CultureInfo.GetCultureInfo("en-US");

    public static IReadOnlyList<SaleLayerSlice> Plan(IReadOnlyList<SaleStockLayer> layers, decimal quantity, decimal skip = 0)
    {
        if (quantity <= 0 || layers.Count == 0) return [];

        var remainingSkip = skip < 0 ? 0 : skip;
        var remaining = quantity;
        var slices = new List<SaleLayerSlice>();

        foreach (var layer in layers)
        {
            var available = layer.Available;
            if (available <= 0) continue;

            if (remainingSkip > 0)
            {
                var skipped = Math.Min(available, remainingSkip);
                available -= skipped;
                remainingSkip -= skipped;
            }

            if (available <= 0 || remaining <= 0) continue;

            var take = Math.Min(available, remaining);
            slices.Add(new SaleLayerSlice(take, layer.UnitCost));
            remaining -= take;
        }

        return slices;
    }

    public static decimal OldestAvailable(IReadOnlyList<SaleStockLayer> layers, decimal skip = 0)
    {
        var remainingSkip = skip < 0 ? 0 : skip;
        foreach (var layer in layers)
        {
            var available = layer.Available;
            if (available <= 0) continue;
            if (remainingSkip >= available)
            {
                remainingSkip -= available;
                continue;
            }

            return available - remainingSkip;
        }

        return 0;
    }

    public static string? Describe(IReadOnlyList<SaleLayerSlice> slices, decimal oldestAvailable, bool includeCost)
    {
        if (slices.Count == 0) return null;

        if (slices.Count == 1)
        {
            var left = oldestAvailable > 0 ? oldestAvailable : slices[0].Quantity;
            var text = $"Oldest stock: {Qty(left)} left.";
            return includeCost ? $"{text} Cost Rs {Money(slices[0].UnitCost)}." : text;
        }

        var parts = includeCost
            ? slices.Select(s => $"{Qty(s.Quantity)} at Rs {Money(s.UnitCost)}").ToList()
            : slices.Select(s => Qty(s.Quantity)).ToList();
        return $"This quantity uses {Join(parts)}. Add a second line if those should have different sale prices.";
    }

    public static string FormatSplit(IEnumerable<(decimal Quantity, decimal UnitCost)> allocations)
    {
        var parts = allocations
            .Where(a => a.Quantity > 0)
            .Select(a => $"{Qty(a.Quantity)} at Rs {Money(a.UnitCost)}")
            .ToList();
        return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
    }

    private static string Join(IReadOnlyList<string> parts)
    {
        if (parts.Count == 1) return parts[0];
        if (parts.Count == 2) return $"{parts[0]} and {parts[1]}";
        return string.Join(", ", parts.Take(parts.Count - 1)) + ", and " + parts[^1];
    }

    private static string Qty(decimal value)
        => value.ToString(value == decimal.Truncate(value) ? "0" : "0.##", CultureInfo.InvariantCulture);

    private static string Money(decimal value)
        => value.ToString(value == decimal.Truncate(value) ? "N0" : "N2", MoneyCulture);
}

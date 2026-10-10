using UD_Autos.Models.Products;

namespace UD_Autos.Services;

public sealed class SetComponentInput
{
    public int ComponentProductId { get; set; }
    public decimal RequiredQuantity { get; set; }
}

public sealed class SaleDemandLine
{
    public int ProductId { get; set; }
    public bool IsSet { get; set; }
    public decimal Quantity { get; set; }
    public List<(int ComponentId, decimal PerSet)> Components { get; set; } = new();
}

public readonly record struct ComponentAvailabilityRow(
    int SetProductId,
    string Name,
    bool IsActive,
    decimal Available,
    decimal RequiredQuantity);

public sealed class BatchRestoreSlice
{
    public int BatchId { get; init; }
    public decimal Quantity { get; init; }
}

public static class ProductSetPlanner
{
    public const string SqlServerProductLockHint = "WITH (UPDLOCK, ROWLOCK)";

    public static string? ValidateComposition(
        int? setProductId,
        IReadOnlyList<SetComponentInput> components,
        IReadOnlyDictionary<int, ProductKind> kinds,
        IReadOnlySet<int> activeIds)
    {
        if (components.Count == 0)
            return "Add at least one item to the set.";

        var seen = new HashSet<int>();
        foreach (var component in components)
        {
            if (component.RequiredQuantity <= 0)
                return "Required quantity must be greater than zero.";
            if (setProductId is int setId && component.ComponentProductId == setId)
                return "A set cannot contain itself.";
            if (!seen.Add(component.ComponentProductId))
                return "Each item can appear only once in a set.";
            if (!activeIds.Contains(component.ComponentProductId))
                return "That item is not available.";
            if (!kinds.TryGetValue(component.ComponentProductId, out var kind) || kind != ProductKind.SingleItem)
                return "A complete set can only contain single items.";
        }

        return null;
    }

    public static decimal CalculateAvailableSets(IReadOnlyList<(decimal Available, decimal Required)> components)
    {
        if (components.Count == 0)
            return 0;

        decimal? min = null;
        foreach (var (available, required) in components)
        {
            if (required <= 0)
                return 0;
            var sets = Math.Floor(Math.Max(0, available) / required);
            min = min is null ? sets : Math.Min(min.Value, sets);
        }

        return min ?? 0;
    }

    public static void ApplyAvailability(Product set, IReadOnlyList<ComponentAvailabilityRow> components)
    {
        if (components.Count == 0)
        {
            set.VirtualAvailableSets = 0;
            set.LimitingComponentName = "No items";
            return;
        }

        decimal min = decimal.MaxValue;
        string? limiting = null;
        foreach (var component in components)
        {
            decimal sets;
            if (!component.IsActive || component.RequiredQuantity <= 0)
                sets = 0;
            else
                sets = Math.Floor(Math.Max(0, component.Available) / component.RequiredQuantity);

            if (sets < min)
            {
                min = sets;
                limiting = component.IsActive ? component.Name : component.Name + " (inactive)";
            }
        }

        set.VirtualAvailableSets = min == decimal.MaxValue ? 0 : min;
        set.LimitingComponentName = limiting;
    }

    public static Dictionary<int, decimal> AggregateDemand(IEnumerable<SaleDemandLine> lines)
    {
        var demand = new Dictionary<int, decimal>();
        foreach (var line in lines)
        {
            if (!line.IsSet)
            {
                Add(demand, line.ProductId, line.Quantity);
                continue;
            }

            foreach (var component in line.Components)
                Add(demand, component.ComponentId, line.Quantity * component.PerSet);
        }

        return demand;
    }

    public static string BuildProductLockSql(IReadOnlyCollection<int> productIds)
    {
        var ids = productIds.Where(id => id > 0).Distinct().OrderBy(id => id).ToArray();
        if (ids.Length == 0)
            throw new ArgumentException("At least one product id is required.", nameof(productIds));

        return $"SELECT [Id] FROM [Products] {SqlServerProductLockHint} WHERE [Id] IN ({string.Join(",", ids)})";
    }

    public static (bool Ok, string? Error, decimal Quantity, List<BatchRestoreSlice> Slices) PlanComponentRestore(
        decimal setQuantity,
        decimal requiredPerSet,
        decimal quantityConsumed,
        decimal alreadyReturned,
        IEnumerable<(int BatchId, decimal Quantity, decimal Returned)> allocations)
    {
        var want = Math.Round(setQuantity * requiredPerSet, 4);
        var remaining = Math.Round(quantityConsumed - alreadyReturned, 4);
        if (want <= 0)
            return (false, "Return quantity must be greater than zero.", 0, new());
        if (want > remaining)
            return (false, "This set sale cannot be reversed again.", 0, new());

        var slices = new List<BatchRestoreSlice>();
        var left = want;
        foreach (var allocation in allocations.OrderByDescending(a => a.BatchId))
        {
            if (left <= 0) break;
            var open = allocation.Quantity - allocation.Returned;
            if (open <= 0) continue;
            var put = Math.Min(open, left);
            slices.Add(new BatchRestoreSlice { BatchId = allocation.BatchId, Quantity = put });
            left -= put;
        }

        return (true, null, want, slices);
    }

    private static void Add(Dictionary<int, decimal> demand, int productId, decimal quantity)
    {
        demand.TryGetValue(productId, out var current);
        demand[productId] = current + quantity;
    }
}

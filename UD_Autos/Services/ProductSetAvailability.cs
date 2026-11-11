using Microsoft.EntityFrameworkCore;
using UD_Autos.Data;
using UD_Autos.Models.Products;

namespace UD_Autos.Services;

public static class ProductSetAvailability
{
    public static async Task AttachAsync(
        ApplicationDbContext db,
        IEnumerable<Product> products,
        CancellationToken cancellationToken = default)
    {
        var sets = products.Where(p => p.ProductKind == ProductKind.CompleteSet).ToList();
        if (sets.Count == 0)
            return;

        var ids = sets.Select(s => s.Id).ToList();
        var rows = await db.ProductSetComponents.AsNoTracking()
            .Where(c => c.IsActive && ids.Contains(c.SetProductId))
            .Select(c => new ComponentAvailabilityRow(
                c.SetProductId,
                c.ComponentProduct.Name,
                c.ComponentProduct.IsActive,
                c.ComponentProduct.CurrentStock - c.ComponentProduct.ReservedQuantity,
                c.RequiredQuantity))
            .ToListAsync(cancellationToken);

        foreach (var set in sets)
        {
            var components = rows.Where(r => r.SetProductId == set.Id).ToList();
            ProductSetPlanner.ApplyAvailability(set, components);
            set.CompositionNote = UnitRoles.IsPair(set.Unit?.Name)
                ? string.Join(" · ", components.Select(c => $"{c.Name} {c.Available:N0}"))
                : null;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using UD_Autos.Models.Products;

namespace UD_Autos.Data;

public static class Phase7ModelBuilderExtensions
{
    public static void ConfigurePhase7(this ModelBuilder builder)
    {
        builder.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Barcode)
                .IsUnique()
                .HasFilter("[Barcode] IS NOT NULL");
        });
    }
}

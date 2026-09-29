using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UD_Autos.Authorization;
using UD_Autos.Models.Identity;
using UD_Autos.Models.Settings;

namespace UD_Autos.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();

        await db.Database.MigrateAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await SeedLookupsAsync(db);

        var seedOwner = config.GetValue("SeedOwner:Enabled", true);
        if (seedOwner)
        {
            var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var email = config["SeedOwner:Email"] ?? "owner@udautos.local";
            var password = config["SeedOwner:Password"] ?? "Owner@12345";
            var fullName = config["SeedOwner:FullName"] ?? "Admin User";

            var existing = await userManager.FindByEmailAsync(email);
            if (existing is null)
            {
                var owner = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(owner, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(owner, AppRoles.Owner);
                    logger.LogInformation("Seeded Owner user {Email}", email);
                }
                else
                {
                    logger.LogError("Failed to seed Owner: {Errors}",
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
            else if (!await userManager.IsInRoleAsync(existing, AppRoles.Owner))
            {
                await userManager.AddToRoleAsync(existing, AppRoles.Owner);
            }
        }
    }

    private static async Task SeedLookupsAsync(ApplicationDbContext db)
    {
        if (!await db.UnitsOfMeasure.AnyAsync())
        {
            db.UnitsOfMeasure.AddRange(
                new UnitOfMeasure { Name = "Piece", Abbreviation = "Pcs" },
                new UnitOfMeasure { Name = "Set", Abbreviation = "Set" },
                new UnitOfMeasure { Name = "Pair", Abbreviation = "Pair" },
                new UnitOfMeasure { Name = "Kilogram", Abbreviation = "Kg" },
                new UnitOfMeasure { Name = "Carton", Abbreviation = "Ctn" }
            );
        }

        if (!await db.ProductCategories.AnyAsync())
        {
            db.ProductCategories.AddRange(
                new ProductCategory { Name = "Engine Parts" },
                new ProductCategory { Name = "Brake System" },
                new ProductCategory { Name = "Electrical" },
                new ProductCategory { Name = "Body Parts" },
                new ProductCategory { Name = "Filters" },
                new ProductCategory { Name = "Suspension" }
            );
        }

        if (!await db.ExpenseCategories.AnyAsync())
        {
            db.ExpenseCategories.AddRange(
                new ExpenseCategory { Name = "Rent" },
                new ExpenseCategory { Name = "Salary" },
                new ExpenseCategory { Name = "Electricity" },
                new ExpenseCategory { Name = "Delivery" },
                new ExpenseCategory { Name = "Fuel" },
                new ExpenseCategory { Name = "Loading", IsDirectImportCost = true },
                new ExpenseCategory { Name = "Bank Charges" },
                new ExpenseCategory { Name = "Repairs" },
                new ExpenseCategory { Name = "Software" },
                new ExpenseCategory { Name = "Miscellaneous" }
            );
        }

        if (!await db.BusinessSettings.AnyAsync())
        {
            db.BusinessSettings.AddRange(
                new BusinessSetting { Key = "BusinessName", Value = "Auto Parts Business Solution", Description = "Display name" },
                new BusinessSetting { Key = "Currency", Value = "PKR", Description = "Primary currency" },
                new BusinessSetting { Key = "DefaultPageSize", Value = "20", Description = "List page size" },
                new BusinessSetting { Key = "SkuPrefix", Value = "SKU", Description = "Product SKU prefix" },
                new BusinessSetting { Key = "CustomerCodePrefix", Value = "CUS", Description = "Customer code prefix" },
                new BusinessSetting { Key = "SupplierCodePrefix", Value = "SUP", Description = "Supplier code prefix" }
            );
        }

        await db.SaveChangesAsync();
    }
}

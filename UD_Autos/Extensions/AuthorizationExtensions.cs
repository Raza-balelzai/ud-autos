using Microsoft.AspNetCore.Authorization;
using UD_Autos.Authorization;

namespace UD_Autos.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AppPolicies.ViewCosts, p =>
                p.RequireRole(AppRoles.Owner, AppRoles.Manager, AppRoles.Accountant));

            options.AddPolicy(AppPolicies.ViewProfit, p =>
                p.RequireRole(AppRoles.Owner, AppRoles.Manager, AppRoles.Accountant));

            options.AddPolicy(AppPolicies.ManageSettings, p =>
                p.RequireRole(AppRoles.Owner));

            options.AddPolicy(AppPolicies.ManageProducts, p =>
                p.RequireRole(AppRoles.Owner, AppRoles.Manager, AppRoles.Salesman));

            options.AddPolicy(AppPolicies.ManagePurchases, p =>
                p.RequireRole(AppRoles.Owner, AppRoles.Manager));

            options.AddPolicy(AppPolicies.ManageShipments, p =>
                p.RequireRole(AppRoles.Owner, AppRoles.Manager));

            options.AddPolicy(AppPolicies.ManageInventory, p =>
                p.RequireRole(AppRoles.Owner, AppRoles.Manager));

            options.AddPolicy(AppPolicies.ManageSales, p =>
                p.RequireRole(AppRoles.Owner, AppRoles.Manager, AppRoles.Salesman));

            options.AddPolicy(AppPolicies.ManageCustomers, p =>
                p.RequireRole(AppRoles.Owner, AppRoles.Manager, AppRoles.Salesman, AppRoles.Accountant));

            options.AddPolicy(AppPolicies.ManagePayments, p =>
                p.RequireRole(AppRoles.Owner, AppRoles.Manager, AppRoles.Accountant));

            options.AddPolicy(AppPolicies.ManageExpenses, p =>
                p.RequireRole(AppRoles.Owner, AppRoles.Accountant));

            options.AddPolicy(AppPolicies.ViewOperationalReports, p =>
                p.RequireRole(AppRoles.Owner, AppRoles.Manager, AppRoles.Accountant));

            options.AddPolicy(AppPolicies.ViewFinancialReports, p =>
                p.RequireRole(AppRoles.Owner, AppRoles.Accountant));

            options.AddPolicy(AppPolicies.ManageUsers, p =>
                p.RequireRole(AppRoles.Owner));

            // Default: authenticated users for the app shell
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}

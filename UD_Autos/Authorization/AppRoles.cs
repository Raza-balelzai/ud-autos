namespace UD_Autos.Authorization;

public static class AppRoles
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Salesman = "Salesman";
    public const string Accountant = "Accountant";

    public static readonly string[] All =
    [
        Owner,
        Manager,
        Salesman,
        Accountant
    ];
}

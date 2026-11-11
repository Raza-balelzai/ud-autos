namespace UD_Autos.Services;

public static class UnitRoles
{
    public static string Key(string? name) => (name ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsCompleteSet(string? name)
    {
        var key = Key(name);
        return key is "complete set" or "set";
    }

    public static bool IsPair(string? name) => Key(name) == "pair";

    public static bool IsGrouped(string? name) => IsCompleteSet(name) || IsPair(name);
}

namespace UD_Autos.Services;

public static class ShortSetNotes
{
    public static string Marker(int parentId) => $"[short-set:{parentId}]";

    public static bool IsShortSet(string? description) =>
        !string.IsNullOrWhiteSpace(description)
        && description.StartsWith("[short-set:", StringComparison.Ordinal);

    public static bool BelongsTo(string? description, int parentId) =>
        !string.IsNullOrWhiteSpace(description)
        && description.StartsWith(Marker(parentId), StringComparison.Ordinal);

    public static string? Includes(string? description)
    {
        if (!IsShortSet(description))
            return null;

        var line = description!
            .Split('\n')
            .FirstOrDefault(part => part.StartsWith("Includes:", StringComparison.Ordinal));
        return line is null ? string.Empty : line["Includes:".Length..].Trim();
    }

    public static string Build(int parentId, IEnumerable<string> itemNames)
    {
        var text = Marker(parentId) + "\nIncludes: " + string.Join(", ", itemNames);
        return text.Length <= 1000 ? text : text[..1000];
    }
}

namespace UD_Autos.Services;

public static class ProductImageFiles
{
    public static bool TryDelete(IWebHostEnvironment env, string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return true;

        try
        {
            var full = Resolve(env, imagePath);
            if (full is null)
                return true;
            if (!File.Exists(full))
                return true;

            File.Delete(full);
            return !File.Exists(full);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? Resolve(IWebHostEnvironment env, string imagePath)
    {
        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;

        var cleaned = imagePath.Trim().Replace('\\', '/');
        var marker = "uploads/products/";
        var index = cleaned.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return null;

        var relative = cleaned[index..];
        if (relative.Contains("..", StringComparison.Ordinal))
            return null;

        var productsRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads", "products"));
        var full = Path.GetFullPath(Path.Combine(webRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = productsRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return full;
    }
}

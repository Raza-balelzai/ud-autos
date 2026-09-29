namespace UD_Autos.Services;

public interface IFileStorageService
{
    Task<(bool Ok, string? RelativePath, string? Error)> SaveAsync(
        IFormFile file,
        string folder,
        string[] allowedExtensions,
        long maxBytes,
        CancellationToken cancellationToken = default);
}

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;
    private static readonly HashSet<string> DangerousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".js", ".html", ".htm", ".php", ".aspx", ".config"
    };

    public FileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<(bool Ok, string? RelativePath, string? Error)> SaveAsync(
        IFormFile file,
        string folder,
        string[] allowedExtensions,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
            return (false, null, "Empty file.");

        if (file.Length > maxBytes)
            return (false, null, $"File exceeds {maxBytes / 1_000_000} MB limit.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || DangerousExtensions.Contains(ext))
            return (false, null, "File type is not allowed.");

        if (!allowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return (false, null, $"Allowed types: {string.Join(", ", allowedExtensions)}");

        var uploadsRoot = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));
        var safeFolder = string.Join("_", (folder ?? "misc")
            .Split(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Replace("..", string.Empty).Trim())
            .Where(p => p.Length > 0));
        if (string.IsNullOrWhiteSpace(safeFolder)) safeFolder = "misc";

        var physicalFolder = Path.GetFullPath(Path.Combine(uploadsRoot, safeFolder));
        if (!physicalFolder.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
            return (false, null, "Invalid upload folder.");

        Directory.CreateDirectory(physicalFolder);

        var storedName = $"{Guid.NewGuid():N}{ext}";
        var physicalPath = Path.Combine(physicalFolder, storedName);
        await using (var stream = File.Create(physicalPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return (true, $"/uploads/{safeFolder}/{storedName}", null);
    }
}

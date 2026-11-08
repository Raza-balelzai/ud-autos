using System.Text.Json;
using Microsoft.AspNetCore.StaticFiles;

namespace UD_Autos.Services;

public sealed class UdShopDesktopDownloadInfo
{
    public bool IsPublished { get; init; }

    public string ApplicationName { get; init; } = "UD Shop";

    public string Description { get; init; } =
        "Windows desktop application for the shop. Installing from this page keeps one UD Shop application, and later versions replace it.";

    public string? Version { get; init; }

    public string? ReleasedAt { get; init; }

    public string? Notes { get; init; }

    public string? AppInstallerUrl { get; init; }

    public string? MsixUrl { get; init; }

    public string? CertificateUrl { get; init; }
}

public static class UdShopDesktopDownloads
{
    public const string RelativeFolder = "downloads/ud-shop";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static UdShopDesktopDownloadInfo Read(IWebHostEnvironment environment)
    {
        return ReadFolder(Path.Combine(environment.WebRootPath, "downloads", "ud-shop"));
    }

    public static UdShopDesktopDownloadInfo ReadFolder(string folder)
    {
        var unpublished = new UdShopDesktopDownloadInfo();
        var notesPath = Path.Combine(folder, "release-notes.json");
        var installerPath = Path.Combine(folder, "UDShop.appinstaller");
        if (!File.Exists(notesPath) || !File.Exists(installerPath))
        {
            return unpublished;
        }

        ReleaseNotesFile? notes;
        try
        {
            notes = JsonSerializer.Deserialize<ReleaseNotesFile>(File.ReadAllText(notesPath), JsonOptions);
        }
        catch (JsonException)
        {
            return unpublished;
        }

        if (notes is null
            || string.IsNullOrWhiteSpace(notes.Version)
            || string.IsNullOrWhiteSpace(notes.PackageFile))
        {
            return unpublished;
        }

        var packageName = Path.GetFileName(notes.PackageFile);
        if (!packageName.EndsWith(".msix", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(Path.Combine(folder, packageName)))
        {
            return unpublished;
        }

        var certificatePath = Path.Combine(folder, "UDShop.cer");
        return new UdShopDesktopDownloadInfo
        {
            IsPublished = true,
            Version = notes.Version.Trim(),
            ReleasedAt = string.IsNullOrWhiteSpace(notes.ReleasedAt) ? null : notes.ReleasedAt.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes.Notes) ? null : notes.Notes.Trim(),
            AppInstallerUrl = "/downloads/ud-shop/UDShop.appinstaller",
            MsixUrl = "/downloads/ud-shop/" + Uri.EscapeDataString(packageName),
            CertificateUrl = File.Exists(certificatePath) ? "/downloads/ud-shop/UDShop.cer" : null
        };
    }

    public static void UseAppStaticFiles(WebApplication app)
    {
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".appinstaller"] = "application/appinstaller";
        contentTypes.Mappings[".msix"] = "application/msix";
        contentTypes.Mappings[".msixbundle"] = "application/msixbundle";
        contentTypes.Mappings[".cer"] = "application/x-x509-ca-cert";

        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = context =>
            {
                var path = context.Context.Request.Path.Value ?? string.Empty;
                if (!path.Contains("/downloads/ud-shop/", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var name = Path.GetFileName(path);
                if (name.Equals("UDShop.appinstaller", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("release-notes.json", StringComparison.OrdinalIgnoreCase))
                {
                    context.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                    context.Context.Response.Headers.Pragma = "no-cache";
                    context.Context.Response.Headers.Expires = "0";
                }
            }
        });
    }

    private sealed class ReleaseNotesFile
    {
        public string? Version { get; set; }

        public string? ReleasedAt { get; set; }

        public string? Notes { get; set; }

        public string? PackageFile { get; set; }
    }
}

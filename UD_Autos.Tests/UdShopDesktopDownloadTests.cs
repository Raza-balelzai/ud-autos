using UD_Autos.Services;

namespace UD_Autos.Tests;

public class UdShopDesktopDownloadTests
{
    [Fact]
    public void ReadFolder_RequiresInstallerNotesAndPackage()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ud-shop-download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            Assert.False(UdShopDesktopDownloads.ReadFolder(folder).IsPublished);

            File.WriteAllText(Path.Combine(folder, "UDShop.appinstaller"), "<AppInstaller />");
            File.WriteAllText(Path.Combine(folder, "release-notes.json"), """
                {
                  "version": "1.0.0.0",
                  "releasedAt": "2026-10-16",
                  "notes": "First shop release.",
                  "packageFile": "UDShop_1.0.0.0_x64.msix"
                }
                """);
            Assert.False(UdShopDesktopDownloads.ReadFolder(folder).IsPublished);

            File.WriteAllText(Path.Combine(folder, "UDShop_1.0.0.0_x64.msix"), "package");
            var published = UdShopDesktopDownloads.ReadFolder(folder);
            Assert.True(published.IsPublished);
            Assert.Equal("1.0.0.0", published.Version);
            Assert.Equal("First shop release.", published.Notes);
            Assert.Equal("/downloads/ud-shop/UDShop.appinstaller", published.AppInstallerUrl);
            Assert.Equal("/downloads/ud-shop/UDShop_1.0.0.0_x64.msix", published.MsixUrl);
            Assert.Null(published.CertificateUrl);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ReadFolder_IgnoresPackagePathsOutsideTheFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ud-shop-download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "UDShop.appinstaller"), "<AppInstaller />");
            File.WriteAllText(Path.Combine(folder, "release-notes.json"), """
                { "version": "1.0.0.0", "packageFile": "..\\secret.msix" }
                """);

            Assert.False(UdShopDesktopDownloads.ReadFolder(folder).IsPublished);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

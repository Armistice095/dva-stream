using DvaStream.Core.Platform;

namespace DvaStream.Core.Tests;

public class EngineLocatorTests
{
    private static readonly AppDirectories Directories = new("/cfg", "/data");

    [Theory]
    [InlineData(HostOs.Windows, "yt-dlp.exe")]
    [InlineData(HostOs.MacOs, "yt-dlp")]
    [InlineData(HostOs.Linux, "yt-dlp")]
    public void Le_nom_de_l_executable_depend_de_l_OS(HostOs os, string expected) =>
        Assert.Equal(expected, EngineLocator.GetExecutableName(os));

    [Theory]
    [InlineData(HostOs.Windows)]
    [InlineData(HostOs.MacOs)]
    [InlineData(HostOs.Linux)]
    public void Le_chemin_est_dans_le_dossier_engine_des_donnees(HostOs os)
    {
        var path = EngineLocator.GetExecutablePath(Directories, os);

        Assert.Equal(Path.Combine(Directories.EngineDirectory, EngineLocator.GetExecutableName(os)), path);
        Assert.StartsWith(Directories.DataDirectory, path, StringComparison.Ordinal);
    }

    [Fact]
    public void Des_dossiers_nuls_sont_refuses() =>
        Assert.Throws<ArgumentNullException>(() => EngineLocator.GetExecutablePath(null!, HostOs.Linux));
}

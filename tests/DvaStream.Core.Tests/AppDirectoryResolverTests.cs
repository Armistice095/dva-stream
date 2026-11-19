using DvaStream.Core.Platform;

namespace DvaStream.Core.Tests;

public class AppDirectoryResolverTests
{
    private static readonly string Home = Path.Combine(Path.GetTempPath(), "home", "ada");
    private static readonly string RoamingAppData = Path.Combine(Path.GetTempPath(), "appdata", "Roaming");
    private static readonly string LocalAppData = Path.Combine(Path.GetTempPath(), "appdata", "Local");

    private static string? NoEnvironment(string name) => null;

    [Fact]
    public void Windows_utilise_AppData_pour_la_config_et_LocalAppData_pour_les_donnees()
    {
        var dirs = AppDirectoryResolver.Resolve(HostOs.Windows, Home, RoamingAppData, LocalAppData, NoEnvironment);

        Assert.Equal(Path.Combine(RoamingAppData, "DvaStream"), dirs.ConfigDirectory);
        Assert.Equal(Path.Combine(LocalAppData, "DvaStream"), dirs.DataDirectory);
    }

    [Fact]
    public void MacOs_utilise_Library_Application_Support_pour_les_deux()
    {
        var dirs = AppDirectoryResolver.Resolve(HostOs.MacOs, Home, RoamingAppData, LocalAppData, NoEnvironment);

        var expected = Path.Combine(Home, "Library", "Application Support", "DvaStream");
        Assert.Equal(expected, dirs.ConfigDirectory);
        Assert.Equal(expected, dirs.DataDirectory);
    }

    [Fact]
    public void Linux_sans_variables_XDG_utilise_config_et_local_share()
    {
        var dirs = AppDirectoryResolver.Resolve(HostOs.Linux, Home, RoamingAppData, LocalAppData, NoEnvironment);

        Assert.Equal(Path.Combine(Home, ".config", "DvaStream"), dirs.ConfigDirectory);
        Assert.Equal(Path.Combine(Home, ".local", "share", "DvaStream"), dirs.DataDirectory);
    }

    [Fact]
    public void Linux_respecte_XDG_CONFIG_HOME_et_XDG_DATA_HOME_absolus()
    {
        var xdgConfig = Path.Combine(Path.GetTempPath(), "xdg", "config");
        var xdgData = Path.Combine(Path.GetTempPath(), "xdg", "data");
        string? Env(string name) => name switch
        {
            "XDG_CONFIG_HOME" => xdgConfig,
            "XDG_DATA_HOME" => xdgData,
            _ => null,
        };

        var dirs = AppDirectoryResolver.Resolve(HostOs.Linux, Home, RoamingAppData, LocalAppData, Env);

        Assert.Equal(Path.Combine(xdgConfig, "DvaStream"), dirs.ConfigDirectory);
        Assert.Equal(Path.Combine(xdgData, "DvaStream"), dirs.DataDirectory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relatif/chemin")]
    public void Linux_ignore_les_variables_XDG_vides_ou_relatives(string value)
    {
        string? Env(string name) => value;

        var dirs = AppDirectoryResolver.Resolve(HostOs.Linux, Home, RoamingAppData, LocalAppData, Env);

        Assert.Equal(Path.Combine(Home, ".config", "DvaStream"), dirs.ConfigDirectory);
        Assert.Equal(Path.Combine(Home, ".local", "share", "DvaStream"), dirs.DataDirectory);
    }

    [Fact]
    public void Les_sous_dossiers_derivent_du_dossier_de_donnees()
    {
        var dirs = new AppDirectories("config", "data");

        Assert.Equal(Path.Combine("data", "logs"), dirs.LogsDirectory);
        Assert.Equal(Path.Combine("data", "engine"), dirs.EngineDirectory);
        Assert.Equal(Path.Combine("data", "temp"), dirs.TempDirectory);
    }

    [Fact]
    public void Un_dossier_personnel_vide_est_refuse()
    {
        Assert.Throws<ArgumentException>(() =>
            AppDirectoryResolver.Resolve(HostOs.Linux, " ", RoamingAppData, LocalAppData, NoEnvironment));
    }
}

using DvaStream.Core.Settings;

namespace DvaStream.Core.Tests.Settings;

/// <summary>M3-01 : valeurs par défaut et correction des valeurs invalides.</summary>
public class AppSettingsTests
{
    private static readonly AppSettings Defaults = AppSettings.CreateDefaults(@"C:\Telechargements");

    [Fact]
    public void Les_valeurs_par_defaut_sont_celles_du_TDD()
    {
        Assert.Equal(AppSettings.CurrentSchemaVersion, Defaults.SchemaVersion);
        Assert.Equal(@"C:\Telechargements", Defaults.DefaultFolder);
        Assert.Equal(ThemeChoice.System, Defaults.Theme);
        Assert.Equal(1080, Defaults.PreferredHeight);
        Assert.Equal(AudioFormat.M4a, Defaults.PreferredAudio);
        Assert.True(Defaults.ClipboardDetection);
        Assert.Equal("auto", Defaults.Language);
    }

    [Fact]
    public void Des_valeurs_valides_sont_conservees()
    {
        var s = new AppSettings(1, "D:/x", ThemeChoice.Dark, 720, AudioFormat.Mp3, false, "EN");

        var n = s.Normalize(Defaults);

        Assert.Equal(new AppSettings(1, "D:/x", ThemeChoice.Dark, 720, AudioFormat.Mp3, false, "en"), n);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(100000)]
    public void Une_hauteur_invalide_revient_a_la_valeur_par_defaut(int height)
    {
        Assert.Equal(1080, (Defaults with { PreferredHeight = height }).Normalize(Defaults).PreferredHeight);
    }

    [Fact]
    public void Les_autres_valeurs_invalides_sont_corrigees()
    {
        var bad = new AppSettings(99, "  ", (ThemeChoice)42, 1080, (AudioFormat)42, true, "klingon");

        var n = bad.Normalize(Defaults);

        Assert.Equal(AppSettings.CurrentSchemaVersion, n.SchemaVersion);
        Assert.Equal(Defaults.DefaultFolder, n.DefaultFolder);
        Assert.Equal(ThemeChoice.System, n.Theme);
        Assert.Equal(AudioFormat.M4a, n.PreferredAudio);
        Assert.Equal("auto", n.Language);
    }

    [Fact]
    public void Une_langue_nulle_est_corrigee()
    {
        Assert.Equal("auto", (Defaults with { Language = null! }).Normalize(Defaults).Language);
    }
}

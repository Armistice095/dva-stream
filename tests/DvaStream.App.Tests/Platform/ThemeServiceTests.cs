using Avalonia.Styling;
using DvaStream.App.Platform;
using DvaStream.Core.Settings;

namespace DvaStream.App.Tests.Platform;

/// <summary>M3-07 : correspondance entre le choix de thème et la variante Avalonia.</summary>
public class ThemeServiceTests
{
    [Fact]
    public void Chaque_choix_correspond_a_sa_variante()
    {
        Assert.Equal(ThemeVariant.Light, ThemeService.ToVariant(ThemeChoice.Light));
        Assert.Equal(ThemeVariant.Dark, ThemeService.ToVariant(ThemeChoice.Dark));
        Assert.Equal(ThemeVariant.Default, ThemeService.ToVariant(ThemeChoice.System));
    }

    [Fact]
    public void Un_choix_inconnu_suit_le_systeme()
    {
        Assert.Equal(ThemeVariant.Default, ThemeService.ToVariant((ThemeChoice)99));
    }

    [Fact]
    public void Un_magasin_nul_est_refuse()
    {
        Assert.Throws<ArgumentNullException>(() => new ThemeService(null!));
    }
}

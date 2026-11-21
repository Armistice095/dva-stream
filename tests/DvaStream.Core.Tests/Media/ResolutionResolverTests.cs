using DvaStream.Core.Media;

namespace DvaStream.Core.Tests.Media;

/// <summary>M3-04 : résolution de repli.</summary>
public class ResolutionResolverTests
{
    [Theory]
    [InlineData(1080, new[] { 2160, 1080, 720 }, 1080)]      // préférée disponible
    [InlineData(1080, new[] { 720, 480, 360 }, 720)]         // la plus proche inférieure
    [InlineData(1080, new[] { 2160, 720, 144 }, 720)]        // inférieure même s'il y a plus haut
    [InlineData(1080, new[] { 1440, 2160 }, 1440)]           // tout au-dessus : la plus basse
    [InlineData(720, new[] { 1032, 688, 344 }, 688)]         // hauteurs non standard : 688 < 720
    [InlineData(720, new[] { 1032, 344 }, 344)]
    [InlineData(1080, new[] { 1032, 2160 }, 1032)]
    public void Choisit_la_bonne_resolution(int preferred, int[] available, int expected)
    {
        Assert.Equal(expected, ResolutionResolver.Resolve(preferred, available));
    }

    [Fact]
    public void Sans_resolution_le_resultat_est_nul()
    {
        Assert.Null(ResolutionResolver.Resolve(1080, []));
        Assert.Null(ResolutionResolver.Resolve(1080, new[] { 0, -5 }));
    }

    [Fact]
    public void Une_liste_nulle_est_refusee()
    {
        Assert.Throws<ArgumentNullException>(() => ResolutionResolver.Resolve(1080, null!));
    }
}

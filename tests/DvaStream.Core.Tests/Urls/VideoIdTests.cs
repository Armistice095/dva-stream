using DvaStream.Core.Urls;

namespace DvaStream.Core.Tests.Urls;

public class VideoIdTests
{
    [Theory]
    [InlineData("dQw4w9WgXcQ")]
    [InlineData("a-b_c-d_e-f")]
    [InlineData("___________")]
    [InlineData("-----------")]
    [InlineData("00000000000")]
    public void Un_identifiant_bien_forme_est_cree(string candidate)
    {
        Assert.True(VideoId.IsValid(candidate));
        Assert.Equal(candidate, Create(candidate).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dQw4w9WgXc")]      // 10 caractères
    [InlineData("dQw4w9WgXcQQ")]    // 12 caractères
    [InlineData("dQw4w9WgXc ")]     // espace
    [InlineData("dQw4w9WgXc!")]
    [InlineData("dQw4w9WgXc/")]
    [InlineData("dQw4w9WgXcé")]     // lettre non ASCII
    [InlineData("dQw4w9WgXc\u0661")] // chiffre arabo-indien : char.IsDigit l'accepterait
    public void Un_identifiant_mal_forme_est_refuse(string? candidate)
    {
        Assert.False(VideoId.IsValid(candidate));
        Assert.False(VideoId.TryCreate(candidate, out var id));
        Assert.Null(id);
    }

    [Fact]
    public void Deux_identifiants_de_meme_valeur_sont_egaux()
    {
        var a = Create("dQw4w9WgXcQ");
        var b = Create("dQw4w9WgXcQ");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void La_casse_est_significative()
    {
        var a = Create("dQw4w9WgXcQ");
        var b = Create("DQW4W9WGXCQ");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ToString_renvoie_la_valeur_brute()
    {
        var id = Create("dQw4w9WgXcQ");

        Assert.Equal("dQw4w9WgXcQ", id.ToString());
    }

    private static VideoId Create(string candidate)
    {
        if (!VideoId.TryCreate(candidate, out var id))
        {
            Assert.Fail($"« {candidate} » aurait dû être un identifiant valide.");
        }

        return id;
    }
}

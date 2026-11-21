using System.Globalization;
using DvaStream.App.Localization;
using DvaStream.Core.Urls;

namespace DvaStream.App.Tests.Localization;

/// <summary>M1-04 et M1-05 : textes présents en français et en anglais, un message par raison de refus.</summary>
[Collection(CultureCollection.Name)]
public class StringsTests
{
    private static readonly CultureInfo French = new("fr");
    private static readonly CultureInfo English = new("en");

    public static TheoryData<UrlInvalidReason> AllReasons()
    {
        var data = new TheoryData<UrlInvalidReason>();
        foreach (var reason in Enum.GetValues<UrlInvalidReason>())
        {
            data.Add(reason);
        }

        return data;
    }

    [Fact]
    public void Un_texte_de_test_est_different_en_francais_et_en_anglais()
    {
        var fr = Strings.Get("Main_Status", French);
        var en = Strings.Get("Main_Status", English);

        Assert.Equal("Fondations en place (jalon M0)", fr);
        Assert.Equal("Foundations in place (milestone M0)", en);
    }

    [Fact]
    public void Une_cle_inconnue_est_signalee_sans_exception()
    {
        Assert.Equal("[Cle_Inexistante]", Strings.Get("Cle_Inexistante", French));
    }

    [Fact]
    public void Les_fichiers_francais_et_anglais_definissent_les_memes_cles()
    {
        var fr = Strings.GetKeys(CultureInfo.InvariantCulture);
        var en = Strings.GetKeys(English);

        Assert.NotEmpty(fr);
        Assert.True(fr.SetEquals(en), "Clés différentes entre Strings.resx et Strings.en.resx.");
    }

    [Theory]
    [MemberData(nameof(AllReasons))]
    public void Chaque_raison_de_refus_a_un_message_en_francais_et_en_anglais(UrlInvalidReason reason)
    {
        var key = UrlErrorMessages.KeyFor(reason);

        AssertTranslated(key, French);
        AssertTranslated(key, English);
    }

    [Fact]
    public void Le_message_de_playlist_existe_en_francais_et_en_anglais()
    {
        AssertTranslated(UrlErrorMessages.PlaylistKey, French);
        AssertTranslated(UrlErrorMessages.PlaylistKey, English);
    }

    [Fact]
    public void Une_raison_inconnue_est_rejetee()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UrlErrorMessages.KeyFor((UrlInvalidReason)999));
    }

    [Fact]
    public void Les_messages_utilisent_la_culture_forcee()
    {
        var previous = Strings.Culture;
        try
        {
            Strings.Culture = English;
            Assert.Equal("Playlists are not supported in this version.", UrlErrorMessages.ForPlaylist());
            Assert.Contains("not from YouTube", UrlErrorMessages.For(UrlInvalidReason.UnsupportedHost), StringComparison.Ordinal);

            Strings.Culture = French;
            Assert.Equal("Les playlists ne sont pas prises en charge dans cette version.", UrlErrorMessages.ForPlaylist());
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    private static void AssertTranslated(string key, CultureInfo culture)
    {
        var text = Strings.Get(key, culture);

        Assert.False(string.IsNullOrWhiteSpace(text), $"{key} vide en {culture.Name}.");
        Assert.False(text.StartsWith('['), $"{key} absente en {culture.Name}.");
    }
}

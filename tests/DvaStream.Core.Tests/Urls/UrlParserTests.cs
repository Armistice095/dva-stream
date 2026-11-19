using DvaStream.Core.Urls;

namespace DvaStream.Core.Tests.Urls;

/// <summary>Cas de M1-03 : validation stricte des URL (FR-01, FR-03, FR-04, TDD §12).</summary>
public class UrlParserTests
{
    private const string Id = "dQw4w9WgXcQ";

    private readonly UrlParser _parser = new();

    // ---------------------------------------------------------------- URL valides

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", Id)]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ", Id)]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ", Id)]
    [InlineData("http://www.youtube.com/watch?v=dQw4w9WgXcQ", Id)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", Id)]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", Id)]
    [InlineData("https://m.youtube.com/shorts/dQw4w9WgXcQ", Id)]
    [InlineData("https://youtube.com/shorts/dQw4w9WgXcQ?feature=share", Id)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=42s", Id)]
    [InlineData("https://www.youtube.com/watch?feature=share&v=dQw4w9WgXcQ", Id)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ#t=30", Id)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=42", Id)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=AbC123xyz", Id)]
    [InlineData("HTTPS://WWW.YOUTUBE.COM/WATCH?v=dQw4w9WgXcQ", Id)]
    [InlineData("https://YouTu.be/dQw4w9WgXcQ", Id)]
    [InlineData("  https://youtu.be/dQw4w9WgXcQ\n", Id)]
    [InlineData("\thttps://www.youtube.com/watch?v=dQw4w9WgXcQ  ", Id)]
    [InlineData("https://youtu.be/a-b_c-d_e-f", "a-b_c-d_e-f")]
    [InlineData("https://www.youtube.com:443/watch?v=dQw4w9WgXcQ", Id)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=", Id)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&v=aaaaaaaaaaa", Id)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&v=", Id)]
    public void Une_url_de_video_valide_renvoie_son_identifiant(string input, string expectedId)
    {
        var result = _parser.Parse(input);

        var valid = Assert.IsType<UrlParseResult.Valid>(result);
        Assert.Equal(expectedId, valid.VideoId.Value);
    }

    // ---------------------------------------------------------------- Playlists (FR-04)

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PLabc123", "PLabc123", null)]
    [InlineData("https://www.youtube.com/playlist?list=PLabc123&v=dQw4w9WgXcQ", "PLabc123", null)]
    [InlineData("https://www.youtube.com/watch?list=PLabc123", "PLabc123", null)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLabc123", "PLabc123", Id)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLabc123&index=3", "PLabc123", Id)]
    [InlineData("https://www.youtube.com/watch?list=PLabc123&v=dQw4w9WgXcQ", "PLabc123", Id)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=RDdQw4w9WgXcQ", "RDdQw4w9WgXcQ", Id)]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?list=PLabc123", "PLabc123", Id)]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ?list=PLabc123", "PLabc123", Id)]
    public void Une_url_avec_list_est_une_playlist_et_conserve_la_video_si_elle_existe(
        string input, string expectedPlaylistId, string? expectedVideoId)
    {
        var result = _parser.Parse(input);

        var playlist = Assert.IsType<UrlParseResult.Playlist>(result);
        Assert.Equal(expectedPlaylistId, playlist.PlaylistId);
        Assert.Equal(expectedVideoId, playlist.VideoId?.Value);
    }

    // ---------------------------------------------------------------- Refus

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Une_saisie_vide_est_refusee_comme_vide(string? input) =>
        AssertInvalid(input, UrlInvalidReason.Empty);

    [Theory]
    [InlineData("bonjour")]
    [InlineData("bonjour tout le monde")]
    [InlineData("dQw4w9WgXcQ")]
    [InlineData("www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ extra")]
    [InlineData("https://www.youtube.com/wa tch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ\0")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ\u0007")]
    public void Un_texte_qui_n_est_pas_une_url_est_refuse(string input) =>
        AssertInvalid(input, UrlInvalidReason.NotAUrl);

    [Fact]
    public void Une_saisie_demesuree_est_refusee_sans_etre_analysee()
    {
        var input = "https://www.youtube.com/watch?v=" + new string('a', UrlParser.MaxInputLength);

        AssertInvalid(input, UrlInvalidReason.NotAUrl);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://youtu.be/dQw4w9WgXcQ")]
    [InlineData("mailto:quelquun@example.com")]
    [InlineData("data:text/plain,hello")]
    public void Un_schema_autre_que_http_ou_https_est_refuse(string input) =>
        AssertInvalid(input, UrlInvalidReason.UnsupportedScheme);

    [Theory]
    [InlineData("https://evil.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://notyoutube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com.evil.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be.evil.com/dQw4w9WgXcQ")]
    [InlineData("https://youtube.com@evil.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://evil.com@youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://user:secret@youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com:8443/watch?v=dQw4w9WgXcQ")]
    [InlineData("http://127.0.0.1/watch?v=dQw4w9WgXcQ")]
    public void Un_domaine_non_autorise_ou_une_forme_trompeuse_est_refuse(string input) =>
        AssertInvalid(input, UrlInvalidReason.UnsupportedHost);

    [Theory]
    [InlineData("https://www.youtube.com")]
    [InlineData("https://www.youtube.com/")]
    [InlineData("https://www.youtube.com/watch")]
    [InlineData("https://www.youtube.com/watch?v=")]
    [InlineData("https://www.youtube.com/watch?list=")]
    [InlineData("https://www.youtube.com/watch/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/@DVALabs")]
    [InlineData("https://www.youtube.com/channel/UC1234567890")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ/extra")]
    [InlineData("https://www.youtube.com/playlist")]
    [InlineData("https://www.youtube.com/playlist?list=")]
    [InlineData("https://youtu.be")]
    [InlineData("https://youtu.be/")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ/extra")]
    public void Un_domaine_autorise_avec_un_format_non_reconnu_est_refuse(string input) =>
        AssertInvalid(input, UrlInvalidReason.UnrecognizedFormat);

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXc")]       // 10 caractères
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQQ")]     // 12 caractères
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXc!")]      // caractère interdit
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXc.")]      // caractère interdit
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcé")]      // lettre non ASCII
    [InlineData("https://www.youtube.com/watch?v=court")]
    [InlineData("https://youtu.be/court")]
    [InlineData("https://youtu.be/dQw4w9WgXcQQ")]
    [InlineData("https://www.youtube.com/shorts/court")]
    [InlineData("https://www.youtube.com/watch?v=court&list=PLabc123")]
    public void Un_identifiant_de_video_mal_forme_est_refuse(string input) =>
        AssertInvalid(input, UrlInvalidReason.InvalidVideoId);

    // ---------------------------------------------------------------- Robustesse

    [Theory]
    [InlineData("%")]
    [InlineData("https://%")]
    [InlineData("http://[::1")]
    [InlineData("????")]
    [InlineData("https://www.youtube.com/watch?v=%E0%A4%A&list=%")]
    [InlineData("https://www.youtube.com/watch?=&&&")]
    [InlineData("https://www.youtube.com/watch?v")]
    public void Aucune_saisie_ne_provoque_d_exception(string input)
    {
        var result = _parser.Parse(input);

        Assert.IsType<UrlParseResult.Invalid>(result);
    }

    private void AssertInvalid(string? input, UrlInvalidReason expected)
    {
        var result = _parser.Parse(input);

        var invalid = Assert.IsType<UrlParseResult.Invalid>(result);
        Assert.Equal(expected, invalid.Reason);
    }
}

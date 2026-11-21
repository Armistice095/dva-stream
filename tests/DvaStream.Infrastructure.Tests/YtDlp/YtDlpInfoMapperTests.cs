using DvaStream.Core.Errors;
using DvaStream.Core.Media;
using DvaStream.Infrastructure.YtDlp;

namespace DvaStream.Infrastructure.Tests.YtDlp;

/// <summary>M2-05 : lecture tolérante de la sortie de yt-dlp et conversion en <see cref="MediaInfo"/>.</summary>
public class YtDlpInfoMapperTests
{
    public static TheoryData<string> Successes() => YtDlpFixtures.Successes();

    [Theory]
    [MemberData(nameof(Successes))]
    public void Les_sorties_reelles_sont_converties(string name)
    {
        var info = YtDlpInfoMapper.Parse(YtDlpFixtures.Json(name));

        Assert.Equal(11, info.Id.Value.Length);
        Assert.False(string.IsNullOrWhiteSpace(info.Title));
        Assert.False(string.IsNullOrWhiteSpace(info.Channel));
        Assert.True(info.Duration > TimeSpan.Zero);
        Assert.NotEmpty(info.Streams);
    }

    [Theory]
    [MemberData(nameof(Successes))]
    public void Aucune_planche_de_vignettes_n_est_listee_et_chaque_flux_est_coherent(string name)
    {
        var info = YtDlpInfoMapper.Parse(YtDlpFixtures.Json(name));

        foreach (var s in info.Streams)
        {
            Assert.NotEqual("mhtml", s.Container);
            Assert.False(string.IsNullOrWhiteSpace(s.FormatId));
            switch (s.Kind)
            {
                case StreamKind.Video:
                    Assert.NotNull(s.VideoCodec);
                    Assert.Null(s.AudioCodec);
                    Assert.NotNull(s.Height);
                    break;
                case StreamKind.Audio:
                    Assert.Null(s.VideoCodec);
                    Assert.Null(s.Height);
                    break;
                case StreamKind.Muxed:
                    Assert.NotNull(s.VideoCodec);
                    Assert.NotNull(s.AudioCodec);
                    break;
            }
        }
    }

    [Fact]
    public void La_video_standard_expose_ses_resolutions_et_ses_flux_audio()
    {
        var info = YtDlpInfoMapper.Parse(YtDlpFixtures.Json("standard"));

        Assert.Equal("dQw4w9WgXcQ", info.Id.Value);
        var heights = info.Streams.Where(s => s.Kind == StreamKind.Video).Select(s => s.Height).Distinct().ToList();
        Assert.Contains(144, heights);
        Assert.Contains(1080, heights);
        Assert.Contains(2160, heights);
        Assert.Contains(info.Streams, s => s.Kind == StreamKind.Audio && s.AudioCodec == "opus");
    }

    [Fact]
    public void La_taille_exacte_est_preferee_a_l_estimation_et_les_fps_decimaux_sont_arrondis()
    {
        var info = YtDlpInfoMapper.Parse(YtDlpFixtures.Json("standard"));

        var f137 = info.Streams.Single(s => s.FormatId == "137");
        Assert.Equal(80911999, f137.Bytes);
        Assert.Equal(25, f137.Fps);
        Assert.Equal(StreamKind.Video, f137.Kind);
        Assert.Equal("mp4", f137.Container);

        var hls = info.Streams.Single(s => s.FormatId == "269");
        Assert.Null(hls.Bytes);
        Assert.Equal(25, hls.Fps);
    }

    [Fact]
    public void L_audio_HLS_sans_codec_connu_est_classe_en_audio()
    {
        var info = YtDlpInfoMapper.Parse(YtDlpFixtures.Json("standard"));

        var hlsAudio = info.Streams.Single(s => s.FormatId == "234");
        Assert.Equal(StreamKind.Audio, hlsAudio.Kind);
        Assert.Null(hlsAudio.AudioCodec);
    }

    [Fact]
    public void La_miniature_est_une_uri_absolue()
    {
        var info = YtDlpInfoMapper.Parse(YtDlpFixtures.Json("short"));

        Assert.NotNull(info.ThumbnailUrl);
        Assert.StartsWith("http", info.ThumbnailUrl!.Scheme, StringComparison.Ordinal);
    }

    [Fact]
    public void Une_sortie_minimale_est_acceptee_avec_des_valeurs_par_defaut()
    {
        var info = YtDlpInfoMapper.Parse("""{"id":"dQw4w9WgXcQ"}""");

        Assert.Equal("dQw4w9WgXcQ", info.Title);
        Assert.Equal(string.Empty, info.Channel);
        Assert.Equal(TimeSpan.Zero, info.Duration);
        Assert.Null(info.ThumbnailUrl);
        Assert.Empty(info.Streams);
    }

    [Fact]
    public void La_chaine_est_remplacee_par_l_auteur_et_les_champs_inconnus_sont_ignores()
    {
        var info = YtDlpInfoMapper.Parse(
            """{"id":"dQw4w9WgXcQ","title":" T ","channel":"","uploader":"Auteur","duration":12.5,"nouveau_champ":{"x":1},"thumbnail":"pas une url"}""");

        Assert.Equal("T", info.Title);
        Assert.Equal("Auteur", info.Channel);
        Assert.Equal(TimeSpan.FromSeconds(12.5), info.Duration);
        Assert.Null(info.ThumbnailUrl);
    }

    [Fact]
    public void Un_format_avec_image_et_son_est_classe_en_muxed_et_la_taille_decimale_est_acceptee()
    {
        var info = YtDlpInfoMapper.Parse(
            """{"id":"dQw4w9WgXcQ","formats":[{"format_id":"18","ext":"mp4","vcodec":"avc1.42001E","acodec":"mp4a.40.2","height":360,"fps":29.97,"filesize_approx":1234567.0}]}""");

        var s = Assert.Single(info.Streams);
        Assert.Equal(StreamKind.Muxed, s.Kind);
        Assert.Equal(1234567L, s.Bytes);
        Assert.Equal(30, s.Fps);
    }

    [Fact]
    public void Une_playlist_est_refusee()
    {
        var ex = Assert.Throws<MediaSourceException>(() => YtDlpInfoMapper.Parse("""{"_type":"playlist","id":"PLabc"}"""));

        Assert.Equal(ErrorKind.UnsupportedPlaylist, ex.Error.Kind);
    }

    [Theory]
    [InlineData("""{"title":"sans id"}""")]
    [InlineData("""{"id":"trop-court"}""")]
    public void Un_identifiant_absent_ou_invalide_est_refuse(string json)
    {
        var ex = Assert.Throws<MediaSourceException>(() => YtDlpInfoMapper.Parse(json));

        Assert.Equal(ErrorKind.EngineBroken, ex.Error.Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("pas du json")]
    [InlineData("null")]
    [InlineData("[1,2]")]
    public void Une_sortie_illisible_est_signalee_comme_moteur_defaillant(string json)
    {
        var ex = Assert.Throws<MediaSourceException>(() => YtDlpInfoMapper.Parse(json));

        Assert.Equal(ErrorKind.EngineBroken, ex.Error.Kind);
    }
}

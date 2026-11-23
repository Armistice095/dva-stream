using DvaStream.Core.Media;
using DvaStream.Core.Urls;

namespace DvaStream.Core.Tests.Media;

/// <summary>M2-10 : résolutions réellement proposées (FR-12).</summary>
public class MediaInfoExtensionsTests
{
    private static MediaStream Stream(StreamKind kind, int? height, string id = "x") =>
        new(id, kind, height, "mp4", kind == StreamKind.Audio ? null : "avc1", kind == StreamKind.Video ? null : "mp4a", null, null);

    private static MediaInfo Info(params MediaStream[] streams)
    {
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        return new MediaInfo(id!, "t", "c", TimeSpan.FromSeconds(1), null, streams);
    }

    [Fact]
    public void Les_hauteurs_sont_triees_de_la_plus_grande_a_la_plus_petite_sans_doublon()
    {
        var info = Info(
            Stream(StreamKind.Video, 360),
            Stream(StreamKind.Video, 1080),
            Stream(StreamKind.Video, 1080),
            Stream(StreamKind.Muxed, 720),
            Stream(StreamKind.Video, 144));

        Assert.Equal(new[] { 1080, 720, 360, 144 }, info.GetAvailableResolutions().ToArray());
    }

    [Fact]
    public void L_audio_seul_et_les_flux_sans_hauteur_ne_comptent_pas()
    {
        var info = Info(Stream(StreamKind.Audio, null), Stream(StreamKind.Video, null), Stream(StreamKind.Video, 0));

        Assert.Empty(info.GetAvailableResolutions());
    }

    [Fact]
    public void Sans_aucun_flux_la_liste_est_vide()
    {
        Assert.Empty(Info().GetAvailableResolutions());
    }

    [Fact]
    public void HasAudio_detecte_l_audio_seul_ou_avec_image()
    {
        Assert.True(Info(Stream(StreamKind.Audio, null)).HasAudio());
        Assert.True(Info(Stream(StreamKind.Muxed, 360)).HasAudio());
        Assert.False(Info(Stream(StreamKind.Video, 360)).HasAudio());
    }
}

/// <summary>M3-08 : taille estimée.</summary>
public class EstimateSizeTests
{
    private static MediaInfo Info(params MediaStream[] streams)
    {
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        return new MediaInfo(id!, "t", "c", TimeSpan.FromSeconds(1), null, streams);
    }

    [Fact]
    public void Video_prefere_h264_et_ajoute_laudio_aac()
    {
        var info = Info(
            new MediaStream("a", StreamKind.Video, 1080, "webm", "vp9", null, 90, null),
            new MediaStream("b", StreamKind.Video, 1080, "mp4", "avc1.64", null, 100, null),
            new MediaStream("c", StreamKind.Audio, null, "webm", null, "opus", 50, null),
            new MediaStream("d", StreamKind.Audio, null, "m4a", null, "mp4a.40", 10, null));

        Assert.Equal(110, info.EstimateSizeBytes(DownloadMode.Video, 1080));
        Assert.Equal(10, info.EstimateSizeBytes(DownloadMode.Audio, null));
    }

    [Fact]
    public void Flux_avec_son_et_taille_inconnue()
    {
        var info = Info(
            new MediaStream("m", StreamKind.Muxed, 360, "mp4", "avc1", "mp4a", 40, null),
            new MediaStream("v", StreamKind.Video, 720, "mp4", "avc1", null, null, null));

        Assert.Equal(40, info.EstimateSizeBytes(DownloadMode.Video, 360));
        Assert.Null(info.EstimateSizeBytes(DownloadMode.Video, 720));
        Assert.Null(info.EstimateSizeBytes(DownloadMode.Video, null));
        Assert.Null(info.EstimateSizeBytes(DownloadMode.Audio, null));
    }
}

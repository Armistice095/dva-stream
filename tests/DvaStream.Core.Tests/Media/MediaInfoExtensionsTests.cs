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

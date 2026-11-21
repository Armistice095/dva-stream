using DvaStream.Core.Media;
using DvaStream.Core.Settings;
using DvaStream.Core.Urls;

namespace DvaStream.Core.Tests.Media;

/// <summary>M3-09 : construction et validation d'une demande de téléchargement.</summary>
public class DownloadRequestTests
{
    private static readonly string Folder = Path.GetFullPath(Path.GetTempPath());

    private static MediaStream Video(int height) =>
        new($"v{height}", StreamKind.Video, height, "mp4", "avc1", null, null, 25);

    private static MediaStream Audio() => new("a1", StreamKind.Audio, null, "m4a", null, "mp4a", null, null);

    private static MediaInfo Info(params MediaStream[] streams)
    {
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        return new MediaInfo(id!, "Mon titre", "Chaîne", TimeSpan.FromMinutes(1), null, streams);
    }

    [Fact]
    public void Une_demande_video_valide_est_construite()
    {
        var result = DownloadRequest.Create(Info(Video(1080), Video(720), Audio()), DownloadMode.Video, 720, AudioFormat.M4a, Folder);

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
        var request = result.Request!;
        Assert.Equal("dQw4w9WgXcQ", request.Id.Value);
        Assert.Equal("Mon titre", request.Title);
        Assert.Equal(DownloadMode.Video, request.Mode);
        Assert.Equal(720, request.Height);
        Assert.Equal(Folder, request.Folder);
    }

    [Fact]
    public void Une_demande_audio_valide_ignore_la_hauteur()
    {
        var result = DownloadRequest.Create(Info(Video(720), Audio()), DownloadMode.Audio, 720, AudioFormat.M4a, Folder);

        Assert.True(result.IsValid);
        Assert.Null(result.Request!.Height);
        Assert.Equal(AudioFormat.M4a, result.Request.AudioFormat);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_dossier_absent_est_refuse(string? folder)
    {
        var result = DownloadRequest.Create(Info(Video(720), Audio()), DownloadMode.Video, 720, AudioFormat.M4a, folder);

        Assert.Equal(DownloadRequestError.FolderMissing, result.Error);
        Assert.Null(result.Request);
    }

    [Theory]
    [InlineData("dossier/relatif")]
    [InlineData("\0")]
    public void Un_dossier_invalide_ou_relatif_est_refuse(string folder)
    {
        var result = DownloadRequest.Create(Info(Video(720), Audio()), DownloadMode.Video, 720, AudioFormat.M4a, folder);

        Assert.Equal(DownloadRequestError.FolderInvalid, result.Error);
    }

    [Fact]
    public void Le_mode_video_sans_hauteur_est_refuse()
    {
        var result = DownloadRequest.Create(Info(Video(720), Audio()), DownloadMode.Video, null, AudioFormat.M4a, Folder);

        Assert.Equal(DownloadRequestError.HeightMissing, result.Error);
    }

    [Fact]
    public void Une_resolution_non_offerte_est_refusee()
    {
        var result = DownloadRequest.Create(Info(Video(720), Audio()), DownloadMode.Video, 1080, AudioFormat.M4a, Folder);

        Assert.Equal(DownloadRequestError.ResolutionNotOffered, result.Error);
    }

    [Fact]
    public void Le_mode_video_sans_flux_video_est_refuse()
    {
        var result = DownloadRequest.Create(Info(Audio()), DownloadMode.Video, 720, AudioFormat.M4a, Folder);

        Assert.Equal(DownloadRequestError.NoVideoStream, result.Error);
    }

    [Fact]
    public void Le_mode_audio_sans_flux_sonore_est_refuse()
    {
        var result = DownloadRequest.Create(Info(Video(720)), DownloadMode.Audio, null, AudioFormat.M4a, Folder);

        Assert.Equal(DownloadRequestError.NoAudioStream, result.Error);
    }

    [Fact]
    public void Une_video_sans_audio_reste_telechargeable_en_mode_video()
    {
        var result = DownloadRequest.Create(Info(Video(720)), DownloadMode.Video, 720, AudioFormat.M4a, Folder);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Une_information_nulle_est_refusee()
    {
        Assert.Throws<ArgumentNullException>(() => DownloadRequest.Create(null!, DownloadMode.Video, 720, AudioFormat.M4a, Folder));
    }
}

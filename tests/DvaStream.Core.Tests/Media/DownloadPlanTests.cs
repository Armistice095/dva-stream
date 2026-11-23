using DvaStream.Core.Media;
using DvaStream.Core.Settings;
using DvaStream.Core.Urls;

namespace DvaStream.Core.Tests.Media;

/// <summary>M4-03 : plan de téléchargement (sélecteurs de format, dossier temporaire, extension).</summary>
public class DownloadPlanTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "dva-plan");
    private static readonly Guid Job = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static MediaInfo Info(params MediaStream[] streams)
    {
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        return new MediaInfo(id!, "Titre", "Chaîne", TimeSpan.FromMinutes(3), null, streams);
    }

    private static MediaStream V(int h, long? bytes = null, string codec = "avc1.64") =>
        new($"v{h}", StreamKind.Video, h, "mp4", codec, null, bytes, 25);

    private static MediaStream A(string codec = "mp4a.40.2", long? bytes = null) =>
        new("a1", StreamKind.Audio, null, "m4a", null, codec, bytes, null);

    private static DownloadRequest Request(MediaInfo info, DownloadMode mode, int? height = null, AudioFormat audio = AudioFormat.M4a)
    {
        var result = DownloadRequest.Create(info, mode, height, audio, Path.Combine(Path.GetTempPath(), "dest"));
        return Assert.IsType<DownloadRequest>(result.Request);
    }

    [Fact]
    public void Video_1080p_prefere_H264_et_AAC_puis_repli_et_fusionne_en_MP4()
    {
        var info = Info(V(1080), A());

        var plan = DownloadPlan.Create(Request(info, DownloadMode.Video, 1080), info, Root, Job);

        Assert.Equal(
            "bv*[vcodec^=avc1][height=1080]+ba[acodec^=mp4a]/bv*[height=1080]+ba[acodec^=mp4a]/bv*[height=1080]+ba/b[height<=1080]",
            plan.FormatSelector);
        Assert.Equal(new[] { "-f", plan.FormatSelector, "--merge-output-format", "mp4" }, plan.FormatArguments);
        Assert.Equal("mp4", plan.Extension);
    }

    [Fact]
    public void Aucun_plan_ne_demande_de_reencodage_ni_de_decoupe()
    {
        var info = Info(V(2160, codec: "vp09"), A());

        var plan = DownloadPlan.Create(Request(info, DownloadMode.Video, 2160), info, Root, Job);

        Assert.Contains("[height=2160]", plan.FormatSelector, StringComparison.Ordinal);
        Assert.DoesNotContain("--download-sections", plan.FormatArguments);
        Assert.DoesNotContain("--force-keyframes-at-cuts", plan.FormatArguments);
        Assert.DoesNotContain("--recode-video", plan.FormatArguments);
        Assert.DoesNotContain("-x", plan.FormatArguments);
    }

    [Fact]
    public void Dossier_temporaire_propre_au_job_et_fichier_attendu()
    {
        var info = Info(V(720), A());

        var plan = DownloadPlan.Create(Request(info, DownloadMode.Video, 720), info, Root, Job);

        Assert.Equal(Path.Combine(Root, "11111111222233334444555555555555"), plan.TempDirectory);
        Assert.Equal("media.mp4", plan.TempFileName);
        Assert.Equal(Path.Combine(plan.TempDirectory, "media.mp4"), plan.TempFilePath);
        Assert.Equal(Path.Combine(plan.TempDirectory, "media.%(ext)s"), plan.OutputTemplate);
    }

    [Fact]
    public void Deux_jobs_ont_deux_dossiers_differents()
    {
        var info = Info(V(720), A());
        var request = Request(info, DownloadMode.Video, 720);

        Assert.NotEqual(
            DownloadPlan.Create(request, info, Root, Guid.NewGuid()).TempDirectory,
            DownloadPlan.Create(request, info, Root, Guid.NewGuid()).TempDirectory);
    }

    [Fact]
    public void Taille_attendue_somme_des_flux_ou_inconnue()
    {
        var known = Info(V(720, 100), A(bytes: 10));
        Assert.Equal(110, DownloadPlan.Create(Request(known, DownloadMode.Video, 720), known, Root, Job).ExpectedBytes);

        var unknown = Info(V(720), A());
        Assert.Null(DownloadPlan.Create(Request(unknown, DownloadMode.Video, 720), unknown, Root, Job).ExpectedBytes);
    }

    [Fact]
    public void Audio_M4A_avec_AAC_est_un_telechargement_direct()
    {
        var info = Info(V(720), A(bytes: 5));

        var plan = DownloadPlan.Create(Request(info, DownloadMode.Audio), info, Root, Job);

        Assert.Equal("ba[ext=m4a]/ba[acodec^=mp4a]/ba", plan.FormatSelector);
        Assert.Equal(new[] { "-f", plan.FormatSelector }, plan.FormatArguments);
        Assert.Equal("m4a", plan.Extension);
        Assert.Equal(5, plan.ExpectedBytes);
    }

    [Fact]
    public void Audio_M4A_sans_AAC_est_converti()
    {
        var info = Info(V(720), A("opus"));

        var plan = DownloadPlan.Create(Request(info, DownloadMode.Audio), info, Root, Job);

        Assert.Equal(new[] { "-f", plan.FormatSelector, "-x", "--audio-format", "m4a" }, plan.FormatArguments);
        Assert.Equal("m4a", plan.Extension);
    }

    [Fact]
    public void Audio_MP3_extrait_et_convertit_en_meilleure_qualite()
    {
        var info = Info(V(720), A());

        var plan = DownloadPlan.Create(Request(info, DownloadMode.Audio, audio: AudioFormat.Mp3), info, Root, Job);

        Assert.Equal(
            new[] { "-f", "ba[ext=m4a]/ba", "-x", "--audio-format", "mp3", "--audio-quality", "0" },
            plan.FormatArguments);
        Assert.Equal("mp3", plan.Extension);
        Assert.Equal("media.mp3", plan.TempFileName);
    }

    [Fact]
    public void Demande_video_sans_hauteur_est_refusee()
    {
        var info = Info(V(720), A());
        var bad = new DownloadRequest(info.Id, "t", DownloadMode.Video, null, AudioFormat.M4a, Root);

        Assert.Throws<ArgumentException>(() => DownloadPlan.Create(bad, info, Root, Job));
    }
}

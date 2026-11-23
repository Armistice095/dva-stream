using DvaStream.Core.Media;
using DvaStream.Core.Settings;
using DvaStream.Core.Urls;
using DvaStream.Infrastructure.YtDlp;

namespace DvaStream.Infrastructure.Tests.YtDlp;

/// <summary>M4-05 : progression globale à partir des lignes de yt-dlp (vraies sorties du spike S2).</summary>
public class DownloadProgressTrackerTests
{
    // Tailles réelles des deux flux de direct-long.txt.
    private const long VideoBytes = 82_052_307;
    private const long AudioBytes = 37_548_169;

    private static DownloadPlan Plan(DownloadMode mode = DownloadMode.Video, long? expected = null)
    {
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        var request = new DownloadRequest(id!, "t", mode, mode == DownloadMode.Video ? 360 : null, AudioFormat.M4a, Path.GetTempPath());
        return new DownloadPlan(request, "sel", [], mode == DownloadMode.Video ? "mp4" : "m4a", Path.GetTempPath(), expected);
    }

    private static List<DownloadProgress> Replay(string file, long? expected)
    {
        var tracker = new DownloadProgressTracker(Plan(expected: expected));
        var snapshots = new List<DownloadProgress>();
        foreach (var line in File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "progress", file)))
        {
            if (tracker.Apply(YtDlpProgressParser.Parse(line)) is { } p)
            {
                snapshots.Add(p);
            }
        }

        return snapshots;
    }

    public static TheoryData<string, bool> Cases() => new()
    {
        { "direct-long.txt", true },
        { "direct-long.txt", false },
        { "direct-short.txt", true },
        { "direct-short.txt", false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Les_etapes_se_suivent_dans_l_ordre(string file, bool withExpected)
    {
        var snapshots = Replay(file, withExpected ? (file == "direct-long.txt" ? VideoBytes + AudioBytes : 20_086_214 + 6_893_812) : null);

        var phases = snapshots.Select(s => s.Phase).Distinct().ToList();

        Assert.Equal(
            new[] { DownloadPhase.DownloadingVideo, DownloadPhase.DownloadingAudio, DownloadPhase.Merging, DownloadPhase.Finalizing },
            phases);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void La_progression_globale_ne_recule_jamais_et_reste_dans_les_bornes(string file, bool withExpected)
    {
        var snapshots = Replay(file, withExpected ? (file == "direct-long.txt" ? VideoBytes + AudioBytes : 20_086_214 + 6_893_812) : null);

        var overall = snapshots.Select(s => s.OverallPercent).ToList();
        Assert.Equal(overall.OrderBy(x => x), overall);
        Assert.All(overall, v => Assert.InRange(v, 0, 97));
        Assert.Equal(97, overall[^1]);
        Assert.All(snapshots, s => Assert.InRange(s.Percent, 0, 100));
        Assert.Equal(snapshots.Select(s => s.BytesDone).OrderBy(x => x), snapshots.Select(s => s.BytesDone));
    }

    [Fact]
    public void Avec_la_taille_attendue_exacte_la_fin_du_telechargement_vaut_95()
    {
        var snapshots = Replay("direct-long.txt", VideoBytes + AudioBytes);

        var lastDownload = snapshots.Last(s => s.Phase == DownloadPhase.DownloadingAudio);
        Assert.Equal(95, lastDownload.OverallPercent, 3);
        Assert.Equal(VideoBytes + AudioBytes, lastDownload.BytesDone);
        Assert.Equal(VideoBytes + AudioBytes, lastDownload.BytesTotal);
    }

    [Fact]
    public void Sans_taille_attendue_la_fin_du_telechargement_vaut_aussi_95()
    {
        var snapshots = Replay("direct-long.txt", null);

        var lastDownload = snapshots.Last(s => s.Phase == DownloadPhase.DownloadingAudio);
        Assert.Equal(95, lastDownload.OverallPercent, 3);
        Assert.Equal(VideoBytes + AudioBytes, lastDownload.BytesDone);
    }

    [Fact]
    public void Au_changement_de_flux_le_pourcentage_de_l_etape_repart_mais_pas_le_global()
    {
        var snapshots = Replay("direct-long.txt", VideoBytes + AudioBytes);

        var lastVideo = snapshots.Last(s => s.Phase == DownloadPhase.DownloadingVideo);
        var firstAudio = snapshots.First(s => s.Phase == DownloadPhase.DownloadingAudio);

        Assert.Equal(100, lastVideo.Percent, 3);
        Assert.True(firstAudio.Percent < 1);
        Assert.True(firstAudio.OverallPercent >= lastVideo.OverallPercent);
        Assert.True(firstAudio.BytesDone >= VideoBytes);
    }

    [Fact]
    public void Vitesse_lissee_et_temps_restant_pendant_le_telechargement()
    {
        var snapshots = Replay("direct-long.txt", VideoBytes + AudioBytes);

        var middle = snapshots.First(s => s.BytesDone > 60_000_000 && s.Phase == DownloadPhase.DownloadingVideo);
        Assert.NotNull(middle.BytesPerSecond);
        Assert.True(middle.BytesPerSecond > 0);
        Assert.NotNull(middle.Remaining);
        Assert.True(middle.Remaining > TimeSpan.Zero);

        Assert.Null(snapshots.First(s => s.Phase == DownloadPhase.Merging).Remaining);
    }

    [Fact]
    public void Complete_donne_100_pour_cent()
    {
        var tracker = new DownloadProgressTracker(Plan());
        tracker.Apply(new DestinationLine("a"));
        tracker.Apply(new DownloadLine(YtDlpDownloadStatus.Finished, 10, 10, null, null, null, null, null));

        var done = tracker.Complete();

        Assert.Equal(DownloadPhase.Completed, done.Phase);
        Assert.Equal(100, done.OverallPercent);
        Assert.Equal(10, done.BytesDone);
        Assert.Equal(TimeSpan.Zero, done.Remaining);
    }

    [Fact]
    public void Etat_initial_Preparing_a_zero()
    {
        var current = new DownloadProgressTracker(Plan()).Current;

        Assert.Equal(DownloadPhase.Preparing, current.Phase);
        Assert.Equal(0, current.OverallPercent);
        Assert.Null(current.BytesTotal);
    }

    [Fact]
    public void Audio_seul_un_flux_puis_conversion()
    {
        var tracker = new DownloadProgressTracker(Plan(DownloadMode.Audio));

        var start = tracker.Apply(new DestinationLine("a"));
        var mid = tracker.Apply(new DownloadLine(YtDlpDownloadStatus.Downloading, 50, 100, null, 10, 5, null, null));
        var conv = tracker.Apply(new PostprocessLine("ExtractAudio", false));

        Assert.Equal(DownloadPhase.DownloadingAudio, start!.Phase);
        Assert.Equal(47.5, mid!.OverallPercent, 3);
        Assert.Equal(DownloadPhase.Converting, conv!.Phase);
    }

    [Fact]
    public void Lignes_inconnues_et_null_ne_changent_rien()
    {
        var tracker = new DownloadProgressTracker(Plan());

        Assert.Null(tracker.Apply(null));
        Assert.Null(tracker.Apply(new PostprocessLine("Merger", true)));
        Assert.Null(tracker.Apply(new FormatsAnnouncedLine(["1", "2"])));
    }

    [Fact]
    public void Une_ligne_de_progression_sans_destination_ouvre_quand_meme_le_premier_flux()
    {
        var tracker = new DownloadProgressTracker(Plan());

        var p = tracker.Apply(new DownloadLine(YtDlpDownloadStatus.Downloading, 5, 10, null, null, null, null, null));

        Assert.Equal(DownloadPhase.DownloadingVideo, p!.Phase);
    }

    [Fact]
    public void Un_flux_unique_complet_reste_en_telechargement_video()
    {
        var tracker = new DownloadProgressTracker(Plan());
        tracker.Apply(new FormatsAnnouncedLine(["18"]));

        var p = tracker.Apply(new DestinationLine("a"));
        var end = tracker.Apply(new DownloadLine(YtDlpDownloadStatus.Finished, 10, 10, null, null, null, null, null));

        Assert.Equal(DownloadPhase.DownloadingVideo, p!.Phase);
        Assert.Equal(95, end!.OverallPercent, 3);
    }
}

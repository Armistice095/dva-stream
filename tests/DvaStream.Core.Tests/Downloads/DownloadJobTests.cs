using DvaStream.Core.Downloads;
using DvaStream.Core.Errors;
using DvaStream.Core.Media;
using DvaStream.Core.Settings;
using DvaStream.Core.Urls;
using static DvaStream.Core.Media.DownloadPhase;

namespace DvaStream.Core.Tests.Downloads;

/// <summary>M4-08 : machine à états d'un téléchargement (TDD §8.1).</summary>
public class DownloadJobTests
{
    private static DownloadJob NewJob()
    {
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        return new DownloadJob(new DownloadRequest(id!, "t", DownloadMode.Video, 720, AudioFormat.M4a, Path.GetTempPath()));
    }

    private static DownloadProgress P(DownloadPhase phase, double overall = 10) => new(phase, 0, overall, 1, 2, null, null);

    // Table attendue, écrite à part de l'implémentation : TDD §8.1 + trois ajouts (D-041).
    private static readonly Dictionary<DownloadPhase, DownloadPhase[]> Expected = new()
    {
        [Queued] = [Preparing, Cancelled],
        [Preparing] = [DownloadingVideo, DownloadingAudio, Merging, Converting, Finalizing, Failed, Cancelled],
        [DownloadingVideo] = [DownloadingAudio, Merging, Finalizing, Completed, Failed, Cancelled],
        [DownloadingAudio] = [Merging, Converting, Finalizing, Failed, Cancelled],
        [Merging] = [Finalizing, Failed, Cancelled],
        [Converting] = [Finalizing, Failed, Cancelled],
        [Finalizing] = [Completed, Failed],
        [Completed] = [],
        [Failed] = [],
        [Cancelled] = [],
    };

    public static TheoryData<DownloadPhase, DownloadPhase, bool> AllPairs()
    {
        var data = new TheoryData<DownloadPhase, DownloadPhase, bool>();
        foreach (var from in Enum.GetValues<DownloadPhase>())
        {
            foreach (var to in Enum.GetValues<DownloadPhase>())
            {
                data.Add(from, to, Expected[from].Contains(to));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPairs))]
    public void Chaque_transition_est_autorisee_ou_refusee_comme_prevu(DownloadPhase from, DownloadPhase to, bool allowed) =>
        Assert.Equal(allowed, DownloadJob.CanTransition(from, to));

    [Fact]
    public void Un_job_neuf_est_en_file_sans_progression()
    {
        var job = NewJob();

        Assert.Equal(Queued, job.Phase);
        Assert.Null(job.Progress);
        Assert.Null(job.Error);
        Assert.Null(job.FinalPath);
        Assert.False(job.IsTerminal);
        Assert.NotEqual(Guid.Empty, job.Id);
    }

    [Fact]
    public void Parcours_normal_d_une_video_fusionnee()
    {
        var job = NewJob();

        Assert.True(job.TryTransition(Preparing));
        Assert.True(job.Report(P(DownloadingVideo, 20)));
        Assert.True(job.Report(P(DownloadingVideo, 40)));
        Assert.True(job.Report(P(DownloadingAudio, 80)));
        Assert.True(job.Report(P(Merging, 97)));
        Assert.True(job.Report(P(Finalizing, 97)));
        Assert.True(job.Complete(Path.Combine(Path.GetTempPath(), "x.mp4")));

        Assert.Equal(Completed, job.Phase);
        Assert.True(job.IsTerminal);
        Assert.Equal(100, job.Progress!.OverallPercent);
        Assert.EndsWith("x.mp4", job.FinalPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Une_transition_refusee_ne_change_rien()
    {
        var job = NewJob();

        Assert.False(job.TryTransition(Merging));
        Assert.False(job.TryTransition(Completed));
        Assert.Equal(Queued, job.Phase);
    }

    [Fact]
    public void Un_avancement_dans_le_desordre_est_ignore()
    {
        var job = NewJob();
        job.TryTransition(Preparing);
        job.Report(P(DownloadingAudio, 50));

        Assert.False(job.Report(P(DownloadingVideo, 10)));

        Assert.Equal(DownloadingAudio, job.Phase);
        Assert.Equal(50, job.Progress!.OverallPercent);
    }

    [Theory]
    [InlineData(Completed)]
    [InlineData(Failed)]
    [InlineData(Cancelled)]
    [InlineData(Queued)]
    public void Un_avancement_ne_pose_jamais_une_etape_de_fin(DownloadPhase phase)
    {
        var job = NewJob();
        job.TryTransition(Preparing);

        Assert.False(job.Report(P(phase)));

        Assert.Equal(Preparing, job.Phase);
        Assert.Null(job.Progress);
    }

    [Fact]
    public void Echec_depuis_le_telechargement_garde_l_erreur()
    {
        var job = NewJob();
        job.TryTransition(Preparing);
        job.Report(P(DownloadingVideo));

        Assert.True(job.Fail(new AppError(ErrorKind.Network, "coupure")));

        Assert.Equal(Failed, job.Phase);
        Assert.Equal(ErrorKind.Network, job.Error!.Kind);
        Assert.True(job.IsTerminal);
    }

    [Fact]
    public void Annulation_possible_avant_la_finalisation_mais_pas_pendant()
    {
        var early = NewJob();
        early.TryTransition(Preparing);
        early.Report(P(Merging));
        Assert.True(early.Cancel());
        Assert.Equal(Cancelled, early.Phase);

        var late = NewJob();
        late.TryTransition(Preparing);
        late.Report(P(Finalizing));
        Assert.False(late.Cancel());
        Assert.Equal(Finalizing, late.Phase);
    }

    [Fact]
    public void Un_etat_terminal_est_definitif()
    {
        var job = NewJob();
        job.TryTransition(Preparing);
        job.Fail(new AppError(ErrorKind.Unknown, "x"));

        Assert.False(job.Report(P(DownloadingVideo)));
        Assert.False(job.Cancel());
        Assert.False(job.Complete("a.mp4"));
        Assert.False(job.Fail(new AppError(ErrorKind.Unknown, "y")));
        Assert.Equal(Failed, job.Phase);
        Assert.Equal("x", job.Error!.TechnicalDetails);
    }

    [Fact]
    public void Audio_seul_passe_par_la_conversion()
    {
        var job = NewJob();
        job.TryTransition(Preparing);

        Assert.True(job.Report(P(DownloadingAudio)));
        Assert.True(job.Report(P(Converting)));
        Assert.True(job.Report(P(Finalizing)));
        Assert.True(job.Complete("a.mp3"));
    }

    [Fact]
    public void Fichier_deja_present_saute_directement_a_la_finalisation()
    {
        var job = NewJob();
        job.TryTransition(Preparing);

        Assert.True(job.Report(P(Finalizing)));
    }

    [Fact]
    public void L_evenement_Changed_est_leve_a_chaque_changement_accepte_seulement()
    {
        var job = NewJob();
        var count = 0;
        job.Changed += (_, _) => count++;

        job.TryTransition(Preparing);        // 1
        job.Report(P(DownloadingVideo));     // 2
        job.Report(P(DownloadingVideo, 30)); // 3
        job.TryTransition(Queued);           // refusée
        job.Report(P(Completed));            // ignorée
        job.Cancel();                        // 4

        Assert.Equal(4, count);
    }

    [Fact]
    public void Avancements_concurrents_sans_exception_ni_recul_d_etape()
    {
        var job = NewJob();
        job.TryTransition(Preparing);

        Parallel.For(0, 200, i => job.Report(P(i % 2 == 0 ? DownloadingVideo : DownloadingAudio, i % 90)));

        Assert.True(job.Phase is DownloadingVideo or DownloadingAudio);
        Assert.NotNull(job.Progress);
    }

    [Fact]
    public void Une_demande_nulle_est_refusee()
    {
        Assert.Throws<ArgumentNullException>(() => new DownloadJob(null!));
    }
}

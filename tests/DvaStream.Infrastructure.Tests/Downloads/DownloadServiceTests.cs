using DvaStream.Core.Downloads;
using DvaStream.Core.Errors;
using DvaStream.Core.Files;
using DvaStream.Core.Media;
using DvaStream.Core.Settings;
using DvaStream.Core.Urls;
using DvaStream.Infrastructure.Downloads;
using NSubstitute;

namespace DvaStream.Infrastructure.Tests.Downloads;

/// <summary>M4-09 : un téléchargement à la fois, avancement limité, annulation, échecs classés, nettoyage.</summary>
public sealed class DownloadServiceTests : IDisposable
{
    private sealed class FakeTime : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => 1000;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void AdvanceMs(long ms) => Interlocked.Add(ref _ticks, ms);
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dvastream-svc-{Guid.NewGuid():N}");
    private readonly IMediaSource _source = Substitute.For<IMediaSource>();
    private readonly IFileFinalizer _finalizer = Substitute.For<IFileFinalizer>();
    private readonly FakeTime _time = new();
    private readonly MediaInfo _info;
    private readonly DownloadRequest _request;

    public DownloadServiceTests()
    {
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        _info = new MediaInfo(id!, "Titre", "Chaîne", TimeSpan.FromMinutes(1), null,
            [new MediaStream("v", StreamKind.Video, 720, "mp4", "avc1", null, 100, 25), new MediaStream("a", StreamKind.Audio, null, "m4a", null, "mp4a", 10, null)]);
        _request = new DownloadRequest(id!, "Titre", DownloadMode.Video, 720, AudioFormat.M4a, Path.Combine(_root, "dest"));
        _finalizer.MoveToFolder(default!, default!, default!, default!).ReturnsForAnyArgs(Path.Combine(_root, "dest", "Titre.mp4"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private DownloadService Service() => new(_source, _finalizer, Path.Combine(_root, "temp"), time: _time);

    private string TempOf(DownloadJob job) => Path.Combine(_root, "temp", job.Id.ToString("N"));

    private static DownloadProgress P(DownloadPhase phase, double overall = 10) => new(phase, 0, overall, 1, 2, null, null);

    /// <summary>Crée le dossier temporaire et le fichier du job, comme le ferait le moteur.</summary>
    private string MakeFile(DownloadPlan plan)
    {
        Directory.CreateDirectory(plan.TempDirectory);
        File.WriteAllText(plan.TempFilePath, "video");
        return plan.TempFilePath;
    }

    private void SetupSource(Func<DownloadPlan, IProgress<DownloadProgress>, CancellationToken, Task<string>> behavior) =>
        _source.DownloadAsync(Arg.Any<DownloadPlan>(), Arg.Any<IProgress<DownloadProgress>>(), Arg.Any<CancellationToken>())
            .Returns(call => behavior(call.Arg<DownloadPlan>(), call.Arg<IProgress<DownloadProgress>>(), call.Arg<CancellationToken>()));

    [Fact]
    public async Task Succes_les_etapes_se_suivent_le_fichier_est_place_et_le_temporaire_supprime()
    {
        SetupSource((plan, progress, _) =>
        {
            progress.Report(P(DownloadPhase.DownloadingVideo, 20));
            _time.AdvanceMs(200);
            progress.Report(P(DownloadPhase.DownloadingAudio, 60));
            _time.AdvanceMs(200);
            progress.Report(P(DownloadPhase.Merging, 97));
            _time.AdvanceMs(200);
            progress.Report(P(DownloadPhase.Finalizing, 97)); // « MoveFiles » de yt-dlp
            return Task.FromResult(MakeFile(plan));
        });
        var service = Service();
        var job = service.Enqueue(_request, _info);
        var phases = new List<DownloadPhase>();
        job.Changed += (_, _) => phases.Add(job.Phase);

        await service.RunAsync(job, CancellationToken.None);

        Assert.Equal(DownloadPhase.Completed, job.Phase);
        Assert.Equal(Path.Combine(_root, "dest", "Titre.mp4"), job.FinalPath);
        Assert.Equal(
            new[]
            {
                DownloadPhase.Preparing, DownloadPhase.DownloadingVideo, DownloadPhase.DownloadingAudio,
                DownloadPhase.Merging, DownloadPhase.Merging, DownloadPhase.Finalizing, DownloadPhase.Completed,
            },
            phases);
        _finalizer.Received(1).MoveToFolder(Path.Combine(TempOf(job), "media.mp4"), _request.Folder, "Titre", "mp4");
        Assert.False(Directory.Exists(TempOf(job)));
    }

    [Fact]
    public async Task L_avancement_est_limite_a_10_par_seconde_sauf_changement_d_etape()
    {
        SetupSource((plan, progress, _) =>
        {
            for (var i = 0; i < 1000; i++)
            {
                progress.Report(P(DownloadPhase.DownloadingVideo, i % 90)); // même instant : une seule passe
            }

            for (var i = 0; i < 10; i++)
            {
                _time.AdvanceMs(100);
                progress.Report(P(DownloadPhase.DownloadingVideo, 50 + i)); // une toutes les 100 ms : toutes passent
            }

            progress.Report(P(DownloadPhase.DownloadingAudio, 70)); // changement d'étape : passe toujours
            return Task.FromResult(MakeFile(plan));
        });
        var service = Service();
        var job = service.Enqueue(_request, _info);
        var changes = 0;
        job.Changed += (_, _) => changes++;

        await service.RunAsync(job, CancellationToken.None);

        // Preparing + 1 + 10 + audio + Finalizing + Completed
        Assert.Equal(1 + 1 + 10 + 1 + 1 + 1, changes);
    }

    [Fact]
    public async Task Un_echec_du_moteur_est_garde_dans_le_job_et_le_temporaire_est_conserve()
    {
        SetupSource((plan, progress, _) =>
        {
            MakeFile(plan);
            throw new MediaSourceException(new AppError(ErrorKind.Network, "coupure"));
        });
        var service = Service();
        var job = service.Enqueue(_request, _info);

        await service.RunAsync(job, CancellationToken.None);

        Assert.Equal(DownloadPhase.Failed, job.Phase);
        Assert.Equal(ErrorKind.Network, job.Error!.Kind);
        Assert.True(Directory.Exists(TempOf(job)));
        _finalizer.DidNotReceiveWithAnyArgs().MoveToFolder(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Un_echec_du_placement_du_fichier_est_classe_et_le_fichier_reste_dans_le_temporaire()
    {
        SetupSource((plan, _, _) => Task.FromResult(MakeFile(plan)));
        _finalizer.MoveToFolder(default!, default!, default!, default!).ReturnsForAnyArgs(
            _ => throw new FileFinalizeException(new AppError(ErrorKind.FolderNotWritable, "refusé")));
        var service = Service();
        var job = service.Enqueue(_request, _info);

        await service.RunAsync(job, CancellationToken.None);

        Assert.Equal(DownloadPhase.Failed, job.Phase);
        Assert.Equal(ErrorKind.FolderNotWritable, job.Error!.Kind);
        Assert.True(File.Exists(Path.Combine(TempOf(job), "media.mp4")));
    }

    [Fact]
    public async Task Une_erreur_inattendue_devient_Unknown_sans_planter()
    {
        SetupSource((_, _, _) => throw new InvalidOperationException("boom"));
        var service = Service();
        var job = service.Enqueue(_request, _info);

        await service.RunAsync(job, CancellationToken.None);

        Assert.Equal(DownloadPhase.Failed, job.Phase);
        Assert.Equal(ErrorKind.Unknown, job.Error!.Kind);
    }

    [Fact]
    public async Task Annuler_en_cours_arrete_le_job_sans_exception_et_supprime_le_temporaire()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetupSource(async (plan, progress, ct) =>
        {
            MakeFile(plan);
            progress.Report(P(DownloadPhase.DownloadingVideo, 30));
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return string.Empty;
        });
        var service = Service();
        var job = service.Enqueue(_request, _info);

        var run = service.RunAsync(job, CancellationToken.None);
        await started.Task;
        service.Cancel(job);
        await run;

        Assert.Equal(DownloadPhase.Cancelled, job.Phase);
        Assert.False(Directory.Exists(TempOf(job)));
        Assert.Null(job.Error);
    }

    [Fact]
    public async Task Annuler_pendant_le_deplacement_interne_de_yt_dlp_annule_quand_meme()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetupSource(async (plan, progress, ct) =>
        {
            MakeFile(plan);
            progress.Report(P(DownloadPhase.Merging, 97));
            _time.AdvanceMs(200);
            progress.Report(P(DownloadPhase.Finalizing, 97)); // « MoveFiles » de yt-dlp : pas la finalisation du service
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return string.Empty;
        });
        var service = Service();
        var job = service.Enqueue(_request, _info);

        var run = service.RunAsync(job, CancellationToken.None);
        await started.Task;
        Assert.Equal(DownloadPhase.Merging, job.Phase);
        service.Cancel(job);
        await run;

        Assert.Equal(DownloadPhase.Cancelled, job.Phase);
    }

    [Fact]
    public async Task Annuler_un_job_en_attente_ne_lance_jamais_le_moteur()
    {
        var service = Service();
        var job = service.Enqueue(_request, _info);

        service.Cancel(job);
        await service.RunAsync(job, CancellationToken.None);

        Assert.Equal(DownloadPhase.Cancelled, job.Phase);
        await _source.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default!, default);
    }

    [Fact]
    public async Task Le_jeton_de_l_appelant_annule_aussi_le_job()
    {
        SetupSource(async (plan, _, ct) =>
        {
            MakeFile(plan);
            await Task.Delay(Timeout.Infinite, ct);
            return string.Empty;
        });
        var service = Service();
        var job = service.Enqueue(_request, _info);
        using var cts = new CancellationTokenSource();

        var run = service.RunAsync(job, cts.Token);
        await Task.Delay(100);
        await cts.CancelAsync();
        await run;

        Assert.Equal(DownloadPhase.Cancelled, job.Phase);
    }

    [Fact]
    public async Task Un_seul_telechargement_a_la_fois_le_second_attend_son_tour()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = 0;
        var maxRunning = 0;
        var calls = 0;
        SetupSource(async (plan, _, _) =>
        {
            var now = Interlocked.Increment(ref running);
            maxRunning = Math.Max(maxRunning, now);
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstStarted.SetResult();
                await release.Task;
            }

            var file = MakeFile(plan);
            Interlocked.Decrement(ref running);
            return file;
        });
        var service = Service();
        var first = service.Enqueue(_request, _info);
        var second = service.Enqueue(_request, _info);

        var run1 = service.RunAsync(first, CancellationToken.None);
        await firstStarted.Task;
        var run2 = service.RunAsync(second, CancellationToken.None);
        await Task.Delay(200);

        Assert.Equal(1, Volatile.Read(ref calls));
        Assert.Equal(DownloadPhase.Queued, second.Phase);

        release.SetResult();
        await Task.WhenAll(run1, run2);

        Assert.Equal(2, calls);
        Assert.Equal(1, maxRunning);
        Assert.Equal(DownloadPhase.Completed, first.Phase);
        Assert.Equal(DownloadPhase.Completed, second.Phase);
    }

    [Fact]
    public async Task Annuler_le_job_qui_attend_son_tour_ne_perturbe_pas_celui_en_cours()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetupSource(async (plan, _, _) =>
        {
            firstStarted.TrySetResult();
            await release.Task;
            return MakeFile(plan);
        });
        var service = Service();
        var first = service.Enqueue(_request, _info);
        var second = service.Enqueue(_request, _info);

        var run1 = service.RunAsync(first, CancellationToken.None);
        await firstStarted.Task;
        var run2 = service.RunAsync(second, CancellationToken.None);
        await Task.Delay(100);
        service.Cancel(second);
        await run2;
        release.SetResult();
        await run1;

        Assert.Equal(DownloadPhase.Cancelled, second.Phase);
        Assert.Equal(DownloadPhase.Completed, first.Phase);
        await _source.Received(1).DownloadAsync(Arg.Any<DownloadPlan>(), Arg.Any<IProgress<DownloadProgress>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Enqueue_garde_l_ordre_et_previent()
    {
        var service = Service();
        var seen = new List<DownloadJob>();
        service.JobEnqueued += (_, j) => seen.Add(j);

        var a = service.Enqueue(_request, _info);
        var b = service.Enqueue(_request, _info);

        Assert.Equal(new[] { a, b }, service.Jobs);
        Assert.Equal(new[] { a, b }, seen);
        Assert.Equal(DownloadPhase.Queued, a.Phase);
        Assert.Same(_info, a.Info);
    }

    [Fact]
    public async Task Un_job_inconnu_ou_deja_execute_est_refuse()
    {
        var service = Service();
        var stranger = new DownloadJob(_request, _info);
        await Assert.ThrowsAsync<ArgumentException>(() => service.RunAsync(stranger, CancellationToken.None));

        SetupSource((plan, _, _) => Task.FromResult(MakeFile(plan)));
        var job = service.Enqueue(_request, _info);
        await service.RunAsync(job, CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentException>(() => service.RunAsync(job, CancellationToken.None));
    }
}

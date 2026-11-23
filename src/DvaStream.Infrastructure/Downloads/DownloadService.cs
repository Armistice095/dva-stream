using DvaStream.Core.Downloads;
using DvaStream.Core.Errors;
using DvaStream.Core.Files;
using DvaStream.Core.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DvaStream.Infrastructure.Downloads;

/// <summary>
/// Un téléchargement à la fois (M4-09). Étapes d'un job : plan, téléchargement par <see cref="IMediaSource"/>, déplacement
/// par <see cref="IFileFinalizer"/>, nettoyage. Le dossier temporaire est supprimé après un succès ou une annulation, et
/// <b>conservé après un échec</b> (la reprise, M5-03, en aura besoin). L'avancement est limité à 10 mises à jour par
/// seconde, les changements d'étape passent toujours.
/// </summary>
public sealed class DownloadService : IDownloadService
{
    /// <summary>Intervalle minimal entre deux mises à jour d'avancement de même étape (10 par seconde).</summary>
    public static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(100);

    private readonly IMediaSource _source;
    private readonly IFileFinalizer _finalizer;
    private readonly string _tempRoot;
    private readonly ILogger<DownloadService> _logger;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _lock = new();
    private readonly List<DownloadJob> _jobs = [];
    private readonly Dictionary<Guid, CancellationTokenSource> _cancellations = [];

    public DownloadService(
        IMediaSource source,
        IFileFinalizer finalizer,
        string tempRoot,
        ILogger<DownloadService>? logger = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(finalizer);
        ArgumentException.ThrowIfNullOrWhiteSpace(tempRoot);
        _source = source;
        _finalizer = finalizer;
        _tempRoot = tempRoot;
        _logger = logger ?? NullLogger<DownloadService>.Instance;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public event EventHandler<DownloadJob>? JobEnqueued;

    /// <inheritdoc />
    public IReadOnlyList<DownloadJob> Jobs
    {
        get { lock (_lock) { return _jobs.ToArray(); } }
    }

    /// <inheritdoc />
    public DownloadJob Enqueue(DownloadRequest request, MediaInfo info)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(info);

        var job = new DownloadJob(request, info);
        lock (_lock)
        {
            _jobs.Add(job);
            _cancellations[job.Id] = new CancellationTokenSource();
        }

        JobEnqueued?.Invoke(this, job);
        return job;
    }

    /// <inheritdoc />
    public void Cancel(DownloadJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        CancellationTokenSource? source;
        lock (_lock)
        {
            _cancellations.TryGetValue(job.Id, out source);
        }

        // Un job qui n'a pas encore démarré est annulé tout de suite ; un job en cours l'est par son jeton.
        if (job.Phase == DownloadPhase.Queued)
        {
            job.Cancel();
        }

        try
        {
            source?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Le job vient de se terminer : rien à annuler.
        }
    }

    /// <inheritdoc />
    public async Task RunAsync(DownloadJob job, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(job);

        CancellationTokenSource? jobSource;
        lock (_lock)
        {
            _cancellations.TryGetValue(job.Id, out jobSource);
        }

        if (jobSource is null)
        {
            throw new ArgumentException("Ce job n'a pas été créé par ce service (ou a déjà été exécuté).", nameof(job));
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, jobSource.Token);
            try
            {
                await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                job.Cancel();
                return;
            }

            try
            {
                await RunJobAsync(job, linked.Token).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            lock (_lock)
            {
                _cancellations.Remove(job.Id);
            }

            jobSource.Dispose();
        }
    }

    private async Task RunJobAsync(DownloadJob job, CancellationToken ct)
    {
        if (job.IsTerminal || !job.TryTransition(DownloadPhase.Preparing))
        {
            return;
        }

        DownloadPlan? plan = null;
        try
        {
            var info = job.Info ?? throw new InvalidOperationException("Le job n'a pas de description de la vidéo.");
            plan = DownloadPlan.Create(job.Request, info, _tempRoot, job.Id);

            var progress = new ThrottledProgress(update => Forward(job, update), _time, ProgressInterval);
            var file = await _source.DownloadAsync(plan, progress, ct).ConfigureAwait(false);

            // À partir d'ici le fichier est prêt : plus d'annulation possible.
            job.TryTransition(DownloadPhase.Finalizing);
            var extension = Path.GetExtension(file).TrimStart('.');
            var final = _finalizer.MoveToFolder(
                file, job.Request.Folder, job.Request.Title, extension.Length > 0 ? extension : plan.Extension);

            job.Complete(final);
            DeleteTemp(plan);
        }
        catch (OperationCanceledException)
        {
            job.Cancel();
            DeleteTemp(plan);
        }
        catch (MediaSourceException ex)
        {
            _logger.LogWarning("Téléchargement échoué ({Kind}).", ex.Error.Kind);
            job.Fail(ex.Error);
        }
        catch (FileFinalizeException ex)
        {
            _logger.LogWarning("Fichier non placé ({Kind}).", ex.Error.Kind);
            job.Fail(ex.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur inattendue pendant le téléchargement.");
            job.Fail(new AppError(ErrorKind.Unknown, ex.Message, ex));
        }
    }

    /// <summary>
    /// Transmet l'avancement du moteur au job. L'étape « finalisation » vue par yt-dlp (déplacement dans son propre
    /// dossier) n'est pas la nôtre : tant que le fichier n'est pas rendu, on reste sur l'étape précédente, car la
    /// finalisation n'est plus annulable.
    /// </summary>
    private static void Forward(DownloadJob job, DownloadProgress update)
    {
        job.Report(update.Phase == DownloadPhase.Finalizing ? update with { Phase = job.Phase } : update);
    }

    private void DeleteTemp(DownloadPlan? plan)
    {
        if (plan is null)
        {
            return;
        }

        try
        {
            if (Directory.Exists(plan.TempDirectory))
            {
                Directory.Delete(plan.TempDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Dossier temporaire non supprimé : {Directory}", plan.TempDirectory);
        }
    }

    private sealed class ThrottledProgress : IProgress<DownloadProgress>
    {
        private readonly Action<DownloadProgress> _sink;
        private readonly TimeProvider _time;
        private readonly TimeSpan _interval;
        private readonly Lock _gate = new();
        private bool _hasLast;
        private DownloadPhase _lastPhase;
        private long _lastTimestamp;

        public ThrottledProgress(Action<DownloadProgress> sink, TimeProvider time, TimeSpan interval)
        {
            _sink = sink;
            _time = time;
            _interval = interval;
        }

        public void Report(DownloadProgress value)
        {
            lock (_gate)
            {
                var now = _time.GetTimestamp();
                if (_hasLast && value.Phase == _lastPhase && _time.GetElapsedTime(_lastTimestamp, now) < _interval)
                {
                    return;
                }

                _hasLast = true;
                _lastPhase = value.Phase;
                _lastTimestamp = now;
            }

            _sink(value);
        }
    }
}

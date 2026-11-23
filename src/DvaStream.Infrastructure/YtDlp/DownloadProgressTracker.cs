using DvaStream.Core.Media;

namespace DvaStream.Infrastructure.YtDlp;

/// <summary>
/// Transforme les lignes de yt-dlp en avancement global (M4-05, D-038). yt-dlp rapporte chaque flux séparément (la
/// progression repart de zéro pour l'image puis pour le son) : le suivi additionne les flux, déduit l'étape en cours
/// et lisse la vitesse. Pur : aucune lecture de disque ni de processus. Non sûr entre threads (appels successifs).
/// </summary>
public sealed class DownloadProgressTracker
{
    private const double DownloadShare = 95;
    private const double PostShare = 97;
    private const double SpeedSmoothing = 0.3;
    private static readonly TimeSpan MaxRemaining = TimeSpan.FromHours(99);

    private readonly DownloadMode _mode;
    private readonly long? _expectedBytes;
    private int _streamCount;
    private int _streamIndex = -1;
    private long _completedBytes;
    private long _currentBytes;
    private long? _currentTotal;
    private double? _speed;
    private double _overall;
    private DownloadPhase _phase = DownloadPhase.Preparing;

    public DownloadProgressTracker(DownloadPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _mode = plan.Request.Mode;
        _expectedBytes = plan.ExpectedBytes;
        _streamCount = _mode == DownloadMode.Video ? 2 : 1;
    }

    /// <summary>Avancement actuel.</summary>
    public DownloadProgress Current => Snapshot();

    /// <summary>Prend en compte une ligne ; renvoie le nouvel avancement, ou <c>null</c> si la ligne ne change rien.</summary>
    public DownloadProgress? Apply(YtDlpOutputLine? line)
    {
        switch (line)
        {
            case FormatsAnnouncedLine formats:
                _streamCount = Math.Max(1, formats.FormatIds.Count);
                return null;

            case DestinationLine:
                BeginStream();
                return Snapshot();

            case DownloadLine download:
                if (_streamIndex < 0)
                {
                    BeginStream();
                }

                _currentBytes = download.DownloadedBytes;
                if (download.KnownTotal is { } total)
                {
                    _currentTotal = total;
                }

                if (download.BytesPerSecond is > 0)
                {
                    _speed = _speed is null
                        ? download.BytesPerSecond
                        : (_speed * (1 - SpeedSmoothing)) + (download.BytesPerSecond * SpeedSmoothing);
                }

                if (download.Status == YtDlpDownloadStatus.Finished)
                {
                    _currentTotal ??= _currentBytes;
                    _currentBytes = _currentTotal.Value;
                }

                return Snapshot();

            case PostprocessLine { Finished: false } post:
                _phase = PhaseOf(post.Processor);
                return Snapshot();

            default:
                return null;
        }
    }

    /// <summary>Marque le téléchargement comme terminé.</summary>
    public DownloadProgress Complete()
    {
        _phase = DownloadPhase.Completed;
        _overall = 100;
        var done = _completedBytes + _currentBytes;
        return new DownloadProgress(DownloadPhase.Completed, 100, 100, done, done, null, TimeSpan.Zero);
    }

    private static DownloadPhase PhaseOf(string processor)
    {
        if (processor.Contains("Merger", StringComparison.OrdinalIgnoreCase))
        {
            return DownloadPhase.Merging;
        }

        return processor.Contains("Extract", StringComparison.OrdinalIgnoreCase)
               || processor.Contains("Convert", StringComparison.OrdinalIgnoreCase)
               || processor.Contains("Remux", StringComparison.OrdinalIgnoreCase)
               || processor.Contains("Recode", StringComparison.OrdinalIgnoreCase)
            ? DownloadPhase.Converting
            : DownloadPhase.Finalizing;
    }

    private void BeginStream()
    {
        if (_streamIndex >= 0)
        {
            _completedBytes += _currentTotal ?? _currentBytes;
        }

        _streamIndex++;
        _currentBytes = 0;
        _currentTotal = null;
        // Image d'abord, son ensuite (ordre observé : « 396+251 ») ; en audio seul, un seul flux sonore.
        _phase = _mode == DownloadMode.Video && _streamIndex == 0
            ? DownloadPhase.DownloadingVideo
            : DownloadPhase.DownloadingAudio;
    }

    private bool IsDownloading => _phase is DownloadPhase.Preparing or DownloadPhase.DownloadingVideo or DownloadPhase.DownloadingAudio;

    private DownloadProgress Snapshot()
    {
        var done = _completedBytes + _currentBytes;

        long? total = null;
        if (_expectedBytes is > 0)
        {
            total = Math.Max(_expectedBytes.Value, done);
        }
        else if (_streamIndex >= _streamCount - 1 && _currentTotal is { } known)
        {
            total = _completedBytes + known;
        }

        var phasePercent = IsDownloading && _currentTotal is > 0
            ? Math.Min(100.0, 100.0 * _currentBytes / _currentTotal.Value)
            : 0;

        double overall;
        if (_phase == DownloadPhase.Completed)
        {
            overall = 100;
        }
        else if (!IsDownloading)
        {
            overall = PostShare;
        }
        else
        {
            double fraction;
            if (total is > 0)
            {
                fraction = (double)done / total.Value;
            }
            else
            {
                fraction = _streamIndex < 0 ? 0 : (_streamIndex + (phasePercent / 100.0)) / _streamCount;
            }

            overall = Math.Min(DownloadShare, DownloadShare * fraction);
        }

        _overall = Math.Max(_overall, overall);

        TimeSpan? remaining = null;
        if (IsDownloading && total is > 0 && _speed is > 0)
        {
            var seconds = (total.Value - done) / _speed.Value;
            remaining = seconds >= MaxRemaining.TotalSeconds ? null : TimeSpan.FromSeconds(Math.Max(0, seconds));
        }

        return new DownloadProgress(_phase, phasePercent, _overall, done, total, _speed, remaining);
    }
}

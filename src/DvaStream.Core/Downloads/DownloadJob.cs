using DvaStream.Core.Errors;
using DvaStream.Core.Media;

namespace DvaStream.Core.Downloads;

/// <summary>
/// Un téléchargement et son état (M4-08, TDD §8.1). L'état est une <see cref="DownloadPhase"/> ; seules les
/// transitions de <see cref="CanTransition"/> sont acceptées, les autres sont refusées (la méthode renvoie <c>false</c>)
/// sans rien changer. Les états terminaux (<see cref="DownloadPhase.Completed"/>, <see cref="DownloadPhase.Failed"/>,
/// <see cref="DownloadPhase.Cancelled"/>) sont définitifs. Sûr entre threads ; l'événement <see cref="Changed"/> est
/// déclenché hors verrou, sur le thread qui a fait le changement.
/// </summary>
public sealed class DownloadJob
{
    // TDD §8.1, avec trois ajouts voulus (D-041) : Preparing et DownloadingVideo peuvent aller directement
    // vers Merging / Converting / Finalizing (fichier déjà présent, flux unique déjà complet).
    private static readonly IReadOnlyDictionary<DownloadPhase, DownloadPhase[]> Allowed =
        new Dictionary<DownloadPhase, DownloadPhase[]>
        {
            [DownloadPhase.Queued] = [DownloadPhase.Preparing, DownloadPhase.Cancelled],
            [DownloadPhase.Preparing] =
            [
                DownloadPhase.DownloadingVideo, DownloadPhase.DownloadingAudio, DownloadPhase.Merging,
                DownloadPhase.Converting, DownloadPhase.Finalizing, DownloadPhase.Failed, DownloadPhase.Cancelled,
            ],
            [DownloadPhase.DownloadingVideo] =
            [
                DownloadPhase.DownloadingAudio, DownloadPhase.Merging, DownloadPhase.Finalizing,
                DownloadPhase.Completed, DownloadPhase.Failed, DownloadPhase.Cancelled,
            ],
            [DownloadPhase.DownloadingAudio] =
            [
                DownloadPhase.Merging, DownloadPhase.Converting, DownloadPhase.Finalizing,
                DownloadPhase.Failed, DownloadPhase.Cancelled,
            ],
            [DownloadPhase.Merging] = [DownloadPhase.Finalizing, DownloadPhase.Failed, DownloadPhase.Cancelled],
            [DownloadPhase.Converting] = [DownloadPhase.Finalizing, DownloadPhase.Failed, DownloadPhase.Cancelled],
            [DownloadPhase.Finalizing] = [DownloadPhase.Completed, DownloadPhase.Failed],
            [DownloadPhase.Completed] = [],
            [DownloadPhase.Failed] = [],
            [DownloadPhase.Cancelled] = [],
        };

    private readonly Lock _gate = new();
    private DownloadPhase _phase = DownloadPhase.Queued;
    private DownloadProgress? _progress;
    private AppError? _error;
    private string? _finalPath;

    public DownloadJob(DownloadRequest request, MediaInfo? info = null, Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
        Info = info;
        Id = id ?? Guid.NewGuid();
    }

    /// <summary>Identifiant du job (nom du dossier temporaire).</summary>
    public Guid Id { get; }

    /// <summary>Demande à l'origine du job.</summary>
    public DownloadRequest Request { get; }

    /// <summary>Description de la vidéo analysée (flux, tailles) dont le plan de téléchargement a besoin.</summary>
    public MediaInfo? Info { get; }

    /// <summary>Étape actuelle.</summary>
    public DownloadPhase Phase
    {
        get { lock (_gate) { return _phase; } }
    }

    /// <summary>Dernier avancement accepté, ou <c>null</c> avant le premier.</summary>
    public DownloadProgress? Progress
    {
        get { lock (_gate) { return _progress; } }
    }

    /// <summary>Erreur, si le job a échoué.</summary>
    public AppError? Error
    {
        get { lock (_gate) { return _error; } }
    }

    /// <summary>Chemin du fichier final, si le job est terminé.</summary>
    public string? FinalPath
    {
        get { lock (_gate) { return _finalPath; } }
    }

    /// <summary>Vrai dans un état définitif.</summary>
    public bool IsTerminal => Phase is DownloadPhase.Completed or DownloadPhase.Failed or DownloadPhase.Cancelled;

    /// <summary>Déclenché après chaque changement d'étape ou d'avancement.</summary>
    public event EventHandler? Changed;

    /// <summary>Vrai si le passage de <paramref name="from"/> à <paramref name="to"/> est autorisé.</summary>
    public static bool CanTransition(DownloadPhase from, DownloadPhase to) =>
        Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    /// <summary>Passe à <paramref name="next"/> si c'est autorisé ; sinon ne change rien et renvoie <c>false</c>.</summary>
    public bool TryTransition(DownloadPhase next)
    {
        lock (_gate)
        {
            if (!CanTransition(_phase, next))
            {
                return false;
            }

            _phase = next;
        }

        RaiseChanged();
        return true;
    }

    /// <summary>
    /// Enregistre un avancement du moteur. L'étape qu'il porte est suivie si la transition est autorisée ; les
    /// étapes de fin (terminé, échec, annulé) ne sont jamais posées par un avancement, mais par <see cref="Complete"/>,
    /// <see cref="Fail"/> et <see cref="Cancel"/>. Renvoie <c>false</c> si l'avancement est ignoré (job terminé,
    /// étape non autorisée).
    /// </summary>
    public bool Report(DownloadProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (progress.Phase is DownloadPhase.Completed or DownloadPhase.Failed or DownloadPhase.Cancelled or DownloadPhase.Queued)
        {
            return false;
        }

        lock (_gate)
        {
            if (_phase is DownloadPhase.Completed or DownloadPhase.Failed or DownloadPhase.Cancelled)
            {
                return false;
            }

            if (progress.Phase != _phase)
            {
                if (!CanTransition(_phase, progress.Phase))
                {
                    return false;
                }

                _phase = progress.Phase;
            }

            _progress = progress;
        }

        RaiseChanged();
        return true;
    }

    /// <summary>Termine le job avec succès (depuis la finalisation, ou le téléchargement vidéo d'un flux complet).</summary>
    public bool Complete(string finalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);
        lock (_gate)
        {
            if (!CanTransition(_phase, DownloadPhase.Completed))
            {
                return false;
            }

            _phase = DownloadPhase.Completed;
            _finalPath = finalPath;
            _progress = new DownloadProgress(
                DownloadPhase.Completed, 100, 100, _progress?.BytesDone ?? 0, _progress?.BytesTotal, null, TimeSpan.Zero);
        }

        RaiseChanged();
        return true;
    }

    /// <summary>Termine le job en échec.</summary>
    public bool Fail(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        lock (_gate)
        {
            if (!CanTransition(_phase, DownloadPhase.Failed))
            {
                return false;
            }

            _phase = DownloadPhase.Failed;
            _error = error;
        }

        RaiseChanged();
        return true;
    }

    /// <summary>Termine le job comme annulé (impossible une fois en finalisation : le fichier est déjà prêt).</summary>
    public bool Cancel() => TryTransition(DownloadPhase.Cancelled);

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

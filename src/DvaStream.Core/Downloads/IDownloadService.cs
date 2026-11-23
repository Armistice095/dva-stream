using DvaStream.Core.Media;

namespace DvaStream.Core.Downloads;

/// <summary>
/// Orchestre les téléchargements (M4-09, TDD §5) : un seul à la fois dans le MVP, mais l'interface est déjà celle
/// d'une file (<see cref="Enqueue"/>, <see cref="Jobs"/>) pour la v1.1.
/// </summary>
public interface IDownloadService
{
    /// <summary>Tous les jobs de la session, du plus ancien au plus récent (terminés compris : base de l'historique).</summary>
    IReadOnlyList<DownloadJob> Jobs { get; }

    /// <summary>Déclenché quand un job est ajouté.</summary>
    event EventHandler<DownloadJob>? JobEnqueued;

    /// <summary>Crée un job en attente. Ne lance rien : voir <see cref="RunAsync"/>.</summary>
    DownloadJob Enqueue(DownloadRequest request, MediaInfo info);

    /// <summary>
    /// Exécute le job jusqu'à son état final, en attendant son tour si un autre téléchargement est en cours. Ne lève
    /// pas d'exception pour un échec ou une annulation : le résultat est dans <see cref="DownloadJob.Phase"/> et
    /// <see cref="DownloadJob.Error"/>. <paramref name="ct"/> annule aussi le job.
    /// </summary>
    Task RunAsync(DownloadJob job, CancellationToken ct);

    /// <summary>Annule le job (en attente ou en cours). Sans effet s'il est déjà terminé ou en finalisation.</summary>
    void Cancel(DownloadJob job);
}

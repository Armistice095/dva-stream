using DvaStream.Core.Urls;

namespace DvaStream.Core.Media;

/// <summary>
/// Moteur d'extraction (TDD §5, D-001) : métadonnées (M1, M2) et téléchargement (M4).
/// </summary>
public interface IMediaSource
{
    /// <summary>Nom du moteur, pour les journaux.</summary>
    string Name { get; }

    /// <summary>Récupère les métadonnées de la vidéo ; annulable.</summary>
    Task<MediaInfo> GetInfoAsync(VideoId id, CancellationToken ct);

    /// <summary>
    /// Exécute le plan : écrit dans <see cref="DownloadPlan.TempDirectory"/>, rend compte de l'avancement et renvoie le
    /// chemin du fichier produit. Aucun nettoyage : le dossier temporaire reste au service de téléchargement.
    /// </summary>
    /// <exception cref="DvaStream.Core.Errors.MediaSourceException">Échec classé (moteur ou FFmpeg absent, réseau, disque…).</exception>
    /// <exception cref="OperationCanceledException">Annulation demandée : le processus et ses enfants sont arrêtés.</exception>
    Task<string> DownloadAsync(DownloadPlan plan, IProgress<DownloadProgress> progress, CancellationToken ct);
}

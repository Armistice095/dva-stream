using DvaStream.Core.Urls;

namespace DvaStream.Core.Media;

/// <summary>
/// Moteur d'extraction (TDD §5, D-001). Au jalon M1 : lecture des métadonnées seulement ;
/// <c>DownloadAsync</c> est ajouté au jalon M4.
/// </summary>
public interface IMediaSource
{
    /// <summary>Nom du moteur, pour les journaux.</summary>
    string Name { get; }

    /// <summary>Récupère les métadonnées de la vidéo ; annulable.</summary>
    Task<MediaInfo> GetInfoAsync(VideoId id, CancellationToken ct);
}

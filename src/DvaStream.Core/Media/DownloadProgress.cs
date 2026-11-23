namespace DvaStream.Core.Media;

/// <summary>Étape d'un téléchargement (TDD §4). Les étapes de fin (<see cref="Failed"/>, <see cref="Cancelled"/>) sont posées par le service.</summary>
public enum DownloadPhase
{
    Queued,
    Preparing,
    DownloadingVideo,
    DownloadingAudio,
    Merging,
    Converting,
    Finalizing,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>Photographie de l'avancement d'un téléchargement.</summary>
/// <param name="Phase">Étape en cours.</param>
/// <param name="Percent">0 à 100 pour l'étape en cours (0 quand elle n'est pas mesurable).</param>
/// <param name="OverallPercent">0 à 100 pour l'ensemble ; ne recule jamais. Le téléchargement occupe 0 à 95, les étapes suivantes 97, la fin 100.</param>
/// <param name="BytesDone">Octets reçus, tous flux confondus.</param>
/// <param name="BytesTotal">Taille totale estimée, ou <c>null</c> si inconnue.</param>
/// <param name="BytesPerSecond">Vitesse lissée, ou <c>null</c> si inconnue.</param>
/// <param name="Remaining">Temps restant estimé, ou <c>null</c> si inconnu.</param>
public sealed record DownloadProgress(
    DownloadPhase Phase,
    double Percent,
    double OverallPercent,
    long BytesDone,
    long? BytesTotal,
    double? BytesPerSecond,
    TimeSpan? Remaining);

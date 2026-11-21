namespace DvaStream.Core.Media;

/// <summary>Calculs de présentation sur un <see cref="MediaInfo"/> (M2-10, FR-12).</summary>
public static class MediaInfoExtensions
{
    /// <summary>
    /// Hauteurs (en pixels) réellement proposées, de la plus grande à la plus petite, sans doublon. Un flux vidéo
    /// seul ou avec audio compte ; l'audio seul n'a pas de hauteur et n'est pas compté.
    /// </summary>
    public static IReadOnlyList<int> GetAvailableResolutions(this MediaInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return info.Streams
            .Where(s => s.Kind is StreamKind.Video or StreamKind.Muxed && s.Height is > 0)
            .Select(s => s.Height!.Value)
            .Distinct()
            .OrderDescending()
            .ToList();
    }

    /// <summary>Vrai si au moins un flux audio (seul ou avec image) est proposé.</summary>
    public static bool HasAudio(this MediaInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return info.Streams.Any(s => s.Kind is StreamKind.Audio or StreamKind.Muxed);
    }
}

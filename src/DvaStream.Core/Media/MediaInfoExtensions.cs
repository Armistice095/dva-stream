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

    /// <summary>
    /// Taille approximative du fichier produit, ou <c>null</c> si elle est inconnue. Vidéo : le flux image de la
    /// hauteur demandée (H.264 de préférence, comme le MP4 visé) plus le meilleur flux AAC, ou le flux avec son s'il
    /// n'y a pas de flux séparé. Audio : le meilleur flux AAC (M4A), sinon le plus gros flux audio.
    /// </summary>
    public static long? EstimateSizeBytes(this MediaInfo info, DownloadMode mode, int? height)
    {
        ArgumentNullException.ThrowIfNull(info);

        var audio = BestAudio(info);
        if (mode == DownloadMode.Audio)
        {
            return audio?.Bytes;
        }

        if (height is null)
        {
            return null;
        }

        var candidates = info.Streams
            .Where(s => s.Kind is StreamKind.Video or StreamKind.Muxed && s.Height == height && s.Bytes is > 0)
            .ToList();
        var video = candidates.Where(s => s.VideoCodec?.StartsWith("avc1", StringComparison.Ordinal) == true)
            .OrderByDescending(s => s.Bytes).FirstOrDefault()
            ?? candidates.OrderByDescending(s => s.Bytes).FirstOrDefault();
        if (video is null)
        {
            return null;
        }

        return video.Kind == StreamKind.Muxed ? video.Bytes : video.Bytes + (audio?.Bytes ?? 0);
    }

    private static MediaStream? BestAudio(MediaInfo info)
    {
        var audios = info.Streams.Where(s => s.Kind == StreamKind.Audio && s.Bytes is > 0).ToList();
        return audios.Where(s => s.AudioCodec?.StartsWith("mp4a", StringComparison.Ordinal) == true)
            .OrderByDescending(s => s.Bytes).FirstOrDefault()
            ?? audios.OrderByDescending(s => s.Bytes).FirstOrDefault();
    }

    /// <summary>Vrai si au moins un flux audio (seul ou avec image) est proposé.</summary>
    public static bool HasAudio(this MediaInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return info.Streams.Any(s => s.Kind is StreamKind.Audio or StreamKind.Muxed);
    }
}

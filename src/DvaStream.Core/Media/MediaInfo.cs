using DvaStream.Core.Urls;

namespace DvaStream.Core.Media;

/// <summary>Métadonnées d'une vidéo, telles que renvoyées par un <see cref="IMediaSource"/> (TDD §4).</summary>
/// <param name="Id">Identifiant de la vidéo.</param>
/// <param name="Title">Titre.</param>
/// <param name="Channel">Nom de la chaîne.</param>
/// <param name="Duration">Durée.</param>
/// <param name="ThumbnailUrl">Miniature, si disponible.</param>
/// <param name="Streams">Flux disponibles (vidéo, audio, ou les deux).</param>
public sealed record MediaInfo(
    VideoId Id,
    string Title,
    string Channel,
    TimeSpan Duration,
    Uri? ThumbnailUrl,
    IReadOnlyList<MediaStream> Streams);

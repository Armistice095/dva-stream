namespace DvaStream.Core.Media;

/// <summary>Nature d'un flux.</summary>
public enum StreamKind
{
    /// <summary>Image seule.</summary>
    Video,

    /// <summary>Son seul.</summary>
    Audio,

    /// <summary>Image et son dans le même flux.</summary>
    Muxed,
}

/// <summary>Un flux téléchargeable d'une vidéo (TDD §4).</summary>
/// <param name="FormatId">Identifiant du format côté moteur.</param>
/// <param name="Kind">Nature du flux.</param>
/// <param name="Height">Hauteur en pixels (720, 1080, 2160…), <c>null</c> pour l'audio.</param>
/// <param name="Container">Conteneur (<c>mp4</c>, <c>webm</c>, <c>m4a</c>…).</param>
/// <param name="VideoCodec">Codec vidéo, s'il y en a un.</param>
/// <param name="AudioCodec">Codec audio, s'il y en a un.</param>
/// <param name="Bytes">Taille connue ou estimée, en octets.</param>
/// <param name="Fps">Images par seconde.</param>
public sealed record MediaStream(
    string FormatId,
    StreamKind Kind,
    int? Height,
    string Container,
    string? VideoCodec,
    string? AudioCodec,
    long? Bytes,
    int? Fps);

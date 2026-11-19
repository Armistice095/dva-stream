namespace DvaStream.Core.Urls;

/// <summary>
/// Résultat de l'analyse d'une saisie par <see cref="IUrlParser"/> : exactement l'un des trois cas
/// <see cref="Valid"/>, <see cref="Playlist"/> ou <see cref="Invalid"/> (hiérarchie fermée).
/// </summary>
public abstract record UrlParseResult
{
    private UrlParseResult()
    {
    }

    /// <summary>URL d'une vidéo unique, prête à être analysée.</summary>
    /// <param name="VideoId">Identifiant de la vidéo.</param>
    public sealed record Valid(VideoId VideoId) : UrlParseResult;

    /// <summary>
    /// URL contenant un paramètre <c>list=</c> ou pointant vers une playlist : non prise en charge en v1.0 (FR-04).
    /// </summary>
    /// <param name="PlaylistId">Identifiant de la playlist, tel que fourni (non interprété).</param>
    /// <param name="VideoId">Vidéo ciblée par la même URL, si elle en contient une (permet de la proposer seule, M7-02).</param>
    public sealed record Playlist(string PlaylistId, VideoId? VideoId) : UrlParseResult;

    /// <summary>Saisie refusée.</summary>
    /// <param name="Reason">Raison du refus, traduite en message par la couche présentation.</param>
    public sealed record Invalid(UrlInvalidReason Reason) : UrlParseResult;
}

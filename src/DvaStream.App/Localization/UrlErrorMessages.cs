using DvaStream.Core.Urls;

namespace DvaStream.App.Localization;

/// <summary>Messages d'erreur de saisie d'URL (M1-05, FR-03) : une clé de ressource par cas refusé.</summary>
public static class UrlErrorMessages
{
    /// <summary>Clé du message affiché pour une playlist (message FR-04 provisoire, finalisé en M7-02).</summary>
    public const string PlaylistKey = "UrlError_Playlist";

    /// <summary>Message pour une URL refusée.</summary>
    public static string For(UrlInvalidReason reason) => Strings.Get(KeyFor(reason));

    /// <summary>Message pour une URL de playlist.</summary>
    public static string ForPlaylist() => Strings.Get(PlaylistKey);

    /// <summary>Clé de ressource associée à <paramref name="reason"/>.</summary>
    public static string KeyFor(UrlInvalidReason reason) => reason switch
    {
        UrlInvalidReason.Empty => "UrlError_Empty",
        UrlInvalidReason.NotAUrl => "UrlError_NotAUrl",
        UrlInvalidReason.UnsupportedScheme => "UrlError_UnsupportedScheme",
        UrlInvalidReason.UnsupportedHost => "UrlError_UnsupportedHost",
        UrlInvalidReason.UnrecognizedFormat => "UrlError_UnrecognizedFormat",
        UrlInvalidReason.InvalidVideoId => "UrlError_InvalidVideoId",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Raison d'URL sans message associé."),
    };
}

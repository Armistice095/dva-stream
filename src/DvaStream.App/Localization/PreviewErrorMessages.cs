using DvaStream.Core.Errors;

namespace DvaStream.App.Localization;

/// <summary>Messages d'erreur de prévisualisation (M2-12, FR-13) : un message et une action suggérée par cas.</summary>
public static class PreviewErrorMessages
{
    /// <summary>Message pour <paramref name="kind"/>.</summary>
    public static string MessageFor(ErrorKind kind) => Strings.Get(MessageKeyFor(kind));

    /// <summary>Action suggérée pour <paramref name="kind"/>.</summary>
    public static string ActionFor(ErrorKind kind) => Strings.Get(ActionKeyFor(kind));

    /// <summary>Clé du message ; les cas du téléchargement (M5-04) retombent provisoirement sur « inattendue ».</summary>
    public static string MessageKeyFor(ErrorKind kind) => $"Err_{Resolve(kind)}_Message";

    /// <summary>Clé de l'action suggérée.</summary>
    public static string ActionKeyFor(ErrorKind kind) => $"Err_{Resolve(kind)}_Action";

    private static string Resolve(ErrorKind kind) => kind switch
    {
        ErrorKind.VideoPrivate => nameof(ErrorKind.VideoPrivate),
        ErrorKind.VideoUnavailable => nameof(ErrorKind.VideoUnavailable),
        ErrorKind.VideoRestricted => nameof(ErrorKind.VideoRestricted),
        ErrorKind.Network => nameof(ErrorKind.Network),
        ErrorKind.Timeout => nameof(ErrorKind.Timeout),
        ErrorKind.RateLimited => nameof(ErrorKind.RateLimited),
        ErrorKind.EngineMissing => nameof(ErrorKind.EngineMissing),
        ErrorKind.EngineBroken => nameof(ErrorKind.EngineBroken),
        ErrorKind.UnsupportedPlaylist => nameof(ErrorKind.UnsupportedPlaylist),
        ErrorKind.InvalidUrl => nameof(ErrorKind.InvalidUrl),
        _ => nameof(ErrorKind.Unknown),
    };
}

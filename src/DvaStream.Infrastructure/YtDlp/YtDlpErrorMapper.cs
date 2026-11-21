using DvaStream.Core.Errors;

namespace DvaStream.Infrastructure.YtDlp;

/// <summary>
/// Traduit le message d'erreur de yt-dlp (sortie d'erreur) en <see cref="ErrorKind"/> (M2-07). Les motifs viennent
/// de sorties réelles enregistrées (version 2026.10.26) ; tout cas non reconnu devient <see cref="ErrorKind.Unknown"/>.
/// </summary>
public static class YtDlpErrorMapper
{
    private const int MaxDetailsLength = 2000;

    // L'ordre compte : les cas précis passent avant les cas génériques. Les erreurs réseau précèdent
    // « indisponible » car yt-dlp ajoute « please report this issue » à des échecs de connexion.
    private static readonly (ErrorKind Kind, string[] Markers)[] Rules =
    [
        (ErrorKind.VideoPrivate, ["private video", "granted access"]),
        (ErrorKind.VideoRestricted,
        [
            "confirm your age", "age-restricted", "inappropriate for some users",
            "not available in your country", "in your country", "members-only", "join this channel",
        ]),
        (ErrorKind.RateLimited, ["not a bot", "http error 429", "too many requests"]),
        (ErrorKind.InvalidUrl, ["unsupported url"]),
        (ErrorKind.Network,
        [
            "unable to connect to proxy", "unable to download", "failed to establish a new connection",
            "getaddrinfo failed", "name or service not known", "temporary failure in name resolution",
            "network is unreachable", "connection refused", "connection reset", "timed out",
            "ssl:", "certificate verify failed",
        ]),
        (ErrorKind.VideoUnavailable,
        [
            "video is unavailable", "video unavailable", "has been removed", "removed by the uploader",
            "account associated with this video has been terminated", "does not exist", "copyright",
        ]),
    ];

    /// <summary>Classe l'échec d'un appel à yt-dlp à partir de sa sortie d'erreur.</summary>
    public static AppError Map(int exitCode, string? standardError)
    {
        var text = (standardError ?? string.Empty).Trim();
        var details = $"yt-dlp a échoué (code {exitCode}) : {Truncate(text)}";

        foreach (var (kind, markers) in Rules)
        {
            foreach (var marker in markers)
            {
                if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    return new AppError(kind, details);
                }
            }
        }

        return new AppError(ErrorKind.Unknown, details);
    }

    private static string Truncate(string text) =>
        text.Length <= MaxDetailsLength ? text : string.Concat(text.AsSpan(0, MaxDetailsLength), "…");
}

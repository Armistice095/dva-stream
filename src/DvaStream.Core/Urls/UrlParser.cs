namespace DvaStream.Core.Urls;

/// <summary>
/// Implémentation stricte de <see cref="IUrlParser"/> (FR-01, FR-03, FR-04).
/// </summary>
/// <remarks>
/// Accepté : schéma <c>http</c>/<c>https</c> ; hôtes <c>youtube.com</c>, <c>www.youtube.com</c>,
/// <c>m.youtube.com</c> et <c>youtu.be</c> ; formats <c>/watch?v=ID</c>, <c>/shorts/ID</c>, <c>youtu.be/ID</c>.
/// Une URL portant <c>list=</c> est une playlist, même si elle contient aussi <c>v=</c> (la vidéo est alors conservée).
/// Tout le reste est refusé (D-021) : autres sous-domaines, <c>/embed/</c>, <c>/live/</c>, chaînes, ports, identifiants dans l'URL.
/// </remarks>
public sealed class UrlParser : IUrlParser
{
    /// <summary>Longueur maximale d'une saisie examinée ; au-delà, elle n'est pas considérée comme une URL.</summary>
    public const int MaxInputLength = 2048;

    private static readonly HashSet<string> YouTubeHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com",
        "www.youtube.com",
        "m.youtube.com",
    };

    private const string ShortHost = "youtu.be";

    /// <inheritdoc />
    public UrlParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Invalid(UrlInvalidReason.Empty);
        }

        var text = input.Trim();
        if (text.Length > MaxInputLength || ContainsWhitespaceOrControl(text))
        {
            return Invalid(UrlInvalidReason.NotAUrl);
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return Invalid(UrlInvalidReason.NotAUrl);
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return Invalid(UrlInvalidReason.UnsupportedScheme);
        }

        // « https://youtube.com@evil.com/… » : l'hôte réel est evil.com. « https://evil.com@youtube.com/… » :
        // hôte autorisé mais forme typique d'hameçonnage. Dans les deux cas, on refuse.
        if (!string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort)
        {
            return Invalid(UrlInvalidReason.UnsupportedHost);
        }

        var query = ParseQuery(uri.Query);
        query.TryGetValue("list", out var playlistId);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (string.Equals(uri.Host, ShortHost, StringComparison.OrdinalIgnoreCase))
        {
            return segments.Length == 1
                ? Resolve(segments[0], playlistId)
                : Invalid(UrlInvalidReason.UnrecognizedFormat);
        }

        if (!YouTubeHosts.Contains(uri.Host))
        {
            return Invalid(UrlInvalidReason.UnsupportedHost);
        }

        if (segments.Length == 1 && IsSegment(segments[0], "watch"))
        {
            query.TryGetValue("v", out var videoId);
            return Resolve(videoId, playlistId);
        }

        if (segments.Length == 2 && IsSegment(segments[0], "shorts"))
        {
            return Resolve(segments[1], playlistId);
        }

        if (segments.Length == 1 && IsSegment(segments[0], "playlist"))
        {
            return Resolve(null, playlistId);
        }

        return Invalid(UrlInvalidReason.UnrecognizedFormat);
    }

    /// <summary>
    /// Combine l'identifiant de vidéo candidat et l'éventuel identifiant de playlist en un résultat.
    /// </summary>
    private static UrlParseResult Resolve(string? videoIdCandidate, string? playlistId)
    {
        VideoId? videoId = null;
        if (!string.IsNullOrEmpty(videoIdCandidate) && !VideoId.TryCreate(videoIdCandidate, out videoId))
        {
            return Invalid(UrlInvalidReason.InvalidVideoId);
        }

        if (!string.IsNullOrEmpty(playlistId))
        {
            return new UrlParseResult.Playlist(playlistId, videoId);
        }

        if (videoId is not null)
        {
            return new UrlParseResult.Valid(videoId);
        }

        return Invalid(UrlInvalidReason.UnrecognizedFormat);
    }

    private static UrlParseResult.Invalid Invalid(UrlInvalidReason reason) => new(reason);

    private static bool IsSegment(string segment, string expected) =>
        string.Equals(segment, expected, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsWhitespaceOrControl(string text)
    {
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Lit la chaîne de requête ; en cas de paramètre répété, la première occurrence l'emporte.</summary>
    private static Dictionary<string, string> ParseQuery(string rawQuery)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in rawQuery.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            var key = separator < 0 ? pair : pair[..separator];
            var value = separator < 0 ? string.Empty : pair[(separator + 1)..];
            result.TryAdd(Decode(key), Decode(value));
        }

        return result;
    }

    private static string Decode(string component) =>
        Uri.UnescapeDataString(component.Replace('+', ' '));
}

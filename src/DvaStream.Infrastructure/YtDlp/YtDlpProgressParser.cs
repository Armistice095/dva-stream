using System.Globalization;

namespace DvaStream.Infrastructure.YtDlp;

/// <summary>
/// Comprend les lignes de sortie de yt-dlp utiles à la progression (M4-04). Tolérante : une ligne inconnue ou
/// abîmée donne <c>null</c>, jamais une exception ; les valeurs <c>NA</c> deviennent <c>null</c>.
/// Les formats ont été relevés sur des sorties réelles (spike S2, D-038).
/// </summary>
public static class YtDlpProgressParser
{
    /// <summary>Préfixe des lignes de progression du téléchargement.</summary>
    public const string DownloadPrefix = "DVAPROG";

    /// <summary>Préfixe des lignes de post-traitement.</summary>
    public const string PostprocessPrefix = "DVAPOST";

    /// <summary>Valeur de <c>--progress-template download:</c> : champs séparés par <c>|</c>.</summary>
    public const string DownloadTemplate =
        "DVAPROG|%(progress.status)s|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s|%(progress.fragment_index)s|%(progress.fragment_count)s";

    /// <summary>Valeur de <c>--progress-template postprocess:</c>.</summary>
    public const string PostprocessTemplate = "DVAPOST|%(progress.status)s|%(progress.postprocessor)s";

    private const string DestinationMarker = "[download] Destination:";
    private const string InfoMarker = "Downloading ";
    private const string FormatsMarker = " format(s):";

    /// <summary>Comprend une ligne ; <c>null</c> si elle n'intéresse pas la progression ou si elle est illisible.</summary>
    public static YtDlpOutputLine? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var text = line.Trim();
        if (text.StartsWith(DownloadPrefix + "|", StringComparison.Ordinal))
        {
            return ParseDownload(text);
        }

        if (text.StartsWith(PostprocessPrefix + "|", StringComparison.Ordinal))
        {
            return ParsePostprocess(text);
        }

        if (text.StartsWith(DestinationMarker, StringComparison.Ordinal))
        {
            var path = text[DestinationMarker.Length..].Trim();
            return path.Length == 0 ? null : new DestinationLine(path);
        }

        if (text.StartsWith("[info]", StringComparison.Ordinal))
        {
            return ParseFormats(text);
        }

        return null;
    }

    private static DownloadLine? ParseDownload(string text)
    {
        var parts = text.Split('|');
        if (parts.Length < 9)
        {
            return null;
        }

        var downloaded = ToLong(parts[2]);
        if (downloaded is null)
        {
            return null;
        }

        return new DownloadLine(
            ToStatus(parts[1]),
            downloaded.Value,
            ToLong(parts[3]),
            ToLong(parts[4]),
            ToDouble(parts[5]),
            ToDouble(parts[6]),
            ToInt(parts[7]),
            ToInt(parts[8]));
    }

    private static PostprocessLine? ParsePostprocess(string text)
    {
        var parts = text.Split('|');
        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]))
        {
            return null;
        }

        return parts[1].Trim().ToLowerInvariant() switch
        {
            "started" => new PostprocessLine(parts[2].Trim(), Finished: false),
            "finished" => new PostprocessLine(parts[2].Trim(), Finished: true),
            _ => null,
        };
    }

    private static FormatsAnnouncedLine? ParseFormats(string text)
    {
        var marker = text.IndexOf(FormatsMarker, StringComparison.Ordinal);
        if (marker < 0 || !text.Contains(InfoMarker, StringComparison.Ordinal))
        {
            return null;
        }

        var ids = text[(marker + FormatsMarker.Length)..]
            .Trim()
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ids.Length == 0 ? null : new FormatsAnnouncedLine(ids);
    }

    private static YtDlpDownloadStatus ToStatus(string value) => value.Trim().ToLowerInvariant() switch
    {
        "downloading" => YtDlpDownloadStatus.Downloading,
        "finished" => YtDlpDownloadStatus.Finished,
        "error" => YtDlpDownloadStatus.Error,
        _ => YtDlpDownloadStatus.Unknown,
    };

    private static long? ToLong(string value)
    {
        var d = ToDouble(value);
        return d is null or >= long.MaxValue ? null : (long)d.Value;
    }

    private static int? ToInt(string value)
    {
        var d = ToDouble(value);
        return d is null or > int.MaxValue ? null : (int)d.Value;
    }

    /// <summary>Nombre positif ou nul ; <c>NA</c>, texte, valeur négative ou infinie donnent <c>null</c>.</summary>
    private static double? ToDouble(string value)
    {
        return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
               && double.IsFinite(d) && d >= 0
            ? d
            : null;
    }
}

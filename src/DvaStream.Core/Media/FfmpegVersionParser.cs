namespace DvaStream.Core.Media;

/// <summary>Ce que la sortie de <c>ffmpeg -version</c> apprend sur le binaire.</summary>
/// <param name="Version">Version annoncée (ex. <c>n8.1-12-gabc123</c>).</param>
/// <param name="IsGpl">Vrai si le binaire est compilé avec <c>--enable-gpl</c> (ou nonfree) : plus contraignant que le LGPL visé (D-004).</param>
public sealed record FfmpegInfo(string Version, bool IsGpl);

/// <summary>Lit la sortie de <c>ffmpeg -version</c> (M4-02). Fonction pure, tolérante.</summary>
public static class FfmpegVersionParser
{
    /// <summary>Renvoie <c>null</c> si la sortie ne ressemble pas à celle de FFmpeg.</summary>
    public static FfmpegInfo? Parse(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        string? version = null;
        var gpl = false;
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (version is null && line.StartsWith("ffmpeg version ", StringComparison.OrdinalIgnoreCase))
            {
                var rest = line["ffmpeg version ".Length..].Trim();
                var space = rest.IndexOf(' ', StringComparison.Ordinal);
                version = space >= 0 ? rest[..space] : rest;
            }
            else if (line.StartsWith("configuration:", StringComparison.OrdinalIgnoreCase)
                     && (line.Contains("--enable-gpl", StringComparison.Ordinal)
                         || line.Contains("--enable-nonfree", StringComparison.Ordinal)))
            {
                gpl = true;
            }
        }

        return string.IsNullOrEmpty(version) ? null : new FfmpegInfo(version, gpl);
    }
}

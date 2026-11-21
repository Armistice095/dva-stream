using System.Text.Json;
using DvaStream.Core.Errors;
using DvaStream.Core.Media;
using DvaStream.Core.Urls;

namespace DvaStream.Infrastructure.YtDlp;

/// <summary>Convertit la sortie JSON de yt-dlp en <see cref="MediaInfo"/> (M2-05).</summary>
public static class YtDlpInfoMapper
{
    private const string None = "none";

    // Bornes constantes (un motif relationnel exige une constante) : 10 jours, et 2^62 octets.
    private const double MaxSeconds = 864_000;
    private const double MaxBytes = 4.6e18;

    /// <summary>Analyse <paramref name="json"/> ; lève <see cref="MediaSourceException"/> si la sortie est inexploitable.</summary>
    public static MediaInfo Parse(string json)
    {
        YtDlpInfoDto dto;
        try
        {
            dto = YtDlpInfoDto.Parse(json);
        }
        catch (JsonException ex)
        {
            throw Fail(ErrorKind.EngineBroken, $"Sortie de yt-dlp illisible : {ex.Message}", ex);
        }

        return Map(dto);
    }

    /// <summary>Convertit un DTO déjà lu.</summary>
    public static MediaInfo Map(YtDlpInfoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.Equals(dto.Type, "playlist", StringComparison.Ordinal))
        {
            throw Fail(ErrorKind.UnsupportedPlaylist, "yt-dlp a renvoyé une playlist.");
        }

        if (!VideoId.TryCreate(dto.Id, out var id))
        {
            throw Fail(ErrorKind.EngineBroken, $"Identifiant de vidéo absent ou invalide : « {dto.Id} ».");
        }

        var streams = (dto.Formats ?? [])
            .Select(MapStream)
            .OfType<MediaStream>()
            .ToList();

        return new MediaInfo(
            id,
            string.IsNullOrWhiteSpace(dto.Title) ? id.Value : dto.Title.Trim(),
            FirstNonBlank(dto.Channel, dto.Uploader) ?? string.Empty,
            ToDuration(dto.Duration),
            ToThumbnail(dto.Thumbnail),
            streams);
    }

    private static MediaStream? MapStream(YtDlpFormatDto f)
    {
        if (string.IsNullOrWhiteSpace(f.FormatId) || string.IsNullOrWhiteSpace(f.Ext))
        {
            return null;
        }

        // Planches de vignettes (mhtml/storyboard) : pas un flux téléchargeable.
        if (string.Equals(f.Ext, "mhtml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(f.FormatNote, "storyboard", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var hasVideo = IsCodec(f.VideoCodec);
        var hasAudio = IsCodec(f.AudioCodec);

        StreamKind kind;
        if (hasVideo && hasAudio)
        {
            kind = StreamKind.Muxed;
        }
        else if (hasVideo)
        {
            kind = StreamKind.Video;
        }
        else if (hasAudio || string.Equals(f.VideoCodec, None, StringComparison.Ordinal))
        {
            // Sans image et avec un codec audio, ou « vcodec none » et acodec inconnu (audio HLS) : de l'audio.
            kind = StreamKind.Audio;
        }
        else
        {
            return null;
        }

        return new MediaStream(
            f.FormatId,
            kind,
            f.Height is > 0 ? (int)Math.Round(f.Height.Value) : null,
            f.Ext,
            hasVideo ? f.VideoCodec : null,
            hasAudio ? f.AudioCodec : null,
            ToBytes(f.FileSize ?? f.FileSizeApprox),
            f.Fps is > 0 ? (int)Math.Round(f.Fps.Value) : null);
    }

    private static long? ToBytes(double? value) =>
        value is > 0 and < MaxBytes ? (long)value.Value : null;

    private static bool IsCodec(string? codec) =>
        !string.IsNullOrWhiteSpace(codec) && !string.Equals(codec, None, StringComparison.Ordinal);

    private static TimeSpan ToDuration(double? seconds) =>
        seconds is > 0 and < MaxSeconds ? TimeSpan.FromSeconds(seconds.Value) : TimeSpan.Zero;

    private static Uri? ToThumbnail(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri : null;

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static MediaSourceException Fail(ErrorKind kind, string details, Exception? inner = null) =>
        new(new AppError(kind, details, inner));
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DvaStream.Infrastructure.YtDlp;

/// <summary>
/// Sous-ensemble tolérant de la sortie <c>--dump-single-json</c> de yt-dlp (M2-05). Seuls les champs utiles
/// sont lus ; tout champ absent, nul ou inconnu est ignoré, car le format évolue avec chaque version de yt-dlp.
/// </summary>
public sealed class YtDlpInfoDto
{
    /// <summary>Identifiant de la vidéo.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Type de résultat : <c>video</c>, <c>playlist</c>…</summary>
    [JsonPropertyName("_type")]
    public string? Type { get; set; }

    /// <summary>Titre.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Chaîne (préférée à <see cref="Uploader"/>).</summary>
    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    /// <summary>Auteur de la mise en ligne.</summary>
    [JsonPropertyName("uploader")]
    public string? Uploader { get; set; }

    /// <summary>Durée en secondes (absente ou nulle pour un direct).</summary>
    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    /// <summary>Miniature principale.</summary>
    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    /// <summary>Formats disponibles.</summary>
    [JsonPropertyName("formats")]
    public List<YtDlpFormatDto>? Formats { get; set; }

    /// <summary>Désérialise la sortie de yt-dlp ; lève <see cref="JsonException"/> si ce n'est pas un objet JSON.</summary>
    public static YtDlpInfoDto Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<YtDlpInfoDto>(json, Options);
        return dto ?? throw new JsonException("La sortie de yt-dlp est vide ou nulle.");
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}

/// <summary>Un format de la liste <c>formats</c> de yt-dlp.</summary>
public sealed class YtDlpFormatDto
{
    /// <summary>Identifiant du format (ex. <c>137</c>).</summary>
    [JsonPropertyName("format_id")]
    public string? FormatId { get; set; }

    /// <summary>Extension du conteneur.</summary>
    [JsonPropertyName("ext")]
    public string? Ext { get; set; }

    /// <summary>Codec vidéo, <c>none</c> si le format n'a pas d'image.</summary>
    [JsonPropertyName("vcodec")]
    public string? VideoCodec { get; set; }

    /// <summary>Codec audio, <c>none</c> si le format n'a pas de son, absent si inconnu.</summary>
    [JsonPropertyName("acodec")]
    public string? AudioCodec { get; set; }

    /// <summary>Hauteur en pixels.</summary>
    [JsonPropertyName("height")]
    public double? Height { get; set; }

    /// <summary>Images par seconde (entier ou décimal selon le format).</summary>
    [JsonPropertyName("fps")]
    public double? Fps { get; set; }

    /// <summary>Taille exacte en octets.</summary>
    [JsonPropertyName("filesize")]
    public double? FileSize { get; set; }

    /// <summary>Taille estimée en octets.</summary>
    [JsonPropertyName("filesize_approx")]
    public double? FileSizeApprox { get; set; }

    /// <summary>Remarque libre (ex. <c>storyboard</c>).</summary>
    [JsonPropertyName("format_note")]
    public string? FormatNote { get; set; }
}

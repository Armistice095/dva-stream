using DvaStream.Core.Settings;

namespace DvaStream.Core.Media;

/// <summary>
/// Ce que le moteur doit faire pour une demande (M4-03, TDD §4 et §6.3) : sélecteur de format, options de fusion ou
/// d'extraction audio, dossier temporaire du job, nom du fichier produit. Objet pur, sans accès au disque.
/// </summary>
/// <param name="Request">Demande validée à l'origine du plan.</param>
/// <param name="FormatSelector">Valeur de <c>-f</c>.</param>
/// <param name="FormatArguments">Arguments yt-dlp propres au format (<c>-f</c>, fusion MP4, extraction audio), prêts à passer tels quels.</param>
/// <param name="Extension">Extension du fichier produit, sans point (<c>mp4</c>, <c>m4a</c>, <c>mp3</c>).</param>
/// <param name="TempDirectory">Dossier réservé à ce job : rien n'est écrit dans le dossier de destination avant la fin.</param>
/// <param name="ExpectedBytes">Taille approximative attendue (somme des flux), ou <c>null</c> si inconnue : sert à la progression globale.</param>
public sealed record DownloadPlan(
    DownloadRequest Request,
    string FormatSelector,
    IReadOnlyList<string> FormatArguments,
    string Extension,
    string TempDirectory,
    long? ExpectedBytes)
{
    /// <summary>Nom du fichier produit dans <see cref="TempDirectory"/> (le vrai nom vient de <c>IFileNamer</c> à la fin).</summary>
    public string TempFileName => "media." + Extension;

    /// <summary>Chemin complet du fichier attendu dans le dossier temporaire.</summary>
    public string TempFilePath => Path.Combine(TempDirectory, TempFileName);

    /// <summary>Modèle de sortie à donner à <c>-o</c> : l'extension réelle est choisie par yt-dlp.</summary>
    public string OutputTemplate => Path.Combine(TempDirectory, "media.%(ext)s");

    /// <summary>
    /// Construit le plan d'une demande. Vidéo : image de la hauteur choisie en H.264 si elle existe (jusqu'à 1080p),
    /// sinon VP9/AV1 recopié, avec son AAC de préférence, fusionnés en MP4 <b>sans ré-encodage</b> (D-038).
    /// Audio M4A : flux AAC natif ; MP3 : extraction et conversion.
    /// </summary>
    public static DownloadPlan Create(DownloadRequest request, MediaInfo info, string tempRoot, Guid jobId)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(info);
        ArgumentException.ThrowIfNullOrWhiteSpace(tempRoot);

        var tempDirectory = Path.Combine(tempRoot, jobId.ToString("N"));

        if (request.Mode == DownloadMode.Video)
        {
            var height = request.Height ?? throw new ArgumentException("Une demande vidéo doit avoir une hauteur.", nameof(request));
            var selector =
                $"bv*[vcodec^=avc1][height={height}]+ba[acodec^=mp4a]" +
                $"/bv*[height={height}]+ba[acodec^=mp4a]" +
                $"/bv*[height={height}]+ba" +
                $"/b[height<={height}]";
            return new DownloadPlan(
                request,
                selector,
                ["-f", selector, "--merge-output-format", "mp4"],
                "mp4",
                tempDirectory,
                info.EstimateSizeBytes(DownloadMode.Video, height));
        }

        var bytes = info.EstimateSizeBytes(DownloadMode.Audio, null);
        if (request.AudioFormat == AudioFormat.Mp3)
        {
            const string mp3Selector = "ba[ext=m4a]/ba";
            return new DownloadPlan(
                request,
                mp3Selector,
                ["-f", mp3Selector, "-x", "--audio-format", "mp3", "--audio-quality", "0"],
                "mp3",
                tempDirectory,
                bytes);
        }

        const string m4aSelector = "ba[ext=m4a]/ba[acodec^=mp4a]/ba";
        var hasAac = info.Streams.Any(s =>
            s.Kind == StreamKind.Audio && s.AudioCodec?.StartsWith("mp4a", StringComparison.Ordinal) == true);

        // AAC disponible : téléchargement direct, sans post-traitement (vérifié au spike S1). Sinon : conversion en M4A.
        return new DownloadPlan(
            request,
            m4aSelector,
            hasAac ? new[] { "-f", m4aSelector } : new[] { "-f", m4aSelector, "-x", "--audio-format", "m4a" },
            "m4a",
            tempDirectory,
            bytes);
    }
}

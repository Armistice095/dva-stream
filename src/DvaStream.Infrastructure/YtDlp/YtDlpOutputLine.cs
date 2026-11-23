namespace DvaStream.Infrastructure.YtDlp;

/// <summary>État d'un flux en cours de téléchargement, tel que yt-dlp le rapporte.</summary>
public enum YtDlpDownloadStatus
{
    /// <summary>Statut inconnu ou absent.</summary>
    Unknown,

    /// <summary>Téléchargement en cours.</summary>
    Downloading,

    /// <summary>Le flux est entièrement téléchargé.</summary>
    Finished,

    /// <summary>yt-dlp signale une erreur sur ce flux.</summary>
    Error,
}

/// <summary>Une ligne de la sortie de yt-dlp, comprise (M4-04). Les types dérivés sont les seuls cas utiles à la progression.</summary>
public abstract record YtDlpOutputLine;

/// <summary>Ligne du gabarit de progression. Les valeurs que yt-dlp ne connaît pas (<c>NA</c>) valent <c>null</c>.</summary>
/// <param name="Status">État du flux.</param>
/// <param name="DownloadedBytes">Octets reçus <b>pour le flux en cours</b> : la valeur repart de 0 à chaque flux.</param>
/// <param name="TotalBytes">Taille exacte du flux en cours, si connue.</param>
/// <param name="TotalBytesEstimate">Taille estimée du flux en cours (flux fragmentés), si connue.</param>
/// <param name="BytesPerSecond">Vitesse instantanée, absente par moments.</param>
/// <param name="EtaSeconds">Temps restant pour ce flux, absent par moments.</param>
/// <param name="FragmentIndex">Fragment en cours (flux fragmentés).</param>
/// <param name="FragmentCount">Nombre de fragments (flux fragmentés).</param>
public sealed record DownloadLine(
    YtDlpDownloadStatus Status,
    long DownloadedBytes,
    long? TotalBytes,
    long? TotalBytesEstimate,
    double? BytesPerSecond,
    double? EtaSeconds,
    int? FragmentIndex,
    int? FragmentCount) : YtDlpOutputLine
{
    /// <summary>Taille connue du flux en cours : exacte sinon estimée.</summary>
    public long? KnownTotal => TotalBytes ?? TotalBytesEstimate;
}

/// <summary>Début ou fin d'un post-traitement (<c>Merger</c>, <c>MoveFiles</c>, <c>ExtractAudio</c>…).</summary>
/// <param name="Processor">Nom du post-traitement.</param>
/// <param name="Finished">Vrai à la fin, faux au début.</param>
public sealed record PostprocessLine(string Processor, bool Finished) : YtDlpOutputLine;

/// <summary>Ligne <c>[info] … Downloading 1 format(s): 396+251</c> : liste des flux qui seront téléchargés, dans l'ordre.</summary>
/// <param name="FormatIds">Identifiants des flux (2 pour image + son séparés, 1 pour un flux déjà complet).</param>
public sealed record FormatsAnnouncedLine(IReadOnlyList<string> FormatIds) : YtDlpOutputLine;

/// <summary>Ligne <c>[download] Destination: …</c> : un nouveau flux commence.</summary>
/// <param name="Path">Fichier du flux.</param>
public sealed record DestinationLine(string Path) : YtDlpOutputLine;

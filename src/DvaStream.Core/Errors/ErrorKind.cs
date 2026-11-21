namespace DvaStream.Core.Errors;

/// <summary>
/// Taxonomie des erreurs métier (TDD §10.1). Chaque valeur correspond à une clé de ressource (message en
/// langage courant et action suggérée), ajoutée au fil des jalons : prévisualisation en M2-12, le reste en M5-04.
/// </summary>
public enum ErrorKind
{
    /// <summary>Lien refusé par la validation (normalement arrêté avant le moteur).</summary>
    InvalidUrl,

    /// <summary>Lien de playlist, non pris en charge en v1.0.</summary>
    UnsupportedPlaylist,

    /// <summary>Vidéo privée.</summary>
    VideoPrivate,

    /// <summary>Vidéo supprimée ou introuvable.</summary>
    VideoUnavailable,

    /// <summary>Vidéo restreinte (âge, région, abonnement, vérification anti-robot).</summary>
    VideoRestricted,

    /// <summary>Pas de connexion ou connexion interrompue.</summary>
    Network,

    /// <summary>Délai dépassé.</summary>
    Timeout,

    /// <summary>Trop de requêtes : le service demande d'attendre.</summary>
    RateLimited,

    /// <summary>Plus de place sur le disque.</summary>
    DiskFull,

    /// <summary>Dossier de destination non inscriptible.</summary>
    FolderNotWritable,

    /// <summary>Le moteur (yt-dlp) est introuvable.</summary>
    EngineMissing,

    /// <summary>Le moteur ne fonctionne pas (sortie inattendue, extraction en échec).</summary>
    EngineBroken,

    /// <summary>FFmpeg est introuvable.</summary>
    FfmpegMissing,

    /// <summary>FFmpeg a échoué.</summary>
    FfmpegFailed,

    /// <summary>Opération annulée par l'utilisateur.</summary>
    Cancelled,

    /// <summary>Erreur non reconnue ; les détails techniques sont conservés.</summary>
    Unknown,
}

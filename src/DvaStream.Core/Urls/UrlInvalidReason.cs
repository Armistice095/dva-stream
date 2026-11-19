namespace DvaStream.Core.Urls;

/// <summary>
/// Raison pour laquelle une saisie n'est pas une URL de vidéo acceptée. Le domaine ne contient aucun
/// texte : la couche présentation associe un message traduit à chaque valeur (M1-05).
/// </summary>
public enum UrlInvalidReason
{
    /// <summary>Saisie vide ou composée uniquement d'espaces.</summary>
    Empty,

    /// <summary>Ce n'est pas une URL absolue (texte libre, espaces, caractères de contrôle, trop long).</summary>
    NotAUrl,

    /// <summary>Schéma autre que <c>http</c> ou <c>https</c> (<c>file:</c>, <c>javascript:</c>, <c>ftp:</c>…).</summary>
    UnsupportedScheme,

    /// <summary>Domaine non autorisé, informations d'identification dans l'URL ou port non standard.</summary>
    UnsupportedHost,

    /// <summary>Domaine autorisé mais chemin ou paramètres non reconnus (chaîne, <c>/embed/</c>, <c>watch</c> sans <c>v</c>…).</summary>
    UnrecognizedFormat,

    /// <summary>Un identifiant de vidéo est présent mais n'a pas 11 caractères valides.</summary>
    InvalidVideoId,
}

namespace DvaStream.App.ViewModels;

/// <summary>État de validation du champ de saisie d'URL.</summary>
public enum UrlInputState
{
    /// <summary>Rien de saisi (ou seulement des espaces).</summary>
    Empty,

    /// <summary>La saisie est l'URL d'une vidéo acceptée.</summary>
    Valid,

    /// <summary>La saisie est refusée (y compris une playlist, non prise en charge en v1.0).</summary>
    Invalid,
}

namespace DvaStream.App.ViewModels;

/// <summary>
/// État global de l'écran principal (TDD §9.2). <see cref="Idle"/>, <see cref="Analyzing"/>, <see cref="Ready"/>, <see cref="Error"/>,
/// puis (M4-11) <see cref="Downloading"/>, <see cref="Done"/> et <see cref="DownloadFailed"/>.
/// </summary>
public enum MainState
{
    /// <summary>Rien en cours : la saisie est disponible.</summary>
    Idle,

    /// <summary>Analyse de l'URL en cours (annulable).</summary>
    Analyzing,

    /// <summary>Vidéo analysée : la prévisualisation est affichée.</summary>
    Ready,

    /// <summary>L'analyse a échoué : un message est affiché.</summary>
    Error,

    /// <summary>Téléchargement en cours : la carte de progression remplace le choix du format.</summary>
    Downloading,

    /// <summary>Téléchargement terminé : le fichier est enregistré.</summary>
    Done,

    /// <summary>Le téléchargement a échoué : un message et « Réessayer » sont affichés.</summary>
    DownloadFailed,
}

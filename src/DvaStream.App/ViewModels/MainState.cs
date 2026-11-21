namespace DvaStream.App.ViewModels;

/// <summary>
/// État global de l'écran principal (TDD §9.2). Jalon M2 : <see cref="Idle"/>, <see cref="Analyzing"/>, <see cref="Ready"/> et <see cref="Error"/> ;
/// <c>Downloading</c> et <c>Done</c> s'ajoutent aux jalons suivants.
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
}

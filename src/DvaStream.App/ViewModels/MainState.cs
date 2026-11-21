namespace DvaStream.App.ViewModels;

/// <summary>
/// État global de l'écran principal (TDD §9.2). Au jalon M1 : <see cref="Idle"/> et <see cref="Analyzing"/> ;
/// <c>Ready</c>, <c>Downloading</c>, <c>Done</c> et <c>Error</c> s'ajoutent aux jalons suivants.
/// </summary>
public enum MainState
{
    /// <summary>Rien en cours : la saisie est disponible.</summary>
    Idle,

    /// <summary>Analyse de l'URL en cours (annulable).</summary>
    Analyzing,
}

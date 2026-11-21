namespace DvaStream.App.Platform;

/// <summary>Sélecteur de dossier natif (M3-07).</summary>
public interface IFolderPicker
{
    /// <summary>Chemin du dossier choisi, ou <c>null</c> si l'utilisateur annule ou si le sélecteur est indisponible.</summary>
    Task<string?> PickFolderAsync(string title, CancellationToken ct);
}

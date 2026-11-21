namespace DvaStream.Core.Settings;

/// <summary>Accès aux paramètres enregistrés (M3-01).</summary>
public interface ISettingsStore
{
    /// <summary>Paramètres courants, toujours valides.</summary>
    AppSettings Current { get; }

    /// <summary>Enregistre <paramref name="settings"/> (valeurs invalides corrigées) puis signale le changement.</summary>
    Task SaveAsync(AppSettings settings, CancellationToken ct);

    /// <summary>Déclenché après chaque enregistrement réussi, avec les paramètres enregistrés.</summary>
    event EventHandler<AppSettings> Changed;
}

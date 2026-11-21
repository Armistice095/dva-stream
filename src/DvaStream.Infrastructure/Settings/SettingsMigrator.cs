using System.Text.Json.Nodes;

namespace DvaStream.Infrastructure.Settings;

/// <summary>
/// Fait évoluer un fichier de paramètres ancien vers la version courante (M3-02). Chaque étape <c>n</c> transforme
/// un document de version <c>n</c> en version <c>n + 1</c>. Un fichier sans <c>schemaVersion</c> est de version 0.
/// </summary>
public sealed class SettingsMigrator
{
    private readonly int _target;
    private readonly IReadOnlyDictionary<int, Action<JsonObject>> _steps;

    /// <summary>Crée un migrateur vers la version <paramref name="target"/> avec les étapes données.</summary>
    public SettingsMigrator(int target, IReadOnlyDictionary<int, Action<JsonObject>>? steps = null)
    {
        _target = target;
        _steps = steps ?? new Dictionary<int, Action<JsonObject>>();
    }

    /// <summary>Version de départ du document (0 si absente ou illisible).</summary>
    public static int VersionOf(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return root["schemaVersion"] is JsonValue value && value.TryGetValue<int>(out var version) && version > 0 ? version : 0;
    }

    /// <summary>
    /// Applique les étapes manquantes et renseigne <c>schemaVersion</c>. Un document plus récent que la cible
    /// est laissé tel quel (les champs inconnus sont ignorés à la lecture).
    /// </summary>
    public void Migrate(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var version = VersionOf(root);
        while (version < _target)
        {
            if (_steps.TryGetValue(version, out var step))
            {
                step(root);
            }

            version++;
            root["schemaVersion"] = version;
        }
    }
}

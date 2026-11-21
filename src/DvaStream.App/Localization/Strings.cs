using System.Collections;
using System.Globalization;
using System.Resources;

// Le fichier Strings.resx (sans suffixe de culture) contient le français, langue par défaut (TDD §9.4).
[assembly: NeutralResourcesLanguage("fr")]

namespace DvaStream.App.Localization;

/// <summary>
/// Accès aux textes de l'interface (M1-04). Français par défaut ; l'anglais s'applique quand la langue
/// de l'interface du système est l'anglais. Aucun texte utilisateur ne doit être écrit en dur ailleurs.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("DvaStream.App.Localization.Strings", typeof(Strings).Assembly);

    /// <summary>Culture forcée (outil de test, variable <c>DVASTREAM_LANG</c>) ; <c>null</c> = langue du système.</summary>
    public static CultureInfo? Culture { get; set; }

    /// <summary>Texte de <paramref name="key"/> dans la culture courante ; <c>[clé]</c> si la clé est absente.</summary>
    public static string Get(string key) => Get(key, Culture);

    /// <summary>Texte de <paramref name="key"/> dans <paramref name="culture"/> ; <c>[clé]</c> si la clé est absente.</summary>
    public static string Get(string key, CultureInfo? culture) =>
        Manager.GetString(key, culture) ?? $"[{key}]";

    /// <summary>
    /// Clés définies par le fichier de ressources de <paramref name="culture"/> uniquement, sans repli sur
    /// une autre culture (<see cref="CultureInfo.InvariantCulture"/> = le fichier français par défaut).
    /// </summary>
    public static IReadOnlySet<string> GetKeys(CultureInfo culture)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var set = Manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        if (set is not null)
        {
            foreach (DictionaryEntry entry in set)
            {
                keys.Add((string)entry.Key);
            }
        }

        return keys;
    }
}

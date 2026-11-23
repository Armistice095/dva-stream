using System.Globalization;
using System.Text;

namespace DvaStream.Core.Files;

/// <summary>
/// Nettoie un titre de vidéo pour en faire un nom de fichier sûr (M4-06). Règles : caractères interdits
/// (<c>&lt;&gt;:"/\|?*</c>) et de contrôle remplacés par une espace, caractères invisibles retirés, espaces
/// regroupées, Unicode NFC, espaces et points retirés aux deux bouts, noms réservés Windows (<c>CON</c>, <c>NUL</c>,
/// <c>COM1</c>…) préfixés par <c>_</c>, longueur limitée (octets UTF-8 et chemin complet), conflits résolus par un suffixe.
/// </summary>
public sealed class FileNamer : IFileNamer
{
    /// <summary>Nom utilisé quand le titre ne contient plus rien d'utilisable.</summary>
    public const string FallbackName = "video";

    /// <summary>Longueur maximale du nom sans extension, en octets UTF-8 (la plupart des systèmes de fichiers limitent à 255).</summary>
    public const int MaxStemBytes = 200;

    private const int MinStemChars = 16;
    private const int ConflictReserve = 7; // « (9999) » précédé d'une espace
    private const int MaxConflicts = 9999;

    private static readonly HashSet<char> Forbidden = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private readonly Func<string, bool> _exists;
    private readonly int _maxPathLength;

    /// <param name="exists">Dit si un chemin existe (fichier ou dossier) ; par défaut le disque.</param>
    /// <param name="maxPathLength">Longueur maximale du chemin complet (260 sous Windows sans chemins longs).</param>
    public FileNamer(Func<string, bool>? exists = null, int maxPathLength = 259)
    {
        _exists = exists ?? (path => File.Exists(path) || Directory.Exists(path));
        _maxPathLength = maxPathLength;
    }

    /// <inheritdoc />
    public string BuildSafeName(string title, string extension, string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var ext = NormalizeExtension(extension);
        var stem = Sanitize(title);

        var pathBudget = _maxPathLength - folder.Length - 1 - (ext.Length + 1) - ConflictReserve;
        var charBudget = Math.Max(MinStemChars, pathBudget);
        stem = Truncate(stem, charBudget);

        var candidate = $"{stem}.{ext}";
        if (!_exists(Path.Combine(folder, candidate)))
        {
            return candidate;
        }

        for (var n = 1; n <= MaxConflicts; n++)
        {
            var suffix = $" ({n.ToString(CultureInfo.InvariantCulture)})";
            candidate = $"{Truncate(stem, charBudget - suffix.Length)}{suffix}.{ext}";
            if (!_exists(Path.Combine(folder, candidate)))
            {
                return candidate;
            }
        }

        // Dernier recours (plus de 9 999 homonymes) : un identifiant garantit l'unicité.
        return $"{Truncate(stem, charBudget - 33)} {Guid.NewGuid():N}.{ext}";
    }

    /// <summary>Titre nettoyé, sans extension ni suffixe (fonction pure, publique pour les tests).</summary>
    public static string Sanitize(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return FallbackName;
        }

        var builder = new StringBuilder(title.Length);
        foreach (var rune in title.EnumerateRunes())
        {
            if (rune == Rune.ReplacementChar)
            {
                continue; // caractère invalide ou jumelle orpheline
            }

            var category = Rune.GetUnicodeCategory(rune);
            if (rune.IsAscii && Forbidden.Contains((char)rune.Value))
            {
                builder.Append(' ');
            }
            else if (category is UnicodeCategory.Control or UnicodeCategory.LineSeparator
                     or UnicodeCategory.ParagraphSeparator or UnicodeCategory.SpaceSeparator)
            {
                builder.Append(' ');
            }
            else if (category is UnicodeCategory.Format or UnicodeCategory.PrivateUse)
            {
                continue; // invisibles (espace de largeur nulle, marque de sens d'écriture…)
            }
            else
            {
                builder.Append(rune.ToString());
            }
        }

        var text = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        text = text.Normalize(NormalizationForm.FormC).Trim(' ', '.');
        if (text.Length == 0)
        {
            return FallbackName;
        }

        // Windows réserve le nom avant le premier point, quelle que soit l'extension (« CON.txt »).
        var dot = text.IndexOf('.', StringComparison.Ordinal);
        var first = (dot < 0 ? text : text[..dot]).TrimEnd(' ');
        return ReservedNames.Contains(first) ? "_" + text : text;
    }

    private static string NormalizeExtension(string extension)
    {
        var ext = (extension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
        if (ext.Length is 0 or > 10 || !ext.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9'))
        {
            throw new ArgumentException($"Extension invalide : « {extension} ».", nameof(extension));
        }

        return ext;
    }

    /// <summary>Coupe sur une frontière de caractère complet (jamais au milieu d'une paire de jumelles ou d'un accent combiné).</summary>
    private static string Truncate(string text, int maxChars)
    {
        if (maxChars < 1)
        {
            maxChars = 1;
        }

        if (text.Length <= maxChars && Encoding.UTF8.GetByteCount(text) <= MaxStemBytes)
        {
            return text;
        }

        var result = new StringBuilder();
        var bytes = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            var element = (string)elements.Current;
            var elementBytes = Encoding.UTF8.GetByteCount(element);
            if (result.Length + element.Length > maxChars || bytes + elementBytes > MaxStemBytes)
            {
                break;
            }

            result.Append(element);
            bytes += elementBytes;
        }

        var cut = result.ToString().Trim(' ', '.');
        return cut.Length == 0 ? FallbackName : cut;
    }
}

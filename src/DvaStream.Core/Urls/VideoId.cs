using System.Diagnostics.CodeAnalysis;

namespace DvaStream.Core.Urls;

/// <summary>
/// Identifiant d'une vidéo YouTube : exactement 11 caractères parmi <c>A-Z a-z 0-9 _ -</c>.
/// Une instance est toujours valide : la seule façon d'en obtenir une est <see cref="TryCreate"/>.
/// </summary>
public sealed record VideoId
{
    /// <summary>Longueur d'un identifiant de vidéo.</summary>
    public const int Length = 11;

    private VideoId(string value) => Value = value;

    /// <summary>Valeur brute de l'identifiant (casse significative).</summary>
    public string Value { get; }

    /// <summary>Indique si <paramref name="candidate"/> est un identifiant de vidéo bien formé.</summary>
    public static bool IsValid([NotNullWhen(true)] string? candidate)
    {
        if (candidate is null || candidate.Length != Length)
        {
            return false;
        }

        foreach (var c in candidate)
        {
            if (!IsIdCharacter(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Crée un identifiant si <paramref name="candidate"/> est bien formé ; sinon renvoie <c>false</c>.</summary>
    public static bool TryCreate(string? candidate, [NotNullWhen(true)] out VideoId? id)
    {
        if (IsValid(candidate))
        {
            id = new VideoId(candidate);
            return true;
        }

        id = null;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    // Volontairement limité à l'ASCII : char.IsLetterOrDigit accepterait des lettres Unicode.
    private static bool IsIdCharacter(char c) =>
        c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or '-';
}

namespace DvaStream.Core.Media;

/// <summary>Choix de la résolution à proposer quand la préférée n'existe pas (M3-04).</summary>
public static class ResolutionResolver
{
    /// <summary>
    /// <paramref name="preferred"/> si elle est offerte ; sinon la plus proche <b>inférieure</b> ; sinon (tout est
    /// au-dessus) la plus basse offerte ; <c>null</c> si rien n'est offert.
    /// </summary>
    public static int? Resolve(int preferred, IReadOnlyCollection<int> available)
    {
        ArgumentNullException.ThrowIfNull(available);

        var valid = available.Where(h => h > 0).ToList();
        if (valid.Count == 0)
        {
            return null;
        }

        if (valid.Contains(preferred))
        {
            return preferred;
        }

        var lower = valid.Where(h => h < preferred).ToList();
        return lower.Count > 0 ? lower.Max() : valid.Min();
    }
}

using System.Globalization;

namespace DvaStream.App.ViewModels;

/// <summary>Formatage des durées pour l'affichage (FR-10).</summary>
public static class DurationFormatter
{
    /// <summary><c>h:mm:ss</c> à partir d'une heure, <c>m:ss</c> en dessous ; <c>null</c> si la durée est inconnue (direct).</summary>
    public static string? Format(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return null;
        }

        var totalHours = (long)duration.TotalHours;
        return totalHours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{totalHours}:{duration.Minutes:00}:{duration.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{duration.Minutes}:{duration.Seconds:00}");
    }
}

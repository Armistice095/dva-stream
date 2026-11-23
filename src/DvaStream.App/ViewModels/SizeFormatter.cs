using System.Globalization;
using DvaStream.App.Localization;

namespace DvaStream.App.ViewModels;

/// <summary>Taille de fichier lisible (unités décimales : 1 Mo = 1 000 000 octets).</summary>
public static class SizeFormatter
{
    private const double Megabyte = 1_000_000;
    private const double Gigabyte = 1_000_000_000;

    /// <summary>Ex. « 232 Mo », « 1,4 Go » (« MB », « GB » en anglais) ; moins d'un Mo s'affiche « &lt; 1 Mo ».</summary>
    public static string Format(long bytes)
    {
        var culture = Strings.Culture ?? CultureInfo.CurrentUICulture;
        if (bytes >= Gigabyte)
        {
            return string.Format(culture, Strings.Get("Size_GB"), (bytes / Gigabyte).ToString("0.0", culture));
        }

        var megabytes = Math.Max(1, (long)Math.Round(bytes / Megabyte));
        return string.Format(culture, Strings.Get("Size_MB"), megabytes.ToString("0", culture));
    }

    /// <summary>Vitesse lisible : « 4,1 Mo/s » à partir de 1 Mo/s, « 640 Ko/s » en dessous.</summary>
    public static string FormatSpeed(double bytesPerSecond)
    {
        var culture = Strings.Culture ?? CultureInfo.CurrentUICulture;
        if (bytesPerSecond >= Megabyte)
        {
            return string.Format(culture, Strings.Get("Speed_MBps"), (bytesPerSecond / Megabyte).ToString("0.0", culture));
        }

        var kilobytes = Math.Max(1, (long)Math.Round(bytesPerSecond / 1000));
        return string.Format(culture, Strings.Get("Speed_KBps"), kilobytes.ToString("0", culture));
    }
}

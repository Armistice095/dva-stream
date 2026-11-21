using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace DvaStream.App.Views;

/// <summary>Convertit les octets d'une image en <see cref="Bitmap"/> ; une image illisible donne <c>null</c> (carte sans image).</summary>
public sealed class BytesToBitmapConverter : IValueConverter
{
    /// <summary>Instance partagée pour les liaisons AXAML.</summary>
    public static readonly BytesToBitmapConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException)
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

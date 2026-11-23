namespace DvaStream.App.Localization;

/// <summary>Libellé d'une qualité vidéo : <c>1080p · Haute définition</c> (maquette).</summary>
public static class QualityLabels
{
    /// <summary>Libellé de <paramref name="height"/> avec sa mention (4K, 2K, haute définition, HD, standard).</summary>
    public static string For(int height)
    {
        var key = height switch
        {
            >= 2160 => "Quality_4K",
            >= 1440 => "Quality_2K",
            >= 1080 => "Quality_FullHD",
            >= 720 => "Quality_HD",
            >= 480 => "Quality_Standard",
            _ => null,
        };

        return key is null ? $"{height}p" : $"{height}p · {Strings.Get(key)}";
    }
}

using Avalonia.Markup.Xaml;

namespace DvaStream.App.Localization;

/// <summary>
/// Extension de balisage AXAML : <c>Text="{loc:Loc Main_Status}"</c> (avec <c>xmlns:loc="using:DvaStream.App.Localization"</c>).
/// La langue est fixée au démarrage ; pas de changement à chaud.
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    public LocExtension(string key) => Key = key;

    /// <summary>Clé du texte dans <c>Strings.resx</c>.</summary>
    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.Get(Key);
}

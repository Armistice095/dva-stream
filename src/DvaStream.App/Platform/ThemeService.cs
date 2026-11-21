using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using DvaStream.Core.Settings;

namespace DvaStream.App.Platform;

/// <summary>Applique le thème choisi (système, clair, sombre) au démarrage et à chaud (M3-07, FR-61).</summary>
public sealed class ThemeService
{
    private readonly ISettingsStore _store;
    private bool _started;

    public ThemeService(ISettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <summary>Variante Avalonia correspondant au choix de l'utilisateur.</summary>
    public static ThemeVariant ToVariant(ThemeChoice choice) => choice switch
    {
        ThemeChoice.Light => ThemeVariant.Light,
        ThemeChoice.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    /// <summary>Applique le thème enregistré puis suit les changements de paramètres.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        Apply(_store.Current.Theme);
        _store.Changed += (_, settings) => Apply(settings.Theme);
    }

    private static void Apply(ThemeChoice choice)
    {
        static void Set(ThemeChoice c)
        {
            if (Application.Current is { } app)
            {
                app.RequestedThemeVariant = ToVariant(c);
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Set(choice);
        }
        else
        {
            Dispatcher.UIThread.Post(() => Set(choice));
        }
    }
}

using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DvaStream.App.Composition;
using DvaStream.App.Localization;
using DvaStream.App.Platform;
using DvaStream.App.ViewModels;
using DvaStream.App.Views;
using DvaStream.Core.Platform;
using DvaStream.Infrastructure.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DvaStream.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ApplyLanguageOverride();

        _services = new ServiceCollection()
            .AddDvaStream(new PlatformServices())
            .BuildServiceProvider();

        _services.GetRequiredService<ThemeService>().Start();

        var logger = _services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("DVA Stream démarré (version {Version}).", typeof(App).Assembly.GetName().Version);

        // Vérifie FFmpeg au démarrage sans bloquer l'ouverture de la fenêtre : le résultat est journalisé.
        var platform = _services.GetRequiredService<IPlatformServices>();
        _ = _services.GetRequiredService<FfmpegProbe>().ProbeAsync(
            FfmpegLocator.GetExecutablePath(platform.Directories, OperatingSystem.IsWindows() ? HostOs.Windows : HostOs.Linux));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainViewModel>(),
            };
            desktop.Exit += (_, _) => _services?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Permet d'essayer une autre langue sans changer celle de Windows : <c>DVASTREAM_LANG=en</c>.</summary>
    private static void ApplyLanguageOverride()
    {
        var language = Environment.GetEnvironmentVariable("DVASTREAM_LANG");
        if (string.IsNullOrWhiteSpace(language))
        {
            return;
        }

        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            // Valeur inconnue : on garde la langue du système.
        }
    }
}

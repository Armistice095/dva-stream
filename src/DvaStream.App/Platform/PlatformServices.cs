using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using DvaStream.Core.Platform;

namespace DvaStream.App.Platform;

/// <summary>Implémentation réelle de <see cref="IPlatformServices"/> pour l'OS courant.</summary>
public sealed class PlatformServices : IPlatformServices
{
    private readonly Func<TopLevel?> _topLevelProvider;

    /// <param name="topLevelProvider">
    /// Fournit la fenêtre qui donne accès au presse-papiers. Appelée à chaque lecture, car les services
    /// sont créés avant la fenêtre principale ; par défaut, la fenêtre principale de l'application.
    /// </param>
    public PlatformServices(Func<TopLevel?>? topLevelProvider = null)
    {
        _topLevelProvider = topLevelProvider ?? GetMainWindow;

        var os = OperatingSystem.IsWindows() ? HostOs.Windows
               : OperatingSystem.IsMacOS() ? HostOs.MacOs
               : HostOs.Linux;

        Directories = AppDirectoryResolver.Resolve(
            os,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetEnvironmentVariable);

        EngineExecutablePath = EngineLocator.GetExecutablePath(Directories, os);
    }

    public AppDirectories Directories { get; }

    public string EngineExecutablePath { get; }

    public async Task<string?> ReadClipboardAsync()
    {
        try
        {
            // Le presse-papiers ne s'utilise que sur le thread d'interface.
            return await Dispatcher.UIThread.InvokeAsync<string?>(async () =>
            {
                var clipboard = _topLevelProvider()?.Clipboard;
                return clipboard is null ? null : await clipboard.TryGetTextAsync();
            });
        }
        catch (Exception)
        {
            // Presse-papiers verrouillé par une autre application (fréquent sous Windows), format
            // inattendu, session sans presse-papiers : ce n'est pas une erreur pour l'utilisateur.
            // Le contenu du presse-papiers n'est jamais journalisé.
            return null;
        }
    }

    private static TopLevel? GetMainWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}

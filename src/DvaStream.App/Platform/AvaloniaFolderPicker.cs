using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace DvaStream.App.Platform;

/// <summary>Sélecteur de dossier fourni par la fenêtre principale (boîte de dialogue native du système).</summary>
public sealed class AvaloniaFolderPicker : IFolderPicker
{
    /// <inheritdoc />
    public Task<string?> PickFolderAsync(string title, CancellationToken ct) =>
        Dispatcher.UIThread.CheckAccess()
            ? PickAsync(title)
            : Dispatcher.UIThread.InvokeAsync(() => PickAsync(title));

    private static async Task<string?> PickAsync(string title)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return null;
        }

        var provider = TopLevel.GetTopLevel(window)?.StorageProvider;
        if (provider is null || !provider.CanPickFolder)
        {
            return null;
        }

        var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}

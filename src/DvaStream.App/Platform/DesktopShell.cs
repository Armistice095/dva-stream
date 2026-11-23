using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using DvaStream.Core.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DvaStream.App.Platform;

/// <summary>Implémentation réelle de <see cref="IDesktopShell"/> (M4-12) : lance les commandes de <see cref="ShellCommands"/>.</summary>
public sealed class DesktopShell : IDesktopShell
{
    private readonly HostOs _os;
    private readonly Func<bool> _isAppActive;
    private readonly ILogger _logger;

    public DesktopShell(Func<bool>? isAppActive = null, ILogger<DesktopShell>? logger = null)
    {
        _os = OperatingSystem.IsWindows() ? HostOs.Windows
            : OperatingSystem.IsMacOS() ? HostOs.MacOs
            : HostOs.Linux;
        _isAppActive = isAppActive ?? IsMainWindowActive;
        _logger = logger ?? NullLogger<DesktopShell>.Instance;
    }

    public void OpenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _logger.LogWarning("Fichier à ouvrir introuvable.");
            return;
        }

        Run(ShellCommands.Open(_os, path));
    }

    public void RevealInFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (File.Exists(path))
        {
            Run(ShellCommands.Reveal(_os, path));
        }
        else if (Path.GetDirectoryName(path) is { Length: > 0 } folder && Directory.Exists(folder))
        {
            Run(ShellCommands.OpenFolder(_os, folder));
        }
    }

    public void Notify(string title, string message)
    {
        bool active;
        try
        {
            active = _isAppActive();
        }
        catch (Exception)
        {
            active = false;
        }

        if (!active)
        {
            Run(ShellCommands.Notify(_os, title, message));
        }
    }

    private void Run(ShellCommand command)
    {
        try
        {
            var info = new ProcessStartInfo(command.FileName)
            {
                UseShellExecute = command.UseShellExecute,
                CreateNoWindow = true,
            };
            if (command.RawArguments is { } raw)
            {
                info.Arguments = raw;
            }
            else
            {
                foreach (var argument in command.Arguments)
                {
                    info.ArgumentList.Add(argument);
                }
            }

            using var process = Process.Start(info);
        }
        catch (Exception ex)
        {
            // Programme absent (ex. notify-send sur une session sans notifications) : jamais bloquant.
            _logger.LogWarning(ex, "Commande système impossible : {Program}.", command.FileName);
        }
    }

    private static bool IsMainWindowActive() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.IsActive == true;
}

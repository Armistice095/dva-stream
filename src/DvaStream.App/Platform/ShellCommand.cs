using System.Text;
using DvaStream.Core.Platform;

namespace DvaStream.App.Platform;

/// <summary>Commande système à lancer (M4-12).</summary>
/// <param name="FileName">Programme, ou fichier à ouvrir avec son application associée si <paramref name="UseShellExecute"/>.</param>
/// <param name="Arguments">Arguments, transmis un par un sans réinterprétation.</param>
/// <param name="RawArguments">Ligne d'arguments brute (cas particulier de l'Explorateur Windows), prioritaire sur <paramref name="Arguments"/>.</param>
/// <param name="UseShellExecute">Vrai pour ouvrir un fichier avec son application par défaut (Windows).</param>
public sealed record ShellCommand(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? RawArguments = null,
    bool UseShellExecute = false);

/// <summary>
/// Construit les commandes propres à chaque OS pour ouvrir un fichier, l'afficher dans son dossier et notifier
/// (M4-12, FR-37). Pur : ne lance rien, donc testable sur n'importe quel OS.
/// </summary>
public static class ShellCommands
{
    /// <summary>Identifiant d'application de Windows PowerShell : en attendant l'installateur (M8), c'est lui qui autorise l'affichage d'une notification.</summary>
    public const string WindowsToastAppId = @"{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe";

    /// <summary>Ouvre <paramref name="path"/> avec l'application associée.</summary>
    public static ShellCommand Open(HostOs os, string path) => os switch
    {
        HostOs.Windows => new ShellCommand(path, [], UseShellExecute: true),
        HostOs.MacOs => new ShellCommand("open", [path]),
        _ => new ShellCommand("xdg-open", [path]),
    };

    /// <summary>Affiche <paramref name="path"/> dans le gestionnaire de fichiers (Linux : ouvre son dossier).</summary>
    public static ShellCommand Reveal(HostOs os, string path) => os switch
    {
        HostOs.Windows => new ShellCommand("explorer.exe", [], RawArguments: $"/select,\"{path}\""),
        HostOs.MacOs => new ShellCommand("open", ["-R", path]),
        _ => new ShellCommand("xdg-open", [ParentFolder(path)]),
    };

    // Dossier parent calculé à la main : Path.GetDirectoryName change les séparateurs selon l'OS qui exécute le code.
    private static string ParentFolder(string path)
    {
        var index = path.LastIndexOfAny(['/', '\\']);
        return index > 0 ? path[..index] : index == 0 ? path[..1] : path;
    }

    /// <summary>Ouvre le dossier <paramref name="folder"/>.</summary>
    public static ShellCommand OpenFolder(HostOs os, string folder) => Open(os, folder);

    /// <summary>Affiche une notification système.</summary>
    public static ShellCommand Notify(HostOs os, string title, string message) => os switch
    {
        HostOs.Windows => new ShellCommand("powershell.exe",
        [
            "-NoProfile", "-NonInteractive", "-WindowStyle", "Hidden",
            "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(WindowsToastScript(title, message))),
        ]),
        HostOs.MacOs => new ShellCommand("osascript",
        [
            "-e", $"display notification \"{EscapeAppleScript(message)}\" with title \"{EscapeAppleScript(title)}\"",
        ]),
        _ => new ShellCommand("notify-send", ["--app-name=DVA Stream", "--", title, message]),
    };

    public static string WindowsToastScript(string title, string message) =>
        "$ErrorActionPreference = 'Stop'\n" +
        "[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null\n" +
        "$xml = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)\n" +
        "$text = $xml.GetElementsByTagName('text')\n" +
        $"$text.Item(0).AppendChild($xml.CreateTextNode('{EscapePowerShell(title)}')) | Out-Null\n" +
        $"$text.Item(1).AppendChild($xml.CreateTextNode('{EscapePowerShell(message)}')) | Out-Null\n" +
        "$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)\n" +
        $"[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('{WindowsToastAppId}').Show($toast)\n";

    // Dans une chaîne PowerShell entre apostrophes, seule l'apostrophe est spéciale (elle se double).
    // Les guillemets typographiques ’ ‘ sont aussi traités comme des apostrophes par PowerShell.
    public static string EscapePowerShell(string text) =>
        text.Replace("'", "''", StringComparison.Ordinal)
            .Replace("‘", "‘‘", StringComparison.Ordinal)
            .Replace("’", "’’", StringComparison.Ordinal);

    public static string EscapeAppleScript(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}

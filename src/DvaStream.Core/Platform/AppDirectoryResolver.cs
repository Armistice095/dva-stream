namespace DvaStream.Core.Platform;

/// <summary>
/// Calcule les dossiers de l'application selon l'OS (TDD §11). Fonction pure : toutes les
/// valeurs de l'environnement sont passées en paramètre pour rester testable sur n'importe quel OS.
/// </summary>
public static class AppDirectoryResolver
{
    public const string AppFolderName = "DvaStream";

    public static AppDirectories Resolve(
        HostOs os,
        string homeDirectory,
        string appDataDirectory,
        string localAppDataDirectory,
        Func<string, string?> getEnvironmentVariable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        switch (os)
        {
            case HostOs.Windows:
                return new AppDirectories(
                    Path.Combine(appDataDirectory, AppFolderName),
                    Path.Combine(localAppDataDirectory, AppFolderName));

            case HostOs.MacOs:
                var support = Path.Combine(homeDirectory, "Library", "Application Support", AppFolderName);
                return new AppDirectories(support, support);

            default:
                var config = XdgOrDefault(getEnvironmentVariable, "XDG_CONFIG_HOME", Path.Combine(homeDirectory, ".config"));
                var data = XdgOrDefault(getEnvironmentVariable, "XDG_DATA_HOME", Path.Combine(homeDirectory, ".local", "share"));
                return new AppDirectories(
                    Path.Combine(config, AppFolderName),
                    Path.Combine(data, AppFolderName));
        }
    }

    // La spécification XDG impose un chemin absolu ; une valeur vide ou relative est ignorée.
    private static string XdgOrDefault(Func<string, string?> getEnvironmentVariable, string name, string fallback)
    {
        var value = getEnvironmentVariable(name);
        return !string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value) ? value : fallback;
    }
}

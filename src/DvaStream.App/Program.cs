using Avalonia;

namespace DvaStream.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // Démarrage impossible : on laisse une trace lisible (console et fichier) au lieu de quitter en silence.
            Console.Error.WriteLine($"DVA Stream n'a pas pu démarrer : {ex}");
            TryWriteCrashFile(ex);
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect();

    private static void TryWriteCrashFile(Exception ex)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DvaStream", "logs");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "crash.txt"), $"{DateTimeOffset.Now:O}{Environment.NewLine}{ex}");
        }
        catch (Exception writeError) when (writeError is IOException or UnauthorizedAccessException)
        {
            // Impossible d'écrire : la console a déjà reçu l'erreur.
        }
    }
}

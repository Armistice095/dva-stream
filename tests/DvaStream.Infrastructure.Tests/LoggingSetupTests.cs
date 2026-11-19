using DvaStream.Infrastructure.Logging;

namespace DvaStream.Infrastructure.Tests;

public sealed class LoggingSetupTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dvastream-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Un_fichier_de_journal_est_cree_dans_le_dossier_demande()
    {
        using (var logger = LoggingSetup.CreateLogger(_directory))
        {
            logger.Information("Test de journalisation");
        }

        var files = Directory.GetFiles(_directory, LoggingSetup.LogFilePrefix + "*.log");

        Assert.Single(files);
        Assert.Contains("Test de journalisation", File.ReadAllText(files[0]));
    }

    [Fact]
    public void Un_dossier_vide_est_refuse()
    {
        Assert.Throws<ArgumentException>(() => LoggingSetup.CreateLogger(" "));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}

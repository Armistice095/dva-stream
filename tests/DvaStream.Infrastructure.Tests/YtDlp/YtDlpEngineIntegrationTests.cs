using DvaStream.Core.Errors;
using DvaStream.Core.Platform;
using DvaStream.Core.Urls;
using DvaStream.Infrastructure.Processes;
using DvaStream.Infrastructure.Tests.Processes;
using DvaStream.Infrastructure.YtDlp;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DvaStream.Infrastructure.Tests.YtDlp;

/// <summary>
/// M2-08 : <see cref="YtDlpMediaSource"/> de bout en bout avec le vrai exécuteur de processus. Un faux moteur
/// (<c>DvaStream.TestProcess</c>, mode <c>replay</c>) rejoue les sorties réelles enregistrées de yt-dlp.
/// </summary>
public sealed class YtDlpEngineIntegrationTests : IDisposable
{
    private readonly string _engine = Path.Combine(Path.GetTempPath(), $"dvastream-engine-{Guid.NewGuid():N}");
    private readonly IPlatformServices _platform = Substitute.For<IPlatformServices>();
    private readonly VideoId _id;

    public YtDlpEngineIntegrationTests()
    {
        File.WriteAllText(_engine, "faux moteur");
        _platform.EngineExecutablePath.Returns(_engine);
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        _id = id!;
    }

    public void Dispose() => File.Delete(_engine);

    private static string FixturePath(string file) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "ytdlp", file);

    private YtDlpMediaSource Source(Func<ProcessSpec> fake, TimeSpan? timeout = null) =>
        new(new SubstitutingRunner(fake), _platform, NullLogger<YtDlpMediaSource>.Instance, timeout);

    /// <summary>Remplace la commande yt-dlp demandée par la commande du faux moteur, puis exécute pour de vrai.</summary>
    private sealed class SubstitutingRunner(Func<ProcessSpec> fake) : IProcessRunner
    {
        private readonly CliWrapProcessRunner _real = new();

        public Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct) =>
            _real.RunAsync(fake() with { Timeout = spec.Timeout, Environment = spec.Environment }, ct);
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("short")]
    [InlineData("long")]
    [InlineData("unlisted")]
    public async Task Une_sortie_enregistree_rejouee_par_un_vrai_processus_donne_un_MediaInfo(string name)
    {
        var source = Source(() => TestProcessHost.Spec("replay", FixturePath($"{name}.json"), "-", "0"));

        var info = await source.GetInfoAsync(_id, CancellationToken.None);

        Assert.NotEmpty(info.Streams);
        Assert.False(string.IsNullOrWhiteSpace(info.Title));
    }

    [Theory]
    [InlineData("deleted", ErrorKind.VideoUnavailable)]
    [InlineData("nonexistent", ErrorKind.VideoUnavailable)]
    [InlineData("network", ErrorKind.Network)]
    public async Task Un_echec_enregistre_donne_le_bon_ErrorKind(string name, ErrorKind expected)
    {
        var (exitCode, stderr) = YtDlpFixtures.Error(name);
        var errFile = Path.Combine(Path.GetTempPath(), $"dvastream-err-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(errFile, stderr);
        try
        {
            var source = Source(() => TestProcessHost.Spec("replay", FixturePath("_meta.json"), errFile, exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            var ex = await Assert.ThrowsAsync<MediaSourceException>(() => source.GetInfoAsync(_id, CancellationToken.None));

            Assert.Equal(expected, ex.Error.Kind);
        }
        finally
        {
            File.Delete(errFile);
        }
    }

    [Fact]
    public async Task Un_moteur_trop_lent_donne_Timeout()
    {
        var source = Source(() => TestProcessHost.Spec("sleep", "30000"), TimeSpan.FromMilliseconds(500));

        var ex = await Assert.ThrowsAsync<MediaSourceException>(() => source.GetInfoAsync(_id, CancellationToken.None));

        Assert.Equal(ErrorKind.Timeout, ex.Error.Kind);
    }

    [Fact]
    public async Task L_annulation_par_l_utilisateur_arrete_le_moteur()
    {
        var source = Source(() => TestProcessHost.Spec("sleep", "30000"));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.GetInfoAsync(_id, cts.Token));
    }

    [Fact]
    public async Task Un_moteur_qui_ne_peut_pas_demarrer_donne_EngineBroken()
    {
        var source = Source(() => new ProcessSpec(Path.Combine(Path.GetTempPath(), "introuvable-" + Guid.NewGuid().ToString("N")), []));

        var ex = await Assert.ThrowsAsync<MediaSourceException>(() => source.GetInfoAsync(_id, CancellationToken.None));

        Assert.Equal(ErrorKind.EngineBroken, ex.Error.Kind);
    }
}

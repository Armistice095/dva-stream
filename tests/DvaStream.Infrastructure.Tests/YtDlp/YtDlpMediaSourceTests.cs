using DvaStream.Core.Errors;
using DvaStream.Core.Media;
using DvaStream.Core.Platform;
using DvaStream.Core.Urls;
using DvaStream.Infrastructure.Processes;
using DvaStream.Infrastructure.YtDlp;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DvaStream.Infrastructure.Tests.YtDlp;

/// <summary>M2-06 : <see cref="YtDlpMediaSource"/> avec un exécuteur simulé qui rejoue les sorties enregistrées.</summary>
public sealed class YtDlpMediaSourceTests : IDisposable
{
    private readonly string _engine = Path.Combine(Path.GetTempPath(), $"dvastream-test-{Guid.NewGuid():N}");
    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly IPlatformServices _platform = Substitute.For<IPlatformServices>();
    private readonly VideoId _id;

    public YtDlpMediaSourceTests()
    {
        File.WriteAllText(_engine, "faux exécutable");
        _platform.EngineExecutablePath.Returns(_engine);
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        _id = id!;
    }

    public void Dispose() => File.Delete(_engine);

    private YtDlpMediaSource Create() => new(_runner, _platform, NullLogger<YtDlpMediaSource>.Instance);

    [Fact]
    public async Task Lance_yt_dlp_avec_les_arguments_attendus_et_convertit_la_sortie()
    {
        ProcessSpec? captured = null;
        _runner.RunAsync(Arg.Do<ProcessSpec>(s => captured = s), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, YtDlpFixtures.Json("standard"), string.Empty));

        var info = await Create().GetInfoAsync(_id, CancellationToken.None);

        Assert.Equal("dQw4w9WgXcQ", info.Id.Value);
        Assert.NotNull(captured);
        Assert.Equal(_engine, captured!.Executable);
        Assert.Equal(
            new[] { "--dump-single-json", "--no-playlist", "--no-warnings", "https://www.youtube.com/watch?v=dQw4w9WgXcQ" },
            captured.Arguments.ToArray());
        Assert.Equal(YtDlpMediaSource.InfoTimeout, captured.Timeout);
    }

    [Theory]
    [InlineData("deleted", ErrorKind.VideoUnavailable)]
    [InlineData("nonexistent", ErrorKind.VideoUnavailable)]
    [InlineData("network", ErrorKind.Network)]
    public async Task Un_echec_de_yt_dlp_devient_une_MediaSourceException_classee(string name, ErrorKind expected)
    {
        var (exitCode, stderr) = YtDlpFixtures.Error(name);
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(exitCode, string.Empty, stderr));

        var ex = await Assert.ThrowsAsync<MediaSourceException>(() => Create().GetInfoAsync(_id, CancellationToken.None));

        Assert.Equal(expected, ex.Error.Kind);
    }

    [Fact]
    public async Task Un_moteur_absent_est_signale_sans_lancer_de_processus()
    {
        File.Delete(_engine);

        var ex = await Assert.ThrowsAsync<MediaSourceException>(() => Create().GetInfoAsync(_id, CancellationToken.None));

        Assert.Equal(ErrorKind.EngineMissing, ex.Error.Kind);
        await _runner.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Fact]
    public async Task Un_moteur_qui_ne_demarre_pas_est_signale_comme_defaillant()
    {
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns<Task<ProcessResult>>(_ => throw new ProcessLaunchException("boum"));

        var ex = await Assert.ThrowsAsync<MediaSourceException>(() => Create().GetInfoAsync(_id, CancellationToken.None));

        Assert.Equal(ErrorKind.EngineBroken, ex.Error.Kind);
    }

    [Fact]
    public async Task Un_delai_depasse_donne_Timeout()
    {
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns<Task<ProcessResult>>(_ => throw new TimeoutException());

        var ex = await Assert.ThrowsAsync<MediaSourceException>(() => Create().GetInfoAsync(_id, CancellationToken.None));

        Assert.Equal(ErrorKind.Timeout, ex.Error.Kind);
    }

    [Fact]
    public async Task L_annulation_reste_une_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns<Task<ProcessResult>>(_ => throw new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().GetInfoAsync(_id, cts.Token));
    }

    [Fact]
    public async Task Une_sortie_non_json_avec_code_zero_est_signalee_comme_moteur_defaillant()
    {
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "<html>", string.Empty));

        var ex = await Assert.ThrowsAsync<MediaSourceException>(() => Create().GetInfoAsync(_id, CancellationToken.None));

        Assert.Equal(ErrorKind.EngineBroken, ex.Error.Kind);
    }

    [Fact]
    public void La_source_s_appelle_yt_dlp()
    {
        Assert.Equal("yt-dlp", Create().Name);
    }
}

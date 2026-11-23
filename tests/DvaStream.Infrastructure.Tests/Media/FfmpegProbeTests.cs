using DvaStream.Infrastructure.Media;
using DvaStream.Infrastructure.Processes;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DvaStream.Infrastructure.Tests.Media;

/// <summary>M4-02 : le démarrage signale un FFmpeg absent ou inutilisable.</summary>
public sealed class FfmpegProbeTests : IDisposable
{
    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly string _file = Path.Combine(Path.GetTempPath(), "dva-ffmpeg-" + Guid.NewGuid().ToString("N"));

    public FfmpegProbeTests() => File.WriteAllText(_file, "x");

    public void Dispose() => File.Delete(_file);

    private FfmpegProbe Probe() => new(_runner, NullLogger<FfmpegProbe>.Instance);

    [Fact]
    public async Task Fichier_absent_donne_Missing_sans_lancer_de_processus()
    {
        var status = await Probe().ProbeAsync(_file + ".nope");

        Assert.Equal(FfmpegState.Missing, status.State);
        await _runner.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Fact]
    public async Task Reponse_valide_donne_Ready_avec_la_version()
    {
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "ffmpeg version n8.1 Copyright\nconfiguration: --disable-gpl", ""));

        var status = await Probe().ProbeAsync(_file);

        Assert.Equal(FfmpegState.Ready, status.State);
        Assert.Equal("n8.1", status.Info!.Version);
        await _runner.Received(1).RunAsync(
            Arg.Is<ProcessSpec>(s => s.Executable == _file && s.Arguments.SequenceEqual(new[] { "-version" })),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1, "ffmpeg version n8.1")]
    [InlineData(0, "n'importe quoi")]
    public async Task Reponse_inattendue_donne_Broken(int code, string output)
    {
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(new ProcessResult(code, output, ""));

        Assert.Equal(FfmpegState.Broken, (await Probe().ProbeAsync(_file)).State);
    }

    [Fact]
    public async Task Echec_de_lancement_ou_delai_donne_Broken()
    {
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns<ProcessResult>(_ => throw new ProcessLaunchException("non"));
        Assert.Equal(FfmpegState.Broken, (await Probe().ProbeAsync(_file)).State);

        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns<ProcessResult>(_ => throw new TimeoutException());
        Assert.Equal(FfmpegState.Broken, (await Probe().ProbeAsync(_file)).State);
    }
}

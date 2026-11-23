using DvaStream.Core.Errors;
using DvaStream.Core.Media;
using DvaStream.Core.Platform;
using DvaStream.Core.Settings;
using DvaStream.Core.Urls;
using DvaStream.Infrastructure.Processes;
using DvaStream.Infrastructure.YtDlp;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DvaStream.Infrastructure.Tests.YtDlp;

/// <summary>M4-05 : <see cref="YtDlpMediaSource.DownloadAsync"/> avec un exécuteur simulé qui rejoue une vraie sortie.</summary>
public sealed class YtDlpMediaSourceDownloadTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dvastream-dl-{Guid.NewGuid():N}");
    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly IPlatformServices _platform = Substitute.For<IPlatformServices>();
    private readonly AppDirectories _dirs;
    private readonly string _engine;
    private readonly string _ffmpeg;
    private readonly DownloadPlan _plan;

    public YtDlpMediaSourceDownloadTests()
    {
        _dirs = new AppDirectories(Path.Combine(_root, "config"), Path.Combine(_root, "data"));
        Directory.CreateDirectory(_dirs.EngineDirectory);
        _engine = Path.Combine(_dirs.EngineDirectory, "yt-dlp-test");
        _ffmpeg = FfmpegLocator.GetExecutablePath(_dirs, OperatingSystem.IsWindows() ? HostOs.Windows : HostOs.Linux);
        File.WriteAllText(_engine, "x");
        File.WriteAllText(_ffmpeg, "x");
        _platform.EngineExecutablePath.Returns(_engine);
        _platform.Directories.Returns(_dirs);

        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        var request = new DownloadRequest(id!, "Titre", DownloadMode.Video, 360, AudioFormat.M4a, Path.Combine(_root, "dest"));
        _plan = new DownloadPlan(
            request,
            "sel",
            ["-f", "sel", "--merge-output-format", "mp4"],
            "mp4",
            Path.Combine(_dirs.TempDirectory, "job1"),
            82_052_307 + 37_548_169);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private YtDlpMediaSource Create() => new(_runner, _platform, NullLogger<YtDlpMediaSource>.Instance);

    private sealed class Collector : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> Items { get; } = [];

        public void Report(DownloadProgress value) => Items.Add(value);
    }

    private static string[] RealLines() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "progress", "direct-long.txt"));

    [Fact]
    public void Les_arguments_contiennent_gabarits_format_sortie_et_adresse()
    {
        var args = YtDlpMediaSource.BuildDownloadArguments(_plan, "/ff").ToList();

        Assert.Contains("--newline", args);
        Assert.Equal("/ff", args[args.IndexOf("--ffmpeg-location") + 1]);
        Assert.Equal("download:" + YtDlpProgressParser.DownloadTemplate, args[args.IndexOf("--progress-template") + 1]);
        Assert.Contains("postprocess:" + YtDlpProgressParser.PostprocessTemplate, args);
        Assert.Equal("sel", args[args.IndexOf("-f") + 1]);
        Assert.Equal("mp4", args[args.IndexOf("--merge-output-format") + 1]);
        Assert.Equal(_plan.OutputTemplate, args[args.IndexOf("-o") + 1]);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", args[^1]);
        Assert.DoesNotContain("--download-sections", args);
    }

    [Fact]
    public async Task Rejoue_une_vraie_sortie_rend_compte_de_la_progression_et_renvoie_le_fichier()
    {
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var spec = call.Arg<ProcessSpec>();
            foreach (var line in RealLines())
            {
                spec.OnOutputLine!(line);
            }

            File.WriteAllText(_plan.TempFilePath, "video");
            return new ProcessResult(0, string.Empty, string.Empty);
        });
        var progress = new Collector();

        var path = await Create().DownloadAsync(_plan, progress, CancellationToken.None);

        Assert.Equal(_plan.TempFilePath, path);
        Assert.Equal(DownloadPhase.Preparing, progress.Items[0].Phase);
        Assert.Contains(progress.Items, p => p.Phase == DownloadPhase.Merging);
        Assert.Equal(DownloadPhase.Completed, progress.Items[^1].Phase);
        Assert.Equal(100, progress.Items[^1].OverallPercent);
        Assert.True(Directory.Exists(_plan.TempDirectory));
    }

    [Fact]
    public async Task Une_extension_imprevue_est_retrouvee()
    {
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            File.WriteAllText(Path.Combine(_plan.TempDirectory, "media.mkv"), "v");
            File.WriteAllText(Path.Combine(_plan.TempDirectory, "media.f396.mp4"), "reste");
            return new ProcessResult(0, string.Empty, string.Empty);
        });

        var path = await Create().DownloadAsync(_plan, new Collector(), CancellationToken.None);

        Assert.EndsWith("media.mkv", path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aucun_fichier_produit_est_une_erreur_classee()
    {
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(new ProcessResult(0, string.Empty, string.Empty));

        var ex = await Assert.ThrowsAsync<MediaSourceException>(
            () => Create().DownloadAsync(_plan, new Collector(), CancellationToken.None));

        Assert.Equal(ErrorKind.Unknown, ex.Error.Kind);
    }

    [Theory]
    [InlineData("ERROR: unable to write data: [Errno 28] No space left on device", ErrorKind.DiskFull)]
    [InlineData("ERROR: [Errno 13] Permission denied: 'x'", ErrorKind.FolderNotWritable)]
    [InlineData("ERROR: Unable to download webpage: getaddrinfo failed", ErrorKind.Network)]
    [InlineData("ERROR: [youtube] abc: Private video", ErrorKind.VideoPrivate)]
    [InlineData("quelque chose d'inconnu", ErrorKind.Unknown)]
    public async Task Un_code_de_sortie_non_nul_est_classe_d_apres_stderr(string stderr, ErrorKind expected)
    {
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(new ProcessResult(1, string.Empty, stderr));

        var ex = await Assert.ThrowsAsync<MediaSourceException>(
            () => Create().DownloadAsync(_plan, new Collector(), CancellationToken.None));

        Assert.Equal(expected, ex.Error.Kind);
    }

    [Fact]
    public async Task Moteur_absent()
    {
        File.Delete(_engine);

        var ex = await Assert.ThrowsAsync<MediaSourceException>(
            () => Create().DownloadAsync(_plan, new Collector(), CancellationToken.None));

        Assert.Equal(ErrorKind.EngineMissing, ex.Error.Kind);
        await _runner.DidNotReceiveWithAnyArgs().RunAsync(default!, default);
    }

    [Fact]
    public async Task FFmpeg_absent_empeche_la_video_mais_pas_le_M4A_direct()
    {
        File.Delete(_ffmpeg);

        var ex = await Assert.ThrowsAsync<MediaSourceException>(
            () => Create().DownloadAsync(_plan, new Collector(), CancellationToken.None));
        Assert.Equal(ErrorKind.FfmpegMissing, ex.Error.Kind);

        var request = _plan.Request with { Mode = DownloadMode.Audio, Height = null };
        var audioPlan = _plan with { Request = request, FormatArguments = ["-f", "ba"], Extension = "m4a" };
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            File.WriteAllText(audioPlan.TempFilePath, "a");
            return new ProcessResult(0, string.Empty, string.Empty);
        });

        Assert.Equal(audioPlan.TempFilePath, await Create().DownloadAsync(audioPlan, new Collector(), CancellationToken.None));
    }

    [Fact]
    public async Task Echec_de_demarrage_de_yt_dlp()
    {
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns<ProcessResult>(_ => throw new ProcessLaunchException("non"));

        var ex = await Assert.ThrowsAsync<MediaSourceException>(
            () => Create().DownloadAsync(_plan, new Collector(), CancellationToken.None));

        Assert.Equal(ErrorKind.EngineBroken, ex.Error.Kind);
    }

    [Fact]
    public async Task L_annulation_traverse_sans_etre_transformee()
    {
        _runner.RunAsync(Arg.Any<ProcessSpec>(), Arg.Any<CancellationToken>())
            .Returns<ProcessResult>(_ => throw new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Create().DownloadAsync(_plan, new Collector(), new CancellationToken(canceled: true)));
    }
}

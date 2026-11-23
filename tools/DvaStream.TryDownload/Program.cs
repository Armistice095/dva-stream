using System.Diagnostics;
using System.Globalization;
using DvaStream.Core.Errors;
using DvaStream.Core.Media;
using DvaStream.Core.Platform;
using DvaStream.Core.Settings;
using DvaStream.Core.Urls;
using DvaStream.Infrastructure.Processes;
using DvaStream.Infrastructure.YtDlp;
using Microsoft.Extensions.Logging.Abstractions;

// Test manuel de M4-05 : télécharge une vidéo avec le vrai code de l'application (analyse, plan, DownloadAsync),
// affiche la progression, puis range le fichier dans <Téléchargements>\dva-try.
//   dotnet run --project tools/DvaStream.TryDownload -- <adresse> [--height 1080] [--audio] [--mp3]
// Ctrl+C annule : l'outil vérifie ensuite qu'aucun yt-dlp ou ffmpeg ne reste en cours.

var parsedArgs = args.ToList();
bool Flag(string name) => parsedArgs.Remove(name);
string? Option(string name)
{
    var i = parsedArgs.IndexOf(name);
    if (i < 0 || i + 1 >= parsedArgs.Count)
    {
        return null;
    }

    var value = parsedArgs[i + 1];
    parsedArgs.RemoveRange(i, 2);
    return value;
}

var audio = Flag("--audio");
var mp3 = Flag("--mp3");
var heightOption = Option("--height");
var input = parsedArgs.FirstOrDefault();
if (input is null)
{
    Console.WriteLine("Usage : dotnet run --project tools/DvaStream.TryDownload -- <adresse> [--height 1080] [--audio] [--mp3]");
    return 2;
}

if (new UrlParser().Parse(input) is not UrlParseResult.Valid valid)
{
    Console.WriteLine("Adresse non reconnue comme une vidéo YouTube.");
    return 2;
}

var os = OperatingSystem.IsWindows() ? HostOs.Windows : OperatingSystem.IsMacOS() ? HostOs.MacOs : HostOs.Linux;
var directories = AppDirectoryResolver.Resolve(
    os,
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    Environment.GetEnvironmentVariable);
var platform = new ToolPlatform(directories, EngineLocator.GetExecutablePath(directories, os));
var source = new YtDlpMediaSource(new CliWrapProcessRunner(), platform, NullLogger<YtDlpMediaSource>.Instance);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine();
    Console.WriteLine("Annulation demandée…");
    cts.Cancel();
};

try
{
    Console.WriteLine($"Analyse de {valid.VideoId.Value}…");
    var info = await source.GetInfoAsync(valid.VideoId, cts.Token);
    var heights = info.GetAvailableResolutions();
    Console.WriteLine($"« {info.Title} » — {info.Channel} — {info.Duration}");
    Console.WriteLine("Hauteurs : " + string.Join(", ", heights.Select(h => $"{h}p ({Describe(info.EstimateSizeBytes(DownloadMode.Video, h))})")));

    var height = heightOption is null
        ? heights.FirstOrDefault(h => h <= 1080)
        : int.Parse(heightOption, CultureInfo.InvariantCulture);
    var mode = audio || mp3 ? DownloadMode.Audio : DownloadMode.Video;
    var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "dva-try");

    var created = DownloadRequest.Create(
        info, mode, mode == DownloadMode.Video ? height : null, mp3 ? AudioFormat.Mp3 : AudioFormat.M4a, downloads);
    if (created.Request is null)
    {
        Console.WriteLine($"Demande refusée : {created.Error}");
        return 2;
    }

    var plan = DownloadPlan.Create(created.Request, info, directories.TempDirectory, Guid.NewGuid());
    Console.WriteLine($"Plan : {string.Join(' ', plan.FormatArguments)}");
    Console.WriteLine($"Taille attendue : {Describe(plan.ExpectedBytes)} — dossier temporaire : {plan.TempDirectory}");

    var progress = new ConsoleProgress();
    var file = await source.DownloadAsync(plan, progress, cts.Token);
    Console.WriteLine();
    Console.WriteLine($"Terminé : {file} ({Describe(new FileInfo(file).Length)})");

    Directory.CreateDirectory(downloads);
    var final = Path.Combine(downloads, $"{valid.VideoId.Value}-{(mode == DownloadMode.Video ? height + "p" : "audio")}{Path.GetExtension(file)}");
    File.Move(file, final, overwrite: true);
    Directory.Delete(plan.TempDirectory, recursive: true);
    Console.WriteLine($"Rangé : {final}");
    Console.WriteLine(Probe(Path.Combine(directories.EngineDirectory, FfmpegLocator.GetProbeName(os)), final));
    return 0;
}
catch (OperationCanceledException)
{
    Console.WriteLine("Annulé. Vérification des processus restants (5 s)…");
    await Task.Delay(TimeSpan.FromSeconds(5));
    var left = Process.GetProcessesByName("yt-dlp").Length + Process.GetProcessesByName("ffmpeg").Length;
    Console.WriteLine(left == 0 ? "OK : aucun yt-dlp ni ffmpeg en cours." : $"PROBLÈME : {left} processus yt-dlp/ffmpeg encore en cours.");
    return left == 0 ? 0 : 3;
}
catch (MediaSourceException ex)
{
    Console.WriteLine();
    Console.WriteLine($"Échec ({ex.Error.Kind}) : {ex.Error.TechnicalDetails}");
    return 1;
}

static string Describe(long? bytes) => bytes is null ? "taille inconnue" : $"{bytes.Value / 1_000_000.0:0.#} Mo";

static string Probe(string ffprobe, string file)
{
    if (!File.Exists(ffprobe))
    {
        return "ffprobe absent.";
    }

    var psi = new ProcessStartInfo(ffprobe) { RedirectStandardOutput = true, UseShellExecute = false };
    foreach (var a in new[] { "-v", "error", "-show_entries", "stream=codec_type,codec_name,width,height", "-of", "csv=p=0", file })
    {
        psi.ArgumentList.Add(a);
    }

    using var process = Process.Start(psi)!;
    var output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    return "ffprobe (type de flux, codec, taille) :" + Environment.NewLine + output.Trim();
}

internal sealed class ToolPlatform(AppDirectories directories, string enginePath) : IPlatformServices
{
    public AppDirectories Directories => directories;

    public string EngineExecutablePath => enginePath;

    public Task<string?> ReadClipboardAsync() => Task.FromResult<string?>(null);
}

internal sealed class ConsoleProgress : IProgress<DownloadProgress>
{
    private readonly Lock _gate = new();
    private long _lastTicks;
    private DownloadPhase _lastPhase = DownloadPhase.Queued;

    public void Report(DownloadProgress p)
    {
        lock (_gate)
        {
            var now = Stopwatch.GetTimestamp();
            var changed = p.Phase != _lastPhase;
            if (!changed && Stopwatch.GetElapsedTime(_lastTicks, now) < TimeSpan.FromMilliseconds(250))
            {
                return;
            }

            _lastTicks = now;
            _lastPhase = p.Phase;
            var speed = p.BytesPerSecond is { } s ? $"{s / 1_000_000:0.0} Mo/s" : "— Mo/s";
            var remaining = p.Remaining is { } r ? r.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture) : "—";
            var total = p.BytesTotal is { } t ? $"{t / 1_000_000.0:0.0}" : "?";
            Console.Write($"\r{p.Phase,-17} global {p.OverallPercent,5:0.0} %  étape {p.Percent,5:0.0} %  {p.BytesDone / 1_000_000.0:0.0}/{total} Mo  {speed}  reste {remaining}      ");
            if (changed)
            {
                Console.WriteLine();
            }
        }
    }
}

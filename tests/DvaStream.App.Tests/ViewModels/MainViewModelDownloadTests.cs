using DvaStream.App.Localization;
using DvaStream.App.Platform;
using DvaStream.App.ViewModels;
using DvaStream.Core.Downloads;
using DvaStream.Core.Errors;
using DvaStream.Core.Media;
using DvaStream.Core.Platform;
using DvaStream.Core.Settings;
using DvaStream.Core.Urls;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DvaStream.App.Tests.ViewModels;

/// <summary>M4-10 et M4-11 : du bouton Télécharger à l'état final.</summary>
[Collection(CultureCollection.Name)]
public sealed class MainViewModelDownloadTests
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dva-test");
    private readonly IMediaSource _source = Substitute.For<IMediaSource>();
    private readonly ISettingsStore _settings = Substitute.For<ISettingsStore>();
    private readonly FakeDownloads _downloads = new();
    private readonly UrlInputViewModel _urlInput = new(new UrlParser(), Substitute.For<IPlatformServices>());

    private sealed class FakeDownloads : IDownloadService
    {
        private readonly List<DownloadJob> _jobs = [];

        public IReadOnlyList<DownloadJob> Jobs => _jobs;

        public event EventHandler<DownloadJob>? JobEnqueued
        {
            add { }
            remove { }
        }

        public TaskCompletionSource Gate { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Action<DownloadJob>? Outcome { get; set; }

        public DownloadJob Enqueue(DownloadRequest request, MediaInfo info)
        {
            var job = new DownloadJob(request, info);
            _jobs.Add(job);
            Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return job;
        }

        public async Task RunAsync(DownloadJob job, CancellationToken ct)
        {
            job.TryTransition(DownloadPhase.Preparing);
            await Gate.Task;
            Outcome?.Invoke(job);
        }

        public void Cancel(DownloadJob job)
        {
            job.Cancel();
            Gate.TrySetResult();
        }
    }

    private readonly IDesktopShell _shell = Substitute.For<IDesktopShell>();

    private MainViewModel Create(string folder, bool withService = true)
    {
        _settings.Current.Returns(AppSettings.CreateDefaults(folder));
        var page = new SettingsViewModel(_settings, Substitute.For<IFolderPicker>());
        return new MainViewModel(
            _urlInput, _source, Substitute.For<IThumbnailLoader>(), _settings, page,
            NullLogger<MainViewModel>.Instance, null, withService ? _downloads : null, Substitute.For<IFolderPicker>(), _shell);
    }

    private static MediaInfo Info()
    {
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        return new MediaInfo(id!, "Titre", "Chaîne", TimeSpan.FromMinutes(1), null,
        [
            new MediaStream("v720", StreamKind.Video, 720, "mp4", "avc1", null, null, 25),
            new MediaStream("a1", StreamKind.Audio, null, "m4a", null, "mp4a", null, null),
        ]);
    }

    private async Task<MainViewModel> ReadyAsync(string? folder = null, bool withService = true)
    {
        var vm = Create(folder ?? _folder, withService);
        _source.GetInfoAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>()).Returns(Info());
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        await vm.AnalyzeAsync(id!);
        Assert.Equal(MainState.Ready, vm.State);
        return vm;
    }

    [Fact]
    public async Task Telecharger_n_est_possible_qu_une_fois_la_video_analysee()
    {
        var vm = Create(_folder);
        Assert.False(vm.CanDownload);
        Assert.False(vm.ShowForm);

        var ready = await ReadyAsync();

        Assert.True(ready.CanDownload);
        Assert.True(ready.ShowForm);
        Assert.Equal(_folder, ready.Destination.Folder);
    }

    [Fact]
    public async Task Sans_service_de_telechargement_le_bouton_est_inactif()
    {
        var vm = await ReadyAsync(withService: false);

        Assert.False(vm.CanDownload);
    }

    [Fact]
    public async Task Telecharger_lance_un_job_avec_les_choix_et_affiche_la_progression()
    {
        var vm = await ReadyAsync();

        vm.DownloadCommand.Execute(null);

        Assert.Equal(MainState.Downloading, vm.State);
        var job = Assert.Single(_downloads.Jobs);
        Assert.Equal(DownloadMode.Video, job.Request.Mode);
        Assert.Equal(720, job.Request.Height);
        Assert.Equal(_folder, job.Request.Folder);
        Assert.NotNull(vm.Download);
        Assert.True(vm.ShowDownload);
        Assert.False(vm.ShowForm);
        Assert.False(vm.ShowInput);

        _downloads.Gate.SetResult();
        _downloads.Outcome = null;
    }

    [Fact]
    public async Task Un_telechargement_reussi_passe_en_Done()
    {
        var vm = await ReadyAsync();
        _downloads.Outcome = j =>
        {
            j.TryTransition(DownloadPhase.Finalizing);
            j.Complete(Path.Combine(_folder, "Titre.mp4"));
        };

        vm.DownloadCommand.Execute(null);
        _downloads.Gate.SetResult();
        await vm.DownloadCompletion;

        Assert.Equal(MainState.Done, vm.State);
        Assert.True(vm.Download!.IsDone);
        Assert.Equal("Titre.mp4", vm.Download.FileName);
        Assert.False(vm.ShowInput);
    }

    [Fact]
    public async Task La_fin_du_telechargement_notifie_et_permet_d_ouvrir_et_d_afficher_le_fichier()
    {
        var vm = await ReadyAsync();
        var path = Path.Combine(_folder, "Titre.mp4");
        _downloads.Outcome = j =>
        {
            j.TryTransition(DownloadPhase.Finalizing);
            j.Complete(path);
        };
        vm.DownloadCommand.Execute(null);
        _downloads.Gate.SetResult();
        await vm.DownloadCompletion;

        _shell.Received(1).Notify(Strings.Get("Notify_DoneTitle"), "Titre.mp4");

        vm.Download!.OpenFileCommand.Execute(null);
        vm.Download.RevealCommand.Execute(null);

        _shell.Received(1).OpenFile(path);
        _shell.Received(1).RevealInFolder(path);
    }

    [Fact]
    public async Task Un_echec_ne_notifie_pas()
    {
        var vm = await ReadyAsync();
        _downloads.Outcome = j => j.Fail(new AppError(ErrorKind.Network, "x"));
        vm.DownloadCommand.Execute(null);
        _downloads.Gate.SetResult();
        await vm.DownloadCompletion;

        _shell.DidNotReceive().Notify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Une_notification_en_panne_ne_bloque_pas_la_fin()
    {
        var vm = await ReadyAsync();
        _shell.When(s => s.Notify(Arg.Any<string>(), Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));
        _downloads.Outcome = j =>
        {
            j.TryTransition(DownloadPhase.Finalizing);
            j.Complete(Path.Combine(_folder, "Titre.mp4"));
        };
        vm.DownloadCommand.Execute(null);
        _downloads.Gate.SetResult();
        await vm.DownloadCompletion;

        Assert.Equal(MainState.Done, vm.State);
    }

    [Fact]
    public async Task Annuler_revient_au_choix_du_format()
    {
        var vm = await ReadyAsync();
        vm.DownloadCommand.Execute(null);

        vm.Download!.CancelCommand.Execute(null);
        await vm.DownloadCompletion;

        Assert.Equal(MainState.Ready, vm.State);
        Assert.Null(vm.Download);
        Assert.True(vm.ShowForm);
        Assert.True(vm.ShowInput);
    }

    [Fact]
    public async Task Un_echec_passe_en_DownloadFailed_et_Reessayer_relance_la_meme_demande()
    {
        var vm = await ReadyAsync();
        _downloads.Outcome = j => j.Fail(new AppError(ErrorKind.DiskFull, "plein"));
        vm.DownloadCommand.Execute(null);
        _downloads.Gate.SetResult();
        await vm.DownloadCompletion;

        Assert.Equal(MainState.DownloadFailed, vm.State);
        Assert.True(vm.Download!.IsFailed);
        var first = _downloads.Jobs[0];

        vm.Download.RetryCommand.Execute(null);

        Assert.Equal(2, _downloads.Jobs.Count);
        Assert.Equal(first.Request, _downloads.Jobs[1].Request);
        Assert.Equal(MainState.Downloading, vm.State);
        _downloads.Gate.SetResult();
        await vm.DownloadCompletion;
    }

    [Fact]
    public async Task Une_exception_du_service_devient_un_echec()
    {
        var vm = await ReadyAsync();
        _downloads.Outcome = _ => throw new InvalidOperationException("boom");

        vm.DownloadCommand.Execute(null);
        _downloads.Gate.SetResult();
        await vm.DownloadCompletion;

        Assert.Equal(MainState.DownloadFailed, vm.State);
        Assert.Equal(PreviewErrorMessages.MessageFor(ErrorKind.Unknown), vm.Download!.ErrorMessage);
    }

    [Fact]
    public async Task Autre_video_revient_a_l_accueil_avec_un_champ_vide()
    {
        var vm = await ReadyAsync();
        _downloads.Outcome = j =>
        {
            j.TryTransition(DownloadPhase.Finalizing);
            j.Complete(Path.Combine(_folder, "Titre.mp4"));
        };
        vm.DownloadCommand.Execute(null);
        _downloads.Gate.SetResult();
        await vm.DownloadCompletion;

        vm.Download!.AnotherCommand.Execute(null);

        Assert.Equal(MainState.Idle, vm.State);
        Assert.Null(vm.Download);
        Assert.False(vm.Preview.IsShown);
        Assert.Equal(string.Empty, vm.UrlInput.Text);
        Assert.True(vm.ShowInput);
        Assert.False(vm.CanDownload);
    }

    [Fact]
    public async Task Un_dossier_invalide_affiche_un_message_sans_lancer_de_job()
    {
        var vm = await ReadyAsync(folder: "dossier-relatif");

        vm.DownloadCommand.Execute(null);

        Assert.Empty(_downloads.Jobs);
        Assert.Equal(MainState.Ready, vm.State);
        Assert.True(vm.HasDownloadMessage);
        Assert.Equal(Strings.Get("Dl_FolderInvalid"), vm.DownloadMessage);
    }

    [Fact]
    public async Task Une_analyse_est_ignoree_pendant_un_telechargement()
    {
        var vm = await ReadyAsync();
        vm.DownloadCommand.Execute(null);
        Assert.True(VideoId.TryCreate("aaaaaaaaaaa", out var other));

        await vm.AnalyzeAsync(other!);

        Assert.Equal(MainState.Downloading, vm.State);
        await _source.Received(1).GetInfoAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>());
        _downloads.Gate.SetResult();
        await vm.DownloadCompletion;
    }
}

using System.Globalization;
using DvaStream.App.Localization;
using DvaStream.App.ViewModels;
using DvaStream.Core.Downloads;
using DvaStream.Core.Errors;
using DvaStream.Core.Media;
using DvaStream.Core.Settings;
using DvaStream.Core.Urls;

namespace DvaStream.App.Tests.ViewModels;

/// <summary>M4-11 : présentation de l'avancement d'un téléchargement.</summary>
[Collection(CultureCollection.Name)]
public sealed class DownloadViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dva-test");
    private readonly DownloadJob _job;
    private int _cancelled;
    private int _retried;
    private int _another;

    public DownloadViewModelTests()
    {
        Strings.Culture = new CultureInfo("fr");
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        _job = new DownloadJob(new DownloadRequest(id!, "Titre", DownloadMode.Video, 1080, AudioFormat.M4a, _folder));
    }

    public void Dispose() => Strings.Culture = null;

    private DownloadViewModel Create() =>
        new(_job, () => _cancelled++, () => _retried++, () => _another++);

    [Fact]
    public void Au_depart_la_barre_est_indeterminee_et_l_annulation_possible()
    {
        var vm = Create();

        Assert.Equal("Titre", vm.Title);
        Assert.True(vm.IsIndeterminate);
        Assert.True(vm.IsRunning);
        Assert.True(vm.CanCancel);
        Assert.False(vm.HasStats);
        Assert.Equal(Strings.Get("Dl_Phase_Queued"), vm.PhaseText);
    }

    [Fact]
    public void L_avancement_affiche_pourcentage_taille_vitesse_et_temps_restant()
    {
        var vm = Create();
        _job.TryTransition(DownloadPhase.Preparing);

        _job.Report(new DownloadProgress(
            DownloadPhase.DownloadingVideo, 40, 38.2, 50_000_000, 200_000_000, 4_100_000, TimeSpan.FromSeconds(48)));

        Assert.Equal(38.2, vm.Percent);
        Assert.Equal("38 %", vm.PercentText);
        Assert.False(vm.IsIndeterminate);
        Assert.Equal("50 Mo sur 200 Mo · 4,1 Mo/s · Reste 0:48", vm.StatsText);
        Assert.Equal(Strings.Get("Dl_Phase_DownloadingVideo"), vm.PhaseText);
    }

    [Fact]
    public void Sans_taille_totale_seule_la_taille_recue_est_affichee()
    {
        var vm = Create();
        _job.TryTransition(DownloadPhase.Preparing);

        _job.Report(new DownloadProgress(DownloadPhase.DownloadingAudio, 0, 5, 3_000_000, null, null, null));

        Assert.Equal("3 Mo", vm.StatsText);
    }

    [Fact]
    public async Task Les_changements_du_job_notifient_l_interface()
    {
        var vm = Create();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _job.TryTransition(DownloadPhase.Preparing);

        await DestinationViewModelTests.EventuallyAsync(() => raised.Count > 0);
    }

    [Fact]
    public void La_finalisation_ne_peut_plus_etre_annulee()
    {
        var vm = Create();
        _job.TryTransition(DownloadPhase.Preparing);
        _job.TryTransition(DownloadPhase.Finalizing);

        Assert.False(vm.CanCancel);
        Assert.False(vm.CancelCommand.CanExecute(null));
    }

    [Fact]
    public void Annuler_appelle_l_action_fournie()
    {
        var vm = Create();

        vm.CancelCommand.Execute(null);

        Assert.Equal(1, _cancelled);
    }

    [Fact]
    public void Un_telechargement_termine_montre_le_fichier_et_le_dossier()
    {
        var vm = Create();
        _job.TryTransition(DownloadPhase.Preparing);
        _job.TryTransition(DownloadPhase.Finalizing);
        var path = Path.Combine(_folder, "Titre.mp4");

        _job.Complete(path);

        Assert.True(vm.IsDone);
        Assert.False(vm.IsRunning);
        Assert.Equal(100, vm.Percent);
        Assert.Equal("Titre.mp4", vm.FileName);
        Assert.Equal(_folder, vm.FolderPath);
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, Strings.Get("Dl_SavedTo"), _folder), vm.SavedToText);
        Assert.False(vm.HasStats);
    }

    [Fact]
    public void Un_echec_montre_le_message_de_son_type()
    {
        var vm = Create();
        _job.TryTransition(DownloadPhase.Preparing);

        _job.Fail(new AppError(ErrorKind.DiskFull, "plein"));

        Assert.True(vm.IsFailed);
        Assert.Equal(PreviewErrorMessages.MessageFor(ErrorKind.DiskFull), vm.ErrorMessage);
        Assert.Equal(PreviewErrorMessages.ActionFor(ErrorKind.DiskFull), vm.ErrorAction);
    }

    [Fact]
    public void Reessayer_et_autre_video_appellent_leurs_actions()
    {
        var vm = Create();

        vm.RetryCommand.Execute(null);
        vm.AnotherCommand.Execute(null);

        Assert.Equal(1, _retried);
        Assert.Equal(1, _another);
    }

    [Fact]
    public void Ouvrir_et_afficher_ne_sont_possibles_qu_une_fois_le_fichier_pret()
    {
        var opened = 0;
        var revealed = 0;
        var vm = new DownloadViewModel(_job, () => { }, () => { }, () => { }, () => opened++, () => revealed++);
        Assert.False(vm.CanOpenFile);
        Assert.False(vm.OpenFileCommand.CanExecute(null));

        _job.TryTransition(DownloadPhase.Preparing);
        _job.TryTransition(DownloadPhase.Finalizing);
        _job.Complete(Path.Combine(_folder, "Titre.mp4"));

        Assert.True(vm.CanOpenFile);
        Assert.True(vm.CanReveal);
        vm.OpenFileCommand.Execute(null);
        vm.RevealCommand.Execute(null);
        Assert.Equal(1, opened);
        Assert.Equal(1, revealed);
    }

    [Fact]
    public void Sans_actions_fournies_ouvrir_et_afficher_restent_masques()
    {
        var vm = Create();
        _job.TryTransition(DownloadPhase.Preparing);
        _job.TryTransition(DownloadPhase.Finalizing);
        _job.Complete(Path.Combine(_folder, "Titre.mp4"));

        Assert.False(vm.CanOpenFile);
        Assert.False(vm.CanReveal);
    }

    [Theory]
    [InlineData(4_100_000, "4,1 Mo/s")]
    [InlineData(640_000, "640 Ko/s")]
    [InlineData(100, "1 Ko/s")]
    public void La_vitesse_est_lisible(double bytesPerSecond, string expected)
    {
        Assert.Equal(expected, SizeFormatter.FormatSpeed(bytesPerSecond));
    }
}

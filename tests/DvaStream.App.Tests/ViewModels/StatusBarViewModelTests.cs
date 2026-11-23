using DvaStream.App.ViewModels;
using DvaStream.Core.Errors;

namespace DvaStream.App.Tests.ViewModels;

/// <summary>M3-08 : barre d'état (moteur, version) et bouton Réessayer.</summary>
[Collection(CultureCollection.Name)]
public class StatusBarViewModelTests
{
    [Fact]
    public void Le_moteur_present_ou_absent_est_reflete_apres_Refresh()
    {
        var present = true;
        var vm = new StatusBarViewModel("1.2.3", () => present);
        Assert.True(vm.EngineReady);
        Assert.False(vm.EngineMissing);
        Assert.Contains("1.2.3", vm.VersionText);

        present = false;
        vm.Refresh();
        Assert.False(vm.EngineReady);
        Assert.True(vm.EngineMissing);
    }

    [Fact]
    public void FFmpeg_absent_est_signale_meme_si_yt_dlp_est_la()
    {
        var ffmpeg = false;
        var vm = new StatusBarViewModel("1", () => true, () => ffmpeg);

        Assert.False(vm.EngineReady);
        Assert.Equal(DvaStream.App.Localization.Strings.Get("Status_FfmpegMissing"), vm.EngineText);

        ffmpeg = true;
        vm.Refresh();
        Assert.True(vm.EngineReady);
    }

    [Fact]
    public void Une_erreur_dacces_vaut_moteur_absent()
    {
        var vm = new StatusBarViewModel("1", () => throw new UnauthorizedAccessException());
        Assert.False(vm.EngineReady);
    }

    [Fact]
    public void La_version_courante_ne_contient_pas_dempreinte()
    {
        Assert.DoesNotContain('+', StatusBarViewModel.CurrentVersion());
    }

    [Theory]
    [InlineData(ErrorKind.Network, true)]
    [InlineData(ErrorKind.Timeout, true)]
    [InlineData(ErrorKind.VideoPrivate, false)]
    [InlineData(ErrorKind.VideoUnavailable, false)]
    public void Reessayer_nest_propose_que_pour_les_erreurs_passageres(ErrorKind kind, bool expected)
    {
        var calls = 0;
        var vm = new PreviewViewModel(() => calls++);

        vm.ShowError(kind);

        Assert.Equal(expected, vm.CanRetry);
        vm.RetryCommand.Execute(null);
        Assert.Equal(1, calls);

        vm.Clear();
        Assert.False(vm.CanRetry);
    }
}

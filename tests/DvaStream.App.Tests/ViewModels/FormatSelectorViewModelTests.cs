using DvaStream.App.ViewModels;
using DvaStream.Core.Media;
using DvaStream.Core.Settings;
using DvaStream.Core.Urls;

namespace DvaStream.App.Tests.ViewModels;

/// <summary>M3-03 et M3-04 : mode, résolutions disponibles, repli, format audio.</summary>
[Collection(CultureCollection.Name)]
public class FormatSelectorViewModelTests
{
    private static MediaStream Video(int height) =>
        new($"v{height}", StreamKind.Video, height, "mp4", "avc1", null, null, 25);

    private static MediaStream Audio() => new("a1", StreamKind.Audio, null, "m4a", null, "mp4a", null, null);

    private static MediaInfo Info(params MediaStream[] streams)
    {
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        return new MediaInfo(id!, "t", "c", TimeSpan.FromMinutes(1), null, streams);
    }

    [Fact]
    public void Au_depart_le_selecteur_est_masque()
    {
        var vm = new FormatSelectorViewModel();

        Assert.False(vm.IsConfigured);
        Assert.False(vm.ShowQuality);
        Assert.False(vm.ShowAudioOptions);
        Assert.Empty(vm.Resolutions);
    }

    [Fact]
    public void Seules_les_resolutions_disponibles_sont_proposees_de_la_plus_grande_a_la_plus_petite()
    {
        var vm = new FormatSelectorViewModel();

        vm.Configure(Info(Video(360), Video(1080), Video(720), Audio()), 1080, AudioFormat.M4a);

        Assert.Equal(new[] { "1080p", "720p", "360p" }, vm.Resolutions.Select(r => r.Label).ToArray());
        Assert.Equal(1080, vm.SelectedResolution!.Height);
        Assert.Equal(1080, vm.SelectedHeight);
        Assert.True(vm.IsVideoMode);
        Assert.True(vm.ShowQuality);
        Assert.False(vm.ShowAudioOptions);
        Assert.False(vm.HasFallbackNote);
    }

    [Fact]
    public void La_resolution_preferee_absente_se_replie_sur_la_plus_proche_inferieure_avec_une_mention()
    {
        var vm = new FormatSelectorViewModel();

        vm.Configure(Info(Video(720), Video(480), Audio()), 1080, AudioFormat.M4a);

        Assert.Equal(720, vm.SelectedResolution!.Height);
        Assert.True(vm.HasFallbackNote);
        Assert.Contains("1080", vm.FallbackNote, StringComparison.Ordinal);
        Assert.Contains("720", vm.FallbackNote, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_choix_manuel_efface_la_mention_de_repli()
    {
        var vm = new FormatSelectorViewModel();
        vm.Configure(Info(Video(720), Video(480), Audio()), 1080, AudioFormat.M4a);

        vm.SelectedResolution = vm.Resolutions.Single(r => r.Height == 480);

        Assert.Equal(480, vm.SelectedHeight);
        Assert.False(vm.HasFallbackNote);
    }

    [Fact]
    public void Seul_un_choix_de_l_utilisateur_declenche_ResolutionChosen()
    {
        var vm = new FormatSelectorViewModel();
        var chosen = new List<int>();
        vm.ResolutionChosen += (_, h) => chosen.Add(h);

        vm.Configure(Info(Video(720), Video(480), Audio()), 1080, AudioFormat.M4a);
        Assert.Empty(chosen);

        vm.SelectedResolution = vm.Resolutions.Single(r => r.Height == 480);
        vm.SelectedResolution = vm.Resolutions.Single(r => r.Height == 480);

        Assert.Equal(new[] { 480 }, chosen.ToArray());
    }

    [Fact]
    public void Une_resolution_hors_liste_est_refusee()
    {
        var vm = new FormatSelectorViewModel();
        vm.Configure(Info(Video(720), Audio()), 720, AudioFormat.M4a);

        vm.SelectedResolution = new ResolutionOption(4320, "4320p");

        Assert.Equal(720, vm.SelectedResolution!.Height);
    }

    [Fact]
    public void Le_mode_audio_masque_la_qualite_et_montre_le_format()
    {
        var vm = new FormatSelectorViewModel();
        vm.Configure(Info(Video(720), Audio()), 720, AudioFormat.M4a);

        vm.SelectAudioCommand.Execute(null);

        Assert.True(vm.IsAudioMode);
        Assert.False(vm.ShowQuality);
        Assert.True(vm.ShowAudioOptions);
        Assert.Null(vm.SelectedHeight);

        vm.SelectVideoCommand.Execute(null);
        Assert.True(vm.ShowQuality);
        Assert.Equal(720, vm.SelectedHeight);
    }

    [Fact]
    public void Sans_flux_video_seul_l_audio_est_possible()
    {
        var vm = new FormatSelectorViewModel();

        vm.Configure(Info(Audio()), 1080, AudioFormat.M4a);

        Assert.False(vm.CanChooseVideo);
        Assert.True(vm.CanChooseAudio);
        Assert.True(vm.IsAudioMode);
        Assert.False(vm.SelectVideoCommand.CanExecute(null));
        Assert.Empty(vm.Resolutions);
        Assert.Null(vm.SelectedResolution);
    }

    [Fact]
    public void Sans_audio_le_mode_audio_est_refuse()
    {
        var vm = new FormatSelectorViewModel();

        vm.Configure(Info(Video(720)), 720, AudioFormat.M4a);

        Assert.False(vm.CanChooseAudio);
        Assert.False(vm.SelectAudioCommand.CanExecute(null));
        vm.SelectAudioCommand.Execute(null);
        Assert.True(vm.IsVideoMode);
    }

    [Fact]
    public void Le_MP3_n_est_pas_encore_disponible_et_la_preference_MP3_retombe_sur_M4A()
    {
        var vm = new FormatSelectorViewModel();

        vm.Configure(Info(Video(720), Audio()), 720, AudioFormat.Mp3);

        Assert.False(vm.IsMp3Available);
        Assert.Equal(AudioFormat.M4a, vm.AudioFormat);
        Assert.True(vm.IsM4aSelected);
    }

    [Fact]
    public void Reconfigurer_remplace_la_liste_et_Clear_masque_tout()
    {
        var vm = new FormatSelectorViewModel();
        vm.Configure(Info(Video(1080), Audio()), 1080, AudioFormat.M4a);
        vm.Configure(Info(Video(360), Audio()), 1080, AudioFormat.M4a);

        Assert.Equal(new[] { 360 }, vm.Resolutions.Select(r => r.Height).ToArray());
        Assert.Equal(360, vm.SelectedHeight);

        vm.Clear();

        Assert.False(vm.IsConfigured);
        Assert.Empty(vm.Resolutions);
        Assert.Null(vm.SelectedResolution);
        Assert.False(vm.SelectVideoCommand.CanExecute(null));
    }

    [Fact]
    public void Le_changement_de_mode_est_notifie()
    {
        var vm = new FormatSelectorViewModel();
        vm.Configure(Info(Video(720), Audio()), 720, AudioFormat.M4a);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.SelectAudioCommand.Execute(null);

        Assert.Contains(nameof(FormatSelectorViewModel.IsAudioMode), changed);
        Assert.Contains(nameof(FormatSelectorViewModel.ShowAudioOptions), changed);
    }

    [Fact]
    public void Une_information_nulle_est_refusee()
    {
        Assert.Throws<ArgumentNullException>(() => new FormatSelectorViewModel().Configure(null!, 1080, AudioFormat.M4a));
    }
}

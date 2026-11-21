using System.Globalization;
using DvaStream.App.Localization;
using DvaStream.App.ViewModels;
using DvaStream.Core.Errors;
using DvaStream.Core.Media;
using DvaStream.Core.Urls;

namespace DvaStream.App.Tests.ViewModels;

/// <summary>M2-11 et M2-12 : états de la carte, durée, messages d'erreur FR/EN.</summary>
[Collection(CultureCollection.Name)]
public class PreviewViewModelTests
{
    private static MediaInfo Info(TimeSpan duration, string title = "Titre", string channel = "Chaîne")
    {
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        return new MediaInfo(id!, title, channel, duration, null, []);
    }

    public static TheoryData<ErrorKind> AllKinds()
    {
        var data = new TheoryData<ErrorKind>();
        foreach (var kind in Enum.GetValues<ErrorKind>())
        {
            data.Add(kind);
        }

        return data;
    }

    [Fact]
    public void La_carte_est_masquee_au_depart()
    {
        var vm = new PreviewViewModel();

        Assert.Equal(PreviewState.Empty, vm.State);
        Assert.False(vm.IsShown);
        Assert.False(vm.IsLoading);
        Assert.False(vm.IsReady);
        Assert.False(vm.IsError);
    }

    [Fact]
    public void Chaque_etat_n_active_que_ses_propres_indicateurs()
    {
        var vm = new PreviewViewModel();

        vm.ShowLoading();
        Assert.True(vm.IsShown);
        Assert.True(vm.IsLoading);
        Assert.False(vm.IsReady);
        Assert.False(vm.IsError);

        vm.ShowInfo(Info(TimeSpan.FromSeconds(213)));
        Assert.True(vm.IsReady);
        Assert.False(vm.IsLoading);

        vm.ShowError(ErrorKind.Network);
        Assert.True(vm.IsError);
        Assert.False(vm.IsReady);

        vm.Clear();
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void Les_informations_de_la_video_sont_exposees()
    {
        var vm = new PreviewViewModel();

        vm.ShowInfo(Info(TimeSpan.FromSeconds(213), "Mon titre", "Ma chaîne"));

        Assert.Equal("Mon titre", vm.Title);
        Assert.Equal("Ma chaîne", vm.Channel);
        Assert.Equal("3:33", vm.DurationText);
        Assert.True(vm.HasDuration);
    }

    [Fact]
    public void Une_duree_inconnue_masque_la_pastille()
    {
        var vm = new PreviewViewModel();

        vm.ShowInfo(Info(TimeSpan.Zero));

        Assert.Null(vm.DurationText);
        Assert.False(vm.HasDuration);
    }

    [Fact]
    public void La_miniature_n_est_prise_en_compte_que_si_la_carte_montre_une_video()
    {
        var vm = new PreviewViewModel();

        vm.SetThumbnail([1]);
        Assert.Null(vm.ThumbnailBytes);

        vm.ShowInfo(Info(TimeSpan.FromSeconds(5)));
        vm.SetThumbnail([1, 2]);
        Assert.Equal(new byte[] { 1, 2 }, vm.ThumbnailBytes);

        vm.ShowLoading();
        Assert.Null(vm.ThumbnailBytes);
    }

    [Fact]
    public void Un_nouvel_etat_efface_le_contenu_precedent()
    {
        var vm = new PreviewViewModel();
        vm.ShowInfo(Info(TimeSpan.FromSeconds(5)));
        vm.SetThumbnail([1]);

        vm.ShowError(ErrorKind.VideoPrivate);

        Assert.Equal(string.Empty, vm.Title);
        Assert.Null(vm.ThumbnailBytes);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorAction));
    }

    [Fact]
    public void Les_changements_sont_notifies()
    {
        var vm = new PreviewViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ShowInfo(Info(TimeSpan.FromSeconds(5)));

        Assert.Contains(nameof(PreviewViewModel.State), changed);
        Assert.Contains(nameof(PreviewViewModel.IsReady), changed);
        Assert.Contains(nameof(PreviewViewModel.IsShown), changed);
        Assert.Contains(nameof(PreviewViewModel.Title), changed);
    }

    [Fact]
    public void Une_information_nulle_est_refusee()
    {
        Assert.Throws<ArgumentNullException>(() => new PreviewViewModel().ShowInfo(null!));
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void Chaque_ErrorKind_a_un_message_et_une_action_en_francais_et_en_anglais(ErrorKind kind)
    {
        foreach (var culture in new[] { new CultureInfo("fr"), new CultureInfo("en") })
        {
            foreach (var key in new[] { PreviewErrorMessages.MessageKeyFor(kind), PreviewErrorMessages.ActionKeyFor(kind) })
            {
                var text = Strings.Get(key, culture);
                Assert.False(string.IsNullOrWhiteSpace(text), $"{key} vide en {culture.Name}.");
                Assert.False(text.StartsWith('['), $"{key} absente en {culture.Name}.");
            }
        }
    }

    [Fact]
    public void Les_cas_distincts_ont_des_messages_distincts()
    {
        var kinds = new[]
        {
            ErrorKind.VideoPrivate, ErrorKind.VideoUnavailable, ErrorKind.VideoRestricted, ErrorKind.Network,
            ErrorKind.Timeout, ErrorKind.RateLimited, ErrorKind.EngineMissing, ErrorKind.EngineBroken,
            ErrorKind.UnsupportedPlaylist, ErrorKind.InvalidUrl, ErrorKind.Unknown,
        };

        var messages = kinds.Select(PreviewErrorMessages.MessageFor).ToList();

        Assert.Equal(messages.Count, messages.Distinct().Count());
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-5, null)]
    [InlineData(5, "0:05")]
    [InlineData(59, "0:59")]
    [InlineData(213, "3:33")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(5083, "1:24:43")]
    [InlineData(36000, "10:00:00")]
    public void La_duree_est_formatee_en_m_ss_ou_h_mm_ss(int seconds, string? expected)
    {
        Assert.Equal(expected, DurationFormatter.Format(TimeSpan.FromSeconds(seconds)));
    }
}

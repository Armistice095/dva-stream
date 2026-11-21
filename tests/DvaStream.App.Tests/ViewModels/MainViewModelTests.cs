using DvaStream.App.Localization;
using DvaStream.App.ViewModels;
using DvaStream.Core.Errors;
using DvaStream.Core.Media;
using DvaStream.Core.Platform;
using DvaStream.Core.Urls;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DvaStream.App.Tests.ViewModels;

/// <summary>M1-10 et M2-11/12 : <c>Idle → Analyzing → Ready | Error | Idle</c>, annulation, miniature, robustesse face au moteur.</summary>
[Collection(CultureCollection.Name)]
public sealed class MainViewModelTests
{
    private const string ValidUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

    private readonly IMediaSource _source = Substitute.For<IMediaSource>();
    private readonly IThumbnailLoader _thumbnails = Substitute.For<IThumbnailLoader>();
    private readonly UrlInputViewModel _urlInput = new(new UrlParser(), Substitute.For<IPlatformServices>());
    private readonly MainViewModel _vm;

    public MainViewModelTests()
    {
        _source.Name.Returns("Faux moteur");
        _vm = new MainViewModel(_urlInput, _source, _thumbnails, NullLogger<MainViewModel>.Instance);
    }

    private static VideoId SomeId()
    {
        if (!VideoId.TryCreate("dQw4w9WgXcQ", out var id))
        {
            Assert.Fail("Identifiant de test invalide.");
        }

        return id;
    }

    private static MediaInfo SomeInfo() =>
        new(SomeId(), "Titre", "Chaîne", TimeSpan.FromMinutes(1), null, []);

    /// <summary>Moteur dont la réponse est contrôlée par le test ; l'annulation du jeton l'interrompt.</summary>
    private TaskCompletionSource<MediaInfo> PendingSource()
    {
        var tcs = new TaskCompletionSource<MediaInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        _source.GetInfoAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var ct = call.Arg<CancellationToken>();
            ct.Register(() => tcs.TrySetCanceled(ct));
            return tcs.Task;
        });
        return tcs;
    }

    [Fact]
    public void Au_demarrage_l_etat_est_Idle()
    {
        Assert.Equal(MainState.Idle, _vm.State);
        Assert.False(_vm.IsAnalyzing);
        Assert.False(_urlInput.IsBusy);
        Assert.Same(_urlInput, _vm.UrlInput);
        Assert.Equal("DVA Stream", _vm.Title);
        Assert.Equal(PreviewState.Empty, _vm.Preview.State);
        Assert.False(_vm.Preview.IsShown);
    }

    [Fact]
    public async Task L_analyse_passe_en_Analyzing_puis_en_Ready_avec_la_previsualisation()
    {
        var pending = PendingSource();

        var analysis = _vm.AnalyzeAsync(SomeId());

        Assert.Equal(MainState.Analyzing, _vm.State);
        Assert.True(_vm.IsAnalyzing);
        Assert.True(_urlInput.IsBusy);
        Assert.Equal(PreviewState.Loading, _vm.Preview.State);

        pending.SetResult(SomeInfo());
        await analysis;

        Assert.Equal(MainState.Ready, _vm.State);
        Assert.False(_vm.IsAnalyzing);
        Assert.False(_urlInput.IsBusy);
        Assert.Equal(PreviewState.Ready, _vm.Preview.State);
        Assert.Equal("Titre", _vm.Preview.Title);
        Assert.Equal("Chaîne", _vm.Preview.Channel);
        Assert.Equal("1:00", _vm.Preview.DurationText);
    }

    [Fact]
    public async Task L_analyse_demandee_depuis_le_champ_utilise_le_moteur_avec_l_identifiant()
    {
        var pending = PendingSource();
        _urlInput.Text = ValidUrl;

        _urlInput.AnalyzeCommand.Execute(null);

        Assert.Equal(MainState.Analyzing, _vm.State);
        await _source.Received(1).GetInfoAsync(
            Arg.Is<VideoId>(id => id.Value == "dQw4w9WgXcQ"), Arg.Any<CancellationToken>());

        pending.SetResult(SomeInfo());
        await _vm.AnalysisCompletion;

        Assert.Equal(MainState.Ready, _vm.State);
    }

    [Fact]
    public async Task Une_seconde_analyse_pendant_la_premiere_est_ignoree()
    {
        var pending = PendingSource();

        var first = _vm.AnalyzeAsync(SomeId());
        await _vm.AnalyzeAsync(SomeId());   // ignorée : retourne aussitôt

        await _source.Received(1).GetInfoAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>());

        pending.SetResult(SomeInfo());
        await first;
    }

    [Fact]
    public async Task Annuler_interrompt_l_analyse_et_revient_a_Idle()
    {
        PendingSource();
        _urlInput.Text = ValidUrl;
        _urlInput.AnalyzeCommand.Execute(null);
        Assert.Equal(MainState.Analyzing, _vm.State);

        _urlInput.CancelCommand.Execute(null);
        await _vm.AnalysisCompletion;

        Assert.Equal(MainState.Idle, _vm.State);
        Assert.False(_urlInput.IsBusy);
        Assert.Equal(PreviewState.Empty, _vm.Preview.State);
    }

    [Fact]
    public async Task Une_erreur_inattendue_du_moteur_ne_plante_pas_et_affiche_une_erreur_generique()
    {
        _source.GetInfoAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MediaInfo>(new InvalidOperationException("panne simulée")));

        await _vm.AnalyzeAsync(SomeId());

        Assert.Equal(MainState.Error, _vm.State);
        Assert.False(_urlInput.IsBusy);
        Assert.Equal(PreviewState.Error, _vm.Preview.State);
        Assert.Equal(PreviewErrorMessages.MessageFor(ErrorKind.Unknown), _vm.Preview.ErrorMessage);
    }

    [Theory]
    [InlineData(ErrorKind.VideoPrivate)]
    [InlineData(ErrorKind.VideoUnavailable)]
    [InlineData(ErrorKind.VideoRestricted)]
    [InlineData(ErrorKind.Network)]
    public async Task Une_erreur_de_moteur_classee_affiche_son_message_et_son_action(ErrorKind kind)
    {
        _source.GetInfoAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MediaInfo>(new MediaSourceException(new AppError(kind, "détail"))));

        await _vm.AnalyzeAsync(SomeId());

        Assert.Equal(MainState.Error, _vm.State);
        Assert.Equal(PreviewErrorMessages.MessageFor(kind), _vm.Preview.ErrorMessage);
        Assert.Equal(PreviewErrorMessages.ActionFor(kind), _vm.Preview.ErrorAction);
        Assert.NotEqual(PreviewErrorMessages.MessageFor(ErrorKind.Unknown), _vm.Preview.ErrorMessage);
    }

    [Fact]
    public async Task Apres_une_erreur_on_peut_relancer_une_analyse_qui_reussit()
    {
        _source.GetInfoAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>()).Returns(
            Task.FromException<MediaInfo>(new MediaSourceException(new AppError(ErrorKind.Network, "x"))),
            Task.FromResult(SomeInfo()));

        await _vm.AnalyzeAsync(SomeId());
        Assert.Equal(MainState.Error, _vm.State);

        await _vm.AnalyzeAsync(SomeId());
        Assert.Equal(MainState.Ready, _vm.State);
        Assert.Equal(PreviewState.Ready, _vm.Preview.State);
        Assert.Equal(string.Empty, _vm.Preview.ErrorMessage);
    }

    [Fact]
    public async Task La_miniature_est_chargee_apres_l_affichage_de_la_carte()
    {
        var info = SomeInfo() with { ThumbnailUrl = new Uri("https://i.ytimg.com/vi/x/hq.jpg") };
        _source.GetInfoAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(info));
        _thumbnails.LoadAsync(info.ThumbnailUrl!, Arg.Any<CancellationToken>()).Returns(Task.FromResult<byte[]?>([1, 2, 3]));
        _urlInput.Text = ValidUrl;

        _urlInput.AnalyzeCommand.Execute(null);
        await _vm.AnalysisCompletion;
        await _vm.ThumbnailCompletion;

        Assert.Equal(MainState.Ready, _vm.State);
        Assert.Equal(new byte[] { 1, 2, 3 }, _vm.Preview.ThumbnailBytes);
    }

    [Fact]
    public async Task Un_echec_de_miniature_ne_bloque_pas_la_carte()
    {
        var info = SomeInfo() with { ThumbnailUrl = new Uri("https://i.ytimg.com/vi/x/hq.jpg") };
        _source.GetInfoAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(info));
        _thumbnails.LoadAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<byte[]?>(new InvalidOperationException("boum")));
        _urlInput.Text = ValidUrl;

        _urlInput.AnalyzeCommand.Execute(null);
        await _vm.AnalysisCompletion;
        await _vm.ThumbnailCompletion;

        Assert.Equal(PreviewState.Ready, _vm.Preview.State);
        Assert.Null(_vm.Preview.ThumbnailBytes);
    }

    [Fact]
    public async Task Sans_url_de_miniature_aucun_chargement_n_est_demande()
    {
        _source.GetInfoAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(SomeInfo()));

        await _vm.AnalyzeAsync(SomeId());

        await _thumbnails.DidNotReceiveWithAnyArgs().LoadAsync(default!, default);
    }

    [Fact]
    public async Task On_peut_relancer_une_analyse_apres_la_fin_de_la_precedente()
    {
        _source.GetInfoAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(SomeInfo()));

        await _vm.AnalyzeAsync(SomeId());
        await _vm.AnalyzeAsync(SomeId());

        await _source.Received(2).GetInfoAsync(Arg.Any<VideoId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task L_etat_notifie_ses_changements_a_la_vue()
    {
        var pending = PendingSource();
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        var analysis = _vm.AnalyzeAsync(SomeId());
        pending.SetResult(SomeInfo());
        await analysis;

        Assert.Contains(nameof(MainViewModel.State), changed);
        Assert.Contains(nameof(MainViewModel.IsAnalyzing), changed);
    }

    [Fact]
    public async Task Un_identifiant_nul_est_refuse()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _vm.AnalyzeAsync(null!));
    }

    [Fact]
    public void Des_dependances_nulles_sont_refusees()
    {
        var logger = NullLogger<MainViewModel>.Instance;

        Assert.Throws<ArgumentNullException>(() => new MainViewModel(null!, _source, _thumbnails, logger));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(_urlInput, null!, _thumbnails, logger));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(_urlInput, _source, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(_urlInput, _source, _thumbnails, null!));
    }
}

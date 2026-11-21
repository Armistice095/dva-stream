using System.Net;
using DvaStream.Core.Media;
using DvaStream.Core.Urls;
using DvaStream.Infrastructure.Media;
using DvaStream.Infrastructure.Tests.YtDlp;
using DvaStream.Infrastructure.YtDlp;

namespace DvaStream.Infrastructure.Tests.Media;

/// <summary>M2-09 (chargeur de miniatures) et M2-10 sur les fixtures réelles.</summary>
public class HttpThumbnailLoaderTests
{
    private static readonly Uri Url = new("https://i.ytimg.com/vi/x/hq.jpg");

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return respond(request);
        }
    }

    private static HttpResponseMessage Ok(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    [Fact]
    public async Task Charge_l_image_puis_la_sert_depuis_le_cache()
    {
        var handler = new StubHandler(_ => Task.FromResult(Ok([1, 2, 3])));
        var loader = new HttpThumbnailLoader(new HttpClient(handler));

        var first = await loader.LoadAsync(Url, CancellationToken.None);
        var second = await loader.LoadAsync(Url, CancellationToken.None);

        Assert.Equal(new byte[] { 1, 2, 3 }, first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Le_cache_est_borne_et_evince_la_plus_ancienne()
    {
        var handler = new StubHandler(_ => Task.FromResult(Ok([9])));
        var loader = new HttpThumbnailLoader(new HttpClient(handler), maxEntries: 2);

        await loader.LoadAsync(new Uri("https://a.test/1.jpg"), CancellationToken.None);
        await loader.LoadAsync(new Uri("https://a.test/2.jpg"), CancellationToken.None);
        await loader.LoadAsync(new Uri("https://a.test/3.jpg"), CancellationToken.None);

        Assert.Equal(2, loader.CachedCount);
        await loader.LoadAsync(new Uri("https://a.test/1.jpg"), CancellationToken.None);
        Assert.Equal(4, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Un_statut_d_erreur_donne_null_sans_exception(HttpStatusCode status)
    {
        var loader = new HttpThumbnailLoader(new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(status)))));

        Assert.Null(await loader.LoadAsync(Url, CancellationToken.None));
    }

    [Fact]
    public async Task Une_panne_reseau_donne_null_sans_exception()
    {
        var loader = new HttpThumbnailLoader(new HttpClient(new StubHandler(_ => throw new HttpRequestException("coupé"))));

        Assert.Null(await loader.LoadAsync(Url, CancellationToken.None));
    }

    [Fact]
    public async Task Un_delai_du_client_donne_null_mais_une_annulation_est_propagee()
    {
        var timedOut = new StubHandler(_ => throw new TaskCanceledException("délai"));
        Assert.Null(await new HttpThumbnailLoader(new HttpClient(timedOut)).LoadAsync(Url, CancellationToken.None));

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var loader = new HttpThumbnailLoader(new HttpClient(new StubHandler(_ => throw new TaskCanceledException())));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loader.LoadAsync(Url, cts.Token));
    }

    [Fact]
    public async Task Une_image_trop_grosse_est_refusee()
    {
        var big = new byte[HttpThumbnailLoader.MaxImageBytes + 1];
        var loader = new HttpThumbnailLoader(new HttpClient(new StubHandler(_ => Task.FromResult(Ok(big)))));

        Assert.Null(await loader.LoadAsync(Url, CancellationToken.None));
        Assert.Equal(0, loader.CachedCount);
    }

    [Fact]
    public async Task Une_reponse_vide_ou_un_schema_non_http_donne_null()
    {
        var handler = new StubHandler(_ => Task.FromResult(Ok([])));
        var loader = new HttpThumbnailLoader(new HttpClient(handler));

        Assert.Null(await loader.LoadAsync(Url, CancellationToken.None));
        Assert.Null(await loader.LoadAsync(new Uri("file:///c:/secret.jpg"), CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("standard", 2160, 144)]
    [InlineData("short", 240, 144)]
    [InlineData("long", 2160, 144)]
    public void Les_resolutions_des_fixtures_reelles_sont_triees_et_coherentes(string name, int max, int min)
    {
        var info = YtDlpInfoMapper.Parse(YtDlpFixtures.Json(name));

        var heights = info.GetAvailableResolutions();

        Assert.NotEmpty(heights);
        Assert.Equal(max, heights[0]);
        Assert.Equal(min, heights[^1]);
        Assert.Equal(heights.OrderDescending(), heights);
        Assert.True(info.HasAudio());
    }
}

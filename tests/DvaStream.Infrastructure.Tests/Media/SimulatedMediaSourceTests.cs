using DvaStream.Core.Urls;
using DvaStream.Infrastructure.Media;

namespace DvaStream.Infrastructure.Tests.Media;

public class SimulatedMediaSourceTests
{
    private static VideoId SomeId()
    {
        Assert.True(VideoId.TryCreate("dQw4w9WgXcQ", out var id));
        return id;
    }

    [Fact]
    public async Task Renvoie_une_video_fictive_pour_l_identifiant_demande()
    {
        var source = new SimulatedMediaSource(TimeSpan.Zero);
        var id = SomeId();

        var info = await source.GetInfoAsync(id, CancellationToken.None);

        Assert.Equal(id, info.Id);
        Assert.False(string.IsNullOrWhiteSpace(info.Title));
        Assert.Empty(info.Streams);
    }

    [Fact]
    public async Task Respecte_l_annulation()
    {
        var source = new SimulatedMediaSource(TimeSpan.FromMinutes(1));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.GetInfoAsync(SomeId(), cts.Token));
    }

    [Fact]
    public async Task Refuse_un_identifiant_nul()
    {
        var source = new SimulatedMediaSource(TimeSpan.Zero);

        await Assert.ThrowsAsync<ArgumentNullException>(() => source.GetInfoAsync(null!, CancellationToken.None));
    }
}

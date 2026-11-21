using DvaStream.Core.Errors;
using DvaStream.Infrastructure.YtDlp;

namespace DvaStream.Infrastructure.Tests.YtDlp;

/// <summary>M2-07 : classement des messages d'erreur de yt-dlp.</summary>
public class YtDlpErrorMapperTests
{
    [Theory]
    [InlineData("deleted", ErrorKind.VideoUnavailable)]
    [InlineData("nonexistent", ErrorKind.VideoUnavailable)]
    [InlineData("network", ErrorKind.Network)]
    public void Les_echecs_reels_enregistres_sont_classes(string name, ErrorKind expected)
    {
        var (exitCode, stderr) = YtDlpFixtures.Error(name);

        var error = YtDlpErrorMapper.Map(exitCode, stderr);

        Assert.Equal(expected, error.Kind);
        Assert.Contains(stderr[..30], error.TechnicalDetails, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ERROR: [youtube] abc: Private video. Sign in if you've been granted access to this video", ErrorKind.VideoPrivate)]
    [InlineData("ERROR: [youtube] abc: Sign in to confirm your age. This video may be inappropriate for some users.", ErrorKind.VideoRestricted)]
    [InlineData("ERROR: [youtube] abc: The uploader has not made this video available in your country", ErrorKind.VideoRestricted)]
    [InlineData("ERROR: [youtube] abc: Join this channel to get access to members-only content", ErrorKind.VideoRestricted)]
    [InlineData("ERROR: [youtube] abc: Sign in to confirm you’re not a bot", ErrorKind.RateLimited)]
    [InlineData("ERROR: Unable to download webpage: HTTP Error 429: Too Many Requests", ErrorKind.RateLimited)]
    [InlineData("ERROR: Unsupported URL: https://example.com", ErrorKind.InvalidUrl)]
    [InlineData("ERROR: [youtube] abc: <urlopen error [Errno -2] Name or service not known>", ErrorKind.Network)]
    [InlineData("ERROR: [youtube] abc: Video unavailable", ErrorKind.VideoUnavailable)]
    [InlineData("ERROR: quelque chose d'inattendu", ErrorKind.Unknown)]
    [InlineData("", ErrorKind.Unknown)]
    public void Les_motifs_connus_sont_classes(string stderr, ErrorKind expected)
    {
        Assert.Equal(expected, YtDlpErrorMapper.Map(1, stderr).Kind);
    }

    [Fact]
    public void Une_video_indisponible_dans_le_pays_est_une_restriction_et_non_une_indisponibilite()
    {
        var error = YtDlpErrorMapper.Map(1, "ERROR: [youtube] abc: Video unavailable. This video is not available in your country");

        Assert.Equal(ErrorKind.VideoRestricted, error.Kind);
    }

    [Fact]
    public void Un_message_absent_donne_Unknown_et_le_detail_est_borne()
    {
        Assert.Equal(ErrorKind.Unknown, YtDlpErrorMapper.Map(2, null).Kind);

        var long_ = new string('x', 10_000);
        Assert.True(YtDlpErrorMapper.Map(1, long_).TechnicalDetails.Length < 2100);
    }
}

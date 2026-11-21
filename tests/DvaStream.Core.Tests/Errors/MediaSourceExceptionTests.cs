using DvaStream.Core.Errors;

namespace DvaStream.Core.Tests.Errors;

public class MediaSourceExceptionTests
{
    [Fact]
    public void L_exception_porte_l_erreur_typee_et_un_message_lisible()
    {
        var inner = new InvalidOperationException("cause");
        var error = new AppError(ErrorKind.VideoPrivate, "ERROR: Private video", inner);

        var exception = new MediaSourceException(error);

        Assert.Same(error, exception.Error);
        Assert.Same(inner, exception.InnerException);
        Assert.Contains("VideoPrivate", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Private video", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Une_erreur_nulle_est_refusee() =>
        Assert.Throws<ArgumentNullException>(() => new MediaSourceException(null!));

    [Fact]
    public void Chaque_valeur_de_la_taxonomie_du_TDD_existe()
    {
        string[] expected =
        [
            "InvalidUrl", "UnsupportedPlaylist", "VideoPrivate", "VideoUnavailable", "VideoRestricted",
            "Network", "Timeout", "RateLimited", "DiskFull", "FolderNotWritable",
            "EngineMissing", "EngineBroken", "FfmpegMissing", "FfmpegFailed", "Cancelled", "Unknown",
        ];

        Assert.Equal(expected.Order(), Enum.GetNames<ErrorKind>().Order());
    }
}

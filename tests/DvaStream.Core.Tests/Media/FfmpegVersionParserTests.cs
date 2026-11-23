using DvaStream.Core.Media;
using DvaStream.Core.Platform;

namespace DvaStream.Core.Tests.Media;

/// <summary>M4-02 : lecture de <c>ffmpeg -version</c> et emplacement du binaire.</summary>
public class FfmpegVersionParserTests
{
    [Fact]
    public void Build_LGPL_de_BtbN()
    {
        const string output = "ffmpeg version n8.1-12-gabcdef123-20261101 Copyright (c) 2000-2026 the FFmpeg developers\r\n" +
                              "built with gcc 14\r\nconfiguration: --prefix=/ffbuild/prefix --disable-gpl --enable-version3\r\n";

        var info = FfmpegVersionParser.Parse(output);

        Assert.NotNull(info);
        Assert.Equal("n8.1-12-gabcdef123-20261101", info!.Version);
        Assert.False(info.IsGpl);
    }

    [Fact]
    public void Build_GPL_est_signale()
    {
        var info = FfmpegVersionParser.Parse("ffmpeg version 7.1.1-essentials_build-www.gyan.dev Copyright (c)\nconfiguration: --enable-gpl --enable-version3");

        Assert.Equal("7.1.1-essentials_build-www.gyan.dev", info!.Version);
        Assert.True(info.IsGpl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("yt-dlp 2026.10.26")]
    [InlineData("ffmpeg version ")]
    public void Autre_chose_que_FFmpeg_donne_null(string? output) => Assert.Null(FfmpegVersionParser.Parse(output));

    [Theory]
    [InlineData(HostOs.Windows, "ffmpeg.exe")]
    [InlineData(HostOs.Linux, "ffmpeg")]
    [InlineData(HostOs.MacOs, "ffmpeg")]
    public void Le_binaire_est_a_cote_de_yt_dlp(HostOs os, string name)
    {
        var dirs = new AppDirectories("c", Path.Combine("d", "data"));

        Assert.Equal(Path.Combine("d", "data", "engine", name), FfmpegLocator.GetExecutablePath(dirs, os));
        Assert.Equal(dirs.EngineDirectory, FfmpegLocator.GetDirectory(dirs));
    }
}

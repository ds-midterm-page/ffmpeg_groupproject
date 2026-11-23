using FluentAssertions;
using MediaTool.Core.Download;

namespace MediaTool.Core.Tests.Download;

public class Mp3ArgumentsTests
{
    private static readonly string Job = Path.Combine(Path.GetTempPath(), "MediaTool", "job");
    private static readonly string Input = Path.Combine(Job, "media.webm");
    private static readonly string Output = Path.Combine(Job, "media.mp3");

    [Theory]
    [InlineData(320, "320k")]
    [InlineData(192, "192k")]
    [InlineData(128, "128k")]
    public void Encodes_first_audio_stream_to_mp3_at_constant_bitrate(int kbps, string expected)
    {
        var args = FfmpegArguments.ToMp3(Input, Output, kbps);

        args.Should().ContainInOrder("-i", Input);
        args.Should().ContainInOrder("-map", "0:a:0");
        args.Should().Contain("-vn");
        args.Should().ContainInOrder("-c:a", "libmp3lame");
        args.Should().ContainInOrder("-b:a", expected);
        args.Should().Contain(["-nostdin", "-hide_banner", "-y"]);
        args[^1].Should().Be(Output);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    [InlineData(256)]
    [InlineData(-192)]
    [InlineData(100000)]
    public void Only_the_offered_bitrates_are_allowed(int kbps)
    {
        var act = () => FfmpegArguments.ToMp3(Input, Output, kbps);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Output_must_be_an_mp3_file()
    {
        var act = () => FfmpegArguments.ToMp3(Input, Path.Combine(Job, "media.wav"), 192);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Requires_full_and_different_paths()
    {
        ((Action)(() => FfmpegArguments.ToMp3("media.webm", Output, 192))).Should().Throw<ArgumentException>();
        ((Action)(() => FfmpegArguments.ToMp3(Input, "media.mp3", 192))).Should().Throw<ArgumentException>();
        ((Action)(() => FfmpegArguments.ToMp3(Output, Output, 192))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Offered_bitrates_are_320_192_128()
    {
        AudioQuality.Mp3BitratesKbps.Should().Equal(320, 192, 128);
    }
}

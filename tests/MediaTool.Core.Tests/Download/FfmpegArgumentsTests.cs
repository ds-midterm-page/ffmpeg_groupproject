using FluentAssertions;
using MediaTool.Core.Download;

namespace MediaTool.Core.Tests.Download;

public class FfmpegArgumentsTests
{
    private const string Input = @"C:\Temp\MediaTool\job\media.mp4";
    private const string Output = @"C:\Temp\MediaTool\job\silent.mp4";

    [Fact]
    public void Remove_audio_copies_video_streams_only()
    {
        var args = FfmpegArguments.RemoveAudio(Input, Output);

        args.Should().ContainInOrder("-i", Input);
        args.Should().ContainInOrder("-map", "0:v");
        args.Should().ContainInOrder("-c", "copy");
        args.Should().Contain("-an");
        args[^1].Should().Be(Output);
    }

    [Fact]
    public void Runs_non_interactively_and_overwrites_output()
    {
        FfmpegArguments.RemoveAudio(Input, Output).Should().Contain(["-nostdin", "-hide_banner", "-y"]);
    }

    [Fact]
    public void Paths_are_passed_as_single_arguments()
    {
        var withSpaces = @"C:\Temp\My Folder\media; rm.mp4";

        FfmpegArguments.RemoveAudio(withSpaces, Output).Should().Contain(withSpaces);
    }

    [Theory]
    [InlineData("media.mp4", Output)]
    [InlineData(Input, "silent.mp4")]
    [InlineData("-i", Output)]
    [InlineData(Input, "")]
    public void Requires_full_paths(string input, string output)
    {
        var act = () => FfmpegArguments.RemoveAudio(input, output);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Input_and_output_must_differ()
    {
        var act = () => FfmpegArguments.RemoveAudio(Input, Input);

        act.Should().Throw<ArgumentException>();
    }
}

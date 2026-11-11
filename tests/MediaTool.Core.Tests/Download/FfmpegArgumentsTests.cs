using FluentAssertions;
using MediaTool.Core.Download;

namespace MediaTool.Core.Tests.Download;

public class FfmpegArgumentsTests
{
    // Built from the OS temp folder so the paths are fully qualified on Windows and Linux alike.
    private static readonly string Job = Path.Combine(Path.GetTempPath(), "MediaTool", "job");
    private static readonly string Input = Path.Combine(Job, "media.mp4");
    private static readonly string Output = Path.Combine(Job, "silent.mp4");

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
        var withSpaces = Path.Combine(Path.GetTempPath(), "My Folder", "media; rm.mp4");

        FfmpegArguments.RemoveAudio(withSpaces, Output).Should().Contain(withSpaces);
    }

    public static TheoryData<string, string> NotFullPaths => new()
    {
        { "media.mp4", Output },
        { Input, "silent.mp4" },
        { "-i", Output },
        { Input, "" },
    };

    [Theory]
    [MemberData(nameof(NotFullPaths))]
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

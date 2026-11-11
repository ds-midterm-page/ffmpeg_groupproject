using FluentAssertions;
using MediaTool.Core.Formats;
using MediaTool.Core.Metadata;

namespace MediaTool.Core.Tests.Formats;

/// <summary>"Include sound" switched off.</summary>
public class SilentOptionTests
{
    private static VideoMetadata YouTube => YtDlpMetadataParser.Parse(Fixture.Read("youtube_video.json"));

    private static VideoMetadata TikTok => YtDlpMetadataParser.Parse(Fixture.Read("tiktok_video.json"));

    private static IReadOnlyList<QualityOption> Silent(VideoMetadata m) => FormatPicker.GetOptions(m, includeSound: false);

    [Fact]
    public void Default_includes_sound()
    {
        FormatPicker.GetOptions(YouTube).Where(o => o.Kind == QualityKind.Video).Should().OnlyContain(o => o.HasAudio);
    }

    [Fact]
    public void Uses_video_only_stream_without_merging_audio()
    {
        var hd = Silent(YouTube).Single(o => o.Label == "720p");

        hd.FormatSelector.Should().Be("136");
        hd.HasAudio.Should().BeFalse();
        hd.RemoveAudio.Should().BeFalse("the stream has no audio to remove");
        hd.Extension.Should().Be("mp4");
        hd.EstimatedBytes.Should().Be(26_750_000);
    }

    [Fact]
    public void Prefers_video_only_stream_over_stripping_a_combined_one()
    {
        // 360p: "18" has sound and a higher bitrate, but "134" needs no extra ffmpeg step.
        var sd = Silent(YouTube).Single(o => o.Label == "360p");

        sd.FormatSelector.Should().Be("134");
        sd.RemoveAudio.Should().BeFalse();
    }

    [Fact]
    public void Combined_only_stream_is_marked_for_audio_removal()
    {
        var option = Silent(TikTok).Single();

        option.FormatSelector.Should().Be("download_addr-0");
        option.HasAudio.Should().BeFalse();
        option.RemoveAudio.Should().BeTrue();
        option.IsSizeEstimated.Should().BeTrue("removing sound makes it a bit smaller than the listed size");
    }

    [Fact]
    public void Audio_only_is_not_offered_without_sound()
    {
        Silent(YouTube).Should().NotContain(o => o.Kind == QualityKind.Audio);
        Silent(YouTube).Select(o => o.Label).Should().Equal("720p", "480p", "360p");
    }

    [Fact]
    public void Silent_options_still_respect_limits()
    {
        Silent(YouTube).Should().OnlyContain(o => o.Height <= 720 && o.Width <= 1280);
        Silent(YouTube).Single(o => o.Label == "720p").ExceedsLimit.Should().BeFalse();
        FormatPicker.GetOptions(YouTube, includeSound: false, maxBytes: 10L * 1024 * 1024)
            .Single(o => o.Label == "720p").ExceedsLimit.Should().BeTrue();
    }

    [Fact]
    public void Sound_options_never_ask_for_audio_removal()
    {
        FormatPicker.GetOptions(YouTube).Should().OnlyContain(o => !o.RemoveAudio);
        FormatPicker.GetOptions(TikTok).Should().OnlyContain(o => !o.RemoveAudio);
    }
}

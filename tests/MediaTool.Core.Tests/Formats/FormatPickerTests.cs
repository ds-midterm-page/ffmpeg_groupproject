using FluentAssertions;
using MediaTool.Core.Formats;
using MediaTool.Core.Metadata;

namespace MediaTool.Core.Tests.Formats;

public class FormatPickerTests
{
    private const long MB = 1024 * 1024;

    private static VideoMetadata YouTube => YtDlpMetadataParser.Parse(Fixture.Read("youtube_video.json"));

    private static MediaFormat Video(string id, int w, int h, double tbr, long? size = null, string? audio = null, string ext = "mp4") =>
        new(id, ext, w, h, 30, "avc1", audio, tbr, size, size is null);

    private static MediaFormat Audio(string id, double tbr, long? size, string ext = "m4a") =>
        new(id, ext, null, null, null, null, "mp4a.40.2", tbr, size, false);

    private static VideoMetadata With(TimeSpan? duration, params MediaFormat[] formats) =>
        new("id", "t", null, duration, null, null, false, formats);

    [Fact]
    public void Offers_720p_480p_360p_and_audio_for_youtube()
    {
        var options = FormatPicker.GetOptions(YouTube);

        options.Select(o => o.Label).Should().Equal("720p", "480p", "360p", "Audio only");
    }

    [Fact]
    public void Never_offers_anything_above_1280()
    {
        var options = FormatPicker.GetOptions(YouTube);

        options.Should().NotContain(o => o.FormatSelector.Contains("137") || o.FormatSelector.Contains("313"));
        options.Where(o => o.Kind == QualityKind.Video).Should().OnlyContain(o => o.Height <= 720 && o.Width <= 1280);
    }

    [Fact]
    public void Video_only_stream_is_merged_with_best_m4a_audio()
    {
        var hd = FormatPicker.GetOptions(YouTube).Single(o => o.Label == "720p");

        hd.FormatSelector.Should().Be("136+140");
        hd.EstimatedBytes.Should().Be(26_750_000 + 3_449_826);
        hd.IsSizeEstimated.Should().BeFalse();
        hd.ExceedsLimit.Should().BeFalse();
    }

    [Fact]
    public void At_same_resolution_the_higher_bitrate_choice_wins()
    {
        // 360p: combined "18" (503 kbit/s) beats "134"+"140" (287 + 129.5 kbit/s)
        FormatPicker.GetOptions(YouTube).Single(o => o.Label == "360p").FormatSelector.Should().Be("18");
    }

    [Fact]
    public void Audio_only_prefers_m4a_over_webm()
    {
        var audio = FormatPicker.GetOptions(YouTube).Single(o => o.Kind == QualityKind.Audio);

        audio.FormatSelector.Should().Be("140");
        audio.Extension.Should().Be("m4a");
        audio.EstimatedBytes.Should().Be(3_449_826);
    }

    [Fact]
    public void Portrait_video_uses_short_side_for_label_and_long_side_for_limit()
    {
        var meta = With(TimeSpan.FromSeconds(15),
            Video("tall", 576, 1024, 1100, 2_000_000, audio: "aac"),
            Video("toolong", 720, 1440, 2000, 4_000_000, audio: "aac"));

        var options = FormatPicker.GetOptions(meta);

        options.Should().ContainSingle(o => o.Kind == QualityKind.Video)
            .Which.Should().Match<QualityOption>(o => o.Label == "576p" && o.FormatSelector == "tall");
    }

    [Fact]
    public void Same_stream_is_offered_once_when_presets_collapse()
    {
        // Only a 360p stream exists: 720p and 480p presets would pick the same one.
        var meta = With(TimeSpan.FromSeconds(60), Video("only", 640, 360, 500, 3_000_000, audio: "aac"));

        FormatPicker.GetOptions(meta).Where(o => o.Kind == QualityKind.Video)
            .Should().ContainSingle().Which.Label.Should().Be("360p");
    }

    [Fact]
    public void Options_over_50_MB_are_marked_too_large()
    {
        var meta = With(TimeSpan.FromMinutes(10),
            Video("big720", 1280, 720, 1000, 60 * MB),
            Audio("a", 128, 5 * MB),
            Video("ok360", 640, 360, 300, 20 * MB));

        var options = FormatPicker.GetOptions(meta).ToDictionary(o => o.Label);

        options["720p"].ExceedsLimit.Should().BeTrue();
        options["720p"].EstimatedBytes.Should().Be(65 * MB);
        options["360p"].ExceedsLimit.Should().BeFalse();
        options["Audio only"].ExceedsLimit.Should().BeFalse();
    }

    [Fact]
    public void Exactly_50_MB_is_allowed()
    {
        var meta = With(TimeSpan.FromMinutes(1), Video("v", 1280, 720, 1000, 50 * MB, audio: "aac"));

        FormatPicker.GetOptions(meta).Single().ExceedsLimit.Should().BeFalse();
    }

    [Fact]
    public void Unknown_size_is_allowed_but_flagged()
    {
        var meta = With(null, Video("v", 854, 480, 0, null, audio: "aac"));

        var option = FormatPicker.GetOptions(meta).Single();

        option.EstimatedBytes.Should().BeNull();
        option.ExceedsLimit.Should().BeFalse("the real size is checked after download");
    }

    [Fact]
    public void Video_only_without_any_audio_stream_is_offered_silent()
    {
        var meta = With(TimeSpan.FromSeconds(10), Video("v", 1280, 720, 800, 1 * MB));

        var option = FormatPicker.GetOptions(meta).Single();

        option.FormatSelector.Should().Be("v");
        option.HasAudio.Should().BeFalse();
    }

    [Fact]
    public void No_formats_gives_no_options()
    {
        FormatPicker.GetOptions(With(TimeSpan.FromSeconds(5))).Should().BeEmpty();
    }

    [Fact]
    public void Limit_can_be_overridden()
    {
        var hd = FormatPicker.GetOptions(YouTube, maxBytes: 10 * MB).Single(o => o.Label == "720p");

        hd.ExceedsLimit.Should().BeTrue();
    }
}

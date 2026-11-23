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

        options.Select(o => o.Label).Should().Equal("720p", "480p", "360p", "MP3 320 kbps", "MP3 192 kbps", "MP3 128 kbps");
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
    public void Audio_is_mp3_only_at_320_192_and_128_kbps()
    {
        var audio = FormatPicker.GetOptions(YouTube).Where(o => o.Kind == QualityKind.Audio).ToList();

        audio.Select(o => o.Mp3BitrateKbps).Should().Equal(320, 192, 128);
        audio.Should().OnlyContain(o => o.Extension == "mp3" && o.HasAudio && !o.RemoveAudio);
    }

    [Fact]
    public void Mp3_is_encoded_from_the_highest_bitrate_audio_stream()
    {
        // webm/opus "251" (135 kbit/s) beats m4a "140" (129.5) and "139" (48.8) as the encoding source.
        FormatPicker.GetOptions(YouTube).Where(o => o.Kind == QualityKind.Audio)
            .Should().OnlyContain(o => o.FormatSelector == "251");
    }

    [Fact]
    public void Mp3_size_is_estimated_from_bitrate_and_duration()
    {
        var audio = FormatPicker.GetOptions(YouTube).Where(o => o.Kind == QualityKind.Audio).ToDictionary(o => o.Mp3BitrateKbps!.Value);

        // 213 s: kbps * 1000 / 8 * 213
        audio[320].EstimatedBytes.Should().Be(8_520_000);
        audio[192].EstimatedBytes.Should().Be(5_112_000);
        audio[128].EstimatedBytes.Should().Be(3_408_000);
        audio.Values.Should().OnlyContain(o => o.IsSizeEstimated);
    }

    [Fact]
    public void Long_audio_marks_only_the_bitrates_over_50_MB()
    {
        // 30 min: 320 kbps = 72 MB (too large), 192 kbps = 43.2 MB, 128 kbps = 28.8 MB.
        var meta = With(TimeSpan.FromMinutes(30), Audio("a", 130, 30 * MB));

        var audio = FormatPicker.GetOptions(meta).ToDictionary(o => o.Mp3BitrateKbps!.Value);

        audio[320].ExceedsLimit.Should().BeTrue();
        audio[192].ExceedsLimit.Should().BeFalse();
        audio[128].ExceedsLimit.Should().BeFalse();
    }

    [Fact]
    public void Mp3_without_known_duration_has_unknown_size()
    {
        var meta = With(null, Audio("a", 130, 3 * MB));

        FormatPicker.GetOptions(meta).Should().OnlyContain(o => o.EstimatedBytes == null && !o.ExceedsLimit);
    }

    [Fact]
    public void Mp3_comes_from_a_combined_stream_when_there_is_no_audio_only_stream()
    {
        var tiktok = YtDlpMetadataParser.Parse(Fixture.Read("tiktok_video.json"));

        var audio = FormatPicker.GetOptions(tiktok).Where(o => o.Kind == QualityKind.Audio).ToList();

        audio.Should().HaveCount(3);
        audio.Should().OnlyContain(o => o.FormatSelector == "h264_540p_1100000-0" && o.Extension == "mp3");
    }

    [Fact]
    public void Video_only_streams_never_become_mp3()
    {
        var meta = With(TimeSpan.FromSeconds(10), Video("v", 1280, 720, 800, 1 * MB));

        FormatPicker.GetOptions(meta).Should().NotContain(o => o.Kind == QualityKind.Audio);
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
        options["MP3 128 kbps"].ExceedsLimit.Should().BeFalse();
    }

    [Fact]
    public void Exactly_50_MB_is_allowed()
    {
        var meta = With(TimeSpan.FromMinutes(1), Video("v", 1280, 720, 1000, 50 * MB, audio: "aac"));

        FormatPicker.GetOptions(meta).Single(o => o.Kind == QualityKind.Video).ExceedsLimit.Should().BeFalse();
    }

    [Fact]
    public void Unknown_size_is_allowed_but_flagged()
    {
        var meta = With(null, Video("v", 854, 480, 0, null, audio: "aac"));

        var option = FormatPicker.GetOptions(meta).Single(o => o.Kind == QualityKind.Video);

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

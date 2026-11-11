using FluentAssertions;
using MediaTool.Core.Metadata;

namespace MediaTool.Core.Tests.Metadata;

public class YtDlpMetadataParserTests
{
    [Fact]
    public void Parses_basic_youtube_fields()
    {
        var meta = YtDlpMetadataParser.Parse(Fixture.Read("youtube_video.json"));

        meta.Id.Should().Be("dQw4w9WgXcQ");
        meta.Title.Should().Be("Sample Music Video (Official)");
        meta.Uploader.Should().Be("Sample Channel");
        meta.Duration.Should().Be(TimeSpan.FromSeconds(213));
        meta.ThumbnailUrl.Should().Be(new Uri("https://i.ytimg.com/vi/dQw4w9WgXcQ/maxresdefault.jpg"));
        meta.WebpageUrl.Should().Be(new Uri("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
        meta.IsLive.Should().BeFalse();
    }

    [Fact]
    public void Drops_storyboards_and_keeps_real_formats()
    {
        var meta = YtDlpMetadataParser.Parse(Fixture.Read("youtube_video.json"));

        meta.Formats.Select(f => f.FormatId).Should().NotContain("sb0");
        meta.Formats.Should().HaveCount(9);
    }

    [Fact]
    public void Classifies_audio_only_video_only_and_combined_formats()
    {
        var formats = YtDlpMetadataParser.Parse(Fixture.Read("youtube_video.json")).Formats.ToDictionary(f => f.FormatId);

        formats["140"].IsAudioOnly.Should().BeTrue();
        formats["140"].HasVideo.Should().BeFalse();
        formats["140"].VideoCodec.Should().BeNull("yt-dlp's \"none\" means no codec");
        formats["140"].AudioCodec.Should().Be("mp4a.40.2");

        formats["136"].IsVideoOnly.Should().BeTrue();
        formats["136"].Height.Should().Be(720);
        formats["136"].Width.Should().Be(1280);
        formats["136"].Fps.Should().Be(25);

        formats["18"].HasVideo.Should().BeTrue();
        formats["18"].HasAudio.Should().BeTrue();
        formats["18"].Extension.Should().Be("mp4");
    }

    [Fact]
    public void Uses_exact_size_then_approx_size_then_bitrate_estimate()
    {
        var formats = YtDlpMetadataParser.Parse(Fixture.Read("youtube_video.json")).Formats.ToDictionary(f => f.FormatId);

        formats["140"].FileSizeBytes.Should().Be(3449826);
        formats["140"].IsSizeEstimated.Should().BeFalse();

        formats["251"].FileSizeBytes.Should().Be(3600000);
        formats["251"].IsSizeEstimated.Should().BeTrue();

        // 1080p has no size: tbr 2210 kbit/s * 213 s * 1000 / 8 = 58,841,250 bytes
        formats["137"].FileSizeBytes.Should().NotBeNull();
        formats["137"].FileSizeBytes!.Value.Should().BeCloseTo(58_841_250, 1000);
        formats["137"].IsSizeEstimated.Should().BeTrue();
    }

    [Fact]
    public void Falls_back_to_thumbnails_list_when_thumbnail_missing()
    {
        var meta = YtDlpMetadataParser.Parse(Fixture.Read("tiktok_video.json"));

        meta.ThumbnailUrl.Should().Be(new Uri("https://p16-sign.tiktokcdn.com/cover.jpeg"));
        meta.Duration.Should().Be(TimeSpan.FromSeconds(15));
        meta.Formats.Should().HaveCount(2);
    }

    [Fact]
    public void Tolerates_missing_fields_and_rejects_unsafe_thumbnail()
    {
        var meta = YtDlpMetadataParser.Parse(Fixture.Read("minimal_video.json"));

        meta.Title.Should().Be("Untitled video");
        meta.Uploader.Should().BeNull();
        meta.Duration.Should().BeNull();
        meta.ThumbnailUrl.Should().BeNull("only http(s) thumbnails may reach the page");
        meta.Formats.Should().ContainSingle().Which.FormatId.Should().Be("hls-832");
        meta.Formats[0].FileSizeBytes.Should().BeNull("no size and no duration to estimate from");
    }

    [Fact]
    public void Reads_live_flag()
    {
        var meta = YtDlpMetadataParser.Parse(Fixture.Read("live_stream.json"));

        meta.IsLive.Should().BeTrue();
        meta.Formats.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"title\": \"truncated\"")]
    public void Invalid_json_throws_MetadataParseException(string json)
    {
        var act = () => YtDlpMetadataParser.Parse(json);

        act.Should().Throw<MetadataParseException>();
    }
}

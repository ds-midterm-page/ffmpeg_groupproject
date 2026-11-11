using FluentAssertions;
using MediaTool.Core.Formats;
using MediaTool.Core.Metadata;

namespace MediaTool.Core.Tests.Formats;

public class SizeLimitTests
{
    [Fact]
    public void Limit_is_50_MB()
    {
        SizeLimit.MaxBytes.Should().Be(50L * 1024 * 1024);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(50L * 1024 * 1024, false)]
    [InlineData(50L * 1024 * 1024 + 1, true)]
    public void Real_file_size_check(long bytes, bool tooLarge)
    {
        SizeLimit.IsTooLarge(bytes).Should().Be(tooLarge);
    }

    [Theory]
    [InlineData(null, "size unknown")]
    [InlineData(0L, "0 KB")]
    [InlineData(1_000L, "1 KB")]
    [InlineData(3_449_826L, "3.3 MB")]
    [InlineData(52_428_800L, "50.0 MB")]
    [InlineData(1_610_612_736L, "1.5 GB")]
    public void Formats_sizes_for_people(long? bytes, string expected)
    {
        SizeFormatter.Format(bytes).Should().Be(expected);
    }

    [Fact]
    public void Too_large_has_a_plain_language_message()
    {
        MediaErrorMessages.For(MediaError.TooLarge).Should().Contain("50 MB");
    }
}

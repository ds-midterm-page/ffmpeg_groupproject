using System.Globalization;
using FluentAssertions;
using MediaTool.Core.Formats;

namespace MediaTool.Core.Tests.Localization;

public class LocalizedSizeTests
{
    private static readonly SizeUnits Russian = new("КБ", "МБ", "ГБ", "размер неизвестен");

    [Theory]
    [InlineData(3_449_826L, "3,3 МБ")]
    [InlineData(1_000L, "1 КБ")]
    [InlineData(1_610_612_736L, "1,5 ГБ")]
    [InlineData(null, "размер неизвестен")]
    public void Uses_local_units_and_decimal_separator(long? bytes, string expected)
    {
        SizeFormatter.Format(bytes, Russian, CultureInfo.GetCultureInfo("ru")).Should().Be(expected);
    }

    [Fact]
    public void English_overload_is_unchanged()
    {
        SizeFormatter.Format(3_449_826L, SizeUnits.English, CultureInfo.InvariantCulture).Should().Be(SizeFormatter.Format(3_449_826L));
    }
}

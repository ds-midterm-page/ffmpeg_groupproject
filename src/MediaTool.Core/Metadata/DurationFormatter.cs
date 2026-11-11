using System.Globalization;

namespace MediaTool.Core.Metadata;

public static class DurationFormatter
{
    /// <summary>"3:33" or "1:02:05", like video players. Fractions of a second are dropped.</summary>
    public static string Format(TimeSpan? duration)
    {
        if (duration is not { } d)
        {
            return "—";
        }

        var total = (long)Math.Max(0, Math.Floor(d.TotalSeconds));
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var seconds = total % 60;

        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}:{seconds:00}");
    }
}

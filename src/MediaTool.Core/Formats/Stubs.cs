using MediaTool.Core.Metadata;

namespace MediaTool.Core.Formats;

public static class FormatPicker
{
    public static IReadOnlyList<QualityOption> GetOptions(VideoMetadata metadata, long maxBytes = SizeLimit.MaxBytes) =>
        throw new NotImplementedException();
}

public static class SizeLimit
{
    public const long MaxBytes = 0;

    public static bool IsTooLarge(long bytes) => throw new NotImplementedException();
}

public static class SizeFormatter
{
    public static string Format(long? bytes) => throw new NotImplementedException();
}

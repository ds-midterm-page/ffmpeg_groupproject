namespace MediaTool.Core.Metadata;

public static class YtDlpMetadataParser
{
    public static VideoMetadata Parse(string json) => throw new NotImplementedException();
}

public static class YtDlpErrorClassifier
{
    public static MediaError Classify(string? stderr) => throw new NotImplementedException();
}

public static class MediaErrorMessages
{
    public static string For(MediaError error) => throw new NotImplementedException();
}

public static class YtDlpArguments
{
    public static IReadOnlyList<string> ForMetadata(Uri url, string? cookiesFile) => throw new NotImplementedException();
}

public static class DurationFormatter
{
    public static string Format(TimeSpan? duration) => throw new NotImplementedException();
}

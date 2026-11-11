namespace MediaTool.Core.Download;

public static class FormatSelector
{
    public static bool IsValid(string? selector) => throw new NotImplementedException();
}

public static class DownloadProgressParser
{
    public const string Prefix = "[mt-progress]";

    public static bool TryParse(string? line, out DownloadProgress progress) => throw new NotImplementedException();
}

public static class FileNameSanitizer
{
    public const int MaxLength = 100;

    public static string Sanitize(string? name) => throw new NotImplementedException();
}

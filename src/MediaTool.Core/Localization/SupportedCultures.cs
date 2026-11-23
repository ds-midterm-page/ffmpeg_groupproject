namespace MediaTool.Core.Localization;

/// <summary>Interface languages.</summary>
public static class SupportedCultures
{
    public const string Default = "en";

    public static readonly IReadOnlyList<string> Codes = [];

    public static string Normalize(string? culture) => throw new NotImplementedException();

    public static string NativeName(string code) => throw new NotImplementedException();

    public static string SafeReturnPath(string? path) => throw new NotImplementedException();
}

namespace MediaTool.Core.Hosting;

/// <summary>Startup decisions that differ between the desktop exe and a container.</summary>
public static class HostingSettings
{
    public const string DefaultUrl = "http://localhost:5080";

    public static string ResolveUrls(string? configuredUrls) => throw new NotImplementedException();

    public static bool ShouldOpenBrowser(bool isDevelopment, string? noBrowser, string? runningInContainer) =>
        throw new NotImplementedException();
}

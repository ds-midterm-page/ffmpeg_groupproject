namespace MediaTool.Core.Hosting;

/// <summary>Startup decisions that differ between the desktop exe and a container.</summary>
public static class HostingSettings
{
    /// <summary>Desktop default: only this computer can reach the app.</summary>
    public const string DefaultUrl = "http://localhost:5080";

    /// <summary>
    /// The configured address (ASPNETCORE_URLS, e.g. "http://+:5080" in Docker) or localhost-only by default.
    /// </summary>
    public static string ResolveUrls(string? configuredUrls) =>
        string.IsNullOrWhiteSpace(configuredUrls) ? DefaultUrl : configuredUrls.Trim();

    /// <summary>
    /// Open the user's browser only for a normal desktop start: not under <c>dotnet run</c> (launchSettings does it),
    /// not when MEDIATOOL_NO_BROWSER is set, and not inside a container (DOTNET_RUNNING_IN_CONTAINER=true).
    /// </summary>
    public static bool ShouldOpenBrowser(bool isDevelopment, string? noBrowser, string? runningInContainer) =>
        !isDevelopment
        && noBrowser is null
        && !string.Equals(runningInContainer, "true", StringComparison.OrdinalIgnoreCase);
}

using FluentAssertions;
using MediaTool.Core.Hosting;

namespace MediaTool.Core.Tests.Hosting;

public class HostingSettingsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Defaults_to_localhost_only(string? configured)
    {
        HostingSettings.ResolveUrls(configured).Should().Be("http://localhost:5080");
    }

    [Fact]
    public void Uses_configured_urls_for_containers()
    {
        HostingSettings.ResolveUrls("http://+:5080").Should().Be("http://+:5080");
    }

    [Fact]
    public void Opens_browser_for_a_normal_desktop_start()
    {
        HostingSettings.ShouldOpenBrowser(isDevelopment: false, noBrowser: null, runningInContainer: null).Should().BeTrue();
    }

    [Theory]
    [InlineData(true, null, null)]      // dotnet run: launchSettings opens it
    [InlineData(false, "1", null)]      // MEDIATOOL_NO_BROWSER set
    [InlineData(false, null, "true")]   // DOTNET_RUNNING_IN_CONTAINER set by .NET images
    [InlineData(false, null, "TRUE")]
    public void Does_not_open_browser(bool isDevelopment, string? noBrowser, string? runningInContainer)
    {
        HostingSettings.ShouldOpenBrowser(isDevelopment, noBrowser, runningInContainer).Should().BeFalse();
    }

    [Theory]
    [InlineData("false")]
    [InlineData("")]
    public void Container_flag_must_be_true_to_count(string runningInContainer)
    {
        HostingSettings.ShouldOpenBrowser(false, null, runningInContainer).Should().BeTrue();
    }
}

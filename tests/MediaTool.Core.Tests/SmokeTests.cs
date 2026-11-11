using FluentAssertions;

namespace MediaTool.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Core_assembly_loads()
    {
        typeof(MediaTool.Core.AssemblyMarker).Assembly.GetName().Name.Should().Be("MediaTool.Core");
    }
}

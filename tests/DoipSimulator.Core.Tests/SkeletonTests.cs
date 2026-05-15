using DoipSimulator.Core;

namespace DoipSimulator.Core.Tests;

public class SkeletonTests
{
    [Fact]
    public void CoreAssemblyMarkerCanBeCreated()
    {
        var marker = new AssemblyMarker();

        Assert.NotNull(marker);
    }
}

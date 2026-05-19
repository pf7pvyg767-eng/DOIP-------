using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.RuntimeEvents;
using DoipSimulator.Protocols.Uds.Plugins;

namespace DoipSimulator.Protocols.Uds.Tests;

public class SecurityPluginLoaderTests
{
    [Fact]
    public void MissingDllReturnsClearErrorWithoutThrowing()
    {
        var sink = new CapturingEventSink();
        var result = SecurityPluginLoader.Load(
            new SecurityPluginConfig
            {
                Enabled = true,
                DllPath = Path.Combine(Path.GetTempPath(), "missing-security-plugin.dll"),
                TimeoutMs = 500,
            },
            new RuntimeEventBus([sink]));

        Assert.False(result.IsAvailable);
        Assert.Contains("does not exist", result.Error);
        Assert.Contains(sink.Events, item => item.Name == "uds.securityPlugin.failed");
    }

    [Fact]
    public void AbiMismatchReturnsClearErrorWithoutThrowing()
    {
        var dllPath = SecurityPluginTestSupport.BuildSamplePlugin();
        var original = Environment.GetEnvironmentVariable("DOIP_SEC_SAMPLE_ABI_VERSION");
        Environment.SetEnvironmentVariable("DOIP_SEC_SAMPLE_ABI_VERSION", "2");
        try
        {
            var result = SecurityPluginLoader.Load(new SecurityPluginConfig
            {
                Enabled = true,
                DllPath = dllPath,
                TimeoutMs = 500,
            });

            Assert.False(result.IsAvailable);
            Assert.Contains("expected 1", result.Error);
            Assert.Contains("actual 2", result.Error);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOIP_SEC_SAMPLE_ABI_VERSION", original);
        }
    }

    [Fact]
    public void SampleDllLoadsAndResolvesAbiFunctions()
    {
        var dllPath = SecurityPluginTestSupport.BuildSamplePlugin();
        var sink = new CapturingEventSink();

        var result = SecurityPluginLoader.Load(
            new SecurityPluginConfig
            {
                Enabled = true,
                DllPath = dllPath,
                TimeoutMs = 500,
            },
            new RuntimeEventBus([sink]));

        Assert.True(result.IsAvailable, result.Error);
        Assert.NotNull(result.Algorithm);
        Assert.Contains(sink.Events, item => item.Name == "uds.securityPlugin.loaded");
    }
}

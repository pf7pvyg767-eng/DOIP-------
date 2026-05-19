using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Faults;

namespace DoipSimulator.Core.Tests.Faults;

public class FaultProfileTests
{
    [Fact]
    public void DefaultConfigurationContainsDisabledFaultProfile()
    {
        var config = SimulatorConfig.CreateDefault();

        Assert.False(config.FaultProfile.Enabled);
        Assert.Equal(0, config.FaultProfile.ResponseDelayMs);
        Assert.False(config.FaultProfile.PauseResponses);
        Assert.False(config.FaultProfile.RoutingActivationFailure);
        Assert.False(config.FaultProfile.CorruptNextDoipHeader.IsActive);
        Assert.Null(config.FaultProfile.NextNrc);
        Assert.True(ConfigValidator.Validate(config).IsValid);
    }

    [Fact]
    public void ValidFaultProfilePassesValidation()
    {
        var config = SimulatorConfig.CreateDefault();
        config.FaultProfile = new FaultProfile
        {
            Enabled = true,
            ResponseDelayMs = 25,
            PauseResponses = false,
            RoutingActivationFailure = true,
            CorruptNextDoipHeader = new DoipHeaderFaultConfig
            {
                InverseVersion = true,
                PayloadLengthDelta = 1,
            },
            NextNrc = new UdsFaultOverrideConfig
            {
                ServiceId = "0x22",
                Nrc = "0x31",
            },
            NextCustomResponse = new UdsFaultOverrideConfig
            {
                ServiceId = "0x10",
                ResponseBytes = "500300321388",
            },
        };

        Assert.True(ConfigValidator.Validate(config).IsValid);
    }

    [Fact]
    public void InvalidFaultProfileReturnsFieldSpecificErrors()
    {
        var config = SimulatorConfig.CreateDefault();
        config.FaultProfile = new FaultProfile
        {
            ResponseDelayMs = -1,
            CorruptNextDoipHeader = new DoipHeaderFaultConfig
            {
                PayloadLengthDelta = 5000,
            },
            NextNrc = new UdsFaultOverrideConfig
            {
                ServiceId = "0x123",
                Nrc = "BAD",
            },
            NextCustomResponse = new UdsFaultOverrideConfig
            {
                ServiceId = "GG",
                ResponseBytes = "ABC",
            },
        };

        var result = ConfigValidator.Validate(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Field == "faultProfile.responseDelayMs");
        Assert.Contains(result.Errors, error => error.Field == "faultProfile.corruptNextDoipHeader.payloadLengthDelta");
        Assert.Contains(result.Errors, error => error.Field == "faultProfile.nextNrc.serviceId");
        Assert.Contains(result.Errors, error => error.Field == "faultProfile.nextNrc.nrc");
        Assert.Contains(result.Errors, error => error.Field == "faultProfile.nextCustomResponse.serviceId");
        Assert.Contains(result.Errors, error => error.Field == "faultProfile.nextCustomResponse.responseBytes");
    }

    [Fact]
    public void RuntimeStateConsumesOneShotFaultsDeterministically()
    {
        var runtime = new FaultRuntimeState(new FaultProfile
        {
            Enabled = true,
            CorruptNextDoipHeader = new DoipHeaderFaultConfig
            {
                InverseVersion = true,
                PayloadLengthDelta = 1,
            },
            NextNrc = new UdsFaultOverrideConfig
            {
                ServiceId = "0x22",
                Nrc = "0x31",
            },
        });

        Assert.NotNull(runtime.TryConsumeDoipHeaderFault());
        Assert.Null(runtime.TryConsumeDoipHeaderFault());
        Assert.Null(runtime.TryConsumeUdsOverride(0x10));
        Assert.NotNull(runtime.TryConsumeUdsOverride(0x22));
        Assert.Null(runtime.TryConsumeUdsOverride(0x22));
    }
}

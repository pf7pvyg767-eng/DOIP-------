using DoipSimulator.Core.Configuration;

namespace DoipSimulator.Protocols.Uds.Plugins;

public interface ISecurityAccessAlgorithm
{
    SecurityPluginSeedResult GenerateSeed(SecurityAccessConfig level, byte subFunction);

    SecurityPluginKeyResult VerifyKey(SecurityAccessConfig level, IReadOnlyList<byte> seed, IReadOnlyList<byte> key);
}

public sealed class BuiltInSecurityAccessAlgorithm : ISecurityAccessAlgorithm
{
    public SecurityPluginSeedResult GenerateSeed(SecurityAccessConfig level, byte subFunction)
    {
        return SecurityPluginSeedResult.Succeeded([(byte)level.Level, subFunction, 0xA5, 0x5A]);
    }

    public SecurityPluginKeyResult VerifyKey(SecurityAccessConfig level, IReadOnlyList<byte> seed, IReadOnlyList<byte> key)
    {
        var expectedKey = SecurityAccessService.ComputeExpectedKey(level, seed);
        return expectedKey.SequenceEqual(key)
            ? SecurityPluginKeyResult.Succeeded()
            : SecurityPluginKeyResult.InvalidKey();
    }
}

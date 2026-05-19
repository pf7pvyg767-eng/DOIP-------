namespace DoipSimulator.Protocols.Uds.Plugins;

public enum SecurityPluginStatus
{
    Success,
    InvalidKey,
    Failure,
}

public sealed record SecurityPluginSeedResult(
    SecurityPluginStatus Status,
    byte[] Seed,
    string? Reason = null)
{
    public static SecurityPluginSeedResult Succeeded(byte[] seed) => new(SecurityPluginStatus.Success, seed);

    public static SecurityPluginSeedResult Failed(string reason) => new(SecurityPluginStatus.Failure, [], reason);
}

public sealed record SecurityPluginKeyResult(
    SecurityPluginStatus Status,
    string? Reason = null)
{
    public static SecurityPluginKeyResult Succeeded() => new(SecurityPluginStatus.Success);

    public static SecurityPluginKeyResult InvalidKey() => new(SecurityPluginStatus.InvalidKey, "invalid key");

    public static SecurityPluginKeyResult Failed(string reason) => new(SecurityPluginStatus.Failure, reason);
}

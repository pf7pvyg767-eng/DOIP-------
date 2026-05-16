using System.Globalization;
using System.Text.RegularExpressions;

namespace DoipSimulator.Core.Configuration;

public sealed record ConfigValidationError(string Field, string Message);

public sealed class ConfigValidationResult
{
    public ConfigValidationResult(IReadOnlyList<ConfigValidationError> errors)
    {
        Errors = errors;
    }

    public IReadOnlyList<ConfigValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}

public sealed class ConfigValidationException : Exception
{
    public ConfigValidationException(ConfigValidationResult validationResult)
        : base(BuildMessage(validationResult))
    {
        ValidationResult = validationResult;
    }

    public ConfigValidationResult ValidationResult { get; }

    private static string BuildMessage(ConfigValidationResult validationResult)
    {
        return "Configuration validation failed: "
            + string.Join("; ", validationResult.Errors.Select(error => $"{error.Field}: {error.Message}"));
    }
}

public static partial class ConfigValidator
{
    public static ConfigValidationResult Validate(SimulatorConfig? config)
    {
        var errors = new List<ConfigValidationError>();

        if (config is null)
        {
            errors.Add(new ConfigValidationError("config", "Configuration is required."));
            return new ConfigValidationResult(errors);
        }

        ValidateVin(config.Entity?.Vin, errors);
        ValidateHexIdentifier(config.Entity?.Eid, 12, "entity.eid", "EID", errors);
        ValidateHexIdentifier(config.Entity?.Gid, 12, "entity.gid", "GID", errors);
        ValidateLogicalAddress(config.Entity?.LogicalAddress, "entity.logicalAddress", errors);

        ValidatePort(config.Network?.DoipUdpPort, "network.doipUdpPort", errors);
        ValidatePort(config.Network?.DoipTcpPort, "network.doipTcpPort", errors);
        ValidatePort(config.Network?.DoipTlsPort, "network.doipTlsPort", errors);
        ValidatePort(config.Network?.VehicleAnnouncementTargetPort, "network.vehicleAnnouncementTargetPort", errors);
        ValidateVehicleAnnouncementInterval(config.Network?.VehicleAnnouncementIntervalMilliseconds, errors);
        ValidateTcpConnectionIdleTimeout(config.Network?.TcpConnectionIdleTimeoutMilliseconds, errors);
        ValidateIpAddress(
            config.Network?.VehicleAnnouncementTargetAddress,
            "network.vehicleAnnouncementTargetAddress",
            errors);

        var whitelist = config.Network?.SourceAddressWhitelist;
        if (whitelist is null)
        {
            errors.Add(new ConfigValidationError(
                "network.sourceAddressWhitelist",
                "Source address whitelist is required."));
        }
        else
        {
            for (var index = 0; index < whitelist.Count; index++)
            {
                ValidateLogicalAddress(
                    whitelist[index],
                    $"network.sourceAddressWhitelist[{index}]",
                    errors);
            }
        }

        return new ConfigValidationResult(errors);
    }

    public static void ThrowIfInvalid(SimulatorConfig config)
    {
        var result = Validate(config);
        if (!result.IsValid)
        {
            throw new ConfigValidationException(result);
        }
    }

    private static void ValidateVin(string? vin, List<ConfigValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(vin) || !VinRegex().IsMatch(vin))
        {
            errors.Add(new ConfigValidationError(
                "entity.vin",
                "VIN must be exactly 17 uppercase alphanumeric characters excluding I, O, and Q."));
        }
    }

    private static void ValidateHexIdentifier(
        string? value,
        int expectedLength,
        string field,
        string label,
        List<ConfigValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != expectedLength
            || !value.All(Uri.IsHexDigit))
        {
            errors.Add(new ConfigValidationError(
                field,
                $"{label} must be exactly {expectedLength} hexadecimal characters."));
        }
    }

    private static void ValidateLogicalAddress(
        string? value,
        string field,
        List<ConfigValidationError> errors)
    {
        if (!TryParseUInt16Hex(value, out _))
        {
            errors.Add(new ConfigValidationError(
                field,
                "Logical address must be a hexadecimal value from 0x0000 through 0xFFFF."));
        }
    }

    private static void ValidatePort(int? port, string field, List<ConfigValidationError> errors)
    {
        if (port is null or < 1 or > 65535)
        {
            errors.Add(new ConfigValidationError(
                field,
                "Port must be between 1 and 65535."));
        }
    }

    private static void ValidateVehicleAnnouncementInterval(
        int? intervalMilliseconds,
        List<ConfigValidationError> errors)
    {
        if (intervalMilliseconds is null or < 100)
        {
            errors.Add(new ConfigValidationError(
                "network.vehicleAnnouncementIntervalMilliseconds",
                "Vehicle announcement interval must be at least 100 milliseconds."));
        }
    }

    private static void ValidateTcpConnectionIdleTimeout(
        int? timeoutMilliseconds,
        List<ConfigValidationError> errors)
    {
        if (timeoutMilliseconds is null or < 1000)
        {
            errors.Add(new ConfigValidationError(
                "network.tcpConnectionIdleTimeoutMilliseconds",
                "TCP connection idle timeout must be at least 1000 milliseconds."));
        }
    }

    private static void ValidateIpAddress(
        string? value,
        string field,
        List<ConfigValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || !System.Net.IPAddress.TryParse(value, out _))
        {
            errors.Add(new ConfigValidationError(
                field,
                "IP address must be a valid IPv4 or IPv6 address."));
        }
    }

    private static bool TryParseUInt16Hex(string? value, out ushort parsed)
    {
        parsed = 0;
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hex = value[2..];
        return hex is { Length: > 0 and <= 4 }
            && hex.All(Uri.IsHexDigit)
            && ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed);
    }

    [GeneratedRegex("^[A-HJ-NPR-Z0-9]{17}$")]
    private static partial Regex VinRegex();
}

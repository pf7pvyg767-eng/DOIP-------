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

        ValidateDids(config.Uds?.Dids, errors);
        ValidateDtcs(config.Uds?.Dtcs, errors);
        ValidateRoutines(config.Uds?.Routines, errors);

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

    private static void ValidateDids(List<DidConfig>? dids, List<ConfigValidationError> errors)
    {
        if (dids is null)
        {
            errors.Add(new ConfigValidationError(
                "uds.dids",
                "DID configuration list is required."));
            return;
        }

        for (var index = 0; index < dids.Count; index++)
        {
            var did = dids[index];
            var identifier = ResolveDidIdentifier(did);
            if (!TryParseUInt16Hex(identifier, out _))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.dids[{index}].identifier",
                    "DID identifier must be a hexadecimal value from 0x0000 through 0xFFFF."));
            }

            if (!string.Equals(did.ValueEncoding, "hex", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.dids[{index}].valueEncoding",
                    "DID value encoding must be 'hex'."));
            }

            if (!IsEvenLengthHexBytes(did.Value))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.dids[{index}].value",
                    "DID fixed value must be an even-length hexadecimal byte string."));
            }

            if (did.WriteLength is < 1)
            {
                errors.Add(new ConfigValidationError(
                    $"uds.dids[{index}].writeLength",
                    "DID write length must be a positive byte count when configured."));
            }
        }
    }

    public static bool TryParseDidIdentifier(DidConfig did, out ushort identifier)
    {
        return TryParseUInt16Hex(ResolveDidIdentifier(did), out identifier);
    }

    public static bool TryParseDtcCode(string? value, out uint code)
    {
        code = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value;

        return normalized.Length == 6
            && normalized.All(Uri.IsHexDigit)
            && uint.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)
            && code <= 0xFFFFFF;
    }

    public static bool TryParseStatusByte(string? value, out byte status)
    {
        status = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value;

        return normalized.Length is > 0 and <= 2
            && normalized.All(Uri.IsHexDigit)
            && byte.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out status);
    }

    public static bool TryParseRoutineIdentifier(RoutineConfig routine, out ushort identifier)
    {
        return TryParseUInt16Hex(ResolveRoutineIdentifier(routine), out identifier);
    }

    public static string FormatRoutineIdentifier(ushort routineId) => $"0x{routineId:X4}";

    private static void ValidateDtcs(List<DtcConfig>? dtcs, List<ConfigValidationError> errors)
    {
        if (dtcs is null)
        {
            errors.Add(new ConfigValidationError(
                "uds.dtcs",
                "DTC configuration list is required."));
            return;
        }

        var seenCodes = new HashSet<uint>();
        for (var index = 0; index < dtcs.Count; index++)
        {
            var dtc = dtcs[index];
            if (!TryParseDtcCode(dtc.Code, out var code))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.dtcs[{index}].code",
                    "DTC code must be a 24-bit hexadecimal value such as 0x123456."));
            }
            else if (!seenCodes.Add(code))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.dtcs[{index}].code",
                    "DTC code must be unique."));
            }

            if (!TryParseStatusByte(dtc.Status, out _))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.dtcs[{index}].status",
                    "DTC status must be a hexadecimal byte such as 0x2F."));
            }
        }
    }

    private static void ValidateRoutines(List<RoutineConfig>? routines, List<ConfigValidationError> errors)
    {
        if (routines is null)
        {
            errors.Add(new ConfigValidationError(
                "uds.routines",
                "Routine configuration list is required."));
            return;
        }

        var seenRoutineIds = new HashSet<ushort>();
        for (var index = 0; index < routines.Count; index++)
        {
            var routine = routines[index];
            if (!TryParseRoutineIdentifier(routine, out var routineId))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.routines[{index}].identifier",
                    "Routine identifier must be a hexadecimal value from 0x0000 through 0xFFFF."));
            }
            else if (!seenRoutineIds.Add(routineId))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.routines[{index}].identifier",
                    "Routine identifier must be unique."));
            }

            ValidateRoutineResponse(routine.FixedResponses.Start, $"uds.routines[{index}].fixedResponses.start", errors);
            ValidateRoutineResponse(routine.FixedResponses.Stop, $"uds.routines[{index}].fixedResponses.stop", errors);
            ValidateRoutineResponse(routine.FixedResponses.RequestResults, $"uds.routines[{index}].fixedResponses.requestResults", errors);
        }
    }

    private static string? ResolveDidIdentifier(DidConfig did)
    {
        return string.IsNullOrWhiteSpace(did.Identifier) ? did.Id : did.Identifier;
    }

    private static string? ResolveRoutineIdentifier(RoutineConfig routine)
    {
        return string.IsNullOrWhiteSpace(routine.Identifier) ? routine.RoutineId : routine.Identifier;
    }

    private static void ValidateRoutineResponse(
        string? value,
        string field,
        List<ConfigValidationError> errors)
    {
        if (!string.IsNullOrWhiteSpace(value) && !IsEvenLengthHexBytes(value))
        {
            errors.Add(new ConfigValidationError(
                field,
                "Routine fixed response payload must be an even-length hexadecimal byte string."));
        }
    }

    private static bool IsEvenLengthHexBytes(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length % 2 == 0
            && value.All(Uri.IsHexDigit);
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

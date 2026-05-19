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
        ValidateTiming(config.Uds, errors);
        ValidateSecurityAccess(config.Uds?.SecurityAccess, errors);
        ValidateFlash(config.Uds?.Flash, errors);

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

            ValidateRequiredSecurityLevel(
                did.RequiredSecurityLevel,
                $"uds.dids[{index}].requiredSecurityLevel",
                errors);
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

    public static bool TryParseByteHex(string? value, out byte parsed)
    {
        parsed = 0;
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hex = value[2..];
        return hex is { Length: > 0 and <= 2 }
            && hex.All(Uri.IsHexDigit)
            && byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed);
    }

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
            ValidateRequiredSecurityLevel(
                routine.RequiredSecurityLevel,
                $"uds.routines[{index}].requiredSecurityLevel",
                errors);
        }
    }

    private static void ValidateSecurityAccess(
        List<SecurityAccessConfig>? securityAccess,
        List<ConfigValidationError> errors)
    {
        if (securityAccess is null)
        {
            errors.Add(new ConfigValidationError(
                "uds.securityAccess",
                "SecurityAccess configuration list is required."));
            return;
        }

        var seenLevels = new HashSet<int>();
        var seenSeedSubFunctions = new HashSet<byte>();
        var seenKeySubFunctions = new HashSet<byte>();
        for (var index = 0; index < securityAccess.Count; index++)
        {
            var level = securityAccess[index];
            if (level.Level is < 1 or > 255)
            {
                errors.Add(new ConfigValidationError(
                    $"uds.securityAccess[{index}].level",
                    "SecurityAccess level must be between 1 and 255."));
            }
            else if (!seenLevels.Add(level.Level))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.securityAccess[{index}].level",
                    "SecurityAccess level must be unique."));
            }

            var seedIsValid = TryParseByteHex(level.SeedSubFunction, out var seedSubFunction);
            var keyIsValid = TryParseByteHex(level.KeySubFunction, out var keySubFunction);

            if (!seedIsValid)
            {
                errors.Add(new ConfigValidationError(
                    $"uds.securityAccess[{index}].seedSubFunction",
                    "Seed request sub-function must be a hexadecimal byte such as 0x01."));
            }
            else if (!seenSeedSubFunctions.Add(seedSubFunction))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.securityAccess[{index}].seedSubFunction",
                    "Seed request sub-function must be unique."));
            }

            if (!keyIsValid)
            {
                errors.Add(new ConfigValidationError(
                    $"uds.securityAccess[{index}].keySubFunction",
                    "Key send sub-function must be a hexadecimal byte such as 0x02."));
            }
            else if (!seenKeySubFunctions.Add(keySubFunction))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.securityAccess[{index}].keySubFunction",
                    "Key send sub-function must be unique."));
            }

            if (seedIsValid && keyIsValid && seedSubFunction == keySubFunction)
            {
                errors.Add(new ConfigValidationError(
                    $"uds.securityAccess[{index}].keySubFunction",
                    "Seed and key sub-functions must differ."));
            }

            if (!IsSupportedSecurityAlgorithm(level.Algorithm))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.securityAccess[{index}].algorithm",
                    "SecurityAccess algorithm must be 'builtin-xor' or 'builtin-add'."));
            }

            if (!IsEvenLengthHexBytes(level.AlgorithmParameter))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.securityAccess[{index}].algorithmParameter",
                    "SecurityAccess algorithm parameter must be an even-length hexadecimal byte string."));
            }

            if (level.MaxFailedAttempts < 1)
            {
                errors.Add(new ConfigValidationError(
                    $"uds.securityAccess[{index}].maxFailedAttempts",
                    "SecurityAccess max failed attempts must be at least 1."));
            }

            if (level.LockoutMs < 0)
            {
                errors.Add(new ConfigValidationError(
                    $"uds.securityAccess[{index}].lockoutMs",
                    "SecurityAccess lockout time must be zero or greater."));
            }
        }
    }

    private static void ValidateFlash(FlashConfig? flash, List<ConfigValidationError> errors)
    {
        if (flash is null)
        {
            return;
        }

        if (flash.MaxMemorySize < 1)
        {
            errors.Add(new ConfigValidationError(
                "uds.flash.maxMemorySize",
                "Flash max memory size must be a positive byte count."));
        }

        if (flash.MaxBlockLength < 1)
        {
            errors.Add(new ConfigValidationError(
                "uds.flash.maxBlockLength",
                "Flash max block length must be a positive byte count."));
        }
        else if (flash.MaxMemorySize > 0 && flash.MaxBlockLength > flash.MaxMemorySize)
        {
            errors.Add(new ConfigValidationError(
                "uds.flash.maxBlockLength",
                "Flash max block length must not exceed max memory size."));
        }

        if (flash.AllowedSessions is null || flash.AllowedSessions.Count == 0)
        {
            errors.Add(new ConfigValidationError(
                "uds.flash.allowedSessions",
                "Flash allowed sessions must contain at least one valid session."));
        }
        else
        {
            for (var index = 0; index < flash.AllowedSessions.Count; index++)
            {
                if (!IsKnownSessionName(flash.AllowedSessions[index]))
                {
                    errors.Add(new ConfigValidationError(
                        $"uds.flash.allowedSessions[{index}]",
                        "Flash allowed session must be 'default', 'programming', or 'extended'."));
                }
            }
        }

        if (flash.SecurityRequired && flash.RequiredSecurityLevel is null)
        {
            errors.Add(new ConfigValidationError(
                "uds.flash.requiredSecurityLevel",
                "Flash required SecurityAccess level is required when securityRequired is true."));
        }
        else if (flash.SecurityRequired || flash.RequiredSecurityLevel is not null)
        {
            ValidateRequiredSecurityLevel(
                flash.RequiredSecurityLevel,
                "uds.flash.requiredSecurityLevel",
                errors);
        }
    }

    private static void ValidateTiming(UdsConfig? uds, List<ConfigValidationError> errors)
    {
        if (uds is null)
        {
            errors.Add(new ConfigValidationError("uds", "UDS configuration is required."));
            return;
        }

        if (uds.Sessions is null)
        {
            errors.Add(new ConfigValidationError("uds.sessions", "Session configuration list is required."));
        }
        else
        {
            for (var index = 0; index < uds.Sessions.Count; index++)
            {
                ValidateSessionTiming(uds.Sessions[index], index, errors);
            }
        }

        if (uds.TesterPresentTimeout is null)
        {
            errors.Add(new ConfigValidationError(
                "uds.testerPresentTimeout",
                "TesterPresent timeout configuration is required."));
        }
        else if (uds.TesterPresentTimeout.TimeoutMs < 1)
        {
            errors.Add(new ConfigValidationError(
                "uds.testerPresentTimeout.timeoutMs",
                "TesterPresent timeout must be at least 1 millisecond."));
        }

        if (uds.ResponseDelays is null)
        {
            errors.Add(new ConfigValidationError(
                "uds.responseDelays",
                "Response delay configuration list is required."));
            return;
        }

        var seenServices = new HashSet<byte>();
        for (var index = 0; index < uds.ResponseDelays.Count; index++)
        {
            var delay = uds.ResponseDelays[index];
            if (!TryParseByteHex(delay.ServiceId, out var serviceId))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.responseDelays[{index}].serviceId",
                    "Response delay serviceId must be a hexadecimal byte such as 0x31."));
            }
            else if (!seenServices.Add(serviceId))
            {
                errors.Add(new ConfigValidationError(
                    $"uds.responseDelays[{index}].serviceId",
                    "Response delay serviceId must be unique."));
            }

            if (delay.ResponsePending is null)
            {
                errors.Add(new ConfigValidationError(
                    $"uds.responseDelays[{index}].responsePending",
                    "ResponsePending configuration is required."));
            }

            if (delay.InitialDelayMs < 0)
            {
                errors.Add(new ConfigValidationError(
                    $"uds.responseDelays[{index}].initialDelayMs",
                    "Initial response delay must be zero or greater."));
            }

            if (delay.FinalDelayMs < 0)
            {
                errors.Add(new ConfigValidationError(
                    $"uds.responseDelays[{index}].finalDelayMs",
                    "Final response delay must be zero or greater."));
            }
        }
    }

    private static void ValidateSessionTiming(
        SessionConfig session,
        int index,
        List<ConfigValidationError> errors)
    {
        if (!TryParseByteHex(session.Identifier, out var subFunction)
            || subFunction is not (0x01 or 0x02 or 0x03))
        {
            errors.Add(new ConfigValidationError(
                $"uds.sessions[{index}].identifier",
                "Session identifier must be 0x01, 0x02, or 0x03."));
        }

        if (session.P2Ms is < 1 or > ushort.MaxValue)
        {
            errors.Add(new ConfigValidationError(
                $"uds.sessions[{index}].p2Ms",
                "Session P2 must be between 1 and 65535 milliseconds when configured."));
        }

        if (session.P2StarMs is < 1 or > ushort.MaxValue)
        {
            errors.Add(new ConfigValidationError(
                $"uds.sessions[{index}].p2StarMs",
                "Session P2* must be between 1 and 65535 milliseconds when configured."));
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

    private static void ValidateRequiredSecurityLevel(
        int? value,
        string field,
        List<ConfigValidationError> errors)
    {
        if (value is < 1 or > 255)
        {
            errors.Add(new ConfigValidationError(
                field,
                "Required SecurityAccess level must be between 1 and 255 when configured."));
        }
    }

    private static bool IsSupportedSecurityAlgorithm(string? algorithm)
    {
        return string.Equals(algorithm, "builtin-xor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(algorithm, "builtin-add", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKnownSessionName(string? value)
    {
        return string.Equals(value, "default", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "programming", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "extended", StringComparison.OrdinalIgnoreCase);
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

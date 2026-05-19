using System.Globalization;
using DoipSimulator.Core.Connections;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Core.Faults;

public sealed class FaultRuntimeState
{
    private readonly object gate = new();
    private readonly IRuntimeEventPublisher eventPublisher;
    private FaultProfile profile;

    public FaultRuntimeState(FaultProfile? initialProfile = null, IRuntimeEventPublisher? eventPublisher = null)
    {
        profile = (initialProfile ?? FaultProfile.Disabled()).Clone();
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
    }

    public FaultRuntimeSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return new FaultRuntimeSnapshot(
                profile.Clone(),
                profile.Enabled && profile.PauseResponses,
                profile.Enabled && profile.CorruptNextDoipHeader.IsActive,
                profile.NextNrc?.Clone(),
                profile.NextCustomResponse?.Clone());
        }
    }

    public async ValueTask<FaultRuntimeSnapshot> UpdateProfileAsync(
        FaultProfile newProfile,
        string source = "api",
        CancellationToken cancellationToken = default)
    {
        FaultRuntimeSnapshot snapshot;
        lock (gate)
        {
            profile = newProfile.Clone();
            snapshot = GetSnapshotUnsafe();
        }

        await PublishFaultEventAsync(
            "fault.profile.updated",
            "Fault profile updated.",
            null,
            new Dictionary<string, object?>
            {
                ["source"] = source,
                ["enabled"] = snapshot.Profile.Enabled,
                ["responseDelayMs"] = snapshot.Profile.ResponseDelayMs,
                ["pauseResponses"] = snapshot.Profile.PauseResponses,
                ["routingActivationFailure"] = snapshot.Profile.RoutingActivationFailure,
                ["hasPendingDoipHeaderFault"] = snapshot.HasPendingDoipHeaderFault,
            },
            cancellationToken);

        return snapshot;
    }

    public bool ShouldFailRoutingActivation()
    {
        lock (gate)
        {
            return profile.Enabled && profile.RoutingActivationFailure;
        }
    }

    public int GetResponseDelayMs()
    {
        lock (gate)
        {
            return profile.Enabled ? profile.ResponseDelayMs : 0;
        }
    }

    public bool ShouldPauseResponses()
    {
        lock (gate)
        {
            return profile.Enabled && profile.PauseResponses;
        }
    }

    public DoipHeaderFaultConfig? TryConsumeDoipHeaderFault()
    {
        lock (gate)
        {
            if (!profile.Enabled || !profile.CorruptNextDoipHeader.IsActive)
            {
                return null;
            }

            var consumed = profile.CorruptNextDoipHeader.Clone();
            profile.CorruptNextDoipHeader = new DoipHeaderFaultConfig();
            return consumed;
        }
    }

    public UdsFaultOverride? TryConsumeUdsOverride(byte serviceId)
    {
        lock (gate)
        {
            if (!profile.Enabled)
            {
                return null;
            }

            if (TryParseByteHex(profile.NextNrc?.ServiceId, out var nrcServiceId)
                && nrcServiceId == serviceId
                && TryParseByteHex(profile.NextNrc?.Nrc, out var nrc))
            {
                var consumed = new UdsFaultOverride(serviceId, nrc, null);
                profile.NextNrc = null;
                return consumed;
            }

            if (TryParseByteHex(profile.NextCustomResponse?.ServiceId, out var responseServiceId)
                && responseServiceId == serviceId
                && TryParseHexBytes(profile.NextCustomResponse?.ResponseBytes, out var responseBytes))
            {
                var consumed = new UdsFaultOverride(serviceId, null, responseBytes);
                profile.NextCustomResponse = null;
                return consumed;
            }

            return null;
        }
    }

    public ValueTask PublishFaultEventAsync(
        string name,
        string message,
        string? connectionId,
        IReadOnlyDictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Warning,
                RuntimeEventCategory.Fault,
                name,
                message,
                connectionId,
                data),
            cancellationToken);
    }

    private FaultRuntimeSnapshot GetSnapshotUnsafe()
    {
        return new FaultRuntimeSnapshot(
            profile.Clone(),
            profile.Enabled && profile.PauseResponses,
            profile.Enabled && profile.CorruptNextDoipHeader.IsActive,
            profile.NextNrc?.Clone(),
            profile.NextCustomResponse?.Clone());
    }

    public static string FormatByte(byte value) => $"0x{value:X2}";

    public static bool TryParseByteHex(string? value, out byte parsed)
    {
        parsed = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value;

        return hex is { Length: > 0 and <= 2 }
            && hex.All(Uri.IsHexDigit)
            && byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed);
    }

    public static bool TryParseHexBytes(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Replace(" ", "", StringComparison.Ordinal);
        if (normalized.Length % 2 != 0 || !normalized.All(Uri.IsHexDigit))
        {
            return false;
        }

        bytes = new byte[normalized.Length / 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = byte.Parse(
                normalized.AsSpan(index * 2, 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
        }

        return true;
    }
}

public sealed record UdsFaultOverride(byte ServiceId, byte? Nrc, byte[]? CustomResponseBytes);

using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Core.Configuration;

public sealed record RoutineRuntimeSnapshot(
    string RoutineId,
    string? Name,
    bool HasStartResponse,
    bool HasStopResponse,
    bool HasRequestResultsResponse,
    IReadOnlyList<string> AllowedSessions,
    string? RequiredSecurityState);

public sealed record CommunicationControlSnapshot(
    string ControlType,
    string CommunicationType,
    DateTimeOffset? LastChangedAt,
    string? LastSource);

public sealed record DtcSettingStateSnapshot(
    bool Enabled,
    string SettingType,
    DateTimeOffset? LastChangedAt,
    string? LastSource);

public sealed record ControlServicesSnapshot(
    IReadOnlyList<RoutineRuntimeSnapshot> Routines,
    CommunicationControlSnapshot CommunicationControl,
    DtcSettingStateSnapshot DtcSetting);

public sealed class ControlServiceStateStore
{
    private readonly Lock gate = new();
    private readonly IRuntimeEventPublisher eventPublisher;
    private CommunicationControlSnapshot communicationControl = new(
        "enableRxAndTx",
        "normalAndNetworkManagementCommunication",
        null,
        null);
    private DtcSettingStateSnapshot dtcSetting = new(true, "on", null, null);

    public ControlServiceStateStore(
        SimulatorConfig config,
        IRuntimeEventPublisher? eventPublisher = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        eventPublisher ??= NullRuntimeEventPublisher.Instance;
        this.eventPublisher = eventPublisher;

        Routines = config.Uds.Routines
            .Where(item => ConfigValidator.TryParseRoutineIdentifier(item, out _))
            .Select(ToRoutineSnapshot)
            .OrderBy(item => item.RoutineId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<RoutineRuntimeSnapshot> Routines { get; }

    public ControlServicesSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return new ControlServicesSnapshot(
                Routines,
                communicationControl,
                dtcSetting);
        }
    }

    public async ValueTask<CommunicationControlSnapshot> ApplyCommunicationControlAsync(
        byte controlType,
        byte communicationType,
        string source,
        CancellationToken cancellationToken = default)
    {
        var snapshot = new CommunicationControlSnapshot(
            FormatCommunicationControlType(controlType),
            FormatCommunicationType(communicationType),
            DateTimeOffset.UtcNow,
            source);

        lock (gate)
        {
            communicationControl = snapshot;
        }

        await PublishStateChangedAsync(
            "uds.communicationControl.changed",
            "CommunicationControl runtime state changed.",
            source,
            new Dictionary<string, object?>
            {
                ["serviceId"] = "0x28",
                ["controlType"] = snapshot.ControlType,
                ["communicationType"] = snapshot.CommunicationType,
            },
            cancellationToken);

        return snapshot;
    }

    public async ValueTask<DtcSettingStateSnapshot> ApplyDtcSettingAsync(
        byte settingType,
        string source,
        CancellationToken cancellationToken = default)
    {
        var snapshot = new DtcSettingStateSnapshot(
            settingType == 0x01,
            settingType == 0x01 ? "on" : "off",
            DateTimeOffset.UtcNow,
            source);

        lock (gate)
        {
            dtcSetting = snapshot;
        }

        await PublishStateChangedAsync(
            "uds.dtcSetting.changed",
            "ControlDTCSetting runtime state changed.",
            source,
            new Dictionary<string, object?>
            {
                ["serviceId"] = "0x85",
                ["settingType"] = snapshot.SettingType,
                ["enabled"] = snapshot.Enabled,
            },
            cancellationToken);

        return snapshot;
    }

    public static bool IsSupportedCommunicationControlType(byte controlType)
    {
        return controlType is 0x00 or 0x01 or 0x02 or 0x03;
    }

    public static bool IsSupportedCommunicationType(byte communicationType)
    {
        return communicationType is 0x01 or 0x02 or 0x03;
    }

    public static bool IsSupportedDtcSettingType(byte settingType)
    {
        return settingType is 0x01 or 0x02;
    }

    public static string FormatCommunicationControlType(byte controlType)
    {
        return controlType switch
        {
            0x00 => "enableRxAndTx",
            0x01 => "enableRxDisableTx",
            0x02 => "disableRxEnableTx",
            0x03 => "disableRxAndTx",
            _ => $"unsupported(0x{controlType:X2})",
        };
    }

    public static string FormatCommunicationType(byte communicationType)
    {
        return communicationType switch
        {
            0x01 => "normalCommunication",
            0x02 => "networkManagementCommunication",
            0x03 => "normalAndNetworkManagementCommunication",
            _ => $"unsupported(0x{communicationType:X2})",
        };
    }

    private ValueTask PublishStateChangedAsync(
        string name,
        string message,
        string source,
        Dictionary<string, object?> data,
        CancellationToken cancellationToken)
    {
        data["source"] = source;
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.State,
                name,
                message,
                data: data),
            cancellationToken);
    }

    private static RoutineRuntimeSnapshot ToRoutineSnapshot(RoutineConfig routine)
    {
        ConfigValidator.TryParseRoutineIdentifier(routine, out var routineId);
        return new RoutineRuntimeSnapshot(
            ConfigValidator.FormatRoutineIdentifier(routineId),
            routine.Name,
            !string.IsNullOrWhiteSpace(routine.FixedResponses.Start),
            !string.IsNullOrWhiteSpace(routine.FixedResponses.Stop),
            !string.IsNullOrWhiteSpace(routine.FixedResponses.RequestResults),
            routine.AllowedSessions.ToArray(),
            routine.RequiredSecurityState);
    }
}

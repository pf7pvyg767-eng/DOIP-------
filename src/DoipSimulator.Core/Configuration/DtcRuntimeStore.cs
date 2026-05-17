using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Core.Configuration;

public sealed record DtcRuntimeSnapshot(
    string Code,
    string? Name,
    string? Description,
    string Status,
    bool Active);

public enum DtcOperationFailure
{
    None,
    UnknownDtc,
    InvalidStatus,
}

public sealed record DtcOperationResult(
    DtcOperationFailure Failure,
    DtcRuntimeSnapshot? Snapshot = null,
    string? Message = null)
{
    public bool Succeeded => Failure == DtcOperationFailure.None;

    public static DtcOperationResult Success(DtcRuntimeSnapshot snapshot) => new(DtcOperationFailure.None, snapshot);
}

public sealed class DtcRuntimeStore
{
    private readonly Lock gate = new();
    private readonly IRuntimeEventPublisher eventPublisher;
    private readonly Dictionary<uint, DtcRuntimeEntry> entries;

    public DtcRuntimeStore(SimulatorConfig config, IRuntimeEventPublisher? eventPublisher = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
        entries = config.Uds.Dtcs
            .Where(item => ConfigValidator.TryParseDtcCode(item.Code, out _)
                && ConfigValidator.TryParseStatusByte(item.Status, out _))
            .Select(item =>
            {
                ConfigValidator.TryParseDtcCode(item.Code, out var code);
                ConfigValidator.TryParseStatusByte(item.Status, out var status);
                return new DtcRuntimeEntry(code, item, status);
            })
            .GroupBy(item => item.Code)
            .ToDictionary(group => group.Key, group => group.Last());
    }

    public IReadOnlyList<DtcRuntimeSnapshot> List()
    {
        lock (gate)
        {
            return entries.Values
                .OrderBy(entry => entry.Code)
                .Select(ToSnapshot)
                .ToArray();
        }
    }

    public IReadOnlyList<DtcRuntimeSnapshot> ListActive(byte statusMask)
    {
        lock (gate)
        {
            return entries.Values
                .Where(entry => entry.Active && (entry.Status & statusMask) != 0)
                .OrderBy(entry => entry.Code)
                .Select(ToSnapshot)
                .ToArray();
        }
    }

    public async ValueTask<DtcOperationResult> ActivateAsync(
        uint code,
        string source,
        string? status = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        DtcRuntimeSnapshot? snapshot = null;
        DtcOperationResult? rejected = null;
        string? rejectedReason = null;
        lock (gate)
        {
            if (!entries.TryGetValue(code, out var entry))
            {
                rejectedReason = "unknown DTC";
                rejected = new DtcOperationResult(DtcOperationFailure.UnknownDtc, Message: "DTC is not configured.");
            }
            else if (!ConfigValidator.TryParseStatusByte(status, out var statusByte))
            {
                rejectedReason = "invalid status";
                rejected = new DtcOperationResult(DtcOperationFailure.InvalidStatus, Message: "DTC status must be a hexadecimal byte.");
            }
            else
            {
                entry.Active = true;
                if (!string.IsNullOrWhiteSpace(status))
                {
                    entry.Status = statusByte;
                }
                else if (entry.Status == 0)
                {
                    entry.Status = 0x2F;
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    entry.DescriptionOverride = description;
                }

                snapshot = ToSnapshot(entry);
            }
        }

        if (rejected is not null)
        {
            await PublishRejectedAsync(code, "activate", source, rejectedReason!, cancellationToken);
            return rejected;
        }

        await PublishChangedAsync(snapshot!, "activate", source, cancellationToken);
        return DtcOperationResult.Success(snapshot!);
    }

    public async ValueTask<DtcOperationResult> ClearAsync(
        uint code,
        string source,
        CancellationToken cancellationToken = default)
    {
        DtcRuntimeSnapshot? snapshot = null;
        DtcOperationResult? rejected = null;
        lock (gate)
        {
            if (!entries.TryGetValue(code, out var entry))
            {
                rejected = new DtcOperationResult(DtcOperationFailure.UnknownDtc, Message: "DTC is not configured.");
            }
            else
            {
                entry.Active = false;
                entry.Status = 0;
                snapshot = ToSnapshot(entry);
            }
        }

        if (rejected is not null)
        {
            await PublishRejectedAsync(code, "clear", source, "unknown DTC", cancellationToken);
            return rejected;
        }

        await PublishChangedAsync(snapshot!, "clear", source, cancellationToken);
        return DtcOperationResult.Success(snapshot!);
    }

    public async ValueTask<IReadOnlyList<DtcRuntimeSnapshot>> ClearAllAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        DtcRuntimeSnapshot[] snapshots;
        lock (gate)
        {
            foreach (var entry in entries.Values)
            {
                entry.Active = false;
                entry.Status = 0;
            }

            snapshots = entries.Values.OrderBy(entry => entry.Code).Select(ToSnapshot).ToArray();
        }

        foreach (var snapshot in snapshots)
        {
            await PublishChangedAsync(snapshot, "clear", source, cancellationToken);
        }

        return snapshots;
    }

    public ValueTask PublishReadAsync(
        string source,
        int returnedCount,
        byte statusMask,
        CancellationToken cancellationToken = default)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Uds,
                "uds.dtc.read",
                "DTC runtime state read.",
                data: new Dictionary<string, object?>
                {
                    ["operation"] = "read",
                    ["source"] = source,
                    ["returnedCount"] = returnedCount,
                    ["statusMask"] = FormatStatus(statusMask),
                }),
            cancellationToken);
    }

    public ValueTask PublishRejectedAsync(
        uint code,
        string operation,
        string source,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Warning,
                RuntimeEventCategory.Uds,
                "uds.dtc.rejected",
                "DTC runtime operation rejected.",
                data: new Dictionary<string, object?>
                {
                    ["dtc"] = FormatDtc(code),
                    ["operation"] = operation,
                    ["source"] = source,
                    ["reason"] = reason,
                }),
            cancellationToken);
    }

    public static string FormatDtc(uint code) => $"0x{code:X6}";

    public static string FormatStatus(byte status) => $"0x{status:X2}";

    private ValueTask PublishChangedAsync(
        DtcRuntimeSnapshot snapshot,
        string operation,
        string source,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Uds,
                "uds.dtc.changed",
                "DTC runtime state changed.",
                data: new Dictionary<string, object?>
                {
                    ["dtc"] = snapshot.Code,
                    ["operation"] = operation,
                    ["source"] = source,
                    ["active"] = snapshot.Active,
                    ["status"] = snapshot.Status,
                }),
            cancellationToken);
    }

    private static DtcRuntimeSnapshot ToSnapshot(DtcRuntimeEntry entry)
    {
        return new DtcRuntimeSnapshot(
            FormatDtc(entry.Code),
            entry.Config.Name,
            entry.DescriptionOverride ?? entry.Config.Description,
            FormatStatus(entry.Status),
            entry.Active);
    }

    private sealed class DtcRuntimeEntry
    {
        public DtcRuntimeEntry(uint code, DtcConfig config, byte status)
        {
            Code = code;
            Config = config;
            Status = status;
            Active = config.Active;
        }

        public uint Code { get; }

        public DtcConfig Config { get; }

        public bool Active { get; set; }

        public byte Status { get; set; }

        public string? DescriptionOverride { get; set; }
    }
}

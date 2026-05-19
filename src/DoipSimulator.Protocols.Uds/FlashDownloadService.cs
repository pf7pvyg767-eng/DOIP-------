using System.Buffers.Binary;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds;

public sealed class RequestDownloadService : IUdsService
{
    public const byte Sid = 0x34;

    private readonly EcuRuntimeState state;
    private readonly FlashConfig flash;
    private readonly IRuntimeEventPublisher eventPublisher;

    public RequestDownloadService(
        EcuRuntimeState state,
        SimulatorConfig config,
        IRuntimeEventPublisher? eventPublisher = null)
    {
        this.state = state;
        flash = config.Uds.Flash ?? new FlashConfig { Enabled = false };
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
    }

    public byte ServiceId => Sid;

    public async ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (!flash.Enabled)
        {
            return await RejectAsync(context, request.OriginalServiceId, NegativeResponseCode.ConditionsNotCorrect, "flash-disabled", cancellationToken);
        }

        if (!IsAllowedSession(state.CurrentSession))
        {
            return await RejectAsync(context, request.OriginalServiceId, NegativeResponseCode.ConditionsNotCorrect, "session-not-allowed", cancellationToken);
        }

        if (flash.SecurityRequired)
        {
            var level = flash.RequiredSecurityLevel ?? 1;
            if (!state.IsSecurityLevelUnlocked(level))
            {
                return await RejectAsync(context, request.OriginalServiceId, NegativeResponseCode.SecurityAccessDenied, "security-locked", cancellationToken);
            }
        }

        if (!TryParseRequest(request.Payload, out var parsed))
        {
            return await RejectAsync(context, request.OriginalServiceId, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat, "invalid-format", cancellationToken);
        }

        if (parsed.MemorySize <= 0 || parsed.MemorySize > flash.MaxMemorySize)
        {
            return await RejectAsync(context, request.OriginalServiceId, NegativeResponseCode.RequestOutOfRange, "size-out-of-range", cancellationToken);
        }

        if (!state.TryStartFlashDownload(
                parsed.MemoryAddress,
                parsed.MemorySize,
                flash.MaxBlockLength,
                parsed.DataFormatIdentifier,
                parsed.AddressAndLengthFormatIdentifier))
        {
            return await RejectAsync(context, request.OriginalServiceId, NegativeResponseCode.ConditionsNotCorrect, "download-already-active", cancellationToken);
        }

        await PublishEventAsync(context, "uds.flash.download.started", "Flash download initialized.", "accepted", null, cancellationToken);
        return [new RawUdsResponse([0x74, 0x20, (byte)(flash.MaxBlockLength >> 8), (byte)(flash.MaxBlockLength & 0xFF)])];
    }

    private static bool TryParseRequest(ReadOnlyMemory<byte> payload, out ParsedDownloadRequest parsed)
    {
        parsed = default;
        var span = payload.Span;
        if (span.Length < 3)
        {
            return false;
        }

        var dataFormatIdentifier = span[0];
        var addressAndLengthFormatIdentifier = span[1];
        if (dataFormatIdentifier != 0x00)
        {
            return false;
        }

        var addressLength = addressAndLengthFormatIdentifier & 0x0F;
        var sizeLength = addressAndLengthFormatIdentifier >> 4;
        if (addressLength != 4 || sizeLength != 4 || span.Length != 2 + addressLength + sizeLength)
        {
            return false;
        }

        var memoryAddress = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(2, 4));
        var memorySize = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(6, 4));
        if (memorySize > int.MaxValue)
        {
            return false;
        }

        parsed = new ParsedDownloadRequest(dataFormatIdentifier, addressAndLengthFormatIdentifier, memoryAddress, (int)memorySize);
        return true;
    }

    private bool IsAllowedSession(DiagnosticSession session)
    {
        foreach (var allowed in flash.AllowedSessions ?? [])
        {
            if (string.Equals(allowed, FormatSession(session), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async ValueTask<IReadOnlyList<UdsResponse>> RejectAsync(
        UdsContext context,
        byte serviceId,
        NegativeResponseCode code,
        string reason,
        CancellationToken cancellationToken)
    {
        await PublishEventAsync(context, "uds.flash.download.rejected", "Flash download request rejected.", "rejected", reason, cancellationToken);
        return [new NegativeResponse(serviceId, code)];
    }

    private ValueTask PublishEventAsync(
        UdsContext context,
        string name,
        string message,
        string outcome,
        string? reason,
        CancellationToken cancellationToken)
    {
        var snapshot = state.GetFlashDownloadSnapshot();
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                reason is null ? RuntimeEventLevel.Info : RuntimeEventLevel.Warning,
                RuntimeEventCategory.Uds,
                name,
                message,
                context.ConnectionId,
                CreateEventData(context, snapshot, outcome, reason)),
            cancellationToken);
    }

    internal static Dictionary<string, object?> CreateEventData(
        UdsContext context,
        FlashDownloadSnapshot snapshot,
        string outcome,
        string? reason)
    {
        return new Dictionary<string, object?>
        {
            ["connectionId"] = context.ConnectionId,
            ["remoteEndpoint"] = context.RemoteEndpoint,
            ["testerLogicalAddress"] = context.TesterLogicalAddress,
            ["ecuLogicalAddress"] = context.EcuLogicalAddress,
            ["outcome"] = outcome,
            ["reason"] = reason,
            ["active"] = snapshot.IsActive,
            ["completed"] = snapshot.IsCompleted,
            ["totalSize"] = snapshot.TotalSize,
            ["receivedSize"] = snapshot.ReceivedSize,
            ["maxBlockLength"] = snapshot.MaxBlockLength,
            ["expectedBlockSequenceCounter"] = snapshot.ExpectedBlockSequenceCounter,
        };
    }

    private static string FormatSession(DiagnosticSession session)
    {
        return session switch
        {
            DiagnosticSession.Default => "default",
            DiagnosticSession.Programming => "programming",
            DiagnosticSession.Extended => "extended",
            _ => session.ToString().ToLowerInvariant(),
        };
    }

    private readonly record struct ParsedDownloadRequest(
        byte DataFormatIdentifier,
        byte AddressAndLengthFormatIdentifier,
        uint MemoryAddress,
        int MemorySize);
}

public sealed class TransferDataService : IUdsService
{
    public const byte Sid = 0x36;

    private readonly EcuRuntimeState state;
    private readonly IRuntimeEventPublisher eventPublisher;

    public TransferDataService(EcuRuntimeState state, IRuntimeEventPublisher? eventPublisher = null)
    {
        this.state = state;
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
    }

    public byte ServiceId => Sid;

    public async ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload.Length < 2)
        {
            return await RejectAsync(context, request.OriginalServiceId, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat, "invalid-format", cancellationToken);
        }

        var blockSequenceCounter = request.Payload[0];
        var dataLength = request.Payload.Length - 1;
        var result = state.AcceptFlashTransferBlock(blockSequenceCounter, dataLength);
        if (result != FlashTransferResult.Accepted)
        {
            var code = result switch
            {
                FlashTransferResult.NoActiveDownload => NegativeResponseCode.ConditionsNotCorrect,
                FlashTransferResult.WrongBlockSequenceCounter => NegativeResponseCode.WrongBlockSequenceCounter,
                FlashTransferResult.InvalidBlockLength => NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat,
                FlashTransferResult.TotalSizeExceeded => NegativeResponseCode.RequestOutOfRange,
                _ => NegativeResponseCode.ConditionsNotCorrect,
            };
            return await RejectAsync(context, request.OriginalServiceId, code, FormatResult(result), cancellationToken);
        }

        await PublishEventAsync(context, "uds.flash.transfer.accepted", "Flash transfer block accepted.", "accepted", null, cancellationToken);
        return [new RawUdsResponse([0x76, blockSequenceCounter])];
    }

    private async ValueTask<IReadOnlyList<UdsResponse>> RejectAsync(
        UdsContext context,
        byte serviceId,
        NegativeResponseCode code,
        string reason,
        CancellationToken cancellationToken)
    {
        await PublishEventAsync(context, "uds.flash.transfer.rejected", "Flash transfer block rejected.", "rejected", reason, cancellationToken);
        return [new NegativeResponse(serviceId, code)];
    }

    private ValueTask PublishEventAsync(
        UdsContext context,
        string name,
        string message,
        string outcome,
        string? reason,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                reason is null ? RuntimeEventLevel.Info : RuntimeEventLevel.Warning,
                RuntimeEventCategory.Uds,
                name,
                message,
                context.ConnectionId,
                RequestDownloadService.CreateEventData(context, state.GetFlashDownloadSnapshot(), outcome, reason)),
            cancellationToken);
    }

    private static string FormatResult(FlashTransferResult result)
    {
        return result.ToString();
    }
}

public sealed class RequestTransferExitService : IUdsService
{
    public const byte Sid = 0x37;

    private readonly EcuRuntimeState state;
    private readonly IRuntimeEventPublisher eventPublisher;

    public RequestTransferExitService(EcuRuntimeState state, IRuntimeEventPublisher? eventPublisher = null)
    {
        this.state = state;
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
    }

    public byte ServiceId => Sid;

    public async ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload.Length != 0)
        {
            return await RejectAsync(context, request.OriginalServiceId, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat, "invalid-format", cancellationToken);
        }

        var result = state.CompleteFlashDownload();
        if (result != FlashTransferExitResult.Completed)
        {
            return await RejectAsync(context, request.OriginalServiceId, NegativeResponseCode.ConditionsNotCorrect, result.ToString(), cancellationToken);
        }

        await PublishEventAsync(context, "uds.flash.transfer_exit.accepted", "Flash download transfer exit accepted.", "accepted", null, cancellationToken);
        state.ClearFlashDownload();
        return [new RawUdsResponse([0x77])];
    }

    private async ValueTask<IReadOnlyList<UdsResponse>> RejectAsync(
        UdsContext context,
        byte serviceId,
        NegativeResponseCode code,
        string reason,
        CancellationToken cancellationToken)
    {
        await PublishEventAsync(context, "uds.flash.transfer_exit.rejected", "Flash download transfer exit rejected.", "rejected", reason, cancellationToken);
        return [new NegativeResponse(serviceId, code)];
    }

    private ValueTask PublishEventAsync(
        UdsContext context,
        string name,
        string message,
        string outcome,
        string? reason,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                reason is null ? RuntimeEventLevel.Info : RuntimeEventLevel.Warning,
                RuntimeEventCategory.Uds,
                name,
                message,
                context.ConnectionId,
                RequestDownloadService.CreateEventData(context, state.GetFlashDownloadSnapshot(), outcome, reason)),
            cancellationToken);
    }
}

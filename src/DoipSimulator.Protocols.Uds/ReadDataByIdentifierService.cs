using System.Globalization;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds;

public sealed class ReadDataByIdentifierService : IUdsService
{
    public const byte Sid = 0x22;

    private readonly IReadOnlyDictionary<ushort, byte[]> fixedDids;
    private readonly IRuntimeEventPublisher eventPublisher;

    public ReadDataByIdentifierService(
        IEnumerable<DidConfig> dids,
        IRuntimeEventPublisher? eventPublisher = null)
    {
        fixedDids = dids
            .Where(did => ConfigValidator.TryParseDidIdentifier(did, out _)
                && string.Equals(did.ValueEncoding, "hex", StringComparison.OrdinalIgnoreCase)
                && TryParseHexBytes(did.Value, out _))
            .Select(did =>
            {
                ConfigValidator.TryParseDidIdentifier(did, out var identifier);
                TryParseHexBytes(did.Value, out var value);
                return new KeyValuePair<ushort, byte[]>(identifier, value!);
            })
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.Last().Value);
        this.eventPublisher = eventPublisher ?? NullRuntimeEventPublisher.Instance;
    }

    public byte ServiceId => Sid;

    public async ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken = default)
    {
        if (request.Payload.Length == 0 || request.Payload.Length % 2 != 0)
        {
            return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat)];
        }

        var requestedDids = ParseRequestedDids(request.Payload);
        foreach (var did in requestedDids)
        {
            if (!fixedDids.ContainsKey(did))
            {
                return [new NegativeResponse(request.OriginalServiceId, NegativeResponseCode.RequestOutOfRange)];
            }
        }

        var responseBytes = new List<byte> { 0x62 };
        for (var index = 0; index < requestedDids.Count; index++)
        {
            var did = requestedDids[index];
            var value = fixedDids[did];
            responseBytes.Add((byte)(did >> 8));
            responseBytes.Add((byte)(did & 0xFF));
            responseBytes.AddRange(value);
            await PublishDidReadAsync(context, did, 2 + value.Length, index, cancellationToken);
        }

        return [new RawUdsResponse([.. responseBytes])];
    }

    private static List<ushort> ParseRequestedDids(byte[] payload)
    {
        var dids = new List<ushort>(payload.Length / 2);
        for (var index = 0; index < payload.Length; index += 2)
        {
            dids.Add((ushort)((payload[index] << 8) | payload[index + 1]));
        }

        return dids;
    }

    private ValueTask PublishDidReadAsync(
        UdsContext context,
        ushort did,
        int responseLength,
        int requestIndex,
        CancellationToken cancellationToken)
    {
        return eventPublisher.PublishAsync(
            RuntimeEvent.Create(
                RuntimeEventLevel.Info,
                RuntimeEventCategory.Uds,
                "uds.did.read",
                "UDS DID read completed.",
                context.ConnectionId,
                new Dictionary<string, object?>
                {
                    ["connectionId"] = context.ConnectionId,
                    ["remoteEndpoint"] = context.RemoteEndpoint,
                    ["testerLogicalAddress"] = context.TesterLogicalAddress,
                    ["ecuLogicalAddress"] = context.EcuLogicalAddress,
                    ["did"] = FormatDid(did),
                    ["didId"] = FormatDid(did),
                    ["responseLength"] = responseLength,
                    ["requestIndex"] = requestIndex,
                }),
            cancellationToken);
    }

    private static bool TryParseHexBytes(string? value, out byte[]? bytes)
    {
        bytes = null;
        if (string.IsNullOrWhiteSpace(value) || value.Length % 2 != 0 || !value.All(Uri.IsHexDigit))
        {
            return false;
        }

        bytes = new byte[value.Length / 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = byte.Parse(
                value.AsSpan(index * 2, 2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
        }

        return true;
    }

    private static string FormatDid(ushort did) => $"0x{did:X4}";
}

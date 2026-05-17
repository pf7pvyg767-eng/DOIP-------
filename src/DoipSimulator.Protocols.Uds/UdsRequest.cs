namespace DoipSimulator.Protocols.Uds;

public sealed record UdsRequest(byte ServiceId, byte[] Payload)
{
    public byte OriginalServiceId => ServiceId;

    public static bool TryCreate(ReadOnlySpan<byte> bytes, out UdsRequest? request)
    {
        if (bytes.IsEmpty)
        {
            request = null;
            return false;
        }

        request = new UdsRequest(bytes[0], bytes[1..].ToArray());
        return true;
    }
}

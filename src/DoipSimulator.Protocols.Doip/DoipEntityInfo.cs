using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace DoipSimulator.Protocols.Doip;

public sealed record DoipEntityInfo(
    string Vin,
    byte[] Eid,
    byte[] Gid,
    ushort LogicalAddress)
{
    public static DoipEntityInfo Create(string vin, string eidHex, string gidHex, string logicalAddressHex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vin);

        return new DoipEntityInfo(
            vin,
            ParseHexBytes(eidHex, 6, nameof(eidHex)),
            ParseHexBytes(gidHex, 6, nameof(gidHex)),
            ParseUInt16Hex(logicalAddressHex, nameof(logicalAddressHex)));
    }

    public byte[] EncodeVehicleIdentificationPayload()
    {
        var payload = new byte[32];
        Encoding.ASCII.GetBytes(Vin.AsSpan(), payload.AsSpan(0, 17));
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(17, 2), LogicalAddress);
        Eid.CopyTo(payload.AsSpan(19, 6));
        Gid.CopyTo(payload.AsSpan(25, 6));
        payload[31] = 0x00;
        return payload;
    }

    private static byte[] ParseHexBytes(string value, int expectedLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != expectedLength * 2)
        {
            throw new ArgumentException($"Expected {expectedLength * 2} hexadecimal characters.", parameterName);
        }

        var bytes = new byte[expectedLength];
        for (var index = 0; index < expectedLength; index++)
        {
            bytes[index] = byte.Parse(value.AsSpan(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return bytes;
    }

    private static ushort ParseUInt16Hex(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return ushort.Parse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}

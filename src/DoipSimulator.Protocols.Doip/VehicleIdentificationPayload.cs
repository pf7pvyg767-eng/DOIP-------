using System.Buffers.Binary;
using System.Text;

namespace DoipSimulator.Protocols.Doip;

public sealed record VehicleIdentificationPayload(
    string Vin,
    ushort LogicalAddress,
    byte[] Eid,
    byte[] Gid,
    byte FurtherActionRequired)
{
    public static VehicleIdentificationPayload Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != 32)
        {
            throw new ArgumentException("Vehicle identification payload must be exactly 32 bytes.", nameof(payload));
        }

        return new VehicleIdentificationPayload(
            Encoding.ASCII.GetString(payload[..17]),
            BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(17, 2)),
            payload.Slice(19, 6).ToArray(),
            payload.Slice(25, 6).ToArray(),
            payload[31]);
    }
}

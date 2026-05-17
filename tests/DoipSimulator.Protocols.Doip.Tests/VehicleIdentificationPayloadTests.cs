using DoipSimulator.Protocols.Doip;

namespace DoipSimulator.Protocols.Doip.Tests;

public class VehicleIdentificationPayloadTests
{
    [Fact]
    public void EntityInfoEncodesVehicleIdentificationPayloadFromConfigurationValues()
    {
        var entity = DoipEntityInfo.Create(
            "LTEST123456789012",
            "010203040506",
            "A1A2A3A4A5A6",
            "0x0E01");

        var payload = VehicleIdentificationPayload.Decode(entity.EncodeVehicleIdentificationPayload());

        Assert.Equal("LTEST123456789012", payload.Vin);
        Assert.Equal(0x0E01, payload.LogicalAddress);
        Assert.Equal([0x01, 0x02, 0x03, 0x04, 0x05, 0x06], payload.Eid);
        Assert.Equal([0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6], payload.Gid);
        Assert.Equal(0x00, payload.FurtherActionRequired);
    }
}

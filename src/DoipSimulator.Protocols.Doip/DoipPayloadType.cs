namespace DoipSimulator.Protocols.Doip;

public readonly record struct DoipPayloadType(ushort Value)
{
    public static readonly DoipPayloadType GenericDoipHeaderNegativeAcknowledge = new(0x0000);
    public static readonly DoipPayloadType VehicleIdentificationRequest = new(0x0001);
    public static readonly DoipPayloadType VehicleIdentificationRequestWithEid = new(0x0002);
    public static readonly DoipPayloadType VehicleIdentificationRequestWithVin = new(0x0003);
    public static readonly DoipPayloadType VehicleAnnouncementMessage = new(0x0004);
    public static readonly DoipPayloadType RoutingActivationRequest = new(0x0005);
    public static readonly DoipPayloadType RoutingActivationResponse = new(0x0006);
    public static readonly DoipPayloadType AliveCheckRequest = new(0x0007);
    public static readonly DoipPayloadType AliveCheckResponse = new(0x0008);
    public static readonly DoipPayloadType DoipEntityStatusRequest = new(0x4001);
    public static readonly DoipPayloadType DoipEntityStatusResponse = new(0x4002);
    public static readonly DoipPayloadType DiagnosticPowerModeInformationRequest = new(0x4003);
    public static readonly DoipPayloadType DiagnosticPowerModeInformationResponse = new(0x4004);
    public static readonly DoipPayloadType DiagnosticMessage = new(0x8001);
    public static readonly DoipPayloadType DiagnosticMessagePositiveAcknowledge = new(0x8002);
    public static readonly DoipPayloadType DiagnosticMessageNegativeAcknowledge = new(0x8003);

    private static readonly IReadOnlyDictionary<ushort, string> KnownNames = new Dictionary<ushort, string>
    {
        [GenericDoipHeaderNegativeAcknowledge.Value] = nameof(GenericDoipHeaderNegativeAcknowledge),
        [VehicleIdentificationRequest.Value] = nameof(VehicleIdentificationRequest),
        [VehicleIdentificationRequestWithEid.Value] = nameof(VehicleIdentificationRequestWithEid),
        [VehicleIdentificationRequestWithVin.Value] = nameof(VehicleIdentificationRequestWithVin),
        [VehicleAnnouncementMessage.Value] = nameof(VehicleAnnouncementMessage),
        [RoutingActivationRequest.Value] = nameof(RoutingActivationRequest),
        [RoutingActivationResponse.Value] = nameof(RoutingActivationResponse),
        [AliveCheckRequest.Value] = nameof(AliveCheckRequest),
        [AliveCheckResponse.Value] = nameof(AliveCheckResponse),
        [DoipEntityStatusRequest.Value] = nameof(DoipEntityStatusRequest),
        [DoipEntityStatusResponse.Value] = nameof(DoipEntityStatusResponse),
        [DiagnosticPowerModeInformationRequest.Value] = nameof(DiagnosticPowerModeInformationRequest),
        [DiagnosticPowerModeInformationResponse.Value] = nameof(DiagnosticPowerModeInformationResponse),
        [DiagnosticMessage.Value] = nameof(DiagnosticMessage),
        [DiagnosticMessagePositiveAcknowledge.Value] = nameof(DiagnosticMessagePositiveAcknowledge),
        [DiagnosticMessageNegativeAcknowledge.Value] = nameof(DiagnosticMessageNegativeAcknowledge)
    };

    public bool IsKnown => KnownNames.ContainsKey(Value);

    public string? KnownName => KnownNames.GetValueOrDefault(Value);

    public static DoipPayloadType FromRawValue(ushort value) => new(value);

    public override string ToString() => KnownName is { } name ? $"{name} (0x{Value:X4})" : $"Unknown (0x{Value:X4})";
}

namespace DoipSimulator.Core.Configuration;

public sealed class SimulatorConfig
{
    public EntityConfig Entity { get; set; } = new();

    public NetworkConfig Network { get; set; } = new();

    public UdsConfig Uds { get; set; } = new();

    public TlsConfig Tls { get; set; } = new();

    public static SimulatorConfig CreateDefault()
    {
        return new SimulatorConfig
        {
            Entity = new EntityConfig
            {
                Vin = "LTEST000000000001",
                Eid = "001122334455",
                Gid = "AABBCCDDEEFF",
                LogicalAddress = "0x0E00",
            },
            Network = new NetworkConfig
            {
                BindAddress = "0.0.0.0",
                DoipUdpPort = 13400,
                DoipTcpPort = 13400,
                DoipTlsPort = 3496,
                VehicleAnnouncementEnabled = false,
                VehicleAnnouncementIntervalMilliseconds = 1000,
                VehicleAnnouncementTargetAddress = "255.255.255.255",
                VehicleAnnouncementTargetPort = 13400,
                TcpConnectionIdleTimeoutMilliseconds = 30000,
                SourceAddressWhitelist = ["0x0E80"],
            },
            Uds = new UdsConfig
            {
                Dids =
                [
                    new DidConfig
                    {
                        Identifier = "0xF190",
                        Name = "VIN",
                        ValueEncoding = "hex",
                        Value = "4C54455354303030303030303030303031",
                        Writable = true,
                        WriteLength = 17,
                        AllowedWriteSessions = ["default", "extended"],
                    },
                ],
                Dtcs = [],
                Routines =
                [
                    new RoutineConfig
                    {
                        Identifier = "0x0201",
                        Name = "Erase memory preparation",
                        FixedResponses = new RoutineFixedResponses
                        {
                            Start = "0000",
                            Stop = "0000",
                            RequestResults = "0001",
                        },
                    },
                ],
                Sessions = [],
                SecurityAccess = [],
                Flash = null,
            },
            Tls = new TlsConfig
            {
                Enabled = false,
                ServerCertificatePath = null,
                ServerPrivateKeyPath = null,
                ClientCaPath = null,
                RequireClientCertificate = false,
            },
        };
    }
}

public sealed class EntityConfig
{
    public string Vin { get; set; } = string.Empty;

    public string Eid { get; set; } = string.Empty;

    public string Gid { get; set; } = string.Empty;

    public string LogicalAddress { get; set; } = string.Empty;
}

public sealed class NetworkConfig
{
    public string BindAddress { get; set; } = "0.0.0.0";

    public int DoipUdpPort { get; set; }

    public int DoipTcpPort { get; set; }

    public int DoipTlsPort { get; set; }

    public bool VehicleAnnouncementEnabled { get; set; }

    public int VehicleAnnouncementIntervalMilliseconds { get; set; } = 1000;

    public string VehicleAnnouncementTargetAddress { get; set; } = "255.255.255.255";

    public int VehicleAnnouncementTargetPort { get; set; } = 13400;

    public int TcpConnectionIdleTimeoutMilliseconds { get; set; } = 30000;

    public List<string> SourceAddressWhitelist { get; set; } = [];
}

public sealed class UdsConfig
{
    public List<DidConfig> Dids { get; set; } = [];

    public List<DtcConfig> Dtcs { get; set; } = [];

    public List<RoutineConfig> Routines { get; set; } = [];

    public List<SessionConfig> Sessions { get; set; } = [];

    public List<SecurityAccessConfig> SecurityAccess { get; set; } = [];

    public FlashConfig? Flash { get; set; }
}

public sealed class DidConfig
{
    public string? Id { get; set; }

    public string Identifier { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string ValueEncoding { get; set; } = "hex";

    public string? Value { get; set; }

    public bool Writable { get; set; }

    public int? WriteLength { get; set; }

    public List<string> AllowedWriteSessions { get; set; } = [];

    public string? RequiredSecurityState { get; set; }
}

public sealed class DtcConfig
{
    public string Code { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Status { get; set; } = "0x00";

    public bool Active { get; set; }
}

public sealed class RoutineConfig
{
    public string? RoutineId { get; set; }

    public string Identifier { get; set; } = string.Empty;

    public string? Name { get; set; }

    public List<string> AllowedSessions { get; set; } = [];

    public string? RequiredSecurityState { get; set; }

    public RoutineFixedResponses FixedResponses { get; set; } = new();
}

public sealed class RoutineFixedResponses
{
    public string? Start { get; set; }

    public string? Stop { get; set; }

    public string? RequestResults { get; set; }
}

public sealed class SessionConfig
{
    public string Identifier { get; set; } = string.Empty;

    public string? Name { get; set; }
}

public sealed class SecurityAccessConfig
{
    public string Level { get; set; } = string.Empty;

    public string? Name { get; set; }
}

public sealed class FlashConfig
{
    public bool Enabled { get; set; }

    public string? WorkingDirectory { get; set; }
}

public sealed class TlsConfig
{
    public bool Enabled { get; set; }

    public string? ServerCertificatePath { get; set; }

    public string? ServerPrivateKeyPath { get; set; }

    public string? ClientCaPath { get; set; }

    public bool RequireClientCertificate { get; set; }
}

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
                Dids = [],
                Dtcs = [],
                Routines = [],
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
    public string Identifier { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Value { get; set; }
}

public sealed class DtcConfig
{
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Status { get; set; }
}

public sealed class RoutineConfig
{
    public string Identifier { get; set; } = string.Empty;

    public string? Name { get; set; }
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

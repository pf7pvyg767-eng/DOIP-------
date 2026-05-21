using DoipSimulator.Core.Configuration;

namespace DoipSimulator.Core.Tests.Configuration;

public class ConfigTests
{
    [Fact]
    public async Task MissingConfigurationReturnsDefaultThatValidates()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "simulator.json");
        var store = new ConfigStore();

        var config = await store.LoadAsync(path);
        var validation = ConfigValidator.Validate(config);

        Assert.True(validation.IsValid);
        Assert.Equal("LTEST000000000001", config.Entity.Vin);
        Assert.Equal("0x0E00", config.Entity.LogicalAddress);
        var did = Assert.Single(config.Uds.Dids);
        Assert.Equal("0xF190", did.Identifier);
        Assert.Equal("hex", did.ValueEncoding);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task ValidJsonLoadsIntoStronglyTypedConfiguration()
    {
        var path = CreateTempConfigPath();
        await File.WriteAllTextAsync(
            path,
            """
            {
              "entity": {
                "vin": "LTEST000000000002",
                "eid": "001122334456",
                "gid": "AABBCCDDEEF0",
                "logicalAddress": "0x0E01"
              },
              "network": {
                "bindAddress": "127.0.0.1",
                "doipUdpPort": 13401,
                "doipTcpPort": 13402,
                "doipTlsPort": 3497,
                "sourceAddressWhitelist": ["0x0E81"]
              },
              "uds": {
                "dids": [
                  {
                    "identifier": "0xF190",
                    "name": "VIN",
                    "valueEncoding": "hex",
                    "value": "4C54455354303030303030303030303032"
                  }
                ],
                "dtcs": [],
                "routines": [],
                "sessions": [],
                "securityAccess": [],
                "flash": {
                  "enabled": true,
                  "maxMemorySize": 4096,
                  "maxBlockLength": 256,
                  "allowedSessions": ["programming"],
                  "securityRequired": false
                }
              },
              "tls": {
                "enabled": true,
                "serverCertificatePath": "server.crt",
                "serverPrivateKeyPath": "server.key",
                "clientCaPath": "ca.crt",
                "requireClientCertificate": true
              }
            }
            """);

        var config = await new ConfigStore().LoadAsync(path);

        Assert.Equal("LTEST000000000002", config.Entity.Vin);
        Assert.Equal("127.0.0.1", config.Network.BindAddress);
        var did = Assert.Single(config.Uds.Dids);
        Assert.Equal("VIN", did.Name);
        Assert.Equal("hex", did.ValueEncoding);
        Assert.True(config.Uds.Flash!.Enabled);
        Assert.True(config.Tls.Enabled);
        Assert.True(config.Tls.RequireClientCertificate);
    }

    [Fact]
    public void InvalidFieldsReturnClearFieldSpecificErrors()
    {
        var config = SimulatorConfig.CreateDefault();
        config.Entity.Vin = "BAD";
        config.Entity.LogicalAddress = "0x10000";
        config.Network.DoipTcpPort = 70000;

        var validation = ConfigValidator.Validate(config);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Field == "entity.vin");
        Assert.Contains(validation.Errors, error => error.Field == "entity.logicalAddress");
        Assert.Contains(validation.Errors, error => error.Field == "network.doipTcpPort");
    }

    [Fact]
    public void EidGidAndSourceWhitelistReturnFieldSpecificErrors()
    {
        var config = SimulatorConfig.CreateDefault();
        config.Entity.Eid = "001122";
        config.Entity.Gid = "not-hex-value";
        config.Network.SourceAddressWhitelist = ["0x0E80", "invalid"];

        var validation = ConfigValidator.Validate(config);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Field == "entity.eid");
        Assert.Contains(validation.Errors, error => error.Field == "entity.gid");
        Assert.Contains(validation.Errors, error => error.Field == "network.sourceAddressWhitelist[1]");
    }

    [Fact]
    public void DidValidationReturnsFieldSpecificErrors()
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dids =
        [
            new DidConfig
            {
                Identifier = "0x10000",
                ValueEncoding = "hex",
                Value = "00",
            },
            new DidConfig
            {
                Identifier = "0xF190",
                ValueEncoding = "hex",
                Value = "ABC",
            },
            new DidConfig
            {
                Identifier = "0xF191",
                ValueEncoding = "expression",
                Value = "00",
            },
        ];

        var validation = ConfigValidator.Validate(config);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Field == "uds.dids[0].identifier");
        Assert.Contains(validation.Errors, error => error.Field == "uds.dids[1].value");
        Assert.Contains(validation.Errors, error => error.Field == "uds.dids[2].valueEncoding");
    }

    [Fact]
    public async Task DidValueProviderLoadsSavesAndValidates()
    {
        var path = CreateTempConfigPath();
        var store = new ConfigStore();
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dids =
        [
            new DidConfig
            {
                Identifier = "0xF192",
                Name = "Dynamic random byte",
                ValueProvider = new DidValueProviderConfig
                {
                    Type = "random",
                    NumericType = "uint8",
                    Min = 10,
                    Max = 20,
                    Seed = 1234,
                },
            },
        ];

        await store.SaveAsync(path, config);
        var reloaded = await store.LoadAsync(path);
        var validation = ConfigValidator.Validate(reloaded);

        Assert.True(validation.IsValid);
        var provider = Assert.Single(reloaded.Uds.Dids).ValueProvider;
        Assert.NotNull(provider);
        Assert.Equal("random", provider!.Type);
        Assert.Equal("uint8", provider.NumericType);
        Assert.Equal(10, provider.Min);
        Assert.Equal(20, provider.Max);
        Assert.Equal(1234, provider.Seed);
    }

    [Fact]
    public async Task SampleConfigurationContainsValidDynamicDidExamples()
    {
        var samplePath = FindRepoFile("sample-config/default.simulator.json");

        var config = await new ConfigStore().LoadAsync(samplePath);
        var validation = ConfigValidator.Validate(config);
        var dynamicDids = config.Uds.Dids
            .Where(did => did.ValueProvider is not null && !ConfigValidator.IsStaticDid(did))
            .ToArray();

        Assert.True(validation.IsValid);
        Assert.True(dynamicDids.Length >= 2);
    }


    [Fact]
    public void DidValueProviderValidationReturnsFieldSpecificErrors()
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dids =
        [
            new DidConfig
            {
                Identifier = "0xF192",
                ValueProvider = new DidValueProviderConfig
                {
                    Type = "random",
                    NumericType = "uint8",
                    Min = 20,
                    Max = 10,
                },
            },
            new DidConfig
            {
                Identifier = "0xF193",
                ValueProvider = new DidValueProviderConfig
                {
                    Type = "sine",
                    NumericType = "uint8",
                    Amplitude = 10,
                    Offset = 20,
                    PeriodMs = 0,
                },
            },
            new DidConfig
            {
                Identifier = "0xF194",
                Writable = true,
                ValueProvider = new DidValueProviderConfig
                {
                    Type = "linear",
                    NumericType = "uint16",
                    Offset = 1,
                    SlopePerSecond = 2,
                },
            },
            new DidConfig
            {
                Identifier = "0xF195",
                ValueProvider = new DidValueProviderConfig
                {
                    Type = "linear",
                    NumericType = "float32",
                    Offset = 1,
                    SlopePerSecond = 2,
                },
            },
        ];

        var validation = ConfigValidator.Validate(config);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Field == "uds.dids[0].valueProvider.max");
        Assert.Contains(validation.Errors, error => error.Field == "uds.dids[1].valueProvider.periodMs");
        Assert.Contains(validation.Errors, error => error.Field == "uds.dids[2].writable");
        Assert.Contains(validation.Errors, error => error.Field == "uds.dids[3].valueProvider.numericType");
    }

    [Fact]
    public void SecurityAccessValidationReturnsFieldSpecificErrors()
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.SecurityAccess =
        [
            new SecurityAccessConfig
            {
                Level = 1,
                SeedSubFunction = "0x01",
                KeySubFunction = "0x02",
                Algorithm = "builtin-xor",
                AlgorithmParameter = "A5",
                MaxFailedAttempts = 3,
                LockoutMs = 1000,
            },
            new SecurityAccessConfig
            {
                Level = 1,
                SeedSubFunction = "0x01",
                KeySubFunction = "0x01",
                Algorithm = "external-dll",
                AlgorithmParameter = "not-hex",
                MaxFailedAttempts = 0,
                LockoutMs = -1,
            },
        ];

        var validation = ConfigValidator.Validate(config);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Field == "uds.securityAccess[1].level");
        Assert.Contains(validation.Errors, error => error.Field == "uds.securityAccess[1].seedSubFunction");
        Assert.Contains(validation.Errors, error => error.Field == "uds.securityAccess[1].keySubFunction");
        Assert.Contains(validation.Errors, error => error.Field == "uds.securityAccess[1].algorithm");
        Assert.Contains(validation.Errors, error => error.Field == "uds.securityAccess[1].algorithmParameter");
        Assert.Contains(validation.Errors, error => error.Field == "uds.securityAccess[1].maxFailedAttempts");
        Assert.Contains(validation.Errors, error => error.Field == "uds.securityAccess[1].lockoutMs");
    }

    [Fact]
    public void SecurityPluginDisabledDoesNotRequireDllPath()
    {
        var config = SimulatorConfig.CreateDefault();
        config.SecurityPlugin.Enabled = false;
        config.SecurityPlugin.DllPath = null;
        config.SecurityPlugin.TimeoutMs = 500;

        var result = ConfigValidator.Validate(config);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SecurityPluginEnabledRequiresDllPathAndPositiveTimeout()
    {
        var config = SimulatorConfig.CreateDefault();
        config.SecurityPlugin.Enabled = true;
        config.SecurityPlugin.DllPath = "";
        config.SecurityPlugin.TimeoutMs = 0;

        var result = ConfigValidator.Validate(config);

        Assert.Contains(result.Errors, error => error.Field == "securityPlugin.dllPath");
        Assert.Contains(result.Errors, error => error.Field == "securityPlugin.timeoutMs");
    }

    [Fact]
    public void TimingValidationReturnsFieldSpecificErrors()
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Sessions =
        [
            new SessionConfig
            {
                Identifier = "0x10",
                P2Ms = 0,
                P2StarMs = -1,
            },
        ];
        config.Uds.TesterPresentTimeout.TimeoutMs = 0;
        config.Uds.ResponseDelays =
        [
            new ServiceResponseDelayConfig
            {
                ServiceId = "0x100",
                InitialDelayMs = -1,
                FinalDelayMs = -2,
            },
        ];

        var validation = ConfigValidator.Validate(config);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Field == "uds.sessions[0].identifier");
        Assert.Contains(validation.Errors, error => error.Field == "uds.sessions[0].p2Ms");
        Assert.Contains(validation.Errors, error => error.Field == "uds.sessions[0].p2StarMs");
        Assert.Contains(validation.Errors, error => error.Field == "uds.testerPresentTimeout.timeoutMs");
        Assert.Contains(validation.Errors, error => error.Field == "uds.responseDelays[0].serviceId");
        Assert.Contains(validation.Errors, error => error.Field == "uds.responseDelays[0].initialDelayMs");
        Assert.Contains(validation.Errors, error => error.Field == "uds.responseDelays[0].finalDelayMs");
    }

    [Fact]
    public void DtcValidationReturnsFieldSpecificErrors()
    {
        var config = SimulatorConfig.CreateDefault();
        config.Uds.Dtcs =
        [
            new DtcConfig { Code = "0x12345G", Status = "0x2F" },
            new DtcConfig { Code = "0x123456", Status = "0x100" },
            new DtcConfig { Code = "123456", Status = "0x2F" },
            new DtcConfig { Code = "0x123456", Status = "0x2F" },
        ];

        var validation = ConfigValidator.Validate(config);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Field == "uds.dtcs[0].code");
        Assert.Contains(validation.Errors, error => error.Field == "uds.dtcs[1].status");
        Assert.Contains(validation.Errors, error => error.Field == "uds.dtcs[3].code");
    }

    [Fact]
    public async Task DidConfigSupportsIdAliasWhenLoading()
    {
        var path = CreateTempConfigPath();
        await File.WriteAllTextAsync(
            path,
            """
            {
              "entity": {
                "vin": "LTEST000000000004",
                "eid": "001122334457",
                "gid": "AABBCCDDEEF1",
                "logicalAddress": "0x0E04"
              },
              "network": {
                "bindAddress": "127.0.0.1",
                "doipUdpPort": 13401,
                "doipTcpPort": 13402,
                "doipTlsPort": 3497,
                "sourceAddressWhitelist": ["0x0E81"]
              },
              "uds": {
                "dids": [
                  {
                    "id": "0xF191",
                    "name": "Compatibility DID",
                    "valueEncoding": "hex",
                    "value": "0102"
                  }
                ],
                "dtcs": [],
                "routines": [],
                "sessions": [],
                "securityAccess": [],
                "flash": null
              },
              "tls": {
                "enabled": false
              }
            }
            """);

        var config = await new ConfigStore().LoadAsync(path);

        var did = Assert.Single(config.Uds.Dids);
        Assert.Equal("0xF191", did.Id);
        Assert.Equal("Compatibility DID", did.Name);
    }

    [Fact]
    public async Task SaveAndReloadPreservesConfigurationData()
    {
        var path = CreateTempConfigPath();
        var store = new ConfigStore();
        var config = SimulatorConfig.CreateDefault();
        config.Entity.Vin = "LTEST000000000003";
        config.Entity.Eid = "101122334455";
        config.Entity.Gid = "BABBCCDDEEFF";
        config.Entity.LogicalAddress = "0x0E02";
        config.Network.BindAddress = "127.0.0.1";
        config.Network.DoipUdpPort = 13410;
        config.Network.DoipTcpPort = 13411;
        config.Network.DoipTlsPort = 3500;
        config.Network.SourceAddressWhitelist = ["0x0E82", "0x0E83"];
        config.Uds.Dids.Clear();
        config.Uds.Dids.Add(new DidConfig
        {
            Identifier = "0xF190",
            Name = "VIN",
            ValueEncoding = "hex",
            Value = "4C54455354303030303030303030303033",
        });
        config.Uds.Flash = new FlashConfig
        {
            Enabled = true,
            MaxMemorySize = 4096,
            MaxBlockLength = 256,
            AllowedSessions = ["programming"],
            SecurityRequired = false,
        };
        config.Tls.Enabled = true;
        config.Tls.ServerCertificatePath = "cert.pem";
        config.Tls.ServerPrivateKeyPath = "key.pem";
        config.Tls.ClientCaPath = "ca.pem";
        config.Tls.RequireClientCertificate = true;

        await store.SaveAsync(path, config);
        var reloaded = await store.LoadAsync(path);

        Assert.Equal(config.Entity.Vin, reloaded.Entity.Vin);
        Assert.Equal(config.Entity.LogicalAddress, reloaded.Entity.LogicalAddress);
        Assert.Equal(config.Network.DoipTcpPort, reloaded.Network.DoipTcpPort);
        Assert.Equal(config.Network.SourceAddressWhitelist, reloaded.Network.SourceAddressWhitelist);
        Assert.Single(reloaded.Uds.Dids);
        Assert.Equal("VIN", reloaded.Uds.Dids[0].Name);
        Assert.Equal("hex", reloaded.Uds.Dids[0].ValueEncoding);
        Assert.True(reloaded.Uds.Flash!.Enabled);
        Assert.Equal(4096, reloaded.Uds.Flash.MaxMemorySize);
        Assert.Equal("cert.pem", reloaded.Tls.ServerCertificatePath);
        Assert.True(reloaded.Tls.RequireClientCertificate);
    }

    private static string CreateTempConfigPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "simulator.json");
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file '{relativePath}'.");
    }
}

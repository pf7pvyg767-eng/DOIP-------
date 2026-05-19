using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.Ecu;
using DoipSimulator.Core.Odx;
using DoipSimulator.Protocols.Uds;
using DoipSimulator.WebApi;
using Microsoft.AspNetCore.Builder;

namespace DoipSimulator.Core.Tests.Odx;

public class OdxImportServiceTests
{
    [Fact]
    public async Task OdxImportParsesEcuInfoDidsAndSkippedFields()
    {
        await using var stream = ToStream(SampleOdx());

        var operation = await new OdxImportService().ImportAsync(stream);

        Assert.True(operation.Report.Success);
        Assert.True(operation.Report.Imported.EntityInfo);
        Assert.Equal(2, operation.Report.Imported.Dids);
        Assert.Contains(operation.Report.Skipped, item => item.Path.Contains("COMPU-METHOD", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("LTEST000000000002", operation.Result.EntityInfo.Vin);
        Assert.Equal("0xF191", operation.Result.Dids[0].Identifier);
        Assert.Equal("1234", operation.Result.Dids[0].Value);
    }

    [Fact]
    public async Task MalformedOdxReturnsFailedReport()
    {
        await using var stream = ToStream("<ODX><BROKEN>");

        var operation = await new OdxImportService().ImportAsync(stream);

        Assert.False(operation.Report.Success);
        Assert.NotEmpty(operation.Report.Errors);
    }

    [Fact]
    public async Task PdxReaderIdentifiesOdxEntryAndSkipsOtherResources()
    {
        await using var stream = CreatePdx(("index.odx", SampleOdx()), ("notes.txt", "ignored"));

        var operation = await new PdxPackageReader().ImportAsync(stream);

        Assert.True(operation.Report.Success);
        Assert.True(operation.Report.Imported.EntityInfo);
        Assert.Contains(operation.Report.Skipped, item => item.Path == "notes.txt");
    }

    [Fact]
    public async Task PdxReaderRejectsMissingEntry()
    {
        await using var stream = CreatePdx(("notes.txt", "ignored"));

        var operation = await new PdxPackageReader().ImportAsync(stream);

        Assert.False(operation.Report.Success);
        Assert.Contains(operation.Report.Errors, item => item.Message.Contains("does not contain an ODX entry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MergeAndSavePersistsImportedDidAndRuntimeReadUsesExistingUdsPath()
    {
        var configPath = CreateTempConfigPath();
        var config = SimulatorConfig.CreateDefault();
        var configStore = new ConfigStore();
        await configStore.SaveAsync(configPath, config);
        var didStore = new DidRuntimeStore(config, configPath, configStore);
        await using var stream = ToStream(SampleOdx());
        var operation = await new OdxImportService().ImportAsync(stream);

        await new OdxImportMerger().MergeAndSaveAsync(config, configPath, configStore, operation, didStore);
        var reloaded = await configStore.LoadAsync(configPath);
        var readService = new ReadDataByIdentifierService(didStore, new EcuRuntimeState(0x0E00));
        var response = await readService.HandleAsync(new UdsRequest(0x22, [0xF1, 0x91]), new UdsContext());

        Assert.True(operation.Report.Saved);
        Assert.Contains(reloaded.Uds.Dids, did => did.Identifier == "0xF191" && did.Value == "1234");
        Assert.Equal([0x62, 0xF1, 0x91, 0x12, 0x34], Assert.Single(response).ToBytes());
    }

    [Fact]
    public async Task ApiUploadsOdxAndPersistsReport()
    {
        await using var app = CreateApp(out var baseAddress, out var configPath);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(SampleOdx())), "file", "sample.odx");

            var response = await client.PostAsync("/api/import/odx", content);
            var report = await response.Content.ReadFromJsonAsync<OdxImportReport>();
            var reloaded = await new ConfigStore().LoadAsync(configPath);

            Assert.True(response.IsSuccessStatusCode);
            Assert.True(report!.Success);
            Assert.True(report.Saved);
            Assert.Contains(reloaded.Uds.Dids, did => did.Identifier == "0xF191");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ApiRejectsWrongExtensionWithoutMutatingConfig()
    {
        await using var app = CreateApp(out var baseAddress, out var configPath);
        await app.StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = baseAddress };
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(SampleOdx())), "file", "sample.txt");

            var response = await client.PostAsync("/api/import/odx", content);
            var reloaded = await new ConfigStore().LoadAsync(configPath);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.DoesNotContain(reloaded.Uds.Dids, did => did.Identifier == "0xF191");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public async Task ApiUploadsPdxAndImportsEcuInfo()
    {
        await using var app = CreateApp(out var baseAddress, out var configPath);
        await app.StartAsync();

        try
        {
            await using var pdx = CreatePdx(("index.odx", SampleOdx()));
            using var client = new HttpClient { BaseAddress = baseAddress };
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(pdx.ToArray()), "file", "sample.pdx");

            var response = await client.PostAsync("/api/import/pdx", content);
            var report = await response.Content.ReadFromJsonAsync<OdxImportReport>();
            var reloaded = await new ConfigStore().LoadAsync(configPath);

            Assert.True(response.IsSuccessStatusCode);
            Assert.True(report!.Success);
            Assert.Equal("LTEST000000000002", reloaded.Entity.Vin);
            Assert.Equal("0x0E01", reloaded.Entity.LogicalAddress);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static WebApplication CreateApp(out Uri baseAddress, out string configPath)
    {
        var port = GetFreeLoopbackPort();
        baseAddress = new Uri($"http://127.0.0.1:{port}");
        configPath = CreateTempConfigPath();
        var config = SimulatorConfig.CreateDefault();
        var configStore = new ConfigStore();
        configStore.SaveAsync(configPath, config).GetAwaiter().GetResult();
        var didStore = new DidRuntimeStore(config, configPath, configStore);
        return WebApiApplication.Create(
            [],
            new WebApiRuntimeOptions("127.0.0.1", port, DateTimeOffset.UtcNow, configPath),
            configStore,
            didRuntimeStore: didStore);
    }

    private static string SampleOdx()
    {
        return """
            <?xml version="1.0" encoding="utf-8"?>
            <ODX>
              <ECU>
                <SHORT-NAME>Sample ECU</SHORT-NAME>
                <VIN>LTEST000000000002</VIN>
                <EID>102030405060</EID>
                <GID>A0A1A2A3A4A5</GID>
                <LOGICAL-ADDRESS>0x0E01</LOGICAL-ADDRESS>
              </ECU>
              <DIDS>
                <DID ID="0xF191">
                  <SHORT-NAME>Imported serial</SHORT-NAME>
                  <FIXED-VALUE>1234</FIXED-VALUE>
                  <COMPU-METHOD>linear scaling is skipped</COMPU-METHOD>
                </DID>
                <DID>
                  <IDENTIFIER>F192</IDENTIFIER>
                  <NAME>Imported supplier</NAME>
                  <DEFAULT-VALUE>AABBCC</DEFAULT-VALUE>
                </DID>
              </DIDS>
              <FLASH>
                <DATA>unsupported</DATA>
              </FLASH>
            </ODX>
            """;
    }

    private static MemoryStream ToStream(string value)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(value));
    }

    private static MemoryStream CreatePdx(params (string Name, string Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static int GetFreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string CreateTempConfigPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "simulator.json");
    }
}

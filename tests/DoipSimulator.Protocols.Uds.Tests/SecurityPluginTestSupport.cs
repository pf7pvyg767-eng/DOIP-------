using System.Diagnostics;

namespace DoipSimulator.Protocols.Uds.Tests;

internal static class SecurityPluginTestSupport
{
    private static readonly Lazy<string> SamplePluginPath = new(PublishSamplePlugin);

    public static string BuildSamplePlugin()
    {
        return SamplePluginPath.Value;
    }

    private static string PublishSamplePlugin()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(root, "samples", "SecurityPluginExample", "SecurityPluginExample.csproj");
        var outputPath = Path.Combine(root, "artifacts", "SecurityPluginExample");
        var dllPath = Path.Combine(outputPath, "SecurityPluginExample.dll");
        if (File.Exists(dllPath))
        {
            return dllPath;
        }

        Directory.CreateDirectory(outputPath);

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList =
            {
                "publish",
                projectPath,
                "-c",
                "Release",
                "-o",
                outputPath,
                "--nologo",
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        });

        if (process is null)
        {
            throw new InvalidOperationException("Could not start dotnet publish for the sample security plugin.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(120000);
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Timed out while publishing the sample security plugin.");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Sample security plugin publish failed with exit code {process.ExitCode}.{Environment.NewLine}{output}{Environment.NewLine}{error}");
        }

        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException("Sample security plugin DLL was not produced.", dllPath);
        }

        return dllPath;
    }

    public static byte[] ComputeSampleKey(IReadOnlyList<byte> seed)
    {
        var key = new byte[seed.Count];
        for (var index = 0; index < seed.Count; index++)
        {
            key[index] = unchecked((byte)(seed[seed.Count - index - 1] ^ 0x5A));
        }

        return key;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DoipSimulator.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}

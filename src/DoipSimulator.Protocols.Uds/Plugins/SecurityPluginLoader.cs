using System.Diagnostics;
using System.Runtime.InteropServices;
using DoipSimulator.Core.Configuration;
using DoipSimulator.Core.RuntimeEvents;

namespace DoipSimulator.Protocols.Uds.Plugins;

public sealed record SecurityPluginLoadResult(
    bool IsAvailable,
    ISecurityAccessAlgorithm? Algorithm,
    string? Error)
{
    public static SecurityPluginLoadResult Disabled() => new(false, null, null);

    public static SecurityPluginLoadResult Failed(string error) => new(false, null, error);

    public static SecurityPluginLoadResult Loaded(ISecurityAccessAlgorithm algorithm) => new(true, algorithm, null);
}

public static class SecurityPluginLoader
{
    public const int SupportedAbiVersion = 1;

    public static SecurityPluginLoadResult Load(
        SecurityPluginConfig config,
        IRuntimeEventPublisher? eventPublisher = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        eventPublisher ??= NullRuntimeEventPublisher.Instance;

        if (!config.Enabled)
        {
            return SecurityPluginLoadResult.Disabled();
        }

        if (string.IsNullOrWhiteSpace(config.DllPath))
        {
            var error = "Security plugin DLL path is required when the plugin is enabled.";
            PublishLoadFailure(eventPublisher, config.DllPath, error);
            return SecurityPluginLoadResult.Failed(error);
        }

        if (!File.Exists(config.DllPath))
        {
            var error = $"Security plugin DLL does not exist: {config.DllPath}";
            PublishLoadFailure(eventPublisher, config.DllPath, error);
            return SecurityPluginLoadResult.Failed(error);
        }

        if (config.TimeoutMs < 1)
        {
            var error = "Security plugin timeout must be a positive millisecond value.";
            PublishLoadFailure(eventPublisher, config.DllPath, error);
            return SecurityPluginLoadResult.Failed(error);
        }

        try
        {
            var handle = NativeLibrary.Load(config.DllPath);
            try
            {
                var getAbiVersion = Resolve<GetAbiVersionDelegate>(handle, "DoipSec_GetAbiVersion");
                var generateSeed = Resolve<GenerateSeedDelegate>(handle, "DoipSec_GenerateSeed");
                var verifyKey = Resolve<VerifyKeyDelegate>(handle, "DoipSec_VerifyKey");
                var actualAbiVersion = getAbiVersion();
                if (actualAbiVersion != SupportedAbiVersion)
                {
                    NativeLibrary.Free(handle);
                    var error =
                        $"Security plugin ABI version mismatch: expected {SupportedAbiVersion}, actual {actualAbiVersion}.";
                    PublishLoadFailure(eventPublisher, config.DllPath, error);
                    return SecurityPluginLoadResult.Failed(error);
                }

                var algorithm = new NativeSecurityPluginAlgorithm(
                    handle,
                    config.DllPath,
                    generateSeed,
                    verifyKey,
                    config.TimeoutMs,
                    eventPublisher);
                PublishLoaded(eventPublisher, config.DllPath, actualAbiVersion);
                return SecurityPluginLoadResult.Loaded(algorithm);
            }
            catch
            {
                NativeLibrary.Free(handle);
                throw;
            }
        }
        catch (EntryPointNotFoundException exception)
        {
            var error = $"Security plugin required entry point is missing: {exception.Message}";
            PublishLoadFailure(eventPublisher, config.DllPath, error);
            return SecurityPluginLoadResult.Failed(error);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var error = $"Security plugin load failed: {exception.Message}";
            PublishLoadFailure(eventPublisher, config.DllPath, error);
            return SecurityPluginLoadResult.Failed(error);
        }
    }

    private static TDelegate Resolve<TDelegate>(nint handle, string name)
        where TDelegate : Delegate
    {
        if (!NativeLibrary.TryGetExport(handle, name, out var export))
        {
            throw new EntryPointNotFoundException(name);
        }

        return Marshal.GetDelegateForFunctionPointer<TDelegate>(export);
    }

    private static void PublishLoaded(IRuntimeEventPublisher eventPublisher, string dllPath, int abiVersion)
    {
        _ = eventPublisher.PublishAsync(RuntimeEvent.Create(
            RuntimeEventLevel.Info,
            RuntimeEventCategory.Uds,
            "uds.securityPlugin.loaded",
            "Security plugin loaded.",
            null,
            new Dictionary<string, object?>
            {
                ["dllPath"] = dllPath,
                ["abiVersion"] = abiVersion,
            }));
    }

    private static void PublishLoadFailure(IRuntimeEventPublisher eventPublisher, string? dllPath, string reason)
    {
        PublishFailure(eventPublisher, "load", dllPath, reason);
    }

    internal static void PublishFailure(
        IRuntimeEventPublisher eventPublisher,
        string operation,
        string? dllPath,
        string reason)
    {
        _ = eventPublisher.PublishAsync(RuntimeEvent.Create(
            RuntimeEventLevel.Warning,
            RuntimeEventCategory.Uds,
            "uds.securityPlugin.failed",
            "Security plugin operation failed.",
            null,
            new Dictionary<string, object?>
            {
                ["operation"] = operation,
                ["dllPath"] = dllPath,
                ["reason"] = reason,
            }));
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetAbiVersionDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GenerateSeedDelegate(
        int level,
        byte[]? context,
        int contextLength,
        byte[] seedOut,
        ref int seedLength);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int VerifyKeyDelegate(
        int level,
        byte[] seed,
        int seedLength,
        byte[] key,
        int keyLength);

    private sealed class NativeSecurityPluginAlgorithm : ISecurityAccessAlgorithm
    {
        private const int PluginSuccess = 0;
        private const int PluginInvalidKey = 1;
        private const int MaxSeedLength = 64;

        private readonly nint handle;
        private readonly string dllPath;
        private readonly GenerateSeedDelegate generateSeed;
        private readonly VerifyKeyDelegate verifyKey;
        private readonly int timeoutMs;
        private readonly IRuntimeEventPublisher eventPublisher;

        public NativeSecurityPluginAlgorithm(
            nint handle,
            string dllPath,
            GenerateSeedDelegate generateSeed,
            VerifyKeyDelegate verifyKey,
            int timeoutMs,
            IRuntimeEventPublisher eventPublisher)
        {
            this.handle = handle;
            this.dllPath = dllPath;
            this.generateSeed = generateSeed;
            this.verifyKey = verifyKey;
            this.timeoutMs = timeoutMs;
            this.eventPublisher = eventPublisher;
        }

        public SecurityPluginSeedResult GenerateSeed(SecurityAccessConfig level, byte subFunction)
        {
            var seed = new byte[MaxSeedLength];
            var seedLength = seed.Length;
            var context = new[] { subFunction };

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var status = generateSeed(level.Level, context, context.Length, seed, ref seedLength);
                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > timeoutMs)
                {
                    return FailSeed($"DoipSec_GenerateSeed exceeded timeoutMs after returning: {stopwatch.ElapsedMilliseconds}ms.");
                }

                if (status != PluginSuccess)
                {
                    return FailSeed($"DoipSec_GenerateSeed returned failure code {status}.");
                }

                if (seedLength is < 1 or > MaxSeedLength)
                {
                    return FailSeed($"DoipSec_GenerateSeed returned invalid seed length {seedLength}.");
                }

                return SecurityPluginSeedResult.Succeeded(seed[..seedLength]);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                return FailSeed($"DoipSec_GenerateSeed threw {exception.GetType().Name}: {exception.Message}");
            }
        }

        public SecurityPluginKeyResult VerifyKey(SecurityAccessConfig level, IReadOnlyList<byte> seed, IReadOnlyList<byte> key)
        {
            try
            {
                var seedBytes = seed.ToArray();
                var keyBytes = key.ToArray();
                var stopwatch = Stopwatch.StartNew();
                var status = verifyKey(level.Level, seedBytes, seedBytes.Length, keyBytes, keyBytes.Length);
                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > timeoutMs)
                {
                    return FailKey($"DoipSec_VerifyKey exceeded timeoutMs after returning: {stopwatch.ElapsedMilliseconds}ms.");
                }

                return status switch
                {
                    PluginSuccess => SecurityPluginKeyResult.Succeeded(),
                    PluginInvalidKey => SecurityPluginKeyResult.InvalidKey(),
                    _ => FailKey($"DoipSec_VerifyKey returned failure code {status}."),
                };
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                return FailKey($"DoipSec_VerifyKey threw {exception.GetType().Name}: {exception.Message}");
            }
        }

        private SecurityPluginSeedResult FailSeed(string reason)
        {
            SecurityPluginLoader.PublishFailure(eventPublisher, "generateSeed", dllPath, reason);
            return SecurityPluginSeedResult.Failed(reason);
        }

        private SecurityPluginKeyResult FailKey(string reason)
        {
            SecurityPluginLoader.PublishFailure(eventPublisher, "verifyKey", dllPath, reason);
            return SecurityPluginKeyResult.Failed(reason);
        }

        ~NativeSecurityPluginAlgorithm()
        {
            if (handle != 0)
            {
                NativeLibrary.Free(handle);
            }
        }
    }
}

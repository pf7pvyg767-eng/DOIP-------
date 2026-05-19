using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SecurityPluginExample;

public static unsafe class SecurityPluginExports
{
    private const int Success = 0;
    private const int InvalidKey = 1;
    private const int Failure = 2;

    [UnmanagedCallersOnly(EntryPoint = "DoipSec_GetAbiVersion", CallConvs = [typeof(CallConvCdecl)])]
    public static int GetAbiVersion()
    {
        return string.Equals(Environment.GetEnvironmentVariable("DOIP_SEC_SAMPLE_ABI_VERSION"), "2", StringComparison.Ordinal)
            ? 2
            : 1;
    }

    [UnmanagedCallersOnly(EntryPoint = "DoipSec_GenerateSeed", CallConvs = [typeof(CallConvCdecl)])]
    public static int GenerateSeed(
        int level,
        byte* context,
        int contextLength,
        byte* seedOut,
        int* seedLength)
    {
        if (seedOut is null || seedLength is null || *seedLength < 4 || level == 99)
        {
            return Failure;
        }

        var subFunction = context is not null && contextLength > 0 ? context[0] : (byte)0;
        seedOut[0] = 0xD0;
        seedOut[1] = unchecked((byte)level);
        seedOut[2] = subFunction;
        seedOut[3] = 0x23;
        *seedLength = 4;
        return Success;
    }

    [UnmanagedCallersOnly(EntryPoint = "DoipSec_VerifyKey", CallConvs = [typeof(CallConvCdecl)])]
    public static int VerifyKey(
        int level,
        byte* seed,
        int seedLength,
        byte* key,
        int keyLength)
    {
        if (seed is null || key is null || seedLength < 1 || keyLength != seedLength || level == 98)
        {
            return Failure;
        }

        for (var index = 0; index < seedLength; index++)
        {
            var expected = unchecked((byte)(seed[seedLength - index - 1] ^ 0x5A));
            if (key[index] != expected)
            {
                return InvalidKey;
            }
        }

        return Success;
    }
}

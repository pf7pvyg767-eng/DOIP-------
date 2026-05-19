# SecurityPluginExample

This project builds a deterministic sample SecurityAccess plugin DLL for `task-023`.

Build:

```powershell
dotnet publish .\samples\SecurityPluginExample\SecurityPluginExample.csproj -c Release
```

The output DLL exports:

- `DoipSec_GetAbiVersion`
- `DoipSec_GenerateSeed`
- `DoipSec_VerifyKey`

The algorithm is test-only and non-OEM:

- seed bytes are `D0`, level, seed sub-function, `23`
- key bytes are the seed bytes in reverse order XOR `0x5A`

It is deterministic integration-test code and does not provide real vehicle security or cryptographic strength.

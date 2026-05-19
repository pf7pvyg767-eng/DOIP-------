# SecurityAccess DLL Plugin ABI

This document defines the MVP C ABI for external SecurityAccess `0x27` seed/key plugins.

The supported ABI version is `1`. Plugins that return any other ABI version are rejected before they are used.

This ABI is for deterministic simulator and integration-test algorithms. The sample algorithm is not a real OEM algorithm, does not claim cryptographic strength, and must not be treated as vehicle security guidance.

## Exports

All functions use the C calling convention and return an `int` status code unless noted otherwise.

```c
int DoipSec_GetAbiVersion(void);

int DoipSec_GenerateSeed(
  int level,
  const unsigned char* context,
  int contextLength,
  unsigned char* seedOut,
  int* seedLength);

int DoipSec_VerifyKey(
  int level,
  const unsigned char* seed,
  int seedLength,
  const unsigned char* key,
  int keyLength);
```

## Return Codes

`0` means success.

`1` from `DoipSec_VerifyKey` means the supplied key is invalid and maps to the existing SecurityAccess invalid-key NRC.

Any other non-zero return code means plugin failure. The simulator reports a diagnostic plugin failure and does not log seed/key material.

## Buffer Rules

`DoipSec_GenerateSeed` receives a writable `seedOut` buffer. On entry, `*seedLength` contains the maximum output size. On success, the plugin writes seed bytes and updates `*seedLength` to the number of bytes written.

The simulator rejects seed lengths of `0` or lengths larger than the provided output buffer.

`context` is optional input owned by the simulator. For the MVP, it contains the requested seed sub-function as one byte. Plugins must not retain pointers after returning.

## Threading And Failure Boundaries

Plugins must be thread-safe if used concurrently by multiple connections.

The MVP loads plugins in-process. It does not provide process isolation, sandboxing, or guaranteed termination of blocking native code. `timeoutMs` is a simulator-side observation boundary; calls that return after exceeding it are treated as failures, but malicious or permanently blocking native code cannot be forcibly stopped by this MVP.

Diagnostic logs and runtime events must not include secret key material or raw supplied keys.

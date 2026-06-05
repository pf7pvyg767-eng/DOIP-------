# DOIP Simulator

This repository contains the DOIP Simulator development skeleton and the minimal Host/WebApi runtime path. The Host can start the WebApi on a configured loopback endpoint and exposes a health endpoint for smoke tests.

## Directory layout

```text
src/
  DoipSimulator.Host/        Command-line host entrypoint.
  DoipSimulator.Core/        Shared core project placeholder.
  DoipSimulator.WebApi/      Future control-plane API project shell.
  DoipSimulator.WebConsole/  Vue/Vite frontend shell.
tests/
  DoipSimulator.Core.Tests/  Placeholder backend unit tests.
scripts/
  build.ps1                  Unified backend and frontend build.
  test.ps1                   Backend test entrypoint.
  run-host.ps1               Host CLI wrapper.
  package-portable.ps1       Self-contained unzip-and-run Windows package.
```

## Backend commands

```powershell
dotnet build .\DoipSimulator.sln -m:1
dotnet test .\DoipSimulator.sln
dotnet run --project .\src\DoipSimulator.Host -- --help
dotnet run --project .\src\DoipSimulator.Host -- run
dotnet run --project .\src\DoipSimulator.Host -- run --listen-address 127.0.0.1 --port 5080
```

After publishing or building the Host project, the executable assembly name is `doip-simulator`.

The Host `run` command supports only these runtime options:

- `--listen-address <address>`: WebApi listen address. Default: `127.0.0.1`.
- `--port <port>`: WebApi listen port. Default: `5080`.

On successful startup the console prints `http://127.0.0.1:{port}`. `GET /api/health` returns HTTP 200 with minimal health information.

## Frontend commands

```powershell
cd .\src\DoipSimulator.WebConsole
npm install
npm run dev
npm run build
```

## Unified scripts

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\run-host.ps1 --help
.\scripts\run-host.ps1 run
```

## MSI packaging

To build an install-ready Windows MSI with the Web Console, default simulator configuration, bundled .NET runtime, Start Menu shortcut, application icon, and DoIP firewall rules:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-msi.ps1
```

The package is written to `artifacts\installer\msi`. The installed shortcut starts the simulator with the bundled `simulator-config.json` and opens `http://127.0.0.1:5080`. The MSI installs Windows Firewall rules for DoIP TCP/UDP `13400` and DoIP TLS TCP `3496`; the Web Console remains bound to local loopback by default.

## Portable zip packaging

To build a self-contained Windows x64 zip that can be shared directly:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-portable.ps1 -DotnetPath C:\Users\admin\.dotnet\dotnet.exe
```

The package is written to:

```text
artifacts\portable\doip-simulator-0.2.0-win-x64-portable.zip
```

Recipients can unzip it and double-click `Start-DOIP-Simulator.cmd`. The portable package includes the Web Console production build, the simulator executable, the bundled runtime, and `simulator-config.json` with the dynamic sample DIDs `0xF191` through `0xF197`.

## Phase 2 functional smoke

Start the Host with a disposable development configuration, then run:

```powershell
.\scripts\phase2-functional-smoke.ps1
```

The smoke script checks API health, runtime summary, UDP vehicle discovery, TCP Routing Activation, static and dynamic DID `0xF190`, the DID sample API, and the runtime shutdown API. Use `-SkipShutdown` when you want the runtime to remain open after the checks.

MSI installation, full browser UI E2E, and report generation are intentionally excluded from this lightweight development loop.

For the runtime cockpit UI smoke:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\web-console-runtime-cockpit-smoke.ps1
```

To drive DID live charts from a diagnostic-style DoIP loop, start the Host and Web Console, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\did-continuous-read.ps1
```

The default loop reads the dynamic sample DIDs `0xF191` through `0xF197` every 500 ms for 120 seconds. Use `-DurationSeconds 0` to run until Ctrl+C.
Use `-DoipPort <port>` when the simulator is running on a non-default DoIP TCP port. For the live second development instance used during dynamic DID work:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\did-continuous-read.ps1 -DoipPort 13401 -DurationSeconds 0
```

The script preflights the matching Web API (`13400` -> `5080`, `13401` -> `5081`) and stops early with a clear message when the requested DIDs are not configured on that runtime. Pass `-ApiBaseUrl <url>` for a custom API endpoint or `-SkipApiPreflight` for raw DoIP-only testing. For a second Web Console instance, set `VITE_API_PROXY_TARGET` before starting Vite, for example `http://127.0.0.1:5081`.

## Scope note

This is still an MVP simulator rather than a full OEM ECU platform. It supports the local Standard ECU workflow, dynamic DID providers, common UDS service MVPs, observability, PCAP, fault controls, ODX/PDX subset import, Web Console operation, and portable packaging. It does not implement full OEM security algorithms, complex routine scripts, real flashing storage, database integration, or external services.

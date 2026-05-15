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

## Scope note

This skeleton intentionally does not implement full ECU configuration loading, DoIP network services, UDS, DID, DTC, Flash, TLS, PCAP, real Web console business pages, database access, or external service integration.

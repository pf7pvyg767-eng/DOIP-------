Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

dotnet run --project .\src\DoipSimulator.Host -- @args

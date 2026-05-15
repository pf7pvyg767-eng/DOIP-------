Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

dotnet build .\DoipSimulator.sln -m:1
Push-Location .\src\DoipSimulator.WebConsole
try {
    npm install
    npm run build
}
finally {
    Pop-Location
}

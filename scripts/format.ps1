# Format code with dotnet format
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

if (-not (Get-Command dotnet-format -ErrorAction SilentlyContinue)) {
    dotnet tool install -g dotnet-format
}
dotnet format Sentinel.sln --verbosity diagnostic

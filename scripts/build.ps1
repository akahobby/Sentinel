# Sentinel build script
# Usage: .\build.ps1 [-Configuration Debug|Release] [-Pack] [-Portable]

param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Pack,
    [switch]$Portable
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $root

Write-Host "Restore..." -ForegroundColor Cyan
dotnet restore Sentinel.sln

Write-Host "Build ($Configuration)..." -ForegroundColor Cyan
dotnet build Sentinel.sln -c $Configuration --no-restore

if ($Pack) {
    Write-Host "Packaging MSIX..." -ForegroundColor Cyan
    dotnet publish Sentinel.App\Sentinel.App.csproj -c $Configuration -f net8.0-windows10.0.22621.0 -p:RuntimeIdentifier=win-x64 -p:SelfContained=true --no-build
    Write-Host "MSIX output: Sentinel.App\bin\$Configuration\net8.0-windows10.0.22621.0\win-x64\publish\" -ForegroundColor Green
}

if ($Portable) {
    Write-Host "Building portable (unpackaged)..." -ForegroundColor Cyan
    dotnet publish Sentinel.App\Sentinel.App.csproj -c $Configuration -f net8.0-windows10.0.22621.0 -p:RuntimeIdentifier=win-x64 -p:SelfContained=true -p:WindowsPackageType=None --no-build
    Write-Host "Portable output: Sentinel.App\bin\$Configuration\net8.0-windows10.0.22621.0\win-x64\publish\" -ForegroundColor Green
}

Write-Host "Done." -ForegroundColor Green

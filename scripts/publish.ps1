<#
.SYNOPSIS
  Builds the portable AZARIAH app and (optionally) copies it to your drive.

.EXAMPLE
  ./scripts/publish.ps1                 # builds to ./publish/win-x64/Azariah.exe
  ./scripts/publish.ps1 -Drive E:\      # builds and copies Azariah.exe to E:\
#>
param(
    [string]$Drive,
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$repo = Split-Path -Parent $PSScriptRoot
$out = Join-Path $repo "publish/$Runtime"

dotnet publish (Join-Path $repo 'src/Azariah.App') -c Release -r $Runtime -o $out
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$exe = Join-Path $out 'Azariah.exe'
Write-Host "Built $exe"

if ($Drive) {
    if (-not (Test-Path $Drive)) { throw "Drive $Drive not found." }
    Copy-Item $exe (Join-Path $Drive 'Azariah.exe') -Force
    Write-Host "Copied to $(Join-Path $Drive 'Azariah.exe')"
}

<#
.SYNOPSIS
  Tests and builds AZARIAH, then installs the new build on the AZARIAH drive and on this PC.

.DESCRIPTION
  Installs through the app's own update helper (Azariah.exe --finish-update), the same path as
  Settings > Update: it stops the watcher, replaces the drive's Azariah.exe and this PC's
  auto-launch copy (hash-verified), restarts the watcher and reopens AZARIAH. Open AZARIAH
  windows are closed normally first. Build output stays in %LOCALAPPDATA%\Azariah\build, off the USB.

.EXAMPLE
  ./scripts/deploy.ps1                # finds the drive by its .azariah\drive.json marker
  ./scripts/deploy.ps1 -Drive E:\
  ./scripts/deploy.ps1 -SkipTests
#>
param(
    [string]$Drive,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$repo = Split-Path -Parent $PSScriptRoot
$build = Join-Path $env:LOCALAPPDATA 'Azariah\build'
$out = Join-Path $build 'publish\win-x64'
$localExe = Join-Path $env:LOCALAPPDATA 'Programs\Azariah\Azariah.exe'

function Find-AzariahDrive {
    foreach ($d in [IO.DriveInfo]::GetDrives()) {
        if ($d.IsReady -and $d.DriveType -ne 'Network' -and $d.DriveType -ne 'CDRom' -and
            (Test-Path -LiteralPath (Join-Path $d.RootDirectory.FullName '.azariah\drive.json'))) {
            return $d.RootDirectory.FullName
        }
    }

    return $null
}

# Quotes one argument the way Windows splits command lines (backslashes before a quote double).
function ConvertTo-Arg([string]$s) {
    if ($s -and $s -notmatch '[\s"]') { return $s }
    return '"' + ($s -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"'
}

function Get-Sha256([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }

if (-not $Drive) { $Drive = Find-AzariahDrive }
if (-not $Drive -or -not (Test-Path -LiteralPath (Join-Path $Drive '.azariah\drive.json'))) {
    throw 'AZARIAH drive not found. Plug it in, or pass -Drive E:\'
}

$driveExe = Join-Path $Drive 'Azariah.exe'

# 1. Test and build.
if (-not $SkipTests) {
    dotnet test (Join-Path $repo 'Azariah.sln') -c Release --artifacts-path $build
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed. Nothing was installed.' }
}

dotnet publish (Join-Path $repo 'src\Azariah.App') -c Release -r win-x64 --artifacts-path $build -o $out
if ($LASTEXITCODE -ne 0) { throw 'Build failed. Nothing was installed.' }

$built = Join-Path $out 'Azariah.exe'
$info = (Get-Item -LiteralPath $built).VersionInfo
if ($info.ProductName -ne 'Azariah') { throw "Unexpected build output: $built" }
$version = $info.ProductVersion.Split('+')[0]

# 2. Stage it where the app's own updates go (the app clears this folder on its next launch).
$stage = Join-Path ([IO.Path]::GetTempPath()) ('azariah-update\' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $stage | Out-Null
$staged = Join-Path $stage 'Azariah.exe'
Copy-Item -LiteralPath $built -Destination $staged
$hash = Get-Sha256 $staged

# 3. Close AZARIAH windows normally (same as clicking X) so their exe can be replaced.
$windows = @(Get-Process -Name Azariah -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 })
foreach ($p in $windows) {
    [void]$p.CloseMainWindow()
    if (-not $p.WaitForExit(20000)) { throw 'AZARIAH did not close (a dialog may be open). Nothing was installed.' }
}

# 4. Install with the new build's own update helper. It's a GUI exe, so wait on the process.
$argList = @('--finish-update', '--drive-exe', $driveExe)
if (Test-Path -LiteralPath $localExe) {
    $argList += @('--local-exe', $localExe, '--relaunch-local')
}

$argList += @('--root', $Drive)

$psi = New-Object System.Diagnostics.ProcessStartInfo $staged
$psi.UseShellExecute = $false
$psi.Arguments = ($argList | ForEach-Object { ConvertTo-Arg $_ }) -join ' '
$helper = [System.Diagnostics.Process]::Start($psi)
$helper.WaitForExit()
if ($helper.ExitCode -ne 0) {
    throw "The update helper reported an error (exit $($helper.ExitCode)). See %LOCALAPPDATA%\Azariah\logs."
}

# 5. Every target must now hold exactly this build.
$targets = @($driveExe)
if (Test-Path -LiteralPath $localExe) { $targets += $localExe }
foreach ($t in $targets) {
    if ((Get-Sha256 $t) -ne $hash) { throw "$t does not match the new build." }
}

Write-Host "AZARIAH $version installed: $($targets -join ', ')"

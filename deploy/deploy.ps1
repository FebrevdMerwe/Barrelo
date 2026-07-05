#Requires -Version 5.1
<#
  Publishes Barrelo.Api (+ optionally the Board Simulator) for linux-x64 and pushes them to the
  Proxmox LXC over SSH, restarting the system-wide `barrelo` / `barrelo-boardsimulator` systemd services
  under /opt/barrelo.

  One-time box setup: see deploy/README.md before the first run.

  Usage:
    ./deploy/deploy.ps1 -RemoteHost 192.168.1.50 -RemoteUser febre
    ./deploy/deploy.ps1 -RemoteHost 192.168.1.50 -RemoteUser febre -NoBoardSimulator
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RemoteHost,

    [Parameter(Mandatory = $true)]
    [string]$RemoteUser,

    [string]$RemoteDir = "/opt/barrelo",
    [string]$Configuration = "Release",
    [string]$Rid = "linux-x64",
    [switch]$NoBoardSimulator
)

$ErrorActionPreference = "Stop"

$repoRoot  = Resolve-Path "$PSScriptRoot\.."
$stageDir  = Join-Path $repoRoot "publish\stage\Barrelo"
$zipPath   = Join-Path $repoRoot "publish\barrelo-$Rid.zip"
$remote    = "$RemoteUser@$RemoteHost"
$services  = @("barrelo")

if (Test-Path (Join-Path $repoRoot "publish\stage")) { Remove-Item (Join-Path $repoRoot "publish\stage") -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "==> Publishing Barrelo.Api for $Rid ($Configuration)"
dotnet publish "$repoRoot\src\Barrelo.Api\Barrelo.Api.csproj" `
    -c $Configuration -r $Rid --self-contained true `
    -o $stageDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (Barrelo.Api) failed" }

if (-not $NoBoardSimulator) {
    Write-Host "==> Publishing Barrelo.BoardSimulator for $Rid ($Configuration)"
    dotnet publish "$repoRoot\tools\Barrelo.BoardSimulator\Barrelo.BoardSimulator.csproj" `
        -c $Configuration -r $Rid --self-contained true `
        -o (Join-Path $stageDir "tools\BoardSimulator")
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish (BoardSimulator) failed" }
    $services += "barrelo-boardsimulator"
}

Write-Host "==> Zipping publish output"
# Compress-Archive stores entries with backslash path separators on Windows, which isn't valid
# per the zip spec - Linux `unzip` can't recognize them as directory separators, so nested
# folders (plugins/x01/..., tools/BoardSimulator/...) land as garbled single filenames instead
# of real subdirectories. Build the archive by hand with forward-slash entry names instead.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -Path $stageDir -Recurse -File | ForEach-Object {
        $relativePath = $_.FullName.Substring($stageDir.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip, $_.FullName, $relativePath, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {
    $zip.Dispose()
}

$serviceList = $services -join " "

Write-Host "==> Uploading to ${remote}:$RemoteDir"
scp $zipPath "${remote}:/tmp/barrelo-deploy.zip"
if ($LASTEXITCODE -ne 0) { throw "scp upload failed" }

# Built as a file and scp'd over rather than passed as an inline ssh argument: a multi-line
# script with embedded quotes (e.g. "$(id -u)") gets mangled when PowerShell/ssh.exe build the
# Windows process command line for a native exe, so ssh only ever receives one simple command.
$remoteScriptTemplate = @'
set -e
if [ "$(id -u)" -eq 0 ]; then SUDO=""; else SUDO="sudo"; fi
$SUDO systemctl stop __SERVICES__ 2>/dev/null || true
$SUDO mkdir -p __REMOTEDIR__
$SUDO unzip -o -q /tmp/barrelo-deploy.zip -d __REMOTEDIR__ || { rc=$?; if [ "$rc" -gt 1 ]; then exit "$rc"; fi; }
$SUDO chmod +x __REMOTEDIR__/Barrelo.Api
if [ -f __REMOTEDIR__/tools/BoardSimulator/Barrelo.BoardSimulator ]; then
    $SUDO chmod +x __REMOTEDIR__/tools/BoardSimulator/Barrelo.BoardSimulator
fi
rm -f /tmp/barrelo-deploy.zip /tmp/barrelo-deploy.sh
$SUDO systemctl start __SERVICES__
sleep 1
systemctl status __SERVICES__ --no-pager -l | head -n 24
'@
$remoteScriptContent = $remoteScriptTemplate.Replace("__SERVICES__", $serviceList).Replace("__REMOTEDIR__", $RemoteDir)
$remoteScriptContent = $remoteScriptContent.Replace("`r`n", "`n")
$remoteScriptLocal = Join-Path $repoRoot "publish\remote-deploy.sh"
[System.IO.File]::WriteAllText($remoteScriptLocal, $remoteScriptContent)

Write-Host "==> Stopping ($serviceList), unpacking, restarting"
scp $remoteScriptLocal "${remote}:/tmp/barrelo-deploy.sh"
if ($LASTEXITCODE -ne 0) { throw "scp of remote deploy script failed" }

ssh -t $remote "bash /tmp/barrelo-deploy.sh"
if ($LASTEXITCODE -ne 0) { throw "remote deploy/restart failed - see systemctl output above" }

Write-Host "==> Done. Barrelo should be reachable at http://${RemoteHost}:5295"
if (-not $NoBoardSimulator) {
    Write-Host "    Board Simulator at http://${RemoteHost}:5250"
}

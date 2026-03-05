<#
.SYNOPSIS
  Lokal ausführbar: Baut Live Installer, erstellt installer-manifest.json,
  publisht alles als GitHub Release in cOOnStar/pcwaechter-public-release.

.PARAMETER Version
  Semver ohne v-Prefix (z.B. 0.0.76)

.PARAMETER OfflinePath
  Pfad zum fertigen Offline Installer (Standard: .\PCWaechter_offline_installer.exe)

.PARAMETER Repo
  Public Release Repo (Standard: cOOnStar/pcwaechter-public-release)

.EXAMPLE
  .\publish_release.ps1 -Version 0.0.76
  .\publish_release.ps1 -Version 0.0.76 -OfflinePath "C:\builds\PCWaechter_offline_installer.exe"
#>
param(
  [Parameter(Mandatory=$true)][string]$Version,
  [string]$OfflinePath = ".\PCWaechter_offline_installer.exe",
  [string]$Repo = "cOOnStar/pcwaechter-public-release"
)

$ErrorActionPreference = "Stop"

# Stable asset names – NICHT ändern, sonst brechen latest/download URLs
$OfflineName = "PCWaechter_offline_installer.exe"
$LiveName    = "PCWaechter_live_installer.exe"

if (!(Get-Command gh -ErrorAction SilentlyContinue)) {
  throw "gh CLI nicht gefunden. Installation: https://cli.github.com/"
}
if (!(Test-Path $OfflinePath)) {
  throw "Offline Installer nicht gefunden: $OfflinePath"
}

$Root = Split-Path $PSScriptRoot

# ── Build live installer ──────────────────────────────────────────────────
Write-Host "Building live installer..."
$LiveOut = Join-Path $env:TEMP "pcw-live-build"
dotnet publish "$Root\client\installer\bootstrapper\InstallerBootstrapper.csproj" `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -o $LiveOut

$LiveBin = Join-Path $LiveOut "InstallerBootstrapper.exe"
if (!(Test-Path $LiveBin)) { throw "Live installer build output nicht gefunden: $LiveBin" }

Copy-Item $OfflinePath ".\$OfflineName" -Force
Copy-Item $LiveBin    ".\$LiveName"     -Force

# ── Compute hashes ───────────────────────────────────────────────────────
Write-Host "Computing SHA256..."
$OfflineSha  = (Get-FileHash ".\$OfflineName" -Algorithm SHA256).Hash.ToLower()
$LiveSha     = (Get-FileHash ".\$LiveName"    -Algorithm SHA256).Hash.ToLower()
$OfflineSize = (Get-Item    ".\$OfflineName").Length

Write-Host "  Offline SHA256: $OfflineSha"
Write-Host "  Live    SHA256: $LiveSha"

# ── Generate manifest ─────────────────────────────────────────────────────
$Base = "https://github.com/$Repo/releases/latest/download"

$manifest = [ordered]@{
  version      = $Version
  installer    = [ordered]@{
    url        = "$Base/$OfflineName"
    sha256     = $OfflineSha
    silentArgs = "/S"
  }
  bootstrapper = [ordered]@{
    version = $Version
    url     = "$Base/$LiveName"
    sha256  = $LiveSha
  }
  runtime = [ordered]@{
    enabled    = $true
    minVersion = "10.0"
  }
}
$manifestPath = ".\installer-manifest.json"
$manifest | ConvertTo-Json -Depth 4 | Out-File -Encoding utf8 $manifestPath
Write-Host "Manifest geschrieben: $manifestPath"

# ── Create GitHub Release ─────────────────────────────────────────────────
$Tag = "v$Version"
Write-Host "Creating GitHub release $Tag in $Repo ..."

# Re-release support: delete if exists
gh release delete $Tag --repo $Repo --yes 2>$null
Start-Sleep -Milliseconds 500

gh release create $Tag `
  --repo $Repo `
  --title $Tag `
  --notes "Release $Tag" `
  "$manifestPath#installer-manifest.json" `
  ".\$OfflineName#$OfflineName" `
  ".\$LiveName#$LiveName"

Write-Host ""
Write-Host "Release veröffentlicht: https://github.com/$Repo/releases/tag/$Tag"
Write-Host "Manifest URL: $Base/installer-manifest.json"

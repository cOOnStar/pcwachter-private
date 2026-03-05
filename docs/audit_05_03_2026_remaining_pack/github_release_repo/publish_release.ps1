param(
  [Parameter(Mandatory=$true)][string]$Version,
  [string]$OfflinePath=".\PCWaechter_offline_installer.exe",
  [string]$LivePath=".\PCWaechter_live_installer.exe",
  [string]$Repo="cOOnStar/pcwaechter-public-release"
)

$ErrorActionPreference="Stop"

if (!(Get-Command gh -ErrorAction SilentlyContinue)) { throw "gh CLI not installed" }

$offlineHash = (Get-FileHash $OfflinePath -Algorithm SHA256).Hash.ToLower()
$liveHash = (Get-FileHash $LivePath -Algorithm SHA256).Hash.ToLower()

$base = "https://github.com/$Repo/releases/latest/download"

$manifest = @{
  schema_version = 1
  channel = "stable"
  version = $Version
  released_at = (Get-Date).ToUniversalTime().ToString("o")
  min_supported_version = "0.0.0"
  mandatory = $false
  offline = @{
    name = "PCWaechter_offline_installer.exe"
    url = "$base/PCWaechter_offline_installer.exe"
    sha256 = $offlineHash
    size_bytes = (Get-Item $OfflinePath).Length
    signature_required = $true
    signature_subject_allowlist = @("CN=PCWächter","CN=PCWaecher")
  }
  live = @{
    name = "PCWaechter_live_installer.exe"
    sha256 = $liveHash
  }
} | ConvertTo-Json -Depth 6

$manifestPath = ".\installer-manifest.json"
$manifest | Out-File -Encoding utf8 $manifestPath

$tag = "v$Version"
gh release create $tag --repo $Repo --title $tag --notes "Release $tag" `
  "$manifestPath#installer-manifest.json" `
  "$OfflinePath#PCWaechter_offline_installer.exe" `
  "$LivePath#PCWaechter_live_installer.exe"

param(
    [string]$SourceUrl = "https://api.xn--pcwchter-2za.de/openapi.json",
    [string]$OutputFile = "openapi.json"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputPath = Join-Path $scriptDir $OutputFile
$serverDir = Split-Path -Parent $scriptDir
$consolePublicDir = Join-Path $serverDir "console\\public"
$consoleOutputPath = Join-Path $consolePublicDir "openapi.json"

Write-Host "Downloading OpenAPI spec from $SourceUrl"
curl.exe -fsSL $SourceUrl -o $outputPath

$json = Get-Content -Raw $outputPath | ConvertFrom-Json

if (Test-Path $consolePublicDir) {
    Copy-Item -Path $outputPath -Destination $consoleOutputPath -Force
    Write-Host ("Synced: " + $consoleOutputPath)
}

Write-Host ("Saved: " + $outputPath)
Write-Host ("OpenAPI: " + $json.openapi)
Write-Host ("Title: " + $json.info.title)
Write-Host ("Version: " + $json.info.version)

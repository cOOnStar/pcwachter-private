param(
    [string]$SourceFile = "..\\openapi\\openapi.json",
    [string]$TargetFile = "public\\openapi.json"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$consoleDir = Split-Path -Parent $scriptDir
$sourcePath = Join-Path $consoleDir $SourceFile
$targetPath = Join-Path $consoleDir $TargetFile

if (!(Test-Path $sourcePath)) {
    throw "OpenAPI source not found: $sourcePath"
}

$targetDir = Split-Path -Parent $targetPath
if (!(Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

Copy-Item -Path $sourcePath -Destination $targetPath -Force
Write-Host ("Synced OpenAPI: " + $targetPath)

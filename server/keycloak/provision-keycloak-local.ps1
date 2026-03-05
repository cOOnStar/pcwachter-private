$ErrorActionPreference = "Stop"

function Invoke-Kcadm {
  param([string[]]$Arguments)

  $outFile = [System.IO.Path]::GetTempFileName()
  $errFile = [System.IO.Path]::GetTempFileName()
  try {
    $startInfo = @{
      FilePath               = "docker"
      ArgumentList           = @("exec", "pcwaechter-keycloak", "/opt/keycloak/bin/kcadm.sh") + $Arguments
      NoNewWindow            = $true
      PassThru               = $true
      Wait                   = $true
      RedirectStandardOutput = $outFile
      RedirectStandardError  = $errFile
    }
    $process = Start-Process @startInfo

    $stdout = if (Test-Path $outFile) { Get-Content -Raw $outFile } else { "" }
    $stderr = if (Test-Path $errFile) { Get-Content -Raw $errFile } else { "" }
    $combined = ($stdout + "`n" + $stderr).Trim()

    if ($process.ExitCode -ne 0) {
      throw "kcadm failed: $($Arguments -join ' ')`n$combined"
    }

    return $combined
  }
  finally {
    Remove-Item -Force -ErrorAction SilentlyContinue $outFile, $errFile
  }
}

function New-Hex([int]$Bytes) {
  $buffer = New-Object byte[] $Bytes
  $rng = [System.Security.Cryptography.RNGCryptoServiceProvider]::Create()
  $rng.GetBytes($buffer)
  $rng.Dispose()
  return ($buffer | ForEach-Object { $_.ToString("x2") }) -join ""
}

function Get-ClientInternalId {
  param([string]$Realm, [string]$ClientId)
  $raw = Invoke-Kcadm @("get", "clients", "-r", $Realm, "-q", "clientId=$ClientId", "--fields", "id,clientId")
  $items = @($raw | ConvertFrom-Json)
  if ($items.Count -gt 0) { return [string]$items[0].id }
  return ""
}

function Get-UserInternalId {
  param([string]$Realm, [string]$Username)
  $raw = Invoke-Kcadm @("get", "users", "-r", $Realm, "-q", "username=$Username", "--fields", "id,username")
  $items = @($raw | ConvertFrom-Json)
  if ($items.Count -gt 0) {
    $exact = $items | Where-Object { $_.username -eq $Username } | Select-Object -First 1
    if ($exact) { return [string]$exact.id }
    return [string]$items[0].id
  }
  return ""
}

function Get-GroupInternalId {
  param([string]$Realm, [string]$GroupName)
  $raw = Invoke-Kcadm @("get", "groups", "-r", $Realm, "-q", "search=$GroupName", "--fields", "id,name,path")
  $items = @($raw | ConvertFrom-Json)
  $exact = $items | Where-Object { $_.name -eq $GroupName } | Select-Object -First 1
  if ($exact) { return [string]$exact.id }
  return ""
}

function Ensure-Role {
  param([string]$Realm, [string]$RoleName)
  $exists = $true
  try {
    Invoke-Kcadm @("get", "roles/$RoleName", "-r", $Realm, "--fields", "name") | Out-Null
  }
  catch {
    $exists = $false
  }
  if (-not $exists) {
    try {
      Invoke-Kcadm @("create", "roles", "-r", $Realm, "-s", "name=$RoleName") | Out-Null
    }
    catch {
      if (-not $_.Exception.Message.Contains("already exists")) {
        throw
      }
    }
  }
}

function Ensure-Group {
  param([string]$Realm, [string]$GroupName)
  $id = Get-GroupInternalId -Realm $Realm -GroupName $GroupName
  if ([string]::IsNullOrWhiteSpace($id)) {
    Invoke-Kcadm @("create", "groups", "-r", $Realm, "-s", "name=$GroupName") | Out-Null
    $id = Get-GroupInternalId -Realm $Realm -GroupName $GroupName
  }
  return $id
}

function Upsert-Client {
  param([string]$Realm, [string]$ClientId, [hashtable]$ClientConfig)
  $id = Get-ClientInternalId -Realm $Realm -ClientId $ClientId
  $payload = $ClientConfig | ConvertTo-Json -Depth 20
  $hostTmp = [System.IO.Path]::GetTempFileName()
  $containerTmp = "/tmp/client-$ClientId.json"
  $outFile = [System.IO.Path]::GetTempFileName()
  $errFile = [System.IO.Path]::GetTempFileName()

  try {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($hostTmp, $payload, $utf8NoBom)

    $cp = Start-Process `
      -FilePath "docker" `
      -ArgumentList @("cp", $hostTmp, "pcwaechter-keycloak:$containerTmp") `
      -NoNewWindow `
      -PassThru `
      -Wait `
      -RedirectStandardOutput $outFile `
      -RedirectStandardError $errFile

    if ($cp.ExitCode -ne 0) {
      $cpErr = (Get-Content -Raw $outFile) + "`n" + (Get-Content -Raw $errFile)
      throw "docker cp failed for client $ClientId`n$cpErr"
    }

    if ([string]::IsNullOrWhiteSpace($id)) {
      Invoke-Kcadm @("create", "clients", "-r", $Realm, "-f", $containerTmp) | Out-Null
      $id = Get-ClientInternalId -Realm $Realm -ClientId $ClientId
    }
    else {
      Invoke-Kcadm @("update", "clients/$id", "-r", $Realm, "-f", $containerTmp, "-m") | Out-Null
    }

    return $id
  }
  finally {
    Remove-Item -Force -ErrorAction SilentlyContinue $hostTmp, $outFile, $errFile
  }
}

function Ensure-User {
  param(
    [string]$Realm,
    [string]$Username,
    [string]$Email,
    [string]$FirstName,
    [string]$LastName
  )

  $uid = Get-UserInternalId -Realm $Realm -Username $Username
  if ([string]::IsNullOrWhiteSpace($uid)) {
    Invoke-Kcadm @(
      "create", "users", "-r", $Realm,
      "-s", "username=$Username",
      "-s", "enabled=true",
      "-s", "email=$Email",
      "-s", "emailVerified=true",
      "-s", "firstName=$FirstName",
      "-s", "lastName=$LastName"
    ) | Out-Null
    $uid = Get-UserInternalId -Realm $Realm -Username $Username
  }
  else {
    Invoke-Kcadm @(
      "update", "users/$uid", "-r", $Realm, "-n",
      "-s", "enabled=true",
      "-s", "email=$Email",
      "-s", "emailVerified=true",
      "-s", "firstName=$FirstName",
      "-s", "lastName=$LastName"
    ) | Out-Null
  }

  return $uid
}

$realm = "pcwaechter-prod"
$homeSecret = New-Hex 32
$ownerPass = "Owner-$(New-Hex 8)"
$adminPass = "Admin-$(New-Hex 8)"
$userPass = "User-$(New-Hex 8)"

Invoke-Kcadm @(
  "config", "credentials",
  "--server", "http://localhost:8080",
  "--realm", "master",
  "--user", "admin",
  "--password", "CHANGE_ME_KEYCLOAK_ADMIN_PASSWORD"
) | Out-Null

$realmExists = $true
try {
  Invoke-Kcadm @("get", "realms/$realm", "--fields", "realm") | Out-Null
}
catch {
  $realmExists = $false
}

if (-not $realmExists) {
  Invoke-Kcadm @(
    "create", "realms",
    "-s", "realm=$realm",
    "-s", "enabled=true",
    "-s", "displayName=PCWaechter",
    "-s", "registrationAllowed=false",
    "-s", "rememberMe=true",
    "-s", "loginWithEmailAllowed=true",
    "-s", "duplicateEmailsAllowed=false",
    "-s", "resetPasswordAllowed=true",
    "-s", "sslRequired=external",
    "-s", "loginTheme=pcwaechter-v1"
  ) | Out-Null
}

Invoke-Kcadm @(
  "update", "realms/$realm", "-n",
  "-s", "enabled=true",
  "-s", "displayName=PCWaechter",
  "-s", "registrationAllowed=false",
  "-s", "rememberMe=true",
  "-s", "loginWithEmailAllowed=true",
  "-s", "duplicateEmailsAllowed=false",
  "-s", "resetPasswordAllowed=true",
  "-s", "sslRequired=external",
  "-s", "loginTheme=pcwaechter-v1"
) | Out-Null

foreach ($r in @("owner", "admin", "manager", "user")) {
  Ensure-Role -Realm $realm -RoleName $r
}

$groupRoleMap = @{
  "console-owner" = "owner"
  "console-admin" = "admin"
  "console-user" = "user"
}
$groupIds = @{}
foreach ($g in $groupRoleMap.Keys) {
  $gid = Ensure-Group -Realm $realm -GroupName $g
  $groupIds[$g] = $gid
  Invoke-Kcadm @("add-roles", "-r", $realm, "--gid", $gid, "--rolename", $groupRoleMap[$g]) | Out-Null
}

$consoleClientConfig = @{
  clientId                  = "console"
  name                      = "PCWaechter_Admin_Console"
  description               = "React_admin_console"
  enabled                   = $true
  publicClient              = $true
  standardFlowEnabled       = $true
  implicitFlowEnabled       = $false
  directAccessGrantsEnabled = $false
  serviceAccountsEnabled    = $false
  protocol                  = "openid-connect"
  redirectUris              = @(
    "https://console.xn--pcwchter-2za.de/*",
    "http://localhost:13000/*",
    "http://localhost:13001/*",
    "http://localhost:5173/*"
  )
  webOrigins                = @(
    "https://console.xn--pcwchter-2za.de",
    "http://localhost:13000",
    "http://localhost:13001",
    "http://localhost:5173"
  )
  attributes                = @{
    "pkce.code.challenge.method" = "S256"
    "post.logout.redirect.uris" = "https://console.xn--pcwchter-2za.de/*##http://localhost:13000/*##http://localhost:13001/*##http://localhost:5173/*"
  }
}
$consoleClientId = Upsert-Client -Realm $realm -ClientId "console" -ClientConfig $consoleClientConfig

$homeClientConfig = @{
  clientId                  = "home"
  name                      = "PCWaechter_Home_Portal"
  description               = "Next.js_home_portal"
  enabled                   = $true
  publicClient              = $false
  standardFlowEnabled       = $true
  implicitFlowEnabled       = $false
  directAccessGrantsEnabled = $false
  serviceAccountsEnabled    = $false
  protocol                  = "openid-connect"
  secret                    = $homeSecret
  redirectUris              = @(
    "https://home.xn--pcwchter-2za.de/api/auth/callback/keycloak",
    "http://localhost:3000/api/auth/callback/keycloak",
    "http://localhost:13001/api/auth/callback/keycloak",
    "http://localhost:13002/api/auth/callback/keycloak"
  )
  webOrigins                = @(
    "https://home.xn--pcwchter-2za.de",
    "http://localhost:3000",
    "http://localhost:13001",
    "http://localhost:13002"
  )
  attributes                = @{
    "post.logout.redirect.uris" = "https://home.xn--pcwchter-2za.de/*##http://localhost:3000/*##http://localhost:13001/*##http://localhost:13002/*"
  }
}
$homeClientId = Upsert-Client -Realm $realm -ClientId "home" -ClientConfig $homeClientConfig

$desktopClientConfig = @{
  clientId                  = "pcwaechter-desktop"
  name                      = "PCWaechter_Desktop_Client"
  description               = "Windows_desktop_app"
  enabled                   = $true
  publicClient              = $true
  standardFlowEnabled       = $true
  implicitFlowEnabled       = $false
  directAccessGrantsEnabled = $false
  serviceAccountsEnabled    = $false
  protocol                  = "openid-connect"
  redirectUris              = @(
    "http://127.0.0.1:8765/callback",
    "http://localhost:8765/callback"
  )
  webOrigins                = @()
  attributes                = @{
    "pkce.code.challenge.method" = "S256"
    "post.logout.redirect.uris" = "http://127.0.0.1:8765/logout##http://localhost:8765/logout"
  }
}
$desktopClientId = Upsert-Client -Realm $realm -ClientId "pcwaechter-desktop" -ClientConfig $desktopClientConfig

$usersToEnsure = @(
  @{ username = "owner"; email = "owner@pcwaechter.local"; firstName = "Console"; lastName = "Owner"; role = "owner"; group = "console-owner"; password = $ownerPass },
  @{ username = "admin-console"; email = "admin@pcwaechter.local"; firstName = "Console"; lastName = "Admin"; role = "admin"; group = "console-admin"; password = $adminPass },
  @{ username = "user-console"; email = "user@pcwaechter.local"; firstName = "Console"; lastName = "User"; role = "user"; group = "console-user"; password = $userPass }
)

foreach ($u in $usersToEnsure) {
  $uid = Ensure-User -Realm $realm -Username $u.username -Email $u.email -FirstName $u.firstName -LastName $u.lastName
  Invoke-Kcadm @("set-password", "-r", $realm, "--userid", $uid, "--new-password", $u.password) | Out-Null
  Invoke-Kcadm @("add-roles", "-r", $realm, "--uid", $uid, "--rolename", $u.role) | Out-Null
  Invoke-Kcadm @("update", "users/$uid/groups/$($groupIds[$u.group])", "-r", $realm, "-n") | Out-Null
}

$usersOut = @(
  $usersToEnsure |
    Sort-Object username |
    ForEach-Object {
      [pscustomobject]@{
        username = [string]$_.username
        email    = [string]$_.email
        enabled  = $true
      }
    }
)

$result = [pscustomobject]@{
  realm = $realm
  homeClientSecret = $homeSecret
  clientIds = [pscustomobject]@{
    console = $consoleClientId
    home = $homeClientId
    desktop = $desktopClientId
  }
  bootstrapPasswords = [pscustomobject]@{
    owner = $ownerPass
    admin_console = $adminPass
    user_console = $userPass
  }
  users = $usersOut
}

$result | ConvertTo-Json -Depth 8

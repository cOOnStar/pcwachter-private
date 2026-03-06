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

function Ensure-ProtocolMapper {
  param(
    [string]$Realm,
    [string]$ClientInternalId,
    [string]$MapperName,
    [hashtable]$MapperConfig
  )

  if ([string]::IsNullOrWhiteSpace($ClientInternalId)) {
    return
  }

  $raw = Invoke-Kcadm @("get", "clients/$ClientInternalId/protocol-mappers/models", "-r", $Realm)
  $items = @($raw | ConvertFrom-Json)
  $existing = $items | Where-Object { $_.name -eq $MapperName } | Select-Object -First 1
  if ($existing) {
    return
  }

  $payload = $MapperConfig | ConvertTo-Json -Depth 20
  $hostTmp = [System.IO.Path]::GetTempFileName()
  $containerTmp = "/tmp/mapper-$MapperName.json"
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
      throw "docker cp failed for mapper $MapperName`n$cpErr"
    }

    Invoke-Kcadm @("create", "clients/$ClientInternalId/protocol-mappers/models", "-r", $Realm, "-f", $containerTmp) | Out-Null
  }
  finally {
    Remove-Item -Force -ErrorAction SilentlyContinue $hostTmp, $outFile, $errFile
  }
}

function Set-UserProfileConfig {
  param([string]$Realm)

  $payload = @'
{
  "attributes": [
    {
      "name": "username",
      "displayName": "${username}",
      "validations": {
        "length": {
          "min": 3,
          "max": 255
        },
        "username-prohibited-characters": {},
        "up-username-not-idn-homograph": {}
      },
      "permissions": {
        "view": ["admin", "user"],
        "edit": ["admin", "user"]
      },
      "multivalued": false
    },
    {
      "name": "email",
      "displayName": "${email}",
      "validations": {
        "email": {},
        "length": {
          "max": 255
        }
      },
      "required": {
        "roles": ["user"]
      },
      "permissions": {
        "view": ["admin", "user"],
        "edit": ["admin", "user"]
      },
      "multivalued": false
    },
    {
      "name": "firstName",
      "displayName": "${firstName}",
      "validations": {
        "length": {
          "max": 255
        },
        "person-name-prohibited-characters": {}
      },
      "permissions": {
        "view": ["admin", "user"],
        "edit": ["admin", "user"]
      },
      "multivalued": false
    },
    {
      "name": "lastName",
      "displayName": "${lastName}",
      "validations": {
        "length": {
          "max": 255
        },
        "person-name-prohibited-characters": {}
      },
      "permissions": {
        "view": ["admin", "user"],
        "edit": ["admin", "user"]
      },
      "multivalued": false
    }
  ],
  "unmanagedAttributePolicy": "DISABLED"
}
'@

  $hostTmp = [System.IO.Path]::GetTempFileName()
  $containerTmp = "/tmp/user-profile-$Realm.json"
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
      throw "docker cp failed for user profile config`n$cpErr"
    }

    Invoke-Kcadm @("update", "realms/$Realm/users/profile", "-f", $containerTmp) | Out-Null
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
    "-s", "registrationEmailAsUsername=true",
    "-s", "editUsernameAllowed=false",
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
  "-s", "registrationEmailAsUsername=true",
  "-s", "editUsernameAllowed=false",
  "-s", "rememberMe=true",
  "-s", "loginWithEmailAllowed=true",
  "-s", "duplicateEmailsAllowed=false",
  "-s", "resetPasswordAllowed=true",
  "-s", "sslRequired=external",
  "-s", "loginTheme=pcwaechter-v1"
) | Out-Null

Set-UserProfileConfig -Realm $realm

foreach ($r in @("owner", "admin", "manager", "user", "pcw_admin", "pcw_console", "pcw_support", "pcw_user")) {
  Ensure-Role -Realm $realm -RoleName $r
}

$groupRoleMap = @{
  "pcw-admins" = "pcw_admin"
  "pcw-console" = "pcw_console"
  "pcw-support" = "pcw_support"
  "pcw-users" = "pcw_user"
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
Ensure-ProtocolMapper -Realm $realm -ClientInternalId $consoleClientId -MapperName "pcwaechter-api-audience" -MapperConfig @{
  name            = "pcwaechter-api-audience"
  protocol        = "openid-connect"
  protocolMapper  = "oidc-audience-mapper"
  consentRequired = $false
  config          = @{
    "included.custom.audience" = "pcwaechter-api"
    "access.token.claim"       = "true"
    "id.token.claim"           = "false"
  }
}
Ensure-ProtocolMapper -Realm $realm -ClientInternalId $consoleClientId -MapperName "api-audience" -MapperConfig @{
  name            = "api-audience"
  protocol        = "openid-connect"
  protocolMapper  = "oidc-audience-mapper"
  consentRequired = $false
  config          = @{
    "included.custom.audience" = "api"
    "access.token.claim"       = "true"
    "id.token.claim"           = "false"
  }
}
Ensure-ProtocolMapper -Realm $realm -ClientInternalId $consoleClientId -MapperName "realm-roles" -MapperConfig @{
  name            = "realm-roles"
  protocol        = "openid-connect"
  protocolMapper  = "oidc-usermodel-realm-role-mapper"
  consentRequired = $false
  config          = @{
    "claim.name"            = "roles"
    "jsonType.label"        = "String"
    "multivalued"           = "true"
    "userinfo.token.claim"  = "true"
    "access.token.claim"    = "true"
    "id.token.claim"        = "false"
  }
}

$consoleWebClientConfig = @{
  clientId                  = "console-web"
  name                      = "PCWaechter_Admin_Console_Web"
  description               = "Canonical_console_spa"
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
$consoleWebClientId = Upsert-Client -Realm $realm -ClientId "console-web" -ClientConfig $consoleWebClientConfig
Ensure-ProtocolMapper -Realm $realm -ClientInternalId $consoleWebClientId -MapperName "pcwaechter-api-audience" -MapperConfig @{
  name            = "pcwaechter-api-audience"
  protocol        = "openid-connect"
  protocolMapper  = "oidc-audience-mapper"
  consentRequired = $false
  config          = @{
    "included.custom.audience" = "pcwaechter-api"
    "access.token.claim"       = "true"
    "id.token.claim"           = "false"
  }
}
Ensure-ProtocolMapper -Realm $realm -ClientInternalId $consoleWebClientId -MapperName "api-audience" -MapperConfig @{
  name            = "api-audience"
  protocol        = "openid-connect"
  protocolMapper  = "oidc-audience-mapper"
  consentRequired = $false
  config          = @{
    "included.custom.audience" = "api"
    "access.token.claim"       = "true"
    "id.token.claim"           = "false"
  }
}
Ensure-ProtocolMapper -Realm $realm -ClientInternalId $consoleWebClientId -MapperName "realm-roles" -MapperConfig @{
  name            = "realm-roles"
  protocol        = "openid-connect"
  protocolMapper  = "oidc-usermodel-realm-role-mapper"
  consentRequired = $false
  config          = @{
    "claim.name"            = "roles"
    "jsonType.label"        = "String"
    "multivalued"           = "true"
    "userinfo.token.claim"  = "true"
    "access.token.claim"    = "true"
    "id.token.claim"        = "false"
  }
}

$homeClientConfig = @{
  clientId                  = "home"
  name                      = "PCWaechter_Home_Portal"
  description               = "Legacy_Next.js_home_portal"
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

$homeWebClientConfig = @{
  clientId                  = "home-web"
  name                      = "PCWaechter_Home_Portal_Web"
  description               = "React_Vite_home_portal"
  enabled                   = $true
  publicClient              = $true
  standardFlowEnabled       = $true
  implicitFlowEnabled       = $false
  directAccessGrantsEnabled = $false
  serviceAccountsEnabled    = $false
  protocol                  = "openid-connect"
  redirectUris              = @(
    "https://home.xn--pcwchter-2za.de/*",
    "http://localhost:13001/*",
    "http://localhost:3000/*",
    "http://localhost:5173/*"
  )
  webOrigins                = @(
    "https://home.xn--pcwchter-2za.de",
    "http://localhost:13001",
    "http://localhost:3000",
    "http://localhost:5173"
  )
  attributes                = @{
    "pkce.code.challenge.method" = "S256"
    "post.logout.redirect.uris" = "https://home.xn--pcwchter-2za.de/*##http://localhost:13001/*##http://localhost:3000/*##http://localhost:5173/*"
  }
}
$homeWebClientId = Upsert-Client -Realm $realm -ClientId "home-web" -ClientConfig $homeWebClientConfig
Ensure-ProtocolMapper -Realm $realm -ClientInternalId $homeWebClientId -MapperName "pcwaechter-api-audience" -MapperConfig @{
  name            = "pcwaechter-api-audience"
  protocol        = "openid-connect"
  protocolMapper  = "oidc-audience-mapper"
  consentRequired = $false
  config          = @{
    "included.custom.audience" = "pcwaechter-api"
    "access.token.claim"       = "true"
    "id.token.claim"           = "false"
  }
}
Ensure-ProtocolMapper -Realm $realm -ClientInternalId $homeWebClientId -MapperName "api-audience" -MapperConfig @{
  name            = "api-audience"
  protocol        = "openid-connect"
  protocolMapper  = "oidc-audience-mapper"
  consentRequired = $false
  config          = @{
    "included.custom.audience" = "api"
    "access.token.claim"       = "true"
    "id.token.claim"           = "false"
  }
}
Ensure-ProtocolMapper -Realm $realm -ClientInternalId $homeWebClientId -MapperName "realm-roles" -MapperConfig @{
  name            = "realm-roles"
  protocol        = "openid-connect"
  protocolMapper  = "oidc-usermodel-realm-role-mapper"
  consentRequired = $false
  config          = @{
    "claim.name"            = "roles"
    "jsonType.label"        = "String"
    "multivalued"           = "true"
    "userinfo.token.claim"  = "true"
    "access.token.claim"    = "true"
    "id.token.claim"        = "false"
  }
}

$zammadClientConfig = @{
  clientId                  = "zammad"
  name                      = "PCWaechter_Support"
  description               = "Zammad_native_OpenID_Connect_login"
  enabled                   = $true
  publicClient              = $true
  standardFlowEnabled       = $true
  implicitFlowEnabled       = $false
  directAccessGrantsEnabled = $false
  serviceAccountsEnabled    = $false
  protocol                  = "openid-connect"
  redirectUris              = @(
    "http://localhost:3001/auth/openid_connect/callback",
    "https://support.xn--pcwchter-2za.de/auth/openid_connect/callback"
  )
  webOrigins                = @(
    "http://localhost:3001",
    "https://support.xn--pcwchter-2za.de"
  )
  attributes                = @{
    "pkce.code.challenge.method"         = "S256"
    "post.logout.redirect.uris"          = "http://localhost:3001/*##https://support.xn--pcwchter-2za.de/*"
    "backchannel.logout.session.required" = "true"
    "backchannel.logout.url"             = "http://localhost:3001/auth/openid_connect/backchannel_logout"
  }
}
$zammadClientId = Upsert-Client -Realm $realm -ClientId "zammad" -ClientConfig $zammadClientConfig

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
Ensure-ProtocolMapper -Realm $realm -ClientInternalId $desktopClientId -MapperName "api-audience" -MapperConfig @{
  name            = "api-audience"
  protocol        = "openid-connect"
  protocolMapper  = "oidc-audience-mapper"
  consentRequired = $false
  config          = @{
    "included.custom.audience" = "api"
    "access.token.claim"       = "true"
    "id.token.claim"           = "false"
  }
}

$apiClientConfig = @{
  clientId                  = "api"
  name                      = "PCWaechter_API"
  description               = "Canonical_API_audience_client"
  enabled                   = $true
  publicClient              = $false
  standardFlowEnabled       = $false
  implicitFlowEnabled       = $false
  directAccessGrantsEnabled = $false
  serviceAccountsEnabled    = $true
  protocol                  = "openid-connect"
}
$apiClientId = Upsert-Client -Realm $realm -ClientId "api" -ClientConfig $apiClientConfig

$desktopCanonicalClientConfig = @{
  clientId                  = "desktop-client"
  name                      = "PCWaechter_Desktop_Client_Canonical"
  description               = "Canonical_desktop_client"
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
$desktopCanonicalClientId = Upsert-Client -Realm $realm -ClientId "desktop-client" -ClientConfig $desktopCanonicalClientConfig
Ensure-ProtocolMapper -Realm $realm -ClientInternalId $desktopCanonicalClientId -MapperName "api-audience" -MapperConfig @{
  name            = "api-audience"
  protocol        = "openid-connect"
  protocolMapper  = "oidc-audience-mapper"
  consentRequired = $false
  config          = @{
    "included.custom.audience" = "api"
    "access.token.claim"       = "true"
    "id.token.claim"           = "false"
  }
}

$usersToEnsure = @(
  @{ username = "owner"; email = "owner@pcwaechter.local"; firstName = "Console"; lastName = "Owner"; role = "pcw_admin"; group = "pcw-admins"; password = $ownerPass },
  @{ username = "admin-console"; email = "admin@pcwaechter.local"; firstName = "Console"; lastName = "Admin"; role = "pcw_console"; group = "pcw-console"; password = $adminPass },
  @{ username = "support-console"; email = "support@pcwaechter.local"; firstName = "Console"; lastName = "Support"; role = "pcw_support"; group = "pcw-support"; password = $userPass }
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
    console_web = $consoleWebClientId
    home = $homeClientId
    home_web = $homeWebClientId
    api = $apiClientId
    zammad = $zammadClientId
    desktop = $desktopClientId
    desktop_client = $desktopCanonicalClientId
  }
  bootstrapPasswords = [pscustomobject]@{
    owner = $ownerPass
    admin_console = $adminPass
    support_console = $userPass
  }
  users = $usersOut
}

$result | ConvertTo-Json -Depth 8

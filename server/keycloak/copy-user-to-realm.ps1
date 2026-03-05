param(
  [Parameter(Mandatory = $true)]
  [string]$SourceAdminUser,
  [Parameter(Mandatory = $true)]
  [string]$SourceAdminPassword,
  [Parameter(Mandatory = $true)]
  [string]$TargetRealm,
  [Parameter(Mandatory = $true)]
  [string]$Username,
  [Parameter(Mandatory = $true)]
  [string]$Email,
  [Parameter(Mandatory = $true)]
  [string]$Password,
  [string]$Role = "admin",
  [string]$GroupName = "console-admin"
)

$ErrorActionPreference = "Stop"

function Invoke-KC {
  param([string[]]$Arguments)

  $outFile = [System.IO.Path]::GetTempFileName()
  $errFile = [System.IO.Path]::GetTempFileName()
  try {
    $proc = Start-Process `
      -FilePath "docker" `
      -ArgumentList (@("exec", "pcwaechter-keycloak", "/opt/keycloak/bin/kcadm.sh") + $Arguments) `
      -NoNewWindow `
      -PassThru `
      -Wait `
      -RedirectStandardOutput $outFile `
      -RedirectStandardError $errFile

    $stdout = if (Test-Path $outFile) { Get-Content -Raw $outFile } else { "" }
    $stderr = if (Test-Path $errFile) { Get-Content -Raw $errFile } else { "" }
    $combined = ($stdout + "`n" + $stderr).Trim()

    if ($proc.ExitCode -ne 0) {
      throw "kcadm failed: $($Arguments -join ' ')`n$combined"
    }
    return $combined
  }
  finally {
    Remove-Item -Force -ErrorAction SilentlyContinue $outFile, $errFile
  }
}

Invoke-KC -Arguments @(
  "config", "credentials",
  "--server", "http://localhost:8080",
  "--realm", "master",
  "--user", $SourceAdminUser,
  "--password", $SourceAdminPassword
) | Out-Null

function Get-RealmUser {
  param(
    [string]$Realm,
    [string]$LookupUsername
  )
  $allUsersRaw = Invoke-KC -Arguments @("get", "users", "-r", $Realm, "--fields", "id,username,email,enabled")
  $allUsers = @($allUsersRaw | ConvertFrom-Json)
  return ($allUsers | Where-Object { $_.username -eq $LookupUsername } | Select-Object -First 1)
}

$user = Get-RealmUser -Realm $TargetRealm -LookupUsername $Username

if (-not $user) {
  Invoke-KC -Arguments @(
    "create", "users", "-r", $TargetRealm,
    "-s", "username=$Username",
    "-s", "enabled=true",
    "-s", "email=$Email",
    "-s", "emailVerified=true"
  ) | Out-Null
  $user = Get-RealmUser -Realm $TargetRealm -LookupUsername $Username
}

if (-not $user) {
  throw "User $Username konnte im Realm $TargetRealm nicht gefunden/erstellt werden."
}

Invoke-KC -Arguments @(
  "update", "users/$($user.id)", "-r", $TargetRealm, "-n",
  "-s", "enabled=true",
  "-s", "email=$Email",
  "-s", "emailVerified=true"
) | Out-Null

Invoke-KC -Arguments @("set-password", "-r", $TargetRealm, "--userid", "$($user.id)", "--new-password", $Password) | Out-Null

try {
  Invoke-KC -Arguments @("add-roles", "-r", $TargetRealm, "--uid", "$($user.id)", "--rolename", $Role) | Out-Null
}
catch {
  if (-not $_.Exception.Message.Contains("already")) {
    throw
  }
}

$groupsRaw = Invoke-KC -Arguments @("get", "groups", "-r", $TargetRealm, "-q", "search=$GroupName", "--fields", "id,name,path")
$groups = @($groupsRaw | ConvertFrom-Json)
$group = $groups | Where-Object { $_.name -eq $GroupName } | Select-Object -First 1
if ($group) {
  try {
    Invoke-KC -Arguments @("update", "users/$($user.id)/groups/$($group.id)", "-r", $TargetRealm, "-n") | Out-Null
  }
  catch {
    if (-not $_.Exception.Message.Contains("exists")) {
      throw
    }
  }
}

$rolesRaw = Invoke-KC -Arguments @("get", "users/$($user.id)/role-mappings/realm/composite", "-r", $TargetRealm, "--fields", "name")
$roleNames = @($rolesRaw | ConvertFrom-Json | ForEach-Object { [string]$_.name } | Sort-Object -Unique)

$userGroupsRaw = Invoke-KC -Arguments @("get", "users/$($user.id)/groups", "-r", $TargetRealm, "--fields", "name,path")
$userGroups = @($userGroupsRaw | ConvertFrom-Json | ForEach-Object { [string]$_.name } | Sort-Object -Unique)

[pscustomobject]@{
  realm = $TargetRealm
  id = [string]$user.id
  username = $Username
  email = $Email
  enabled = $true
  roles = $roleNames
  groups = $userGroups
} | ConvertTo-Json -Depth 6

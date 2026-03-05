Set-StrictMode -Version Latest

$API_BASE_URL = if ($env:API_BASE_URL) { $env:API_BASE_URL } else { "http://localhost:18080" }
$COMPOSE_FILE = if ($env:COMPOSE_FILE) { $env:COMPOSE_FILE } else { "server/infra/compose/docker-compose.yml" }
$ENV_FILE = if ($env:ENV_FILE) { $env:ENV_FILE } else { ".env" }
$SMOKE_TIMEOUT = if ($env:SMOKE_TIMEOUT) { $env:SMOKE_TIMEOUT } else { "15" }
$TEST_WEBHOOK_SECRET = if ($env:TEST_WEBHOOK_SECRET) { $env:TEST_WEBHOOK_SECRET } else { "" }

$script:PASS_COUNT = 0
$script:FAIL_COUNT = 0
$script:SKIP_COUNT = 0
$script:API_KEY = ""
$script:API_KEY_HEADER = ""

function Write-Pass {
    param([string]$Message)
    $script:PASS_COUNT += 1
    Write-Output ("[PASS] {0}" -f $Message)
}

function Write-Fail {
    param([string]$Message)
    $script:FAIL_COUNT += 1
    Write-Output ("[FAIL] {0}" -f $Message)
}

function Write-Skip {
    param([string]$Message)
    $script:SKIP_COUNT += 1
    Write-Output ("[SKIP] {0}" -f $Message)
}

function Mask-Secret {
    param([string]$Value)
    if ([string]::IsNullOrEmpty($Value)) {
        return "***"
    }
    if ($Value.Length -le 4) {
        return "***"
    }
    $start = $Value.Substring(0, 2)
    $end = $Value.Substring($Value.Length - 2, 2)
    return ("{0}***{1}" -f $start, $end)
}

function Get-EnvFileValue {
    param([string]$Key)
    if (-not (Test-Path -Path $ENV_FILE)) {
        return ""
    }
    $pattern = "^\s*{0}\s*=" -f [regex]::Escape($Key)
    $line = Get-Content -Path $ENV_FILE | Where-Object { $_ -match $pattern } | Select-Object -Last 1
    if (-not $line) {
        return ""
    }
    return ($line -replace $pattern, "").Trim()
}

function Get-FirstCsvItem {
    param([string]$Raw)
    if ([string]::IsNullOrWhiteSpace($Raw)) {
        return ""
    }
    foreach ($part in ($Raw -split ",")) {
        $trimmed = $part.Trim()
        if ($trimmed) {
            return $trimmed
        }
    }
    return ""
}

function Get-JsonStringValue {
    param(
        [string]$Body,
        [string]$Key
    )
    if ([string]::IsNullOrWhiteSpace($Body)) {
        return ""
    }
    try {
        $obj = $Body | ConvertFrom-Json -ErrorAction Stop
        $value = $obj.$Key
        if ($null -eq $value) {
            return ""
        }
        return [string]$value
    }
    catch {
        return ""
    }
}

function New-TempFilePath {
    return [System.IO.Path]::GetTempFileName()
}

function Invoke-CurlStatus {
    param(
        [string]$Method,
        [string]$Url,
        [string]$DataFile,
        [string[]]$Headers
    )
    $args = @("-sS", "-o", "NUL", "-w", "%{http_code}", "--max-time", $SMOKE_TIMEOUT, "-X", $Method, $Url)
    foreach ($header in $Headers) {
        $args += @("-H", $header)
    }
    if ($DataFile) {
        $args += @("--data-binary", "@$DataFile")
    }

    $status = & curl.exe @args 2>$null
    if ($LASTEXITCODE -ne 0) {
        return "000"
    }
    return ($status | Out-String).Trim()
}

function Invoke-CurlBodyAndStatus {
    param(
        [string]$Method,
        [string]$Url,
        [string]$DataFile,
        [string[]]$Headers
    )
    $bodyFile = New-TempFilePath
    $args = @("-sS", "-o", $bodyFile, "-w", "%{http_code}", "--max-time", $SMOKE_TIMEOUT, "-X", $Method, $Url)
    foreach ($header in $Headers) {
        $args += @("-H", $header)
    }
    if ($DataFile) {
        $args += @("--data-binary", "@$DataFile")
    }

    $status = & curl.exe @args 2>$null
    if ($LASTEXITCODE -ne 0) {
        Remove-Item -Path $bodyFile -Force -ErrorAction SilentlyContinue
        return [PSCustomObject]@{
            Status = "000"
            Body = ""
        }
    }

    $body = Get-Content -Path $bodyFile -Raw
    Remove-Item -Path $bodyFile -Force -ErrorAction SilentlyContinue
    return [PSCustomObject]@{
        Status = ($status | Out-String).Trim()
        Body = $body
    }
}

function Resolve-ApiKey {
    $script:API_KEY = ""
    $script:API_KEY_HEADER = ""

    if ($env:SMOKE_API_KEY) {
        $script:API_KEY = $env:SMOKE_API_KEY
        if ($env:SMOKE_API_KEY_HEADER) {
            $script:API_KEY_HEADER = $env:SMOKE_API_KEY_HEADER
        }
        else {
            $script:API_KEY_HEADER = "X-API-Key"
        }
        return
    }

    $apiKeys = if ($env:API_KEYS) { $env:API_KEYS } else { Get-EnvFileValue -Key "API_KEYS" }
    $firstApi = Get-FirstCsvItem -Raw $apiKeys
    if ($firstApi) {
        $script:API_KEY = $firstApi
        $script:API_KEY_HEADER = "X-API-Key"
        return
    }

    $agentKeys = if ($env:AGENT_API_KEYS) { $env:AGENT_API_KEYS } else { Get-EnvFileValue -Key "AGENT_API_KEYS" }
    $firstAgent = Get-FirstCsvItem -Raw $agentKeys
    if ($firstAgent) {
        $script:API_KEY = $firstAgent
        $script:API_KEY_HEADER = "X-Agent-Api-Key"
    }
}

function Has-ConfiguredApiKeys {
    $apiKeys = if ($env:API_KEYS) { $env:API_KEYS } else { Get-EnvFileValue -Key "API_KEYS" }
    $agentKeys = if ($env:AGENT_API_KEYS) { $env:AGENT_API_KEYS } else { Get-EnvFileValue -Key "AGENT_API_KEYS" }
    $firstApi = Get-FirstCsvItem -Raw $apiKeys
    $firstAgent = Get-FirstCsvItem -Raw $agentKeys
    return [bool]($firstApi -or $firstAgent)
}

function Test-Health {
    $status1 = Invoke-CurlStatus -Method "GET" -Url "$API_BASE_URL/health" -DataFile "" -Headers @()
    $status2 = Invoke-CurlStatus -Method "GET" -Url "$API_BASE_URL/api/v1/health" -DataFile "" -Headers @()
    if ($status1 -eq "200" -and $status2 -eq "200") {
        Write-Pass "health endpoints (/health, /api/v1/health) -> 200"
    }
    else {
        Write-Fail ("health endpoints expected 200/200, got {0}/{1}" -f $status1, $status2)
    }
}

function Test-PreAuthRateLimit {
    if (-not (Has-ConfiguredApiKeys)) {
        Write-Skip "pre-auth rate limit: API_KEYS/AGENT_API_KEYS not configured, cannot assert 401 baseline"
        return
    }

    $payloadFile = New-TempFilePath
@'
{"device_install_id":"smoke-rl-device","hostname":"smoke-rl","os":{"name":"Windows","version":"11","build":"26000"},"agent":{"version":"1.0.0","channel":"stable"},"network":{"primary_ip":"127.0.0.1","macs":[]}}
'@ | Set-Content -Path $payloadFile -NoNewline

    Test-PreAuthRateLimitPath -Path "/agent/register" -PayloadFile $payloadFile
    Test-PreAuthRateLimitPath -Path "/api/v1/agent/register" -PayloadFile $payloadFile
    Remove-Item -Path $payloadFile -Force -ErrorAction SilentlyContinue
}

function Test-PreAuthRateLimitPath {
    param(
        [string]$Path,
        [string]$PayloadFile
    )

    $statuses = @()
    for ($i = 1; $i -le 11; $i++) {
        $status = Invoke-CurlStatus -Method "POST" -Url "$API_BASE_URL$Path" -DataFile $PayloadFile -Headers @(
            "Content-Type: application/json",
            "X-API-Key: invalid-smoke-key"
        )
        $statuses += $status
    }

    $ok = $true
    for ($i = 0; $i -lt 10; $i++) {
        if ($statuses[$i] -ne "401") {
            $ok = $false
        }
    }
    if ($statuses[10] -ne "429") {
        $ok = $false
    }

    if ($ok) {
        Write-Pass ("pre-auth rate limit ({0}): 401 x10 -> 429" -f $Path)
    }
    else {
        Write-Fail ("pre-auth rate limit {0} expected 401 x10 -> 429, got: {1}" -f $Path, ($statuses -join " "))
    }
}

function Test-BodyLimit {
    $payloadFile = New-TempFilePath
    ("x" * (1024 * 1024 + 1)) | Set-Content -Path $payloadFile -NoNewline
    Test-BodyLimitPath -Path "/api/v1/payments/webhook" -PayloadFile $payloadFile
    Test-BodyLimitPath -Path "/payments/webhook" -PayloadFile $payloadFile
    Remove-Item -Path $payloadFile -Force -ErrorAction SilentlyContinue
}

function Test-BodyLimitPath {
    param(
        [string]$Path,
        [string]$PayloadFile
    )

    $status = Invoke-CurlStatus -Method "POST" -Url "$API_BASE_URL$Path" -DataFile $PayloadFile -Headers @(
        "Content-Type: application/json"
    )
    if ($status -eq "413") {
        Write-Pass ("body limit {0} >1MB -> 413" -f $Path)
    }
    else {
        Write-Fail ("body limit {0} expected 413, got {1}" -f $Path, $status)
    }
}

function Test-LegacyHeaders {
    $headerFile = New-TempFilePath
    $args = @("-sS", "-o", "NUL", "-D", $headerFile, "-w", "%{http_code}", "--max-time", $SMOKE_TIMEOUT, "$API_BASE_URL/license/status?device_install_id=smoke-legacy")
    $status = & curl.exe @args 2>$null
    if ($LASTEXITCODE -ne 0) {
        Remove-Item -Path $headerFile -Force -ErrorAction SilentlyContinue
        Write-Fail "legacy headers check failed: request did not complete"
        return
    }

    $headersRaw = Get-Content -Path $headerFile -Raw
    Remove-Item -Path $headerFile -Force -ErrorAction SilentlyContinue
    $hasDeprecation = $headersRaw -match "(?im)^deprecation:"
    $hasSunset = $headersRaw -match "(?im)^sunset:"
    $hasLink = $headersRaw -match "(?im)^link:"

    if ((($status | Out-String).Trim() -eq "401") -and $hasDeprecation -and $hasSunset -and $hasLink) {
        Write-Pass "legacy headers on /license/status (401 includes Deprecation/Sunset/Link)"
    }
    else {
        Write-Fail ("legacy headers expected status 401 with Deprecation/Sunset/Link, got status={0}" -f (($status | Out-String).Trim()))
    }
}

function Test-DeviceTokenRotate {
    Resolve-ApiKey
    if (-not $script:API_KEY -or -not $script:API_KEY_HEADER) {
        Write-Skip "device token rotate: API key missing (SMOKE_API_KEY or .env API_KEYS/AGENT_API_KEYS)"
        return
    }

    $maskedKey = Mask-Secret -Value $script:API_KEY
    $deviceId = "smoke-device-{0}" -f ([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())

    $registerPayload = New-TempFilePath
    $registerJson = [ordered]@{
        device_install_id = $deviceId
        hostname = "smoke-host"
        os = [ordered]@{
            name = "Windows"
            version = "11"
            build = "26000"
        }
        agent = [ordered]@{
            version = "1.0.0"
            channel = "stable"
        }
        network = [ordered]@{
            primary_ip = "10.0.0.1"
            macs = @("00:11:22:33:44:55")
        }
    } | ConvertTo-Json -Compress
    $registerJson | Set-Content -Path $registerPayload -NoNewline

    $registerResp = Invoke-CurlBodyAndStatus -Method "POST" -Url "$API_BASE_URL/api/v1/agent/register" -DataFile $registerPayload -Headers @(
        "Content-Type: application/json",
        ("{0}: {1}" -f $script:API_KEY_HEADER, $script:API_KEY)
    )
    Remove-Item -Path $registerPayload -Force -ErrorAction SilentlyContinue

    $oldToken = Get-JsonStringValue -Body $registerResp.Body -Key "device_token"
    if ($registerResp.Status -ne "200" -or -not $oldToken) {
        Write-Fail ("device token rotate: register failed (status={0}, key={1}:{2})" -f $registerResp.Status, $script:API_KEY_HEADER, $maskedKey)
        return
    }

    $rotateResp = Invoke-CurlBodyAndStatus -Method "POST" -Url "$API_BASE_URL/api/v1/agent/token/rotate" -DataFile "" -Headers @(
        ("X-Device-Token: {0}" -f $oldToken)
    )
    $newToken = Get-JsonStringValue -Body $rotateResp.Body -Key "device_token"
    if ($rotateResp.Status -ne "200" -or -not $newToken) {
        Write-Fail ("device token rotate: rotate failed (status={0})" -f $rotateResp.Status)
        return
    }

    $heartbeatPayload = New-TempFilePath
    $heartbeatJson = [ordered]@{
        device_install_id = $deviceId
        at = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        status = @{}
    } | ConvertTo-Json -Compress
    $heartbeatJson | Set-Content -Path $heartbeatPayload -NoNewline

    $oldStatus = Invoke-CurlStatus -Method "POST" -Url "$API_BASE_URL/api/v1/agent/heartbeat" -DataFile $heartbeatPayload -Headers @(
        "Content-Type: application/json",
        ("X-Device-Token: {0}" -f $oldToken)
    )
    $newStatus = Invoke-CurlStatus -Method "POST" -Url "$API_BASE_URL/api/v1/agent/heartbeat" -DataFile $heartbeatPayload -Headers @(
        "Content-Type: application/json",
        ("X-Device-Token: {0}" -f $newToken)
    )
    Remove-Item -Path $heartbeatPayload -Force -ErrorAction SilentlyContinue

    if ($oldStatus -eq "401" -and $newStatus -eq "200") {
        Write-Pass "device token rotate: old token revoked (401), new token valid (200)"
    }
    else {
        Write-Fail ("device token rotate expected old=401/new=200, got old={0} new={1}" -f $oldStatus, $newStatus)
    }
}

function Get-HexHmacSha256 {
    param(
        [string]$Secret,
        [string]$Data
    )
    $hmac = New-Object System.Security.Cryptography.HMACSHA256 ([System.Text.Encoding]::UTF8.GetBytes($Secret))
    try {
        $hash = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Data))
        return -join ($hash | ForEach-Object { $_.ToString("x2") })
    }
    finally {
        $hmac.Dispose()
    }
}

function Test-StripeIdempotency {
    if (-not $TEST_WEBHOOK_SECRET) {
        Write-Skip "stripe idempotency: TEST_WEBHOOK_SECRET not set"
        return
    }

    $eventId = "evt_smoke_idem_{0}" -f ([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())
    $payload = [ordered]@{
        id = $eventId
        type = "smoke.event"
        data = [ordered]@{
            object = @{}
        }
    } | ConvertTo-Json -Compress

    $timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()
    $signedPayload = "{0}.{1}" -f $timestamp, $payload
    $signature = Get-HexHmacSha256 -Secret $TEST_WEBHOOK_SECRET -Data $signedPayload
    $signatureHeader = "t={0},v1={1}" -f $timestamp, $signature

    $payloadFile = New-TempFilePath
    $payload | Set-Content -Path $payloadFile -NoNewline
    $status1 = Invoke-CurlStatus -Method "POST" -Url "$API_BASE_URL/api/v1/payments/webhook" -DataFile $payloadFile -Headers @(
        "Content-Type: application/json",
        ("Stripe-Signature: {0}" -f $signatureHeader)
    )
    $status2 = Invoke-CurlStatus -Method "POST" -Url "$API_BASE_URL/api/v1/payments/webhook" -DataFile $payloadFile -Headers @(
        "Content-Type: application/json",
        ("Stripe-Signature: {0}" -f $signatureHeader)
    )
    Remove-Item -Path $payloadFile -Force -ErrorAction SilentlyContinue

    if ($status1 -ne "200" -or $status2 -ne "200") {
        Write-Fail ("stripe idempotency webhook calls expected 200/200, got {0}/{1}" -f $status1, $status2)
        return
    }

    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Skip "stripe idempotency: webhook delivered (200/200), docker not available for DB count check"
        return
    }

    $count = ""
    try {
        $countOutput = & docker compose -f $COMPOSE_FILE --env-file $ENV_FILE exec -T postgres `
            psql -U pcwaechter -d pcwaechter -t -A `
            -c "SELECT count(*) FROM webhook_events WHERE stripe_event_id='${eventId}';" 2>$null
        $count = ($countOutput | Out-String).Trim()
    }
    catch {
        $count = ""
    }

    if (-not $count) {
        Write-Skip "stripe idempotency: webhook delivered (200/200), DB count check unavailable"
        return
    }

    try {
        & docker compose -f $COMPOSE_FILE --env-file $ENV_FILE exec -T postgres `
            psql -U pcwaechter -d pcwaechter `
            -c "DELETE FROM webhook_events WHERE stripe_event_id='${eventId}';" 1>$null 2>$null
    }
    catch {
        # cleanup best effort
    }

    if ($count -eq "1") {
        Write-Pass "stripe idempotency: same event_id processed once (DB count=1)"
    }
    else {
        Write-Fail ("stripe idempotency expected DB count=1, got {0}" -f $count)
    }
}

if (-not (Get-Command curl.exe -ErrorAction SilentlyContinue)) {
    Write-Output "[FAIL] curl.exe not found. Install curl and retry."
    exit 1
}

Write-Output ("Smoke tests against {0}" -f $API_BASE_URL)
Test-Health
Test-PreAuthRateLimit
Test-BodyLimit
Test-LegacyHeaders
Test-DeviceTokenRotate
Test-StripeIdempotency

Write-Output ""
Write-Output ("Summary: PASS={0} FAIL={1} SKIP={2}" -f $script:PASS_COUNT, $script:FAIL_COUNT, $script:SKIP_COUNT)
if ($script:FAIL_COUNT -gt 0) {
    exit 1
}

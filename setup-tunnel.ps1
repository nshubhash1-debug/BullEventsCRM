# One-time Cloudflare Tunnel setup for portal.bullrealtyglobal.tech
#
# Run this once. It needs a browser, because step 1 signs in to *your*
# Cloudflare account and picks the zone — nobody else can do that part for you.
#
# After this, serve-public.ps1 is all you need to bring the site up.

$ErrorActionPreference = 'Stop'

$Cloudflared = 'C:\Program Files (x86)\cloudflared\cloudflared.exe'
$Hostname    = 'portal.bullrealtyglobal.tech'
$TunnelName  = 'bullrealty-portal'
$ConfigDir   = Join-Path $PSScriptRoot 'cloudflared'
$ConfigFile  = Join-Path $ConfigDir 'config.yml'

if (-not (Test-Path $Cloudflared)) { throw "cloudflared not found at $Cloudflared" }

# ---- 1. authorise this machine against your Cloudflare account -------------
# Opens a browser. Pick the bullrealtyglobal.tech zone when it asks.
$certPath = Join-Path $env:USERPROFILE '.cloudflared\cert.pem'

if (-not (Test-Path $certPath)) {
    Write-Host 'Signing in to Cloudflare — a browser window will open.' -ForegroundColor Yellow
    Write-Host 'Choose the bullrealtyglobal.tech zone on the page that appears.' -ForegroundColor Yellow
    & $Cloudflared tunnel login
    if (-not (Test-Path $certPath)) { throw 'Cloudflare sign-in did not complete.' }
}
else {
    Write-Host 'Already signed in to Cloudflare.' -ForegroundColor Green
}

# ---- 2. create the tunnel (idempotent) ------------------------------------
$existing = & $Cloudflared tunnel list --output json | ConvertFrom-Json
$tunnel = $existing | Where-Object { $_.name -eq $TunnelName } | Select-Object -First 1

if (-not $tunnel) {
    Write-Host "Creating tunnel '$TunnelName'..." -ForegroundColor Yellow
    & $Cloudflared tunnel create $TunnelName
    $existing = & $Cloudflared tunnel list --output json | ConvertFrom-Json
    $tunnel = $existing | Where-Object { $_.name -eq $TunnelName } | Select-Object -First 1
}

if (-not $tunnel) { throw "Could not create or find the tunnel '$TunnelName'." }

$tunnelId = $tunnel.id
Write-Host "Tunnel id: $tunnelId" -ForegroundColor Cyan

$credentials = Join-Path $env:USERPROFILE ".cloudflared\$tunnelId.json"
if (-not (Test-Path $credentials)) { throw "Tunnel credentials not found at $credentials" }

# ---- 3. write the id and credentials path into config.yml -----------------
$config = Get-Content $ConfigFile -Raw
$config = $config -replace 'REPLACE_WITH_TUNNEL_ID', $tunnelId
# Quoted, and the backslashes doubled to survive YAML's double-quoted escaping.
# An unquoted Windows path works by luck rather than by rule here, and a path
# with a space in it would not work at all.
$escaped = '"' + ($credentials -replace '\\', '\\\\') + '"'
$config = $config -replace 'REPLACE_WITH_CREDENTIALS_PATH', $escaped
Set-Content -Path $ConfigFile -Value $config -Encoding utf8

Write-Host "Wrote $ConfigFile" -ForegroundColor Green

# ---- 4. point the DNS record at the tunnel --------------------------------
# Idempotent: re-running just re-points the existing CNAME.
Write-Host "Routing $Hostname to the tunnel..." -ForegroundColor Yellow
& $Cloudflared tunnel route dns $TunnelName $Hostname

Write-Host ''
Write-Host "Done. $Hostname is now pointed at this machine." -ForegroundColor Green
Write-Host 'Nothing is served until serve-public.ps1 is running.' -ForegroundColor Cyan

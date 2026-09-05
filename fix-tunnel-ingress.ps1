# Adds portal.bullrealtyglobal.tech to the tunnel that already owns it.
#
# Why this rather than a DNS change: the record already points at the
# whatsjet-bullrealty tunnel (f9883ae9…), and cloudflared will not re-point a
# hostname from one of your own tunnels to another — `--overwrite-dns` reports
# "already configured" and does nothing. The record is fine; the tunnel simply
# had no rule for that hostname, so every request fell through to its 404.
#
# So we add the rule instead of moving the record. One tunnel, already running
# as a service, already surviving reboots.
#
# The two hostnames that tunnel already serves — whatsapp. and ws. — are left
# exactly as they are. Restarting the service does interrupt them for a few
# seconds; that is the only side effect.
#
# Run in an elevated PowerShell.

$ErrorActionPreference = 'Stop'

$Config = 'C:\Windows\System32\config\systemprofile\.cloudflared\config.yml'
$Backup = Join-Path $PSScriptRoot ('whatsjet-config.backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.yml')

if (-not (Test-Path $Config)) { throw "Tunnel config not found at $Config" }

Copy-Item $Config $Backup -Force
Write-Host "Backed up to $Backup" -ForegroundColor Cyan

$text = Get-Content $Config -Raw

if ($text -match 'portal\.bullrealtyglobal\.tech') {
    Write-Host 'portal.bullrealtyglobal.tech is already in the config — nothing to add.' -ForegroundColor Yellow
}
else {
    $rules = @'
  # Bull Realty CRM. The hostname already pointed at this tunnel but had no
  # rule, so every request fell through to the 404 below.
  #
  # App and API share one hostname, split by path, so the browser never makes a
  # cross-origin request and CORS stays out of it entirely.
  - hostname: portal.bullrealtyglobal.tech
    path: ^/api/.*
    service: http://localhost:5080
  - hostname: portal.bullrealtyglobal.tech
    path: ^/health$
    service: http://localhost:5080
  - hostname: portal.bullrealtyglobal.tech
    service: http://localhost:3000

  - service: http_status:404
'@

    # Inserted immediately before the catch-all, which must stay last: cloudflared
    # takes the first rule that matches, so anything after it is unreachable.
    $updated = $text -replace '(?m)^\s*-\s*service:\s*http_status:404\s*$', $rules

    if ($updated -eq $text) { throw 'Could not find the catch-all rule to insert before.' }

    Set-Content -Path $Config -Value $updated -Encoding ascii
    Write-Host 'Added the portal rules.' -ForegroundColor Green
}

# ---- restart the tunnel so it reads the new config ------------------------
Write-Host 'Restarting the cloudflared service...' -ForegroundColor Yellow
Write-Host '  (whatsapp. and ws. will blip for a few seconds)' -ForegroundColor DarkGray

Restart-Service -Name 'Cloudflared' -Force -ErrorAction SilentlyContinue

if (-not (Get-Service -Name 'Cloudflared' -ErrorAction SilentlyContinue)) {
    # The service is registered under a different name on some installs.
    $svc = Get-Service | Where-Object { $_.Name -like '*cloudflared*' } | Select-Object -First 1
    if ($svc) { Restart-Service -Name $svc.Name -Force }
    else { throw 'Could not find the cloudflared service to restart.' }
}

Start-Sleep -Seconds 8

# ---- prove it -------------------------------------------------------------
try {
    $r = Invoke-WebRequest -Uri 'https://portal.bullrealtyglobal.tech/health' -TimeoutSec 20 -UseBasicParsing
    Write-Host ''
    Write-Host "portal.bullrealtyglobal.tech/health -> $($r.StatusCode)" -ForegroundColor Green
    Write-Host 'The tunnel is serving the CRM.' -ForegroundColor Green
}
catch {
    Write-Host ''
    Write-Host "Still not answering: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "The original config is at $Backup" -ForegroundColor Red
}

# Brings the whole CRM up on this PC and publishes it through the Cloudflare
# tunnel at portal.bullrealtyglobal.tech
#
# Order matters: the API first (the app is useless without it), then the web
# build, then the tunnel — so nothing is reachable from the internet until both
# services behind it are actually answering.
#
# Run setup-tunnel.ps1 once before this.

$ErrorActionPreference = 'Stop'

$Root        = $PSScriptRoot
$Api         = Join-Path $Root 'BullEvents.Api'
$Web         = Join-Path $Root 'bull-realty-crm'
$Cloudflared = 'C:\Program Files (x86)\cloudflared\cloudflared.exe'
$ConfigFile  = Join-Path $Root 'cloudflared\config.yml'
$PublicUrl   = 'https://portal.bullrealtyglobal.tech'

$processes = @()

function Stop-Everything {
    foreach ($p in $script:processes) {
        if ($p -and -not $p.HasExited) {
            Write-Host "  stopping pid $($p.Id)"
            Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

try {
    # ------------------------------------------------------------------ API
    Write-Host 'Starting the API on :5080...' -ForegroundColor Yellow

    # Development on purpose: it is the only environment in which
    # Auth:SkipSecondFactor is honoured, and that flag is what keeps the sign-in
    # code step off. Hosting this properly (Production) turns the code step back
    # on by itself.
    $processes += Start-Process -PassThru -WorkingDirectory $Api -FilePath 'dotnet' `
        -ArgumentList 'run', '--configuration', 'Release', '--no-launch-profile' `
        -Environment @{
            ASPNETCORE_ENVIRONMENT = 'Development'
            ASPNETCORE_URLS        = 'http://127.0.0.1:5080'
        }

    # Wait for it rather than guessing: starting the tunnel in front of a dead
    # API publishes a 502 to the internet.
    Write-Host '  waiting for the API to answer...' -NoNewline
    $ready = $false

    foreach ($attempt in 1..40) {
        Start-Sleep -Seconds 2
        try {
            $r = Invoke-WebRequest -Uri 'http://127.0.0.1:5080/health' -TimeoutSec 3 -UseBasicParsing
            if ($r.StatusCode -eq 200) { $ready = $true; break }
        }
        catch { Write-Host '.' -NoNewline }
    }

    Write-Host ''
    if (-not $ready) { throw 'The API never came up. Check the database connection first.' }
    Write-Host '  API is up.' -ForegroundColor Green

    # ------------------------------------------------------------- frontend
    # The browser must call the API on the public origin, not on localhost —
    # written here rather than into .env.local so that local development keeps
    # working when the tunnel is down. Next loads .env.production.local ahead of
    # .env.local for a production build.
    $envFile = Join-Path $Web '.env.production.local'
    Set-Content -Path $envFile -Encoding utf8 -Value @(
        '# Written by serve-public.ps1. The tunnel serves the app and the API on',
        '# one origin, so the browser calls /api/* on the host it loaded from.',
        "NEXT_PUBLIC_API_URL=$PublicUrl"
    )
    Write-Host "  wrote $envFile" -ForegroundColor DarkGray

    Write-Host 'Building the frontend...' -ForegroundColor Yellow
    Push-Location $Web
    try {
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    }
    finally { Pop-Location }

    Write-Host 'Starting the frontend on :3000...' -ForegroundColor Yellow
    $processes += Start-Process -PassThru -WorkingDirectory $Web -FilePath 'npm' `
        -ArgumentList 'run', 'start', '--', '-H', '127.0.0.1', '-p', '3000'

    Start-Sleep -Seconds 6

    # ---------------------------------------------------------------- tunnel
    Write-Host 'Opening the Cloudflare tunnel...' -ForegroundColor Yellow
    Write-Host ''
    Write-Host "  $PublicUrl" -ForegroundColor Cyan
    Write-Host ''
    Write-Host '  Ctrl+C stops everything.' -ForegroundColor DarkGray
    Write-Host ''

    # Runs in the foreground: this is the process the operator watches, and
    # killing it should take the site off the internet immediately.
    & $Cloudflared tunnel --config $ConfigFile run
}
finally {
    Write-Host ''
    Write-Host 'Shutting down...' -ForegroundColor Yellow
    Stop-Everything
}

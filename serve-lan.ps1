# Runs the CRM on this PC so other machines on the network can reach it.
#
# For testing only. Two things about this setup are deliberately not
# production-safe, and both are switched off by hosting it properly:
#
#   * The environment stays Development, which is the only environment where
#     Auth:SkipSecondFactor is honoured. That is what keeps the sign-in code
#     step off while testing. On AWS the environment is Production, the flag is
#     ignored, and the code step returns on its own.
#   * Every account currently shares the password 123456.
#
# Anyone on this network who reaches port 3000 can therefore sign in as anyone.
# Do not run this on a network you do not control.

$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
$Api = Join-Path $Root 'BullEvents.Api'
$Web = Join-Path $Root 'bull-realty-crm'

# The address other devices use. Change it here if the PC's IP moves, and keep
# bull-realty-crm/.env.local in step — the browser reads the API URL from there
# at build time, so the frontend must be rebuilt after changing it.
$LanIp = '192.168.1.2'

Write-Host "Bull Realty CRM — serving on $LanIp" -ForegroundColor Cyan
Write-Host ''

# ---------------------------------------------------------------- API
Write-Host 'Starting the API on port 5080...' -ForegroundColor Yellow

$apiEnv = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    ASPNETCORE_URLS        = 'http://0.0.0.0:5080'
}

$apiJob = Start-Process -PassThru -WorkingDirectory $Api `
    -FilePath 'dotnet' -ArgumentList 'run', '--configuration', 'Release', '--no-launch-profile' `
    -Environment $apiEnv

Write-Host "  API process id $($apiJob.Id)"

# ---------------------------------------------------------------- frontend
Write-Host 'Building the frontend...' -ForegroundColor Yellow

Push-Location $Web
try {
    & npm run build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }

    Write-Host 'Starting the frontend on port 3000...' -ForegroundColor Yellow

    # -H 0.0.0.0 so the listener is not bound to loopback only; without it the
    # site answers on this PC and nowhere else.
    & npm run start -- -H 0.0.0.0 -p 3000
}
finally {
    Pop-Location

    if (-not $apiJob.HasExited) {
        Write-Host 'Stopping the API...' -ForegroundColor Yellow
        Stop-Process -Id $apiJob.Id -Force
    }
}

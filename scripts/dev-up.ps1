# Avvia database, i tre servizi e il frontend con un comando (#2).
# Uso, dalla cartella del repository:  .\scripts\dev-up.ps1
# Per fermare tutto:                    .\scripts\dev-down.ps1
# Se PowerShell blocca lo script:       powershell -ExecutionPolicy Bypass -File .\scripts\dev-up.ps1
# Le password e la chiave JWT si leggono dal file .env: lo script non le stampa mai.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$runDir = Join-Path $root '.dev'
$envFile = Join-Path $root '.env'

function Fail($message) {
    Write-Host "ERRORE: $message" -ForegroundColor Red
    exit 1
}

function Test-Url($url) {
    try {
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2
        return $response.StatusCode -eq 200
    } catch {
        return $false
    }
}

function Wait-Url($name, $url, $seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Url $url) { return $true }
        Start-Sleep -Seconds 2
    }
    return $false
}

# 1. File .env
if (-not (Test-Path $envFile)) {
    Fail "manca il file .env. Copia .env.example in .env e compila le password (README, passo 2)."
}

$envLines = Get-Content $envFile
$values = @{}
foreach ($line in $envLines) {
    if ($line -match '^\s*([A-Z_][A-Z0-9_]*)=(.*)$') {
        $values[$Matches[1]] = $Matches[2].Trim()
    }
}

$required = 'POSTGRES_ADMIN_PASSWORD', 'BOOKINGS_DB_PASSWORD', 'USERS_DB_PASSWORD', 'MONGO_ADMIN_PASSWORD', 'SPACES_DB_PASSWORD'
$missing = @($required | Where-Object { -not $values[$_] })
if ($missing.Count -gt 0) {
    Fail "nel file .env mancano: $($missing -join ', ')."
}

# La chiave JWT deve essere la stessa per i tre servizi: se manca, ne genero una e la scrivo nel .env.
if (-not $values['JWT_SECRET']) {
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $secret = -join ($bytes | ForEach-Object { $_.ToString('x2') })
    $hasLine = @($envLines | Where-Object { $_ -match '^\s*JWT_SECRET=' }).Count -gt 0
    if ($hasLine) {
        $envLines = $envLines | ForEach-Object { if ($_ -match '^\s*JWT_SECRET=') { "JWT_SECRET=$secret" } else { $_ } }
    } else {
        $envLines = @($envLines) + '' + '# Chiave per firmare i token, la stessa per i tre servizi' + "JWT_SECRET=$secret"
    }
    [IO.File]::WriteAllLines($envFile, [string[]]$envLines)
    $values['JWT_SECRET'] = $secret
    Write-Host "JWT_SECRET era vuota: ne ho generata una nuova nel .env."
}

foreach ($key in $values.Keys) {
    [Environment]::SetEnvironmentVariable($key, $values[$key], 'Process')
}

$pgPort = if ($values['POSTGRES_PORT']) { $values['POSTGRES_PORT'] } else { '5432' }
$mongoPort = if ($values['MONGO_PORT']) { $values['MONGO_PORT'] } else { '27017' }
$env:ConnectionStrings__Spaces = "mongodb://spaces_app:$($values['SPACES_DB_PASSWORD'])@localhost:$mongoPort/spaces?authSource=spaces"
$env:ConnectionStrings__Bookings = "Host=localhost;Port=$pgPort;Database=bookings;Username=bookings_app;Password=$($values['BOOKINGS_DB_PASSWORD'])"
$env:ConnectionStrings__Users = "Host=localhost;Port=$pgPort;Database=users;Username=users_app;Password=$($values['USERS_DB_PASSWORD'])"

# 2. Database
Write-Host "Avvio i database (docker compose up -d --wait)..."
docker compose up -d --wait
if ($LASTEXITCODE -ne 0) { Fail "docker compose non è partito. Docker Desktop è avviato?" }

# 3. Build unica, così i tre servizi non compilano insieme la stessa libreria
New-Item -ItemType Directory -Force $runDir | Out-Null
$pidFile = Join-Path $runDir 'pids.txt'
Write-Host "Compilo i servizi (dotnet build)..."
dotnet build SmartCampus.slnx --nologo -v q
if ($LASTEXITCODE -ne 0) { Fail "dotnet build non è riuscito: leggi l'errore qui sopra." }

# 4. Servizi
$services = @(
    @{ Name = 'Spaces';   Port = 5101 },
    @{ Name = 'Bookings'; Port = 5102 },
    @{ Name = 'Users';    Port = 5103 }
)
foreach ($svc in $services) {
    $health = "http://localhost:$($svc.Port)/health"
    if (Test-Url $health) {
        Write-Host "$($svc.Name) risponde già su :$($svc.Port), non lo riavvio."
        continue
    }
    $project = "src/Services/$($svc.Name)/SmartCampus.$($svc.Name).Api"
    $log = Join-Path $runDir "$($svc.Name).log"
    $process = Start-Process dotnet -ArgumentList 'run', '--no-build', '--project', $project `
        -RedirectStandardOutput $log -RedirectStandardError "$log.err" -WindowStyle Hidden -PassThru
    Add-Content $pidFile "$($process.Id) $($svc.Name)"
    Write-Host "Avviato $($svc.Name) (log in .dev\$($svc.Name).log)"
}

# 5. Frontend
if (Test-Url 'http://localhost:4200') {
    Write-Host "Il frontend risponde già su :4200, non lo riavvio."
} else {
    if (-not (Test-Path 'src/web/node_modules')) {
        Write-Host "Installo le dipendenze del frontend (npm ci)..."
        Push-Location src/web
        npm ci
        $npmExit = $LASTEXITCODE
        Pop-Location
        if ($npmExit -ne 0) { Fail "npm ci non è riuscito." }
    }
    $log = Join-Path $runDir 'web.log'
    $process = Start-Process cmd.exe -ArgumentList '/c', 'npm', 'start' -WorkingDirectory (Join-Path $root 'src/web') `
        -RedirectStandardOutput $log -RedirectStandardError "$log.err" -WindowStyle Hidden -PassThru
    Add-Content $pidFile "$($process.Id) web"
    Write-Host "Avviato il frontend (log in .dev\web.log)"
}

# 6. Attesa dei controlli di salute
$ok = $true
foreach ($svc in $services) {
    if (Wait-Url $svc.Name "http://localhost:$($svc.Port)/health" 120) {
        Write-Host "OK  $($svc.Name)  http://localhost:$($svc.Port)/health"
    } else {
        Write-Host "NO  $($svc.Name) non risponde: leggi .dev\$($svc.Name).log e .dev\$($svc.Name).log.err" -ForegroundColor Red
        $ok = $false
    }
}
if (Wait-Url 'web' 'http://localhost:4200' 180) {
    Write-Host "OK  Frontend  http://localhost:4200"
} else {
    Write-Host "NO  Il frontend non risponde: leggi .dev\web.log" -ForegroundColor Red
    $ok = $false
}

if (-not $ok) { Fail "qualcosa non è partito. Per fermare quello che è partito: .\scripts\dev-down.ps1" }
Write-Host ""
Write-Host "Sistema avviato: apri http://localhost:4200. Per fermarlo: .\scripts\dev-down.ps1"

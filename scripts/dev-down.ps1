# Ferma quello che ha avviato dev-up.ps1: i tre servizi, il frontend e i database (#2).
# Uso:  .\scripts\dev-down.ps1           ferma tutto, i dati dei database restano
#       .\scripts\dev-down.ps1 -KeepDb   lascia accesi i database

param([switch]$KeepDb)

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$pidFile = Join-Path $root '.dev/pids.txt'

if (Test-Path $pidFile) {
    foreach ($line in Get-Content $pidFile) {
        $parts = $line -split ' ', 2
        # /T ferma anche i processi figli: dotnet run avvia l'eseguibile del servizio, npm avvia ng serve
        taskkill /F /T /PID $parts[0] *> $null
        if ($LASTEXITCODE -eq 0) { Write-Host "Fermato $($parts[1])" }
    }
    Remove-Item $pidFile
} else {
    Write-Host "Nessun processo avviato da dev-up.ps1 da fermare."
}

if (-not $KeepDb) {
    Write-Host "Fermo i database (docker compose stop): i dati restano nei volumi."
    docker compose stop
}

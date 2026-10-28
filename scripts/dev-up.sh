#!/usr/bin/env bash
# Avvia database, i tre servizi e il frontend con un comando (#2), su macOS e Linux.
# Uso, dalla cartella del repository:  ./scripts/dev-up.sh
# Per fermare tutto:                    ./scripts/dev-down.sh
# Su Windows usa scripts/dev-up.ps1.
# Le password e la chiave JWT si leggono dal file .env: lo script non le stampa mai.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
run_dir="$root/.dev"
pid_file="$run_dir/pids.txt"

fail() { echo "ERRORE: $1" >&2; exit 1; }
url_ok() { curl -s -o /dev/null -m 2 -f "$1"; }
wait_url() { # url secondi
  local end=$((SECONDS + $2))
  while [ $SECONDS -lt $end ]; do url_ok "$1" && return 0; sleep 2; done
  return 1
}

# 1. File .env
[ -f .env ] || fail "manca il file .env. Copia .env.example in .env e compila le password (README, passo 2)."
set -a
# shellcheck disable=SC1091
. <(tr -d '\r' < .env | grep -E '^[A-Z_][A-Z0-9_]*=')
set +a

missing=""
for key in POSTGRES_ADMIN_PASSWORD BOOKINGS_DB_PASSWORD USERS_DB_PASSWORD MONGO_ADMIN_PASSWORD SPACES_DB_PASSWORD; do
  [ -n "${!key:-}" ] || missing="$missing $key"
done
[ -z "$missing" ] || fail "nel file .env mancano:$missing."

# La chiave JWT deve essere la stessa per i tre servizi: se manca, ne genero una e la scrivo nel .env.
if [ -z "${JWT_SECRET:-}" ]; then
  JWT_SECRET="$(openssl rand -hex 32)"
  if grep -q '^JWT_SECRET=' .env; then
    tmp="$(mktemp)"
    sed "s/^JWT_SECRET=.*/JWT_SECRET=$JWT_SECRET/" .env > "$tmp" && cat "$tmp" > .env && rm -f "$tmp"
  else
    printf '\n# Chiave per firmare i token, la stessa per i tre servizi\nJWT_SECRET=%s\n' "$JWT_SECRET" >> .env
  fi
  export JWT_SECRET
  echo "JWT_SECRET era vuota: ne ho generata una nuova nel .env."
fi

pg_port="${POSTGRES_PORT:-5432}"
mongo_port="${MONGO_PORT:-27017}"
export ConnectionStrings__Spaces="mongodb://spaces_app:$SPACES_DB_PASSWORD@localhost:$mongo_port/spaces?authSource=spaces"
export ConnectionStrings__Bookings="Host=localhost;Port=$pg_port;Database=bookings;Username=bookings_app;Password=$BOOKINGS_DB_PASSWORD"
export ConnectionStrings__Users="Host=localhost;Port=$pg_port;Database=users;Username=users_app;Password=$USERS_DB_PASSWORD"

# 2. Database
echo "Avvio i database (docker compose up -d --wait)..."
docker compose up -d --wait || fail "docker compose non è partito. Docker è avviato?"

# 3. Build unica, così i tre servizi non compilano insieme la stessa libreria
mkdir -p "$run_dir"
echo "Compilo i servizi (dotnet build)..."
dotnet build SmartCampus.slnx --nologo -v q || fail "dotnet build non è riuscito: leggi l'errore qui sopra."

# 4. Servizi
for svc in Spaces:5101 Bookings:5102 Users:5103; do
  name="${svc%%:*}"; port="${svc##*:}"
  if url_ok "http://localhost:$port/health"; then
    echo "$name risponde già su :$port, non lo riavvio."
    continue
  fi
  nohup dotnet run --no-build --project "src/Services/$name/SmartCampus.$name.Api" > "$run_dir/$name.log" 2>&1 &
  echo "$! $name" >> "$pid_file"
  echo "Avviato $name (log in .dev/$name.log)"
done

# 5. Frontend
if url_ok http://localhost:4200; then
  echo "Il frontend risponde già su :4200, non lo riavvio."
else
  if [ ! -d src/web/node_modules ]; then
    echo "Installo le dipendenze del frontend (npm ci)..."
    (cd src/web && npm ci) || fail "npm ci non è riuscito."
  fi
  (cd src/web && nohup npm start > "$run_dir/web.log" 2>&1 & echo "$! web" >> "$pid_file")
  echo "Avviato il frontend (log in .dev/web.log)"
fi

# 6. Attesa dei controlli di salute
ok=1
for svc in Spaces:5101 Bookings:5102 Users:5103; do
  name="${svc%%:*}"; port="${svc##*:}"
  if wait_url "http://localhost:$port/health" 120; then
    echo "OK  $name  http://localhost:$port/health"
  else
    echo "NO  $name non risponde: leggi .dev/$name.log" >&2; ok=0
  fi
done
if wait_url http://localhost:4200 180; then
  echo "OK  Frontend  http://localhost:4200"
else
  echo "NO  Il frontend non risponde: leggi .dev/web.log" >&2; ok=0
fi

[ $ok -eq 1 ] || fail "qualcosa non è partito. Per fermare quello che è partito: ./scripts/dev-down.sh"
echo
echo "Sistema avviato: apri http://localhost:4200. Per fermarlo: ./scripts/dev-down.sh"

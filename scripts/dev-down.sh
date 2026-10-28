#!/usr/bin/env bash
# Ferma quello che ha avviato dev-up.sh: i tre servizi, il frontend e i database (#2).
# Uso:  ./scripts/dev-down.sh            ferma tutto, i dati dei database restano
#       ./scripts/dev-down.sh --keep-db  lascia accesi i database
set -uo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
pid_file="$root/.dev/pids.txt"

# Ferma anche i figli: dotnet run avvia l'eseguibile del servizio, npm avvia ng serve
kill_tree() {
  local child
  for child in $(pgrep -P "$1" 2>/dev/null); do kill_tree "$child"; done
  kill "$1" 2>/dev/null
}

if [ -f "$pid_file" ]; then
  while read -r pid name; do
    kill_tree "$pid" && echo "Fermato $name"
  done < "$pid_file"
  rm -f "$pid_file"
else
  echo "Nessun processo avviato da dev-up.sh da fermare."
fi

if [ "${1:-}" != "--keep-db" ]; then
  echo "Fermo i database (docker compose stop): i dati restano nei volumi."
  docker compose stop
fi

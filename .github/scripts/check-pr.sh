#!/usr/bin/env bash
# Controllo fisso delle PR (docs/agents/pr-checker.md).
# Legge titolo e corpo solo dalle variabili d'ambiente PR_TITLE e PR_BODY: non vanno mai scritti dentro lo script.
set -uo pipefail

title="${PR_TITLE:-}"
body="$(printf '%s' "${PR_BODY:-}" | tr -d '\r')"
errors=()
warnings=()

if ! grep -qE '^(feat|fix|test|docs|chore|refactor|ci)(\([a-z0-9-]+\))?!?: .+' <<<"$title"; then
  errors+=("Il titolo deve iniziare con un tipo: feat:, fix:, test:, docs:, chore:, refactor: o ci:")
fi

if ! grep -qiE '(closes|fixes|refs) #[0-9]+' <<<"$body"; then
  errors+=("Il corpo deve collegare una issue con 'Closes #N' o 'Refs #N'")
fi

if grep -qE '^[[:space:]]*Closes #[[:space:]]*$' <<<"$body"; then
  errors+=("Il segnaposto 'Closes #' del modello è rimasto senza numero")
fi

# Avviso e non errore: non tutte le PR seguono il modello alla lettera (13 delle ultime 40)
if ! grep -qiE '^[[:space:]]*- \[x\]|^#+ .*(verific|test)' <<<"$body"; then
  warnings+=("Manca una sezione con le verifiche eseguite (comandi e risultati osservati)")
fi

if grep -qE '`[nr]($|[[:space:]-])' <<<"$body"; then
  errors+=("Il corpo contiene \`n o \`r: il testo è stato rovinato da PowerShell (usa gh pr edit --body-file)")
fi

for warning in "${warnings[@]}"; do
  echo "::warning::$warning"
done

if [ ${#errors[@]} -eq 0 ]; then
  echo "Controllo PR superato."
  exit 0
fi

for error in "${errors[@]}"; do
  echo "::error::$error"
done
exit 1

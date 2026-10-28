#!/usr/bin/env bash
# Controllo fisso delle PR (docs/agents/pr-checker.md).
# Legge titolo, corpo, branch e messaggi di commit solo dalle variabili d'ambiente PR_TITLE, PR_BODY, PR_BRANCH e
# PR_COMMITS (un messaggio per riga): non vanno mai scritti dentro lo script.
set -uo pipefail

title="${PR_TITLE:-}"
body="$(printf '%s' "${PR_BODY:-}" | tr -d '\r')"
branch="${PR_BRANCH:-}"
commits="$(printf '%s' "${PR_COMMITS:-}" | tr -d '\r')"
words_file="$(dirname "$0")/blocked-words.txt"
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

if grep -qE '`[nr]($|[[:space:]-])' <<<"$body"; then
  errors+=("Il corpo contiene \`n o \`r: il testo è stato rovinato da PowerShell (usa gh pr edit --body-file)")
fi

# Nome del branch: <tipo>/<numero-issue>-<descrizione> (CONTRIBUTING.md, sezione 2)
if [ -n "$branch" ] && ! grep -qE '^(feature|fix|docs|test|chore)/[0-9]+-[a-z0-9]+(-[a-z0-9]+)*$' <<<"$branch"; then
  errors+=("Il branch '$branch' non segue <tipo>/<numero-issue>-<descrizione> in minuscolo (CONTRIBUTING.md, sezione 2)")
fi

# Parole vietate in branch, titolo e messaggi di commit
if [ -f "$words_file" ]; then
  pattern="$(grep -vE '^[[:space:]]*(#|$)' "$words_file" | paste -sd '|' -)"
  if [ -n "$pattern" ]; then
    names="$(printf '%s\n%s\n%s\n' "$branch" "$title" "$commits" | tr '/_.-' '    ')"
    found="$(grep -ioE "(^|[^[:alpha:]])($pattern)([^[:alpha:]]|$)" <<<"$names" | head -n 1)"
    if [ -n "$found" ]; then
      errors+=("Branch, titolo o messaggi di commit contengono una parola non ammessa: rinominali")
    fi
  fi
fi

# Avvisi: non bloccano la PR

# Non tutte le PR seguono il modello alla lettera (13 delle ultime 40 alla prima prova)
if ! grep -qiE '^[[:space:]]*- \[x\]|^#+ .*(verific|test)' <<<"$body"; then
  warnings+=("Manca una sezione con le verifiche eseguite (comandi e risultati osservati)")
fi

# Nomi tra backtick diventati \nome\ o caratteri di controllo: testo passato da PowerShell (#128, #175)
if grep -qE '(^|[[:space:](])\\[A-Za-z_][A-Za-z0-9_./-]*\\' <<<"$body" \
  || [ -n "$(printf '%s' "$body" | tr -d '\t\n' | LC_ALL=C tr -d '[:print:]' | LC_ALL=C tr -d '\200-\377')" ]; then
  warnings+=("Il corpo sembra rovinato: nomi tra \\ o caratteri di controllo al posto dei backtick (usa gh pr edit --body-file)")
fi

# Con lo squash il corpo diventa il messaggio di commit: "Closes #N" in una frase chiude la issue (#171)
if grep -iE '(close[sd]?|fix(e[sd])?|resolve[sd]?) #[0-9]+' <<<"$body" \
  | grep -vqiE '^[[:space:]]*((close[sd]?|fix(e[sd])?|resolve[sd]?) #[0-9]+[[:space:],]*)+$'; then
  warnings+=("'Closes/Fixes/Resolves #N' compare dentro una frase: con lo squash chiude quella issue. Se non vuoi chiuderla, scrivi 'Refs #N'")
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

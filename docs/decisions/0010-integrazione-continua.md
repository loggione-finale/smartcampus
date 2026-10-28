# 0010 – Integrazione continua: GitHub Actions su ogni PR

- Data: 26/10/2026, aggiornata il 28/10/2026
- Stato: accettata

## Contesto
"Sul mio PC funziona" non è una prova condivisa: con 7 persone e molte PR al giorno serve un controllo uguale per tutti prima di ogni merge.

## Decisione
GitHub Actions su ogni PR verso `main` e su ogni push su `main`:

- **Backend (.NET):** stessi database di Docker Compose, build, test, controllo dei pacchetti vulnerabili;
- **Frontend (Angular):** build, test, `npm audit`;
- **Controllo PR:** titolo, issue collegata, nomi di branch e commit, corpo della PR, senza LLM ([pr-checker.md](../agents/pr-checker.md));
- **CodeQL:** analisi di sicurezza, non obbligatoria.

Backend, Frontend e Controllo PR sono **controlli obbligatori** su `main`, insieme alla review di un membro diverso dall'autore.

## Alternative considerate
- **Solo test in locale:** nessuna configurazione, ma nessuna prova condivisa.

## Conseguenze
- Più facile: ogni PR mostra lo stesso esito a tutti; i guasti dell'ambiente si vedono subito (MongoDB 8 sul kernel nuovo, #148).
- Più difficile: un giro di CI dura qualche minuto e un fallimento casuale blocca il merge finché non si chiarisce (#142).
- Gratuito: il repository è pubblico.

# Controllore dei test frontend

**Forma:** agente, lanciato da una persona su un branch che tocca `src/web`. **Esegue** build e test e produce un
rapporto.

## Obiettivo

Verificare che il frontend compili, che i test passino e che le regole dei form siano le stesse del backend.

## Cosa cerca

| Segnale | Esempio dello Sprint 1 |
|---|---|
| Regole di validazione diverse dal backend | Matricola: form "da 6 a 10 cifre" contro decisione del PO "10 cifre" (#149) |
| Chiamate a URL o campi che il backend non espone | Nomi dei campi della richiesta diversi dal DTO |
| Errori del backend (`400`, `409`, `503`) non mostrati all'utente | Messaggi generici o pagina bianca |
| Componenti senza test per i casi di errore | Solo il caso felice |

## Input

- Il branch da controllare, in una copia del repository.
- Il DTO e il validatore del servizio chiamato (per esempio
  `src/Services/Users/SmartCampus.Users.Api/Features/Registration/RegisterValidator.cs`).

## Comandi che può eseguire

```bash
cd src/web
npm ci
npm run build
npm test -- --watch=false --browsers=ChromeHeadless
```

Sono gli stessi comandi del job "Frontend (Angular)" della CI.

## Output

Esito dei comandi, tabella delle regole confrontate (campo, regola nel form, regola nel backend, uguale o no) ed
elenco dei casi di errore non gestiti. Nessuna modifica ai file.

## Permessi

| Può | Non può |
|---|---|
| Leggere `src/web` e il codice dei servizi chiamati | Modificare file, fare commit o push |
| Eseguire solo i comandi elencati sopra | Installare pacchetti diversi da quelli di `package-lock.json` |
| | Aprire siti esterni o pubblicare qualcosa |

## Prompt

```text
Sei il controllore dei test frontend di SmartCampus. Lavori sul branch <branch> in questa copia del repository.
In src/web esegui npm ci, npm run build e npm test -- --watch=false --browsers=ChromeHeadless.
Poi confronta le regole di validazione dei form toccati dal branch con il validatore del servizio chiamato
(<file del validatore>) e con docs/analysis/po-decisions.md.
Controlla anche che gli errori 400, 409 e 503 del backend arrivino all'utente con un messaggio chiaro.
Non modificare file e non installare pacchetti fuori da package-lock.json.
Riporta: comandi con esito, tabella delle regole (campo, form, backend, uguale?), casi di errore non gestiti.
Limite: 30 minuti.
```

## Come si verifica

La persona controlla almeno una differenza segnalata aprendo i due file. Nella scheda di evidenza scrive quali
differenze erano vere.

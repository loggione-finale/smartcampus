# Frontend web

Frontend Angular del progetto SmartCampus, generato con Angular CLI 20 e Angular Material.

## Server di sviluppo

Dalla cartella `src/web` avvia il server:

```bash
npm start
```

Apri `http://localhost:4200/`. Il comando carica automaticamente `proxy.conf.json`, che inoltra le
richieste locali ai tre servizi:

| Percorso del browser | Servizio | Porta |
| --- | --- | --- |
| `/api/spaces/*` | Spaces | `5101` |
| `/api/bookings/*` | Bookings | `5102` |
| `/api/users/*` | Users | `5103` |

Per esempio, `/api/spaces/health` viene inoltrato a `http://localhost:5101/health`.

## Prenotare uno spazio

La form è disponibile nella sezione `#prenota` della home. Seleziona uno spazio, una data futura e
una fascia oraria a intervalli di 15 minuti, poi invia la prenotazione. Il frontend inoltra la richiesta
a `POST /api/bookings`, mostra il codice di riferimento quando la prenotazione è confermata e traduce
in messaggi comprensibili i conflitti di spazio/persona, la capienza esaurita e il commento finale pendente.

La creazione richiede un utente autenticato; senza token il servizio risponde `401` e la form indica di
effettuare l'accesso. La gestione degli invitati e delle richieste di strumenti appartiene alle rispettive
task successive.

## Generazione del codice

Per generare un nuovo componente:

```bash
ng generate component component-name
```

Per l’elenco completo degli schematics disponibili:

```bash
ng generate --help
```

## Build

```bash
ng build
```

Gli artefatti vengono salvati nella cartella `dist/`, esclusa dal repository.

## Test unitari

```bash
npm test -- --watch=false
```

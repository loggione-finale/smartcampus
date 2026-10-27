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

## Registrazione

La pagina di registrazione è disponibile nella sezione `#registrazione` e invia i dati a
`POST /api/users/auth/register`. Le regole client rispecchiano quelle del servizio Users; gli errori
di validazione (`400`) e i duplicati (`409`) vengono mostrati sul campo interessato.

Per simulare un accesso da invito usa `http://localhost:4200/?invite=demo#registrazione`: dopo una
registrazione riuscita viene mostrata la conferma dell’invito.

## Prenotare uno spazio

La form è disponibile nella sezione `#prenota`: seleziona spazio, data e fascia oraria, poi aggiungi
facoltativamente le email dei partecipanti. Con almeno un partecipante compare la selezione degli
strumenti, con indicazione di quelli già presenti nello spazio. Il frontend invia questi dati a
`POST /api/bookings`, mostra il codice di riferimento in caso di conferma e traduce i principali rifiuti
del servizio in messaggi comprensibili. La creazione richiede autenticazione.

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

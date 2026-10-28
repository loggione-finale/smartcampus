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

## Area personale (`/admin`)

Le pagine sotto `/admin` sono per il personale (ruolo `Admin`). Finché non c'è il login (#82), la guardia
legge il ruolo da `sessionStorage` (chiave `smartcampus.role`) e il servizio invia il JWT di `smartcampus.token` come Bearer: per provarle in locale, dalla console del
browser, `sessionStorage.setItem('smartcampus.role', 'Admin')`. Il file `admin-session.ts` va sostituito
con la sessione vera.

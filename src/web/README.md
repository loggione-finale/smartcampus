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

## Accesso e sessione

La pagina di accesso è su `http://localhost:4200/login`. Si entra con l’email oppure con la
matricola e la password di un account del seed; le credenziali demo sono in
[`data/synthetic/README.md`](../../data/synthetic/README.md).

- Il token restituito da `POST /api/users/auth/login` viene salvato in `sessionStorage` sotto le
  chiavi `smartcampus.auth.token`, `smartcampus.auth.user` e `smartcampus.auth.expiresAt`.
  Per compatibilità temporanea vengono aggiornate anche `smartcampus.token` e
  `smartcampus.role`, usate dalle pagine admin già presenti nei branch dedicati.
- Il ruolo per la UI viene letto dai claim del token, non dal campo modificabile dell’utente in
  `sessionStorage`. Resta comunque solo una comodità: la firma e i permessi sono verificati dal
  server a ogni chiamata.
- L’interceptor `authInterceptor` aggiunge `Authorization: Bearer <token>` alle chiamate
  `/api/...`; login e registrazione restano senza header.
- `authGuard` protegge `/account`, `adminGuard` protegge `/admin`. Un utente `User` che apre una
  route admin viene mandato a `/forbidden`; il controllo definitivo resta sul server.
- Con un `401` su una chiamata protetta, o con un token scaduto, la sessione viene cancellata e
  si torna a `/login` con `reason=expired` e `returnUrl` per riprendere il percorso. La scadenza
  viene riconosciuta alla prima navigazione o chiamata API successiva, non con un timer.
- Il pulsante **Esci** cancella la sessione e riporta al login con `reason=logout`. Il logout è
  solo lato client: il token resta valido sul server fino alla sua scadenza di 8 ore.

I test coprono sessione, interceptor, guard, form di login, area personale e area admin:

```bash
npm test -- --watch=false --browsers=ChromeHeadless
```

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

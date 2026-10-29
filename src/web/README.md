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
| `/api/invitations/*` | Bookings | `5102` |
| `/api/users/*` | Users | `5103` |

Per esempio, `/api/spaces/health` viene inoltrato a `http://localhost:5101/health`.

## Registrazione

La registrazione ha una pagina sua, `/register`, e invia i dati a
`POST /api/users/auth/register`. Le regole client rispecchiano quelle del servizio Users; gli errori
di validazione (`400`) e i duplicati (`409`) vengono mostrati sul campo interessato.

La mail d'invito a chi non è registrato punta a `/register?returnUrl=%2Finvitations%2F<id>`: dopo la
registrazione si torna all'invito (passando dal login se manca la sessione). I vecchi link
`/?returnUrl=…#registrazione` vengono reindirizzati a `/register`.

## Accesso e sessione

La pagina di accesso è su `http://localhost:4200/login`. Si entra con l’email oppure con la
matricola e la password di un account del seed; le credenziali demo sono in
[`data/synthetic/README.md`](../../data/synthetic/README.md).

- Il token restituito da `POST /api/users/auth/login` viene salvato in `sessionStorage` sotto le
  chiavi `smartcampus.auth.token`, `smartcampus.auth.user` e `smartcampus.auth.expiresAt`.
  Per compatibilità temporanea vengono aggiornate anche `smartcampus.token` e
  `smartcampus.role`, usate dalle pagine admin già in `main`.
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

I test coprono sessione, interceptor, guard, form di login, area personale, prenotazioni personali
e area admin:

```bash
npm test -- --watch=false --browsers=ChromeHeadless
```

## Prenotare uno spazio

La form è disponibile nella sezione `#prenota`: seleziona spazio, data e fascia oraria, poi aggiungi
facoltativamente le email dei partecipanti. Le email non valide, i duplicati e l'email del responsabile
vengono rifiutati; il form mostra il totale dei partecipanti e lo confronta con la capienza dell'aula
(o con i posti liberi di un'aula mista) prima di inviare. Con almeno un partecipante compare la selezione
degli strumenti, con indicazione di quelli già presenti nello spazio. Il frontend invia questi dati a
`POST /api/bookings`, mostra il codice di riferimento in caso di conferma e traduce i principali rifiuti
del servizio in messaggi comprensibili. La creazione richiede autenticazione.

Il form vive nel componente `src/app/bookings/booking-form.component.ts`, con template, stili e test
accanto; `App` si limita a includerlo nella home con `<app-booking-form />`.

## Le mie prenotazioni (`/bookings`)

La pagina, protetta da `authGuard`, legge `GET /api/bookings/mine`: le prenotazioni in cui l’utente è
organizzatore o partecipante, con spazio, edificio, data, fascia, ruolo e stato (`Confermata`,
`Annullata`, `Conclusa`, `Da commentare`). Le prenotazioni future compaiono prima delle passate, le
annullate restano visibili con lo stato ben evidente e quelle concluse senza commento finale sono
evidenziate con il link a `/closure?booking=<codice>`.

Se almeno una prenotazione ha un invito in attesa, la pagina legge `GET /api/invitations/mine` e
mostra la scadenza e il pulsante “Conferma invito” (`POST /api/invitations/{id}/accept`). Un invito
già scaduto resta visibile ma non è più confermabile. La pagina non legge il token: `authInterceptor`
aggiunge `Authorization: Bearer` a ogni chiamata `/api`.

L’organizzatore di una prenotazione futura confermata può annullarla tramite il pulsante “Annulla prenotazione” con conferma inline (`POST /api/bookings/{id}/cancel`). All’annullamento viene notificato l’evento `smartcampus:bookings-changed` su `window` per sincronizzare la disponibilità degli spazi.

Si arriva alla pagina dal pulsante “Le mie prenotazioni” in `/account`.

## Errori dei servizi

`serviceUnavailableInterceptor` (`src/app/core/service-unavailable.interceptor.ts`) è l'unico punto che mostra un
messaggio quando un servizio non risponde. Intercetta le chiamate `/api/...` e apre uno snackbar di Angular Material:

| Caso | Messaggio per area |
| --- | --- |
| `503` | Catalogo, prenotazioni o accesso «momentaneamente non disponibile/i», in base al servizio chiamato |
| Errore di rete (`0`) | Il servizio interessato «non è raggiungibile» |
| Nessuna risposta entro 10 secondi | Il servizio interessato «non risponde in tempo»; alla pagina arriva un `504` |

Lo snackbar dura 6 secondi, ha il pulsante «Chiudi», non ripete lo stesso messaggio entro 5 secondi e non blocca la
navigazione. Le pagine continuano a mostrare il proprio stato con il pulsante di riprova; i controlli
`/api/*/health` sono esclusi perché la home espone già lo stato dei tre servizi. I codici `400`, `401`, `403`, `404`
e `409` restano gestiti dalle singole pagine, che conoscono il contesto dell'operazione.

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

Le pagine sotto `/admin` sono per il personale (ruolo `Admin`). Dopo il login (#82) la guardia
`adminGuard` legge il ruolo dai claim del JWT e un utente `User` viene mandato a `/forbidden`.
Il servizio della dashboard legge ancora il token dalla chiave legacy `smartcampus.token`, tenuta
sincronizzata da `AuthSessionService`: `admin-session.ts` può essere rimosso quando tutte le pagine
admin useranno la sessione centrale.

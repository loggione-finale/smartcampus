# Prove di guasto per ogni servizio (#66)

> **Stato: eseguita il 28/10/2026.** Procedura ed esiti attesi sono stati scritti prima della prova (#189), come chiede
> [evidence/README.md](README.md); la colonna "Esito osservato" riporta codice HTTP e tempo misurati con `curl`.
> **Risultato: 10 scenari su 12 come atteso, 2 differenze aperte come Bug (#213, #224).** Entrambe sono state
> corrette (#218 e #231). Lo scenario 5 è stato ripetuto il 29/10: vedi [Ripetizione dopo le correzioni](#ripetizione-dopo-le-correzioni-2910).
> Gli esiti attesi riguardano le **API**: su `main` il frontend ha solo catalogo, disponibilità e gestione spazi,
> quindi prenotazioni, login e area admin si provano con chiamate HTTP.

| Campo | Valore |
|---|---|
| Cosa verifica | Che il comportamento con un servizio o un database giù sia quello della tabella "Che cosa succede se va giù un servizio" di [architecture.md](../architecture.md) |
| Ambiente | Windows 11, .NET SDK 10.0.401, Docker 29.8.0, PostgreSQL 17.11, MongoDB 7.0 |
| Commit di `main` | `c79bf89` |
| Chi l'ha eseguita | asprenolapo |
| Data | 28/10/2026 |

## Preparazione

1. Database avviati e sani: `docker compose up -d --wait`, poi `docker compose ps` (tutti `healthy`).
2. I tre servizi avviati in tre terminali, ognuno con le sue stringhe di connessione (vedi [README](../../README.md)):
   Spaces su `http://localhost:5101`, Bookings su `:5102`, Users su `:5103`.
3. Frontend avviato: `npm start` in `src/web`, poi `http://localhost:4200`.
4. In un terminale PowerShell **separato**, prendi un token **prima** di fermare qualcosa (serve anche nello scenario
   di Users giù):

   ```powershell
   $login = Invoke-RestMethod -Method Post http://localhost:5103/auth/login -ContentType application/json `
     -Body '{"username":"giulia.rossi@smartcampus.test","password":"Password123"}'
   $auth = "Authorization: Bearer $($login.token)"
   ```

5. Prepara i corpi delle richieste, con un giorno futuro diverso per ogni prova (vale "una prenotazione per fascia e
   per giorno"):

   ```powershell
   '{"spaceId":"3f2a9c1e-0000-4000-8000-000000000004","start":"2026-11-10T08:00:00Z","end":"2026-11-10T09:00:00Z"}' | Set-Content singola.json
   '{"spaceId":"3f2a9c1e-0000-4000-8000-000000000001","start":"2026-11-11T08:00:00Z","end":"2026-11-11T09:00:00Z","participantEmails":["marco.bianchi@smartcampus.test"]}' | Set-Content gruppo.json
   ```

   L'API accetta solo orari in UTC con `Z` (08:00Z = 09:00 a Roma in novembre): con `+01:00` risponde `400`
   "Times must be expressed in UTC".

   Nella prima esecuzione i corpi usavano `+01:00`: le chiamate `POST` degli scenari 1, 2 e 4 sono state ripetute
   con `Z`, con gli esiti riportati sotto.

6. Comando per leggere codice HTTP e tempo di risposta di una chiamata (esempio):

   ```powershell
   curl.exe -s -o risposta.txt -w "%{http_code} in %{time_total}s`n" -H $auth http://localhost:5102/bookings/mine
   ```

   Per un `POST`: `curl.exe -s -o risposta.txt -w "%{http_code} in %{time_total}s`n" -H $auth -H "Content-Type: application/json" --data "@singola.json" http://localhost:5102/bookings`.
   Il messaggio è in `risposta.txt` (campo `detail`).

7. Controlla che `/health` risponda `Healthy` su tutti e tre: `http://localhost:5101/health`, `:5102`, `:5103`.

Per ogni scenario: fermi il componente, fai le chiamate della riga, lo riavvii e controlli che tutto torni a
funzionare **senza riavviare gli altri**. Accettiamo come "errore chiaro" un `503` con `detail` leggibile **entro 5
secondi**.

## Scenari

Sono 6 guasti, ognuno seguito dalla sua ripresa.

| # | Si ferma | Come fermarlo | Chiamate da provare | Esito atteso | Esito osservato |
|---|---|---|---|---|---|
| 1 | Spaces | Ctrl+C sul suo `dotnet run` | `POST /bookings` con `singola.json`; `GET /bookings/availability/{spaceId}?date=…`; `GET /bookings/mine`; pagina Catalogo del frontend | Creazione e disponibilità: `503` con `detail` "The Spaces service is not reachable. Try again later." entro 5 s; `GET /bookings/mine` `200`; il catalogo mostra un messaggio di servizio non disponibile | `POST /bookings` **503 in 3,08 s** "The Spaces service is not reachable. Try again later."; disponibilità **503 in 3,06 s** "Spaces timed out."; `GET /bookings/mine` **200 in 0,11 s**; catalogo tramite il proxy del frontend `500` (errore del proxy, la schermata non è stata controllata a vista) |
| 1b | Spaces, ripresa | `dotnet run` di nuovo | `POST /bookings` con `singola.json` | `201`, senza riavviare Bookings | **201 in 0,48 s**, Bookings non riavviato |
| 2 | Users | Ctrl+C sul suo `dotnet run` | `POST /auth/login`; con il token già preso: `POST /bookings` con `singola.json` (un altro giorno) e con `gruppo.json` | Login non raggiungibile; prenotazione singola `201` (il token si valida in Bookings); prenotazione di gruppo `503` con "The Users service is not reachable. Try again later." entro 5 s | Login **non raggiungibile** (connessione rifiutata); singola **201 in 0,04 s**; gruppo **503 in 3,01 s** "The Users service is not reachable. Try again later." |
| 2b | Users, ripresa | `dotnet run` di nuovo | `POST /auth/login`; `POST /bookings` con `gruppo.json` | Login `200`, gruppo `201` | Login **200 in 0,44 s**; gruppo **201 in 0,36 s** |
| 3 | Bookings | Ctrl+C sul suo `dotnet run` | Pagina Catalogo; `GET http://localhost:5101/spaces`; `GET /bookings/mine` | Catalogo e Spaces funzionano; le chiamate a Bookings non sono raggiungibili | `GET /spaces` **200 in 0,26 s**; `GET /bookings/mine` non raggiungibile |
| 3b | Bookings, ripresa | `dotnet run` di nuovo | `GET /bookings/mine` | `200` con le prenotazioni create prima: nessuna persa | **200 in 0,45 s**, 3 prenotazioni create prima ancora presenti |
| 4 | MongoDB | `docker compose stop mongo` | Come lo scenario 1; poi `GET http://localhost:5101/health` | Come "Spaces giù"; `/health` di Spaces `503 Unhealthy` entro 5 s | `POST /bookings` **503 in 3,03 s**; `GET /spaces` **503 in 5,10 s** "Unable to query the spaces catalog right now"; `/health` di Spaces **503 in 5,02 s**: codice giusto, tempo appena oltre i 5 s (**#224**) |
| 4b | MongoDB, ripresa | `docker compose start mongo` | `GET http://localhost:5101/spaces` | `200` senza riavviare Spaces; `/health` di nuovo `Healthy` | `GET /spaces` **200**, `/health` **200**, `POST /bookings` **201**, Spaces non riavviato |
| 5 | PostgreSQL | `docker compose stop postgres` | `GET http://localhost:5101/spaces`; `GET /bookings/mine`; `POST /auth/login`; `/health` di Bookings e Users | Catalogo `200` (MongoDB); Bookings e Users `503` con `detail` leggibile **entro 5 s**; `/health` di Bookings e Users `503 Unhealthy` | Catalogo **200 in 0,01 s**; `GET /bookings/mine` **500 in 82,7 s**; `POST /auth/login` **500 in 82,2 s**; `/health` Bookings **503 in 84,9 s**, Users **503 in 86,2 s** (**#213**) |
| 5b | PostgreSQL, ripresa | `docker compose start postgres` | `GET /bookings/mine`; `POST /auth/login` | `200` senza riavviare i servizi; nessuna prenotazione persa | `GET /bookings/mine` **200**, login **200 in 2,1 s**, servizi non riavviati, nessuna prenotazione persa |
| 6 | Frontend | Ctrl+C su `npm start` | `http://localhost:4200`; `GET http://localhost:5102/health` | Interfaccia non raggiungibile; le API restano attive | `:4200` non raggiungibile; `/health` di Bookings **200** |
| 6b | Frontend, ripresa | `npm start` di nuovo | `http://localhost:4200` | L'interfaccia torna raggiungibile | `:4200` **200** |

### Differenza già nota prima della prova

Rhaastrake ha provato lo scenario 5 su Bookings il 28/10 (review della #189): con PostgreSQL fermo `/health` risponde
`503 Unhealthy` dopo **58 secondi** e `POST /bookings/availability` risponde `500` con corpo vuoto dopo **58 secondi**.
La causa è `EnableRetryOnFailure` di EF Core, che ritenta per quasi un minuto, e l'eccezione del database non viene
tradotta in un `503`. L'esito atteso resta quello sopra: la prova dirà se la differenza c'è ancora, e la differenza va
aperta come Bug (owner Bookings). Per Users non è ancora provato.

## Differenze rispetto all'atteso

Ogni differenza diventa un Bug con il numero dello scenario (DoD di #66).

| Scenario | Differenza | Bug |
|---|---|---|
| 5 | Con PostgreSQL fermo Bookings **e Users** rispondono `500` dopo circa 82 s invece di `503` entro 5 s; `/health` `503` dopo 85–86 s. Causa: `EnableRetryOnFailure()` con i valori predefiniti e nessun gestore delle eccezioni | #213, corretto con #218 |
| 4 | Con MongoDB fermo Spaces risponde `503` corretto ma dopo 5,1 s, appena oltre il limite. Causa: `ServerSelectionTimeout` di 5 s | #224, corretto con #231 (timeout a 3 s) |

## Ripetizione dopo le correzioni (29/10)

Scenario 5 ripetuto due volte il 29/10 su `main` (`29e1fbc`, con #218), avviato con `scripts/dev-up.ps1` su Windows 11,
come chiesto da Rhaastrake. Il token di Giulia è stato preso prima di fermare PostgreSQL.

| # | Chiamata | Esito atteso | Esito osservato (due ripetizioni) |
|---|---|---|---|
| 5 | `GET http://localhost:5101/spaces` | `200` | **200** in 0,14 s |
| 5 | `GET /bookings/mine` | `503` con `detail` leggibile entro 5 s | **503** in 7,07–7,21 s, "The database is not reachable. Try again shortly." |
| 5 | `POST /auth/login` | Come sopra | **503** in 7,05–7,20 s, stesso messaggio |
| 5 | `/health` di Bookings e Users | `503` | **503** in 10,1 s |
| 5b | Dopo `docker compose start postgres`: `GET /bookings/mine`, `POST /auth/login`, `/health` | `200` senza riavviare i servizi | **200** su tutte, servizi non riavviati |

**Esito:** il difetto di #213 è risolto. Prima c'era un `500` senza messaggio dopo 82 s; ora c'è un `503` leggibile e la
ripresa avviene da sola. **Resta una differenza:** circa 7 s invece di 5. La causa probabile è il timeout di connessione
di 3 s (`Program.cs` di Bookings e Users) sommato al nuovo tentativo di `EnableRetryOnFailure`. La dichiariamo come limite: non si cambia
dopo il freeze del 29/10.

Lo scenario 4 (MongoDB fermo) non è stato ripetuto dopo #231: il timeout è passato da 5 a 3 secondi ed è verificato da
un test di configurazione, non da una misura.

Nota sulla prova: alla prima ripetizione PostgreSQL non è ripartito, con l'errore `mount source path ... file exists`. Il
container era stato creato da una copia del repository poi cancellata. Ricreato con `docker compose up -d --wait` dalla
cartella del repository, ha funzionato (vedi "Problemi frequenti" nel README). Non è un difetto dei servizi.

## Limiti

- La prova si fa su un solo PC e con un'istanza per servizio: non dice niente sul comportamento con più istanze.
- La prova è sulle API: le schermate del frontend non sono state controllate a vista durante i guasti. Il messaggio di
  servizio non disponibile del frontend (#65, snackbar centralizzato) è arrivato su `main` dopo la prova.
- Tempi misurati su un PC di sviluppo: su un'altra macchina cambiano, ma l'ordine di grandezza (secondi contro minuti) no.
- Token, password e stringhe di connessione non vanno copiati in questo file né negli screenshot.

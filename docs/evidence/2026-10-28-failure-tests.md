# Prove di guasto per ogni servizio (#66)

> **Stato: da eseguire.** Procedura ed esiti attesi sono scritti prima della prova, come chiede
> [evidence/README.md](README.md); la colonna "Esito osservato" si compila durante la prova.
> Gli esiti attesi riguardano le **API**: su `main` il frontend ha solo catalogo, disponibilità e gestione spazi,
> quindi prenotazioni, login e area admin si provano con chiamate HTTP.

| Campo | Valore |
|---|---|
| Cosa verifica | Che il comportamento con un servizio o un database giù sia quello della tabella "Che cosa succede se va giù un servizio" di [architecture.md](../architecture.md) |
| Ambiente | _sistema operativo, versione di .NET e di Docker Desktop_ |
| Commit di `main` | _hash_ |
| Chi l'ha eseguita | _nome GitHub_ |
| Data | _GG/MM/AAAA_ |

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
   '{"spaceId":"3f2a9c1e-0000-4000-8000-000000000004","start":"2026-11-10T09:00:00+01:00","end":"2026-11-10T10:00:00+01:00"}' | Set-Content singola.json
   '{"spaceId":"3f2a9c1e-0000-4000-8000-000000000001","start":"2026-11-11T09:00:00+01:00","end":"2026-11-11T10:00:00+01:00","participantEmails":["marco.bianchi@smartcampus.test"]}' | Set-Content gruppo.json
   ```

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
| 1 | Spaces | Ctrl+C sul suo `dotnet run` | `POST /bookings` con `singola.json`; `GET /bookings/availability/{spaceId}?date=…`; `GET /bookings/mine`; pagina Catalogo del frontend | Creazione e disponibilità: `503` con `detail` "The Spaces service is not reachable. Try again later." entro 5 s; `GET /bookings/mine` `200`; il catalogo mostra un messaggio di servizio non disponibile | |
| 1b | Spaces, ripresa | `dotnet run` di nuovo | `POST /bookings` con `singola.json` | `201`, senza riavviare Bookings | |
| 2 | Users | Ctrl+C sul suo `dotnet run` | `POST /auth/login`; con il token già preso: `POST /bookings` con `singola.json` (un altro giorno) e con `gruppo.json` | Login non raggiungibile; prenotazione singola `201` (il token si valida in Bookings); prenotazione di gruppo `503` con "The Users service is not reachable. Try again later." entro 5 s | |
| 2b | Users, ripresa | `dotnet run` di nuovo | `POST /auth/login`; `POST /bookings` con `gruppo.json` | Login `200`, gruppo `201` | |
| 3 | Bookings | Ctrl+C sul suo `dotnet run` | Pagina Catalogo; `GET http://localhost:5101/spaces`; `GET /bookings/mine` | Catalogo e Spaces funzionano; le chiamate a Bookings non sono raggiungibili | |
| 3b | Bookings, ripresa | `dotnet run` di nuovo | `GET /bookings/mine` | `200` con le prenotazioni create prima: nessuna persa | |
| 4 | MongoDB | `docker compose stop mongo` | Come lo scenario 1; poi `GET http://localhost:5101/health` | Come "Spaces giù"; `/health` di Spaces `503 Unhealthy` entro 5 s | |
| 4b | MongoDB, ripresa | `docker compose start mongo` | `GET http://localhost:5101/spaces` | `200` senza riavviare Spaces; `/health` di nuovo `Healthy` | |
| 5 | PostgreSQL | `docker compose stop postgres` | `GET http://localhost:5101/spaces`; `GET /bookings/mine`; `POST /auth/login`; `/health` di Bookings e Users | Catalogo `200` (MongoDB); Bookings e Users `503` con `detail` leggibile **entro 5 s**; `/health` di Bookings e Users `503 Unhealthy` | |
| 5b | PostgreSQL, ripresa | `docker compose start postgres` | `GET /bookings/mine`; `POST /auth/login` | `200` senza riavviare i servizi; nessuna prenotazione persa | |
| 6 | Frontend | Ctrl+C su `npm start` | `http://localhost:4200`; `GET http://localhost:5102/health` | Interfaccia non raggiungibile; le API restano attive | |
| 6b | Frontend, ripresa | `npm start` di nuovo | `http://localhost:4200` | L'interfaccia torna raggiungibile | |

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
| | | |

## Limiti

- La prova si fa su un solo PC e con un'istanza per servizio: non dice niente sul comportamento con più istanze.
- Le pagine del frontend per prenotazioni, login e area admin non sono ancora su `main`: per quelle la prova va
  ripetuta quando arrivano.
- Token, password e stringhe di connessione non vanno copiati in questo file né negli screenshot.

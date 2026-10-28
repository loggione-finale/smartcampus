# Prove di guasto per ogni servizio (#66)

> **Stato: da eseguire.** Procedura ed esiti attesi sono scritti prima della prova, come chiede
> [evidence/README.md](README.md); la colonna "Esito osservato" si compila durante la prova.

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
   `dotnet run --project src/Services/Spaces/SmartCampus.Spaces.Api`, poi Bookings e Users allo stesso modo.
3. Frontend avviato: `npm start` in `src/web`, poi `http://localhost:4200`.
4. Login con un utente demo (`giulia.rossi@smartcampus.test`, password demo in `data/synthetic/README.md`) in una
   finestra e con l'admin (`admin@smartcampus.test`) in un'altra.
5. Controlla che `/health` risponda `Healthy` su tutti e tre: `http://localhost:5101/health`, `:5102`, `:5103`.

Per ogni scenario: fermi il componente, provi le azioni della riga, lo riavvii e controlli che tutto torni a
funzionare senza riavviare gli altri. Annota anche il codice HTTP che vedi negli strumenti per sviluppatori del
browser (scheda Rete).

## Scenari

| # | Si ferma | Come fermarlo | Azioni da provare | Esito atteso | Esito osservato |
|---|---|---|---|---|---|
| 1 | Spaces | Ctrl+C sul suo `dotnet run` | Catalogo; disponibilità di un'aula; nuova prenotazione; le mie prenotazioni; annullamento | Catalogo e disponibilità mostrano un messaggio di servizio non disponibile; la nuova prenotazione riceve `503` con "Impossibile verificare lo spazio"; le mie prenotazioni e l'annullamento funzionano | |
| 1b | Spaces, ripresa | `dotnet run` di nuovo | Catalogo e nuova prenotazione | Tutto torna a funzionare senza riavviare Bookings | |
| 2 | Users | Ctrl+C sul suo `dotnet run` | Login in una finestra nuova; con Giulia già collegata: catalogo, prenotazione singola, prenotazione di gruppo con un'email | Il login fallisce con un messaggio chiaro; con il token già ottenuto catalogo e prenotazione singola funzionano; la prenotazione di gruppo riceve `503` con "Impossibile verificare i partecipanti" | |
| 2b | Users, ripresa | `dotnet run` di nuovo | Login e prenotazione di gruppo | Tutto torna a funzionare | |
| 3 | Bookings | Ctrl+C sul suo `dotnet run` | Catalogo; le mie prenotazioni; nuova prenotazione; dashboard admin | Il catalogo funziona; le pagine delle prenotazioni e la dashboard mostrano "Prenotazioni momentaneamente non disponibili" | |
| 3b | Bookings, ripresa | `dotnet run` di nuovo | Le mie prenotazioni | Tutto torna a funzionare; nessuna prenotazione persa | |
| 4 | MongoDB | `docker compose stop mongo` | Come lo scenario 1; poi `http://localhost:5101/health` | Come "Spaces giù"; `/health` di Spaces risponde `Unhealthy` (`503`) | |
| 4b | MongoDB, ripresa | `docker compose start mongo` | Catalogo | Spaces torna a rispondere senza essere riavviato; `/health` di nuovo `Healthy` | |
| 5 | PostgreSQL | `docker compose stop postgres` | Catalogo; le mie prenotazioni; login; `/health` di Bookings e Users | Il catalogo funziona (MongoDB); prenotazioni e login rispondono con un errore chiaro; `/health` di Bookings e Users `Unhealthy` | |
| 5b | PostgreSQL, ripresa | `docker compose start postgres` | Le mie prenotazioni e login | Bookings e Users tornano a rispondere senza essere riavviati; nessuna prenotazione persa | |
| 6 | Frontend | Ctrl+C su `npm start` | Una chiamata diretta: `http://localhost:5102/health` | L'interfaccia non è raggiungibile; le API restano attive | |

## Differenze rispetto all'atteso

Ogni differenza diventa un Bug con il numero dello scenario (DoD di #66).

| Scenario | Differenza | Bug |
|---|---|---|
| | | |

## Limiti

- La prova si fa su un solo PC e con un'istanza per servizio: non dice niente sul comportamento con più istanze.
- Le password e le stringhe di connessione non vanno copiate in questo file né negli screenshot.

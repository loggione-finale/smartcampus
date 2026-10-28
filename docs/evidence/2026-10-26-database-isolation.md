# Prova: isolamento e persistenza dei database

- **Task:** #94 (SC-01.T7), insieme alla verifica di #18
- **Cosa verifica:** ogni servizio accede solo ai propri database; le porte sono raggiungibili solo dal proprio PC;
  i dati restano dopo lo spegnimento dei container.
- **Cosa non verifica:** `EnableRetryOnFailure` di EF Core e i log senza dati sensibili, che si scrivono nel
  codice dei servizi (#31 Bookings, #64 Users) e si provano lì.
- **Ambiente:** Windows 11, Docker 29.8.0, `docker-compose.yml` di #18 (PostgreSQL 17, MongoDB 8). I casi su MongoDB
  sono stati ripetuti il 27/10 con MongoDB 7.0, dopo il cambio di versione di #148 (sezione sotto).

## Casi provati

| # | Caso | Comando | Esito atteso | Esito osservato (26/10/2026) |
|---|---|---|---|---|
| 1 | Database e proprietari | `docker exec smartcampus-postgres psql -U postgres -c "\l"` | `bookings`, `bookings_test` di `bookings_app`; `users`, `users_test` di `users_app` | Come atteso |
| 2 | Un utente entra nel proprio database | `docker exec smartcampus-postgres psql -U users_app -d users -c "select 'ok'"` | `ok` | `ok` |
| 3 | **Un utente non entra nel database di un altro servizio** | `docker exec smartcampus-postgres psql -U users_app -d bookings -c "select 'ok'"` | Errore `permission denied for database "bookings"` | Errore `permission denied`, come atteso |
| 4 | L'utente di Spaces scrive nel proprio database | `mongosh -u spaces_app --authenticationDatabase spaces spaces`, poi `db.prova.insertOne({ ok: 1 })` | Inserimento riuscito | Riuscito |
| 5 | **L'utente di Spaces non scrive in un altro database** | Nella stessa shell: `db.getSiblingDB('admin').prova.insertOne({ ok: 1 })` | Errore `not authorized` | Errore `not authorized`, come atteso |
| 6 | **I dati restano dopo lo spegnimento** | `docker compose down`, `docker compose up -d`, poi `db.prova.countDocuments()` | `1` | `1` |
| 7 | Porte solo locali | `docker compose port postgres 5432` | `127.0.0.1:5432` | `127.0.0.1:5432` |
| 8 | Avvio da zero dopo la cancellazione dei volumi (27/10) | `docker compose down -v`, poi `docker compose up -d --wait` | Volumi creati, container `Healthy` | Come atteso (vedi [2026-10-27-startup-from-scratch.md](2026-10-27-startup-from-scratch.md)) |

## Casi su MongoDB ripetuti con la 7.0 (27/10/2026)

Dopo #152 (`mongo:7.0` al posto di `mongo:8`), con volumi ricreati da zero (`docker compose down -v`, poi
`docker compose up -d --wait`), versione `db version v7.0.45`. La password è letta dentro il container
(`$SPACES_DB_PASSWORD`), quindi non compare nei comandi.

| # | Caso | Esito atteso | Esito osservato |
|---|---|---|---|
| 9 | `spaces_app` scrive in `spaces` e in `spaces_test` | Riuscito | Riuscito (`true`) |
| 10 | **`spaces_app` scrive in `admin`** | Errore `not authorized` | `not authorized on admin`, come atteso |
| 11 | **`spaces_app` legge un altro database (`test`)** | Errore `not authorized` | `not authorized on test`, come atteso |
| 12 | Accesso con password sbagliata | Errore di autenticazione | `Authentication failed`, come atteso |
| 13 | **I dati restano dopo `docker compose down` e `up`** | Il documento di prova c'è ancora | `1` documento, poi cancellato |

## Configurazione verificata nel file

| Controllo | Dove |
|---|---|
| Porte solo su `127.0.0.1` | `ports` di entrambi i servizi in `docker-compose.yml` |
| Riavvio automatico | `restart: unless-stopped` |
| Stato di salute | `healthcheck` con `pg_isready` e `mongosh ping` |
| Dati persistenti | Volumi con nome `postgres_data`, `mongo_data` |
| Minimo privilegio | `infra/postgres/init.sh`, `infra/mongo/init.js`: un utente per servizio, `REVOKE CONNECT ... FROM PUBLIC` |
| Nessuna password nel repository | Password solo in `.env` (escluso da `.gitignore`), `.env.example` senza valori |

## Limiti noti

- PostgreSQL resta un punto unico di guasto per Bookings e Users (punto debole W5 in
  [application-flow.md](../analysis/application-flow.md)): accettabile per una demo locale.
- L'amministratore di PostgreSQL entra in tutti i database: serve solo per la manutenzione, i servizi non lo usano.

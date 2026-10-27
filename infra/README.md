# Database locali

Un solo comando avvia i database di tutti i servizi:

| Servizio | Motore | Database | Utente | Porta sul tuo PC |
|---|---|---|---|---|
| Bookings | PostgreSQL 17 | `bookings`, `bookings_test` | `bookings_app` | `POSTGRES_PORT` (5432) |
| Users | PostgreSQL 17 | `users`, `users_test` | `users_app` | `POSTGRES_PORT` (5432) |
| Spaces | MongoDB 7.0 | `spaces`, `spaces_test` | `spaces_app` | `MONGO_PORT` (27017) |

Ogni utente può usare **solo** i propri database. Le porte sono aperte solo su `127.0.0.1`: gli altri PC
della rete non vedono i database.

**Perché MongoDB 7.0 e non 8:** MongoDB 8 si rifiuta di partire sui kernel Linux dal 6.19 al 7.0.13
([SERVER-121912](https://jira.mongodb.org/browse/SERVER-121912)). Con Docker conta il kernel del computer che
ospita il container: alcuni server della CI di GitHub hanno già quel kernel, e lo stesso può succedere sui
nostri PC dopo un aggiornamento di Docker Desktop. Tutto quello che usa Spaces (documenti, indici, utenti) c'è
anche nella 7.0 (bug #148).

Se avevi già avviato i database con MongoDB 8, la 7.0 non legge i dati scritti dalla 8 e il container `mongo`
si riavvia di continuo. Serve una volta `docker compose down -v` e poi `docker compose up -d`: si perdono i dati
locali (solo dati sintetici) e al prossimo avvio vengono ricreati utenti e database.

## Primo avvio

1. Avvia Docker Desktop.
2. Copia `.env.example` in `.env` e scegli le password (il file `.env` non va mai online).
3. Dalla radice del repository: `docker compose up -d`.
4. `docker compose ps`: i due servizi devono risultare `healthy`.

Gli utenti e i database vengono creati **solo al primo avvio**, quando i volumi sono vuoti
(`infra/postgres/init.sh`, `infra/mongo/init.js`).

## Stringhe di connessione

Non vanno scritte in `appsettings*.json`: ognuno le imposta sul proprio PC con `dotnet user-secrets` o con
una variabile d'ambiente, usando le password del proprio `.env`.

| Servizio | Stringa di connessione |
|---|---|
| Bookings | `Host=localhost;Port=5432;Database=bookings;Username=bookings_app;Password=<BOOKINGS_DB_PASSWORD>` |
| Users | `Host=localhost;Port=5432;Database=users;Username=users_app;Password=<USERS_DB_PASSWORD>` |
| Spaces | `mongodb://spaces_app:<SPACES_DB_PASSWORD>@localhost:27017/spaces?authSource=spaces` |

Per i test si usano gli stessi utenti con i database `*_test`.

L'estensione `btree_gist`, che serve ai vincoli di esclusione di Bookings, è "trusted": la può creare
`bookings_app` nelle migrazioni, senza diritti di amministratore.

## Comandi utili

| Comando | Cosa fa |
|---|---|
| `docker compose up -d` | Avvia i database (o li lascia accesi se già avviati) |
| `docker compose ps` | Stato e salute dei container |
| `docker compose logs postgres` | Log di PostgreSQL (o `mongo`) |
| `docker compose stop` | Ferma i database, **i dati restano** |
| `docker compose down` | Rimuove i container, **i dati restano** nei volumi |
| `docker compose down -v` | Rimuove anche i volumi: **cancella tutti i dati** e al prossimo avvio rifà utenti e database |

Se cambi le password nel `.env` dopo il primo avvio, servono `docker compose down -v` e un nuovo
`docker compose up -d`: le password vengono lette solo quando i volumi sono vuoti.

# Prova: avvio da zero seguendo il README

- **Task:** #20 (SC-01.T4)
- **Cosa verifica:** chi non ha mai visto il progetto riesce a scaricarlo, avviare i database e i tre servizi,
  ed eseguire i test seguendo solo la sezione "Installazione, avvio e test" del README.
- **Cosa non verifica:** il controllo del database dentro `/health`, che arriva con #26, #31 e #64;
  il frontend, che arriva con #19.

## Passi

1. Clone in una cartella nuova (sezione 1 del README).
2. `.env` da `.env.example` e `docker compose up -d --wait` (sezione 2).
3. `dotnet build SmartCampus.slnx` e `dotnet test SmartCampus.slnx` (sezione 6).
4. Avvio dei tre servizi (sezione 4): `/health` e `/swagger` per ciascuno.

## Esito atteso

- Database: `smartcampus-postgres` e `smartcampus-mongo` in stato `Healthy`.
- Build: 0 errori. Test: 3 superati.
- `http://localhost:5101/health`, `5102/health`, `5103/health`: `Healthy`.
- `/swagger` si apre su tutti e tre i servizi.

## Esito osservato

| Data | Chi | Sistema operativo | .NET SDK | Docker | Database | Build e test | `/health` e `/swagger` | Note |
|---|---|---|---|---|---|---|---|---|
| 27/10/2026 | asprenolapo | Windows 11 | 10.0.401 | 29.8.0 | Volumi creati da zero, entrambi `Healthy` | 0 errori, 3 test superati | `Healthy` e Swagger su 5101, 5102, 5103 | 1° tentativo: `.env` copiato con password vuote, `docker compose` si ferma con `required variable ... is missing a value`. Poi `.env` sovrascritto rilanciando `cp .env.example .env`: database ricreati con `down -v`. Entrambi i casi chiariti nel README |
| | | macOS o Linux | | | | | | |

## Avvio con un comando (#2)

Passi: `.env` con le 5 password e `JWT_SECRET` vuota, poi `scripts/dev-up` (sezione 3 del README), poi
`scripts/dev-down`.

Esito atteso: `JWT_SECRET` generata nel `.env`; `OK` per Spaces, Bookings, Users e frontend; login da
`http://localhost:4200/api/users/auth/login` e `GET /api/bookings/mine` con quel token `200` (stessa chiave sui tre
servizi); dopo `dev-down` nessuna porta tra 5101, 5102, 5103 e 4200 risponde.

| Data | Chi | Sistema operativo | Comando | Esito | Note |
|---|---|---|---|---|---|
| 28/10/2026 | asprenolapo | Windows 11, PowerShell 5.1, .NET SDK 10.0.401, Docker 29.8.0 | `.\scripts\dev-up.ps1` | `JWT_SECRET` generata; 4 `OK`; login e `bookings/mine` `200` tramite il proxy; dopo `dev-down.ps1` nessuna porta risponde | Checkout nuovo da `main` senza `node_modules` (il 1° avvio esegue `npm ci`), volumi dei database già esistenti. 2° avvio: build in 2,4 s e tutto `OK` in meno di un minuto |
| 29/10/2026 | Rhaastrake | Fedora 44 (kernel 7.2.13, SELinux `Enforcing`), bash, .NET SDK 10.0.113, Docker 29.8.2, Node 24.18 | `./scripts/dev-up.sh` | Con la correzione di #262: `JWT_SECRET` generata nel `.env`; 4 `OK` (Spaces, Bookings, Users, frontend) in 19 s; login da `http://localhost:4200/api/users/auth/login` e `GET /api/bookings/mine` `200` con 3 prenotazioni del seed; catalogo con 8 spazi; dopo `dev-down.sh` nessuna porta risponde | Clone nuovo di `main` (`dc54921`), `.env` con password nuove, volumi dei database cancellati con `docker compose down -v`. **1° tentativo fallito**: SELinux impediva ai container di leggere `init.sh` e `init.js` (`Permission denied`), quindi gli utenti dei database non venivano creati e i servizi rispondevano `password authentication failed`; risolto con l'opzione `z` nei due montaggi di `docker-compose.yml`. I 19 s sono a caldo (`node_modules` e build già presenti dal tentativo precedente): il tempo a freddo non è stato misurato |
| | | macOS | `./scripts/dev-up.sh` | | |

## Limiti noti

- Il README non è ancora stato provato con il frontend: si aggiorna questa prova dopo #19.
- `dev-up.sh` è provato su Windows (28/10) e su Linux (29/10, Fedora con SELinux); non è stato provato su macOS.
- Su Linux con SELinux serve l'opzione `z` nei montaggi degli script di inizializzazione (#262): senza, i database partono ma senza gli utenti dei servizi.

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

## Limiti noti

- Il README non è ancora stato provato con il frontend: si aggiorna questa prova dopo #19.

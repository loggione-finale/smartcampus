# 0007 – Avvio in locale: Docker Compose per i database

- Data: 26/10/2026
- Stato: accettata

## Contesto
Il prodotto deve avviarsi in modo riproducibile su 7 PC diversi e in CI, senza account né servizi esterni.

## Decisione
Docker Compose avvia **solo i database** (PostgreSQL 17 e MongoDB 7.0) con le password lette da `.env`, porte aperte solo su `127.0.0.1` e un utente per servizio. I servizi si avviano con `dotnet run` e il frontend con `ng serve`. La CI usa lo **stesso** `docker-compose.yml`.

## Alternative considerate
- **Database installati su ogni PC:** versioni diverse su 7 macchine e CI da configurare a parte.
- **Tutto in Docker Compose:** un solo comando, ma un Dockerfile per progetto e un debug meno immediato.
- **.NET Aspire:** avvio automatico, ma mai usato nel corso ([0011](0011-strumenti-esclusi.md)).

## Conseguenze
- Più facile: debug normale da Visual Studio o VS Code; stessa configurazione in locale e in CI.
- Più difficile: quattro processi da avviare a mano (istruzioni nel README, prova di avvio da zero in [evidence](../evidence/2026-10-27-startup-from-scratch.md)).
- Se cambiano le password nel `.env` servono `docker compose down -v` e un nuovo avvio, perché gli utenti si creano solo con i volumi vuoti.

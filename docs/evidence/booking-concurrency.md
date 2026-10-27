# Prova: concorrenza sulle prenotazioni (issue #35, scheda #36)

| Campo | Contenuto |
|---|---|
| Cosa verifica | Con molte richieste simultanee sullo stesso spazio: (1) **aula dedicata** — una sola prenotazione confermata, le altre rifiutate dal vincolo di esclusività sull'intervallo `period`; (2) **aula mista** — mai oltre la capienza, grazie a `pg_advisory_xact_lock` + conteggio dei posti occupati dentro la transazione; (3) due prenotazioni **adiacenti** (`[inizio, fine)`) sono entrambe accettate |
| Come | 1. `docker compose up -d` (serve PostgreSQL, `bookings_test`; MongoDB non necessario per questi test)<br>2. `dotnet test tests/SmartCampus.Bookings.Tests --filter "FullyQualifiedName~BookingConcurrencyTests"`<br>3 test su `tests/SmartCampus.Bookings.Tests/ConcurrencyBookingsTestCollection.cs`, con 16 richieste simultanee per prova, capienza 10 e 2 posti per prenotazione sull'aula mista; database PostgreSQL reale (no provider in-memory) |
| Esito atteso | Nella prova dedicata: **1 prenotazione su 16** accettata, 1 confermata sul database. Nella prova mista: `accettate × 2 ≤ 10`, quindi al massimo 5 accettate (il numero esatto può variare col scheduling: il lock garantisce solo il tetto). Adiacenti: entrambe accettate |
| Esito osservato | 27/10/2026, ramo `main` (commit `e517eec`): tutti i 3 test superati in ~6 s (`Superati: 3`). Nessuna prenotazione in più nel database, capienza mai superata |
| Limiti | Prova a livello di **database/EF Core** (vincolo + lock), non dell'endpoint HTTP: le chiamate escono dall'endpoint testati con `WebApplicationFactory` in `CreateBookingEndpointTests`, coprono validazioni ma non la concorrenza reale. La verifica negativa (la prova **fallisce** senza vincolo o senza lock) è solo manuale e non ripetibile in CI: si può far girare una volta togliendo il vincolo o il lock, ma non c'è un test automatizzato che lo dimostri. Nella prova mista il numero esatto di accettate non è deterministico: cambia da run a run, ed è garantito solo il tetto `accettate × seggi ≤ capienza`. Vale per una singola istanza del servizio e un solo database PostgreSQL: con più istanze o un motore diverso andrebbe ripetuta |

## Come riprodurre

```bash
docker compose up -d          # PostgreSQL su 127.0.0.1:5432 (servono bookings/bookings_test)
dotnet test tests/SmartCampus.Bookings.Tests \
  --filter "FullyQualifiedName~BookingConcurrencyTests"
```

Con `docker compose ps` i due servizi di database devono risultare `healthy` prima di lanciare i test
(vedi `infra/README.md`).

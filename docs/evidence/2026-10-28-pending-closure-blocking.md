# Prova: blocco delle nuove prenotazioni con commento di chiusura in sospeso (issue #115, scheda SC-29.T3)

| Campo | Contenuto |
|---|---|
| Cosa verifica | Il blocco di nuove prenotazioni se l'utente ha una prenotazione conclusa priva di commento finale (criterio di accettazione SC-29):<br>1. Una prenotazione conclusa nel passato (`Confirmed`, data/ora di fine trascorsa) senza `ClosingComment` impedisce all'organizzatore di creare nuove prenotazioni (`409 Conflict` con codice della prenotazione da commentare in `bookingToComment`).<br>2. L'invio del commento finale (`POST /bookings/{referenceCode}/closing-comment`) porta la prenotazione nello stato `Closed`, registra il commento nel database e sblocca l'utente.<br>3. Subito dopo l'invio del commento, la creazione di una nuova prenotazione nello stesso slot o in slot futuri ha successo (`201 Created`).<br>4. Prenotazioni annullate nel passato (`Cancelled`), prenotazioni future ancora aperte o prenotazioni non commentate appartenenti ad **altri** utenti non bloccano la creazione.<br>5. In presenza di più prenotazioni non commentate nel passato, l'API segnala la più vecchia in ordine cronologico. |
| Come | 1. `docker compose up -d` (PostgreSQL su `127.0.0.1:5432`, database `bookings_test`).<br>2. `dotnet test tests/SmartCampus.Bookings.Tests --filter "FullyQualifiedName~PendingClosureBlocking"`<br>5 test di integrazione HTTP completi in `tests/SmartCampus.Bookings.Tests/Features/Bookings/PendingClosureBlockingIntegrationTests.cs`, eseguiti con `WebApplicationFactory` e PostgreSQL reale (nessun provider in-memory). |
| Esito atteso | Tutti i 5 test superati: tentativo di prenotazione con commento in sospeso rifiutato con `409 Conflict` e corpo JSON contenente il codice da commentare; invio del commento a buon fine (`200 OK`) con passaggio a stato `Closed`; successiva creazione accettata con `201 Created`. Casi negativi (prenotazioni annullate, future o di altri utenti) non generano blocco. |
| Esito osservato | 28/10/2026, ramo `test/115-pending-closure-block`: tutti i 5 test superati in ~3 s (`Superati: 5, Non superati: 0`). Verifica completa dell'intera suite Bookings: `205` test superati su `205` in ~18 s. |
| Limiti | La prova verifica l'integrazione tra il controller HTTP di Bookings (`BookingsController`, `ClosingCommentsController`), i servizi di dominio (`BookingService`, `ClosingCommentService`, `PendingClosureChecker`) e PostgreSQL reale. Non include l'interfaccia grafica frontend (coperta dal task #114 per la pagina del commento). |

## Come riprodurre

```bash
# Assicurarsi che i container database siano attivi e healthy
docker compose up -d

# Eseguire i test di integrazione specifici della prova
dotnet test tests/SmartCampus.Bookings.Tests \
  --filter "FullyQualifiedName~PendingClosureBlocking"

# Eseguire l'intera suite di Bookings
dotnet test tests/SmartCampus.Bookings.Tests
```

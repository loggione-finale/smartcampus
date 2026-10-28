# Prove e limiti noti

Per ogni prova significativa registriamo:

| Campo | Contenuto |
|---|---|
| Cosa verifica | Il comportamento o criterio di accettazione |
| Come | Comando eseguito, dati usati, ambiente |
| Esito atteso | Definito dal gruppo prima dell'esecuzione |
| Esito osservato | Cosa è successo davvero, con data |
| Limiti | Cosa questa prova **non** dimostra |

Distinguiamo sempre ciò che è stato provato da ciò che è soltanto ipotizzato.

## Prove registrate

- [Isolamento e persistenza dei database](2026-10-26-database-isolation.md) (SC-01.T7, #94)
- [Avvio da zero](2026-10-27-startup-from-scratch.md) (SC-01.T4, #20)
- [Concorrenza sulle prenotazioni](booking-concurrency.md) (SC-06.T4, #35 / #36)
- [Prove di guasto per ogni servizio](2026-10-28-failure-tests.md) (SC-01.T6, #66)
- [Blocco nuove prenotazioni con commento di chiusura in sospeso](2026-10-28-pending-closure-blocking.md) (SC-29.T3, #115)
- [Login, interceptor e guard nel frontend](2026-10-28-login-frontend.md) (SC-25.T3, #82)
- [Email dei partecipanti nel form di prenotazione](2026-10-28-booking-participants-frontend.md) (SC-16.T5, #63)
- [Pagina “Le mie prenotazioni”](2026-10-28-my-bookings-page.md) (SC-08.T2, #38)
- [Messaggi di servizio non disponibile nel frontend](2026-10-28-service-unavailable-snackbar.md) (SC-01.T5, #65)
- [Percorso della demo sull'API](2026-10-28-demo-rehearsal-api.md) (SC-23.T2, #51 / #93)
- [Prova della mattina della consegna](2026-10-29-delivery-morning.md) (#51), da eseguire il 29/10

## Limiti noti

- **Prenotazioni**: la prova di concorrenza (`booking-concurrency.md`, #36) copre solo il livello database, non l'endpoint HTTP; la verifica negativa (senza vincolo o lock la prova fallisce) è solo manuale, e nel caso dell'aula mista il numero esatto di accettate non è deterministico (garantito solo il tetto di capienza).

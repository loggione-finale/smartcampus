# Prova: limite di 4 persone per i gruppi nelle aule miste

Data: 2026-10-28 · Issue: loggione-finale/smartcampus#211 ([SC-16]) · Branch: `feature/211-mixed-space-group-limit`

## Regola

Come da [decisione 20 del PO](../../analysis/po-decisions.md): i singoli prenotano solo aule
miste; i gruppi prenotano aule dedicate, oppure aule miste **fino a 4 persone totali**
(organizzatore + invitati).

## Come è applicata

- **Backend (punto autoritativo)**: `BookingService` rifiuta con esito `GroupTooLarge` ogni
  gruppo con più di 4 persone totali su uno spazio con `AccessMode == Mixed`. Il controllo
  avviene subito dopo la verifica di prenotabilità dello spazio, prima di chiamare i servizi
  downstream (Users, Equipaggiamenti). Risposta HTTP: `409 Conflict`, Problem Details con
  `title: "Group too large"` e `maxGroupSize: 4`.
- **Frontend**: il form di prenotazione mostra un avviso in tempo reale quando il gruppo supera
  4 persone in un'aula mista e blocca l'invio prima della POST. Inoltre traduce la risposta
  `Group too large` del backend in un messaggio italiano equivalente.

## Verifiche eseguite

Comandi (dalla radice del repository):

```bash
# Backend (richiede ConnectionStrings__BookingsTest per il database di test)
dotnet test tests/SmartCampus.Bookings.Tests --nologo
# → 243/243 superati, inclusi i 2 nuovi casi:
#     gruppo di 5 persone in aula mista → 409 "Group too large"
#     gruppo di 4 persone in aula mista con posti liberi → 201 Created

# Frontend
cd src/web
npx ng test --watch=false --browsers=ChromeHeadless
# → 123/123 SUCCESS, inclusi i casi: 5 persone → avviso e nessuna POST;
#   4 persone → invio consentito; risposta 409 "Group too large" → messaggio italiano.
```

## Limiti noti

- Il limite vale solo per i gruppi (`participantEmails` non vuoti): un singolo può prenotare
  un'aula mista se ci sono posti liberi, anche in un'aula da più di 4 posti.
- Il conteggio usa i posti richiesti (`1 + invitati`); non tiene conto di posti occupati da
  altri: quello è già coperto dal controllo dei posti liberi esistente.
- Il form mostra l'avviso solo se la disponibilità giornaliera dello spazio è caricata
  correttamente; in caso di errore del caricamento prevale il controllo del backend.

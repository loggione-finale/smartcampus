# Email dei partecipanti nel form di prenotazione (#63)

| Campo | Contenuto |
|---|---|
| Cosa verifica | Chip per aggiungere e togliere email, rifiuto di email non valide, duplicate o del responsabile, totale dei partecipanti, confronto con la capienza (aula dedicata) o con i posti liberi (aula mista), blocco prima della chiamata quando il gruppo non entra |
| Come | `npm run build` e `npm test -- --watch=false --browsers=ChromeHeadless` in `src/web`; confronto con `EquipmentRequestValidator` e `BookingService` di Bookings |
| Esito atteso | Build senza errori, test verdi, nessuna POST verso `/api/bookings` quando il gruppo supera capienza o posti liberi |
| Esito osservato | Build completata e 109 test su 109 passati il 28/10/2026. Resta il warning già presente sul chunk iniziale (839,38 kB, budget 600 kB); il form ha stili propri, quindi `app.scss` non è più vicino alla soglia di errore. |
| Limiti | Vedi sotto |

## Prove automatiche

- `bookings/booking-form.component.spec.ts`: totale dei partecipanti e capienza mostrati; email del responsabile rifiutata; gruppo oltre la capienza di un'aula dedicata bloccato con messaggio; gruppo oltre i posti liberi di un'aula mista bloccato; gruppo entro i posti liberi accettato con le email nel corpo della richiesta. Email non valide e duplicate erano già coperte.
- `app.spec.ts`: la home include il form nel componente dedicato (`app-booking-form`) e la sezione `#prenota` è raggiungibile.
- Le due prove di blocco sono state eseguite anche con il difetto inserito apposta (controllo di capienza disattivato): falliscono, poi il difetto è stato rimosso. Stessa prova per il controllo dell'email del responsabile.

## Limiti noti

- **Server autoritativo**: il controllo di capienza e posti liberi nel form è un'anteprima. Se la disponibilità dell'aula mista non è leggibile, il form lascia decidere Bookings, che risponde `409 Not enough seats`.
- **Posti liberi delle aule miste**: arrivano da `GET /api/bookings/availability/{spaceId}?date=...`; il servizio interpreta il giorno in UTC, quindi una fascia a cavallo di mezzanotte può non essere coperta e ricade sul controllo del server.
- **Fuso orario**: data e ora sono ancora interpretate nel fuso del dispositivo (follow-up già annotato per #33); il calcolo dei posti liberi usa gli stessi istanti inviati nella POST.
- **Chip**: l'email resta nel campo finché non si preme Invio o si esce dal campo; il form non legge un testo rimasto non confermato.
- **Gruppi nelle aule miste**: la regola della decisione 20 (massimo 4 persone) non è ancora applicata né da Bookings né dal form; il seguito è nella issue #211.

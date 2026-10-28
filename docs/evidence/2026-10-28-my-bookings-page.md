# Pagina “Le mie prenotazioni” (#38)

| Campo | Contenuto |
|---|---|
| Cosa verifica | Elenco delle prenotazioni dell’utente come organizzatore o partecipante, con ruolo, spazio, data, fascia e stato; future prima delle passate; annullate sempre visibili; inviti in attesa con scadenza e conferma; prenotazioni concluse senza commento evidenziate |
| Come | `npm run build` e `npm test -- --watch=false --browsers=ChromeHeadless` in `src/web`; confronto con `MyBookingResponse`/`MyBookingsService`, `InvitationResponse`/`MyInvitationsService`, `AcceptInvitationService` e `InvitationsController` di Bookings; sessione di login con gli account sintetici |
| Esito atteso | Build senza errori, test verdi, pagina protetta da `authGuard`, chiamate a `/api/bookings/mine` e `/api/invitations/mine` con il token aggiunto dall’interceptor |
| Esito osservato | Build completata e 137 test su 137 passati il 28/10/2026 (17 nuovi). La pagina è un chunk lazy di 19,36 kB. Resta il warning già presente su `main` sul chunk iniziale (848,44 kB, budget 600 kB; l’evidenza di #63 registrava 839,38 kB). |
| Limiti | Vedi sotto |

## Prove automatiche

- `bookings/my-bookings.page.spec.ts` (helper): date, fasce e scadenze formattate in `Europe/Rome`; divisione future/passate che conserva l’ordine dell’API; una data di fine non interpretabile finisce nell’archivio invece di sparire; etichette italiane per tutti gli stati (`Confermata`, `Annullata`, `Conclusa`, `Da commentare`) e i ruoli.
- `bookings/my-bookings.page.spec.ts` (componente): spazio, edificio, data, fascia, ruolo e stato visibili; le annullate restano nell’elenco; titolo delle future prima di quello delle passate; singolare/plurale del conteggio; scadenza dell’invito e conferma con `POST /api/invitations/{id}/accept` seguita dal ricaricamento; invito scaduto senza pulsante; `409 Invitation no longer pending` con ricaricamento; `409 Overlapping booking` che lascia l’invito e spiega il conflitto; `404` e `403` con messaggi dedicati; link `/closure?booking=<codice>` sulla prenotazione conclusa senza commento; errore del solo endpoint inviti che lascia visibili le prenotazioni; `503` con pulsante “Riprova”; `401` con invito a rifare l’accesso; stato vuoto con link al catalogo.
- `app.routes.ts`: la rotta `/bookings` usa `loadComponent` ed è protetta da `authGuard`.

## Prova manuale suggerita

1. Avviare i database, i tre servizi e `npm start` in `src/web`.
2. Entrare su `/login` con `sara.conti@smartcampus.test` / `Password123` e aprire “Le mie prenotazioni” da `/account`.
3. Controllare che `SC-SEED-0003` (annullata) sia visibile come “Annullata”, che l’invito di `SC-SEED-0005` mostri la scadenza e che la conferma lo faccia sparire, e che `SC-SEED-0010` (passata e chiusa) non mostri l’avviso di commento.
4. Entrare con `elena.greco@smartcampus.test` / `Password123`: `SC-SEED-0011` deve risultare “Da commentare” con il link al commento finale.

## Limiti noti

- **Raggruppamento sul dispositivo**: future e passate sono separate con l’orologio del browser; un orologio molto errato può collocare una prenotazione nel gruppo sbagliato finché non si preme “Aggiorna”. L’API resta la fonte dei dati.
- **Inviti non leggibili**: se `GET /api/invitations/mine` fallisce (tranne `401`), le prenotazioni restano visibili ma il pulsante di conferma è nascosto e la pagina lo dichiara. Non c’è un retry automatico.
- **Conflitti dell’accettazione**: la pagina distingue i due `409` dal campo `title` (`Overlapping booking` contro scadenza/risposta già data). Se il backend cambiasse quel titolo, il conflitto personale verrebbe mostrato come invito non più disponibile.
- **Nessuna paginazione**: l’endpoint restituisce tutte le prenotazioni dell’utente; la pagina non virtualizza l’elenco.
- **Solo test di componente**: questa prova non esegue un end-to-end contro Bookings reale. La scadenza effettiva degli inviti e l’aggiornamento degli stati restano responsabilità del servizio e dei suoi test di integrazione.
- **Fuso orario**: le date arrivano in UTC e sono mostrate in `Europe/Rome`; il raggruppamento usa lo stesso istante, quindi non dipende dal fuso del dispositivo.

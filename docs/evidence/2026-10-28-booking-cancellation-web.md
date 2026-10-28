# Annullamento prenotazione dal Web (#40)

| Campo | Contenuto |
|---|---|
| Cosa verifica | Pulsante "Annulla prenotazione" con conferma nella pagina "Le mie prenotazioni", visibile solo all'organizzatore per prenotazioni confermate non ancora concluse; chiamata `POST /api/bookings/{id}/cancel`; aggiornamento dell'elenco ed emissione dell'evento `smartcampus:bookings-changed` per sincronizzare la disponibilità degli spazi |
| Come | `npm run build` e `npm test -- --watch=false --browsers=ChromeHeadless` in `src/web`; verifica del dispatch di `smartcampus:bookings-changed` e gestione errori (401, 403, 404, 409, 503/0) |
| Esito atteso | Build completata, test verdi (143/143), pulsante visibile solo all'organizzatore di prenotazioni future confermate, richiesta di conferma inline prima della chiamata, disattivazione durante l'invio, notifica di successo, ricaricamento automatico ed evento di aggiornamento emesso |
| Esito osservato | Build completata e 143 test su 143 passati il 28/10/2026 (6 nuovi test specifici per l'annullamento). Nessuna regressione. |
| Limiti | Vedi sotto |

## Prove automatiche

- `bookings/my-bookings.page.spec.ts`:
  - `canCancel`: pulsante presente per l'organizzatore di prenotazioni confermate non terminate; assente per partecipanti, prenotazioni già annullate o terminate nel passato.
  - Box di conferma inline: il click su "Annulla prenotazione" mostra la richiesta di conferma ("Sì, annulla prenotazione" / "Non annullare"); "Non annullare" ripristina lo stato precedente senza chiamare l'API.
  - Annullamento con successo: invio di `POST /api/bookings/{id}/cancel`, ricaricamento dell'elenco prenotazioni, messaggio di conferma verde ed emissione dell'evento `smartcampus:bookings-changed` su `window` (ascoltato dalla pagina disponibilità).
  - Gestione errore `409`: messaggio "La prenotazione risulta già annullata o non più annullabile." e ricaricamento automatico.
  - Gestione errore `403`: messaggio dedicato "Solo l’organizzatore può annullare la prenotazione.".
  - Gestione errore `503`: messaggio di servizio non raggiungibile.

## Prova manuale suggerita

1. Accedere su `http://localhost:4200/login` con le credenziali di un utente organizzatore (es. `sara.conti@smartcampus.test` / `Password123`).
2. Navigare su `/bookings` ("Le mie prenotazioni").
3. Verificare che sulle prenotazioni future confermate dove l'utente è organizzatore compaia il pulsante "Annulla prenotazione".
4. Cliccare su "Annulla prenotazione": verificare la comparsa del riquadro di conferma.
5. Cliccare su "Sì, annulla prenotazione": verificare che lo stato passi ad "Annullata", che compaia l'avviso di conferma e che la timeline delle disponibilità in `/` rifletta lo spazio liberato.

## Limiti noti

- **Permessi lato client**: il pulsante viene mostrato controllando `booking.role === 'Organizer' && booking.status === 'Confirmed' && !isEnded`. L'effettiva autorizzazione e validazione dello stato avvengono sempre sul backend (`POST /api/bookings/{id}/cancel`).
- **Sincronizzazione della disponibilità**: l'aggiornamento della vista disponibilità avviene via `window.dispatchEvent(new Event('smartcampus:bookings-changed'))`. Se l'utente ha la pagina aperta in un'altra scheda o finestra del browser, la sincronizzazione cross-tab non è automatica (richiede ricaricamento o interazione).

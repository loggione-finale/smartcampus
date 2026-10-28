# Messaggi di servizio non disponibile nel frontend (#65)

| Campo | Contenuto |
|---|---|
| Cosa verifica | Che `503`, errore di rete e timeout produzano un messaggio chiaro e uniforme nell'interfaccia, senza fermare le pagine degli altri servizi |
| Come | `npm run build` e `npm test -- --watch=false --browsers=ChromeHeadless` in `src/web`; revisione dei messaggi e degli stati di errore delle pagine |
| Esito atteso | Build senza errori, test verdi, snackbar con il messaggio del servizio chiamato, errore ancora propagato alla pagina per il pulsante di riprova |
| Esito osservato | Build completata e 156 test su 156 passati il 28/10/2026 (16 nuovi). Resta il warning già presente su `main` sul chunk iniziale (869,72 kB, budget 600 kB; l'evidenza di #38 registrava 848,44 kB). |
| Limiti | Vedi sotto |

## Prove automatiche

- `core/service-unavailable.messages.spec.ts`: riconosce l'area di `/api/spaces`, `/api/bookings`, `/api/invitations` e `/api/users`; distingue `503` («momentaneamente non disponibile»), errore di rete `0` («non raggiungibile») e timeout `504` («non risponde in tempo»); esclude i controlli `/health`.
- `core/service-unavailable.notifier.spec.ts`: lo snackbar dura 6 secondi, ha il pulsante «Chiudi», la classe `sc-snackbar` e `politeness: assertive`; lo stesso messaggio non viene ripetuto entro 5 secondi, ma ricompare dopo la finestra e subito se il messaggio cambia.
- `core/service-unavailable.interceptor.spec.ts`: `503` su Bookings e Users, errore di rete su Spaces e `504` dal proxy mostrano il messaggio giusto e lasciano l'errore alla pagina; `400`, `401`, `403`, `404` e `409` non mostrano nulla; le chiamate `/health` sono escluse; una richiesta senza risposta dopo 10 secondi produce il messaggio di timeout e un `HttpErrorResponse` con stato `504`; dopo un `503` di Bookings una chiamata a Spaces risponde normalmente.

## Prova manuale suggerita

1. Avviare i database, i tre servizi e `npm start` in `src/web`; entrare su `/login` con un account sintetico.
2. Aprire il catalogo degli spazi, poi fermare Spaces con `Ctrl+C` e ricaricare: deve comparire lo snackbar «Catalogo momentaneamente non disponibile. Riprova tra poco.» insieme al pannello della pagina con «Riprova».
3. Senza riavviare il frontend, aprire `/bookings`: la pagina deve funzionare, perché parla solo con Bookings.
4. Fermare Bookings e ricaricare `/bookings`: snackbar «Prenotazioni momentaneamente non disponibili» e pannello di errore con «Riprova». La home e il catalogo (con Spaces riavviato) devono continuare a funzionare.
5. Riavviare i servizi e premere «Riprova»: i dati tornano senza ricaricare il browser.

## Limiti noti

- **Messaggio duplicato**: le pagine che gestiscono già `503` e `0` mostrano anche il proprio messaggio contestuale con il pulsante di riprova. Lo snackbar è transitorio e centralizzato, il messaggio in pagina resta la guida per l'azione.
- **Timeout solo lato client**: la soglia di 10 secondi vale per ogni chiamata `/api/...` del browser; i controlli `/health` ne sono esclusi. Una richiesta più lenta di così viene annullata e la pagina riceve un `504`, anche se il servizio potrebbe rispondere più tardi.
- **Nessun retry automatico**: dopo lo snackbar l'utente deve usare «Riprova» o ricaricare; non viene ripetuta la richiesta, in particolare per i `POST`.
- **Deduplica per messaggio**: due guasti diversi mostrano due snackbar in sequenza (il secondo sostituisce il primo); lo stesso messaggio entro 5 secondi viene mostrato una volta sola.
- **Solo test di componente**: non è stata eseguita una prova end-to-end con i servizi fermi in questa sede; i passi manuali restano da eseguire.

# 0003 – Comunicazione tra servizi: REST sincrono

- Data: 26/10/2026
- Stato: accettata

## Contesto
Bookings deve sapere da Spaces se uno spazio esiste, è attivo, ha capienza e non è indisponibile, e da Users chi sono i partecipanti. Serve un modo semplice da provare e da mostrare.

## Decisione
REST sincrono con `HttpClient` tipizzati, con **timeout** configurabile (`TimeoutSeconds`) su ogni chiamata. Se il servizio chiamato non risponde, il chiamante restituisce `503` con un messaggio chiaro. **Nessun retry automatico sui `POST`**, per non creare falsi `409` con una prenotazione ripetuta.

## Alternative considerate
- **Broker di messaggi (RabbitMQ con outbox):** servizi disaccoppiati, ma mai usato nel corso, un componente in più da installare, duplicati e ordine dei messaggi da gestire. Nessuna funzione del perimetro lo richiede.

## Conseguenze
- Più facile: un flusso che si legge e si prova con una richiesta HTTP.
- Più difficile: con Spaces giù non si creano prenotazioni. È una scelta voluta: meglio un `503` che una prenotazione su uno spazio non verificato. Il comportamento in caso di guasto è nella tabella di [architecture.md](../architecture.md) ("Che cosa succede se va giù un servizio") e va provato (#66).

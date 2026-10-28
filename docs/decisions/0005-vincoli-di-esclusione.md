# 0005 – Nessuna sovrapposizione: vincoli di esclusione in PostgreSQL

- Data: 26/10/2026
- Stato: accettata

## Contesto
È la regola più importante del progetto: uno spazio dedicato e una persona non possono avere due prenotazioni confermate sovrapposte, anche con richieste simultanee. Le slide chiedono di provarlo sul database reale: mock e provider in memoria, da soli, non bastano.

## Decisione
- **Vincoli di esclusione** (`EXCLUDE USING gist` con `btree_gist`) su spazio e periodo e su persona e periodo, solo per le righe confermate. Il nome del vincolo violato dice a Bookings quale `409` restituire.
- **Aule miste:** il conteggio dei posti avviene dentro una transazione con `pg_advisory_xact_lock` sull'aula, perché un vincolo non può contare posti.
- **Una prenotazione per fascia e per giorno** per chi prenota: indice univoco parziale su responsabile, data e fascia (#139).

## Alternative considerate
- **Controllo nel codice** ("se è libero, inserisci"): sbagliato con richieste simultanee, due richieste vedono libero e inseriscono entrambe.
- **Transazione `SERIALIZABLE`:** standard SQL, ma errori di serializzazione da ritentare e più difficile da provare.
- **Lock nel processo** (`lock` in C#): funziona solo con una singola istanza del servizio.

## Conseguenze
- Più facile: è il database a rifiutare la seconda prenotazione; poche righe in una migrazione.
- Più difficile: la soluzione è specifica di PostgreSQL.
- Provato con test di concorrenza sul database reale ([booking-concurrency.md](../evidence/booking-concurrency.md)). Limite: in un giro della CI l'aula mista ha superato la capienza (#142); non riprodotto in 80 esecuzioni, causa probabile una corsa tra test, non dimostrata.

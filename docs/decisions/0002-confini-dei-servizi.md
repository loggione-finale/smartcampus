# 0002 – Confini dei servizi: Spaces, Bookings, Users

- Data: 26/10/2026
- Stato: accettata

## Contesto
I confini devono seguire i bisogni del progetto (trovare uno spazio, prenotarlo anche in gruppo, capire come viene usato) e non gli strati tecnici.

## Decisione
- **Spaces:** catalogo, attributi, strumenti, indisponibilità, gestione da parte dell'admin.
- **Bookings:** prenotazioni singole e di gruppo, inviti, regole di sovrapposizione e capienza, commento finale, statistiche e area admin delle prenotazioni.
- **Users:** registrazione, login, profili e ruoli.

Le statistiche stanno in Bookings perché si calcolano sulle prenotazioni e sulle copie dei dati dello spazio salvate in ogni prenotazione.

## Alternative considerate
- **Spaces, Bookings, Usage, Users:** statistiche isolate, ma Usage dovrebbe copiare i dati di Bookings con sincronizzazione o un broker: più codice e più guasti possibili.
- **Spaces e Bookings, con i profili dentro Bookings:** un servizio in meno, ma i profili non c'entrano con le prenotazioni e Bookings diventa troppo grande.

## Conseguenze
- Bookings è il servizio più grande: ha due owner (Rhaastrake, FranDeSa456) e la cartella degli inviti ha un owner dedicato.
- Le viste che uniscono dati di servizi diversi usano copie salvate in Bookings o la composizione dal frontend ([data-model.md](../data-model.md#aggregare-i-dati-di-servizi-diversi)).
- Le copie (nome, tipo, edificio dello spazio) descrivono lo spazio com'era alla prenotazione: le statistiche lo dichiarano nei limiti.

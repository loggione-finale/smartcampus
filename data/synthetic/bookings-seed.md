# Seed delle prenotazioni (servizio Bookings)

Il file `data/synthetic/bookings.json` contiene utenti, spazi e
prenotazioni inventati. All'avvio in ambiente `Development` il servizio Bookings li carica se la tabella
`bookings` è vuota; un secondo avvio non duplica nulla. Tutto avviene in una sola transazione.

Le date sono relative a oggi (`daysFromToday`, orari in ora di Roma): i casi restano sempre gli stessi, ma "passato" e
"futuro" restano veri nel tempo.

Ogni prenotazione sta dentro una sola fascia (mattina 08:00-14:00, pomeriggio 14:00-19:00, sera 19:00-23:00), dura da 30 minuti a 4 ore e ogni responsabile ne ha al massimo una per fascia e per giorno: il seed rispetta le stesse regole dell'API.

## Identificativi condivisi

Bookings salva solo riferimenti a utenti e spazi, quindi gli id devono coincidere con quelli di Users e Spaces.

| Utente | Id | Email |
|---|---|---|
| Admin Campus | `b7e1c2d3-0000-4000-8000-000000000001` | `admin@smartcampus.test` |
| Giulia Rossi | `b7e1c2d3-0000-4000-8000-000000000002` | `giulia.rossi@smartcampus.test` |
| Marco Bianchi | `b7e1c2d3-0000-4000-8000-000000000003` | `marco.bianchi@smartcampus.test` |
| Sara Conti | `b7e1c2d3-0000-4000-8000-000000000004` | `sara.conti@smartcampus.test` |
| Luca Ferrari | `b7e1c2d3-0000-4000-8000-000000000005` | `luca.ferrari@smartcampus.test` |
| Elena Greco | `b7e1c2d3-0000-4000-8000-000000000006` | `elena.greco@smartcampus.test` |

| Spazio | Id | Tipo | Modalità | Capienza |
|---|---|---|---|---|
| Aula 101 | `3f2a9c1e-0000-4000-8000-000000000001` | Classroom | Dedicated | 30 |
| Laboratorio Informatica 2 | `3f2a9c1e-0000-4000-8000-000000000002` | Lab | Dedicated | 24 |
| Sala Studio Piano Terra | `3f2a9c1e-0000-4000-8000-000000000003` | StudyRoom | Mixed | 12 |
| Aula 204 | `3f2a9c1e-0000-4000-8000-000000000004` | Classroom | Mixed | 6 |

Gli stessi id vanno usati nei dati di Users (#21) e di Spaces (#22).

## Casi limite coperti

| Codice | Caso |
|---|---|
| `SC-SEED-0001`, `0002` | Prenotazioni adiacenti sulla stessa aula dedicata (09:00-11:00 e 11:00-13:00) |
| `SC-SEED-0003`, `0004` | Prenotazione annullata e, nella stessa fascia, una nuova prenotazione confermata |
| `SC-SEED-0005` | Gruppo con inviti in tutti gli stati (`Accepted`, `Invited`, `Left`, `Expired`), un invitato non registrato e una richiesta di strumenti in attesa con avviso per l'admin |
| `SC-SEED-0006`, `0007`, `0008` | Aula mista con più prenotazioni sovrapposte, sotto la capienza |
| `SC-SEED-0009` | Prenotazione passata chiusa, con commento finale senza problemi |
| `SC-SEED-0010` | Prenotazione passata chiusa, con commento che segnala un problema e avviso per l'admin |
| `SC-SEED-0011` | Prenotazione passata senza commento finale: Elena non può crearne di nuove finché non lo scrive |
| `SC-SEED-0012`, `0013` | Aula mista da 6 posti esattamente piena (gruppo da 5 più una prenotazione singola); invitato non registrato |

Per provare il blocco del commento finale accedi come Elena Greco. Per provare l'aula piena, cerca un posto nella
fascia di `SC-SEED-0012` di Aula 204.

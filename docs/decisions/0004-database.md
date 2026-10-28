# 0004 – Database: Spaces su MongoDB, Bookings e Users su PostgreSQL

- Data: 26/10/2026, aggiornata il 27/10/2026
- Stato: accettata

## Contesto
Ogni servizio ha il proprio database. Le slide citano SQL e NoSQL e chiedono di motivare sia l'uso sia l'esclusione. Bookings ha scritture concorrenti che non devono mai sovrapporsi; Spaces ha attributi diversi per tipo di spazio (postazioni e software nei laboratori, proiettore e lavagna nelle aule).

## Decisione
- **Spaces su MongoDB 7.0**, con `MongoDB.Driver` e indici creati all'avvio: un documento per spazio, con le indisponibilità dentro il documento.
- **Bookings e Users su PostgreSQL 17**, con EF Core (Npgsql) e migrazioni: vincoli e transazioni.
- Ogni servizio riceve solo la stringa di connessione del proprio database, con un utente a privilegi minimi.

MongoDB è la **7.0** e non la 8: la 8 non parte sui kernel Linux dal 6.19 (SERVER-121912), che alcuni server della CI di GitHub usano già (#148, #152).

## Alternative considerate
- **Tutto PostgreSQL:** un motore solo, CI più semplice; scartato perché il catalogo ha davvero attributi variabili e NoSQL qui è motivato.
- **SQL Server:** nessun vincolo di esclusione, immagine pesante e scomoda sui Mac ARM.
- **MySQL/MariaDB:** nessun tipo intervallo né vincolo di esclusione.
- **Tutto MongoDB:** impossibile garantire l'assenza di sovrapposizioni con richieste simultanee ([0005](0005-vincoli-di-esclusione.md)).

## Conseguenze
- Più facile: ogni dato sta nel motore adatto; un guasto di MongoDB ferma solo il catalogo.
- Più difficile: due motori da avviare, proteggere e provare; nessuna query unisce i due database, quindi servono le copie in Bookings e i test di coerenza dei dati sintetici (`tests/SmartCampus.SyntheticData.Tests`).
- Rischio che resta: PostgreSQL è un punto unico di guasto per Bookings e Users, mitigato con riavvio automatico, health check ed `EnableRetryOnFailure`.

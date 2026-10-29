# Speech per la presentazione: chi ha fatto cosa

> Un intervento di circa 40 secondi a testa, sul proprio dominio: cosa, perché, cosa si vede e un limite onesto.
> È una traccia: ognuno la riscrive con parole proprie e la prova a voce alle 12:00 (#51). Le PR citate sono in
> [team-work-map.md](team-work-map.md).

## asprenolapo: coordinamento e piattaforma

"Ho tenuto insieme il lavoro: gli incontri con il PO e il registro delle decisioni, la board, la divisione per aree con
un owner e un revisore. Ho preparato la base comune: lo scheletro dei tre servizi, i database con Docker, la CI e il
controllo fisso delle PR, e l'avvio con un solo comando. Perché? Sette persone in quattro giorni lavorano bene solo se
le regole sono scritte e controllate in automatico. Le prove di guasto e di avvio da zero sono nella cartella
evidence. Un limite: con il database fermo l'errore arriva in sette secondi, non in cinque."

## Rhaastrake: prenotazioni senza conflitti

"Ho costruito il cuore del prodotto, il servizio Bookings. La promessa è che nessuno spazio e nessuna persona siano
prenotati due volte, anche con richieste simultanee. Per questo le regole stanno nel database: vincoli di esclusione,
un lock per i posti delle aule miste, una prenotazione per fascia. Ho curato anche il percorso della demo e la prova di
tutto il sistema su Linux. Nella demo lo vedete con il conflitto `409` e con la prenotazione adiacente accettata. Un
limite: il nuovo vincolo sui partecipanti è controllato dall'applicazione, non ancora dal database."

## FranDeSa456: gruppi, inviti ed email

"Ho lavorato sulle prenotazioni di gruppo. Chi prenota scrive le email dei partecipanti: i registrati ricevono la
conferma, gli altri un link per registrarsi e tornare all'invito. Chi non conferma entro due ore viene escluso e il
posto si libera. Le email sono file `.eml`, così si vedono senza un server di posta e senza dati reali. Ho scritto
anche le prove di concorrenza e i test di sicurezza di Bookings. Nella demo: invito, mail, conferma. Un limite: la
scadenza si mostra a voce, perché aspettare due ore non si può."

## antoStr: area admin

"Ho realizzato l'area admin, dalle API alle pagine. L'admin non prenota: governa le eccezioni. Vede quali aule sono
prenotate e libere, riceve un avviso per ogni prenotazione e per gli strumenti mancanti, annulla con un motivo e
una mail, e legge le statistiche costruite dai commenti finali. Perché? Il secondo obiettivo del progetto è capire
come gli spazi vengono usati davvero. Nella demo: dashboard, avvisi e un utente normale che riceve `403`. Ho anche
fatto un audit del backend, che ha trovato due difetti reali nel servizio Users, corretti prima della consegna."

## fedeericodl: accesso, disponibilità, commento finale

"Ho curato chi entra e cosa resta dopo una prenotazione. La libreria di accesso condivisa dai tre servizi, la
registrazione e il login con email o matricola. Poi la disponibilità di un'aula e la ricerca degli spazi liberi in una
fascia. E il commento finale: dopo ogni prenotazione si dichiara se lo spazio è stato usato e da quante persone, e
finché manca non se ne fanno altre. È il dato che alimenta le statistiche dell'admin. Nella demo: il blocco e lo sblocco
dopo il commento. Un limite: è un dato dichiarato, non misurato."

## AldAndrew0: spazi e strumenti

"Ho costruito il servizio Spaces su MongoDB: il catalogo, con attributi diversi per tipo di spazio, la ricerca con i
filtri, la disponibilità del giorno, le indisponibilità e la gestione per l'admin. Bookings chiede a Spaces se uno
spazio è prenotabile prima di confermare. Ho aggiunto gli strumenti, come il videoproiettore: un gruppo li richiede e,
se l'aula non li ha, l'admin riceve un avviso. Nella demo: il catalogo con i filtri e le fasce. Un limite: uno strumento
non in elenco non si può ancora segnalare dall'interfaccia."

## Leo-prova: frontend

"Ho dato forma all'interfaccia in Angular: la struttura comune, la registrazione, il login con la sessione, il form di
prenotazione e la pagina con le mie prenotazioni. Il form controlla prima di inviare: durata, fascia, email dei
partecipanti rispetto ai posti, e mostra gli strumenti solo ai gruppi. Quando un servizio non risponde, compare un solo
messaggio chiaro invece di errori sparsi. Nella demo: la prenotazione dall'inizio alla fine. Un limite: lo stile delle
pagine non è ancora uniforme ovunque, e c'è già una proposta per dopo la consegna."

## Chiusura comune (chi presenta per ultimo)

"Tutto quello che avete visto è provato, e le prove sono nel repository. I dati sono sintetici: dimostrano che il
prototipo funziona, non che il campus ne trarrà benefici. Quello che non è entrato lo abbiamo scritto come limite."

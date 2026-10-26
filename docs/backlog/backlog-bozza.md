# Bozza del Product Backlog

> **Stato: bozza da rivedere insieme, poi da validare con il PO.** Le storie diventeranno issue solo dopo la
> revisione. Gli story point sono una prima proposta: rivotiamoli insieme (planning poker).
> Riferimenti: [analisi](../analisi/analisi-progetto.md), [architettura](../architettura.md).

## Riepilogo

| ID | Titolo | Priorità | Servizio | SP | Giorno |
|---|---|---|---|---|---|
| SC-01 | Avvio riproducibile dell'intero sistema | Must | Piattaforma | 5 | Lun 26 |
| SC-02 | Dati sintetici riproducibili | Must | Piattaforma | 3 | Lun 26 – Mar 27 |
| SC-03 | Scelta dell'utente e del ruolo | Must | Web / Gateway | 2 | Lun 26 |
| SC-04 | Consultare e filtrare il catalogo spazi | Must | Spazi + Web | 3 | Mar 27 |
| SC-05 | Vedere la disponibilità di uno spazio | Must | Prenotazioni + Web | 3 | Mar 27 |
| SC-06 | Prenotare uno spazio libero | Must | Prenotazioni + Web | 5 | Mar 27 |
| SC-07 | Nessuna sovrapposizione, anche con richieste simultanee | Must | Prenotazioni | 5 | Mar 27 |
| SC-08 | Consultare le mie prenotazioni | Must | Prenotazioni + Web | 2 | Mer 28 |
| SC-09 | Annullare una prenotazione | Must | Prenotazioni + Web | 3 | Mer 28 |
| SC-10 | Gestire il catalogo spazi | Must | Spazi + Web | 3 | Mar 27 – Mer 28 |
| SC-11 | Segnalare l'indisponibilità di uno spazio | Should | Spazi + Prenotazioni | 3 | Mer 28 |
| SC-12 | Registrare l'uso effettivo (check-in) | Should | Prenotazioni + Web | 3 | Mer 28 |
| SC-13 | Report di utilizzo con limiti dichiarati | Should | Utilizzo + Web | 5 | Mer 28 |
| SC-14 | Build e test automatici su ogni PR | Should | Piattaforma | 2 | Lun 26 |
| SC-15 | Avviso quando uno spazio si libera | Could | Prenotazioni | 8 | Solo se avanza tempo |

**Totale Must: 34 SP. Totale Should: 13 SP.** Giovedì 29 è riservato a correzioni e prova della demo.

---

## [SC-01] - Avvio riproducibile dell'intero sistema

**User Story**
Come membro del team (o valutatore)
voglio avviare l'intero sistema con un solo comando, seguendo il README
in modo da poter provare il prodotto su qualsiasi macchina senza configurazioni manuali.

**Descrizione**
Lo scheletro della soluzione contiene tutti i servizi, il gateway, l'interfaccia e il database, collegati
tra loro fin dal primo giorno, anche se ancora senza funzioni. Nessun segreto è salvato nel repository.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Seguendo il README su una macchina pulita (con .NET SDK e Docker) il sistema si avvia con un comando.
- [ ] L'interfaccia raggiunge ogni servizio passando dal gateway.
- [ ] Ogni servizio espone un controllo di salute che risponde correttamente.
- [ ] L'avvio è stato provato su almeno due sistemi operativi diversi, con esito registrato in `docs/prove/`.

**Story Point**
Stima: 5

---

## [SC-02] - Dati sintetici riproducibili

**User Story**
Come membro del team
voglio caricare un insieme di dati sintetici sempre uguale
in modo da provare e dimostrare il prodotto con casi realistici senza usare dati personali reali.

**Descrizione**
I dati comprendono spazi di tipi diversi, utenti fittizi con ruoli diversi e prenotazioni che includono
casi limite (prenotazioni adiacenti, annullate, nel passato).

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] All'avvio in sviluppo i database vengono popolati automaticamente con gli stessi dati.
- [ ] Nessun dato corrisponde a persone reali.
- [ ] I casi limite inclusi sono descritti in `data/synthetic/README.md`.

**Story Point**
Stima: 3

---

## [SC-03] - Scelta dell'utente e del ruolo

**User Story**
Come utente del prototipo
voglio scegliere con quale utente fittizio entrare (studente o personale)
in modo da usare le funzioni del mio ruolo senza creare un account reale.

**Descrizione**
All'ingresso si sceglie un utente da un elenco di utenti sintetici. Il ruolo determina quali funzioni sono
visibili. Si tratta di una semplificazione da dichiarare nei limiti noti.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] All'ingresso è possibile scegliere un utente fittizio dall'elenco.
- [ ] Uno studente non vede le funzioni riservate al personale.
- [ ] L'utente scelto è visibile in ogni pagina e si può cambiare.

**Story Point**
Stima: 2

---

## [SC-04] - Consultare e filtrare il catalogo spazi

**User Story**
Come studente
voglio vedere gli spazi del campus e filtrarli per tipo, capienza e attrezzature
in modo da trovare rapidamente uno spazio adatto alla mia attività.

**Descrizione**
Il catalogo mostra per ogni spazio nome, tipo (aula, laboratorio, sala studio), edificio, capienza e
attrezzature. I filtri si possono combinare.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] L'elenco mostra tutti gli spazi attivi con le loro caratteristiche.
- [ ] Filtrando per tipo, capienza minima e attrezzatura compaiono solo gli spazi che soddisfano tutti i filtri.
- [ ] Se nessuno spazio soddisfa i filtri compare un messaggio chiaro.

**Story Point**
Stima: 3

---

## [SC-05] - Vedere la disponibilità di uno spazio

**User Story**
Come studente
voglio vedere se uno spazio è libero in un giorno e in una fascia oraria
in modo da organizzarmi con fiducia prima di andare sul posto.

**Descrizione**
Per uno spazio e un giorno, il sistema mostra le fasce già prenotate e quelle libere. Le informazioni sono
le stesse per tutti gli utenti.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Per uno spazio e un giorno sono visibili le fasce occupate e quelle libere.
- [ ] Una prenotazione annullata non compare più come occupata.
- [ ] Due utenti diversi vedono la stessa disponibilità nello stesso momento.

**Story Point**
Stima: 3

---

## [SC-06] - Prenotare uno spazio libero

**User Story**
Come studente
voglio prenotare uno spazio libero per una fascia oraria
in modo da avere una conferma affidabile che lo spazio sarà a mia disposizione.

**Descrizione**
L'utente sceglie spazio, giorno, ora di inizio e di fine. Se lo spazio è libero la prenotazione viene
confermata; altrimenti il sistema spiega il motivo.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Prenotando una fascia libera si riceve una conferma con i dettagli della prenotazione.
- [ ] Una fascia con fine precedente all'inizio, o nel passato, viene rifiutata con un messaggio chiaro.
- [ ] Prenotando una fascia già occupata si riceve un messaggio che indica il conflitto.

**Story Point**
Stima: 5

---

## [SC-07] - Nessuna sovrapposizione, anche con richieste simultanee

**User Story**
Come studente
voglio che il sistema non accetti mai due prenotazioni sovrapposte sullo stesso spazio
in modo da non trovare l'aula occupata da chi contava di usarla come me.

**Descrizione**
La regola deve valere anche quando più persone prenotano lo stesso spazio nello stesso istante. La prova
deve essere eseguita sul database reale, non con simulazioni in memoria.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Con molte richieste simultanee per la stessa fascia dello stesso spazio, ne viene accettata esattamente una.
- [ ] Due prenotazioni adiacenti (una finisce quando inizia l'altra) sono entrambe accettate.
- [ ] La prova è automatica, gira sul database reale ed è registrata in `docs/prove/`.

**Story Point**
Stima: 5

---

## [SC-08] - Consultare le mie prenotazioni

**User Story**
Come studente
voglio vedere l'elenco delle mie prenotazioni con il loro stato
in modo da ritrovare in ogni momento una conferma affidabile.

**Descrizione**
L'elenco mostra le prenotazioni dell'utente, future e passate, con spazio, fascia oraria e stato
(confermata, annullata).

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] L'elenco mostra solo le prenotazioni dell'utente attuale.
- [ ] Per ogni prenotazione sono visibili spazio, data, fascia e stato.
- [ ] Le prenotazioni future compaiono prima di quelle passate.

**Story Point**
Stima: 2

---

## [SC-09] - Annullare una prenotazione

**User Story**
Come studente
voglio annullare una mia prenotazione
in modo da liberare lo spazio per chi ne ha bisogno.

**Descrizione**
L'annullamento rende subito lo spazio disponibile per tutti. La prenotazione resta nello storico come
annullata, non viene cancellata.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Dopo l'annullamento la fascia risulta libera per tutti gli utenti.
- [ ] La prenotazione resta visibile nel mio elenco con stato "annullata".
- [ ] Non è possibile annullare la prenotazione di un altro studente.

**Story Point**
Stima: 3

---

## [SC-10] - Gestire il catalogo spazi

**User Story**
Come personale del campus
voglio aggiungere, modificare e disattivare gli spazi
in modo che il catalogo corrisponda sempre agli spazi realmente disponibili.

**Descrizione**
Il personale gestisce le caratteristiche degli spazi. Uno spazio disattivato non è più prenotabile ma le
sue prenotazioni passate restano consultabili.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Il personale può aggiungere uno spazio e questo compare subito nel catalogo.
- [ ] Il personale può modificare capienza e attrezzature di uno spazio.
- [ ] Uno spazio disattivato non è più prenotabile e non compare nella ricerca degli studenti.
- [ ] Uno studente non può accedere a queste funzioni.

**Story Point**
Stima: 3

---

## [SC-11] - Segnalare l'indisponibilità di uno spazio

**User Story**
Come personale del campus
voglio segnare uno spazio come indisponibile in un periodo (manutenzione, chiusura)
in modo che nessuno lo prenoti quando non si può usare.

**Descrizione**
Un'indisponibilità ha un periodo e un motivo. Nel periodo lo spazio non è prenotabile e la disponibilità
mostra il motivo.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Non è possibile prenotare uno spazio in un periodo di indisponibilità.
- [ ] La disponibilità mostra il periodo e il motivo dell'indisponibilità.
- [ ] _Da chiarire con il PO: cosa succede alle prenotazioni già esistenti nel periodo._

**Story Point**
Stima: 3

---

## [SC-12] - Registrare l'uso effettivo (check-in)

**User Story**
Come studente
voglio confermare di essere arrivato nello spazio prenotato
in modo che il campus sappia che la prenotazione è stata davvero usata.

**Descrizione**
Il check-in è possibile solo per una propria prenotazione confermata e solo vicino all'orario di inizio.
Distingue l'intenzione (prenotazione) dall'uso dichiarato. Non usa sensori reali.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Il check-in è possibile solo sulle proprie prenotazioni confermate.
- [ ] Fuori dalla finestra di tempo consentita il check-in viene rifiutato con un messaggio chiaro.
- [ ] Lo stato della prenotazione mostra se il check-in è avvenuto.

**Story Point**
Stima: 3

---

## [SC-13] - Report di utilizzo con limiti dichiarati

**User Story**
Come personale del campus
voglio vedere per ogni spazio quante prenotazioni ci sono state, quante con check-in e quante senza dati
in modo da capire l'utilizzo senza trasformare ipotesi in certezze.

**Descrizione**
Il report riguarda un periodo scelto. Distingue prenotazioni, check-in avvenuti e prenotazioni senza
informazioni sull'uso. Una nota spiega che l'assenza di dati non dimostra che lo spazio fosse libero.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Per un periodo scelto, il report mostra per ogni spazio prenotazioni, annullamenti, check-in e prenotazioni senza check-in.
- [ ] I numeri coincidono con quelli calcolati a mano su un insieme di dati sintetici noto.
- [ ] Il report riporta in modo visibile i limiti delle informazioni.

**Story Point**
Stima: 5

---

## [SC-14] - Build e test automatici su ogni PR

**User Story**
Come membro del team
voglio che ogni pull request venga compilata e testata automaticamente
in modo che il revisore sappia subito se la modifica rompe qualcosa.

**Descrizione**
Un workflow di GitHub Actions compila la soluzione ed esegue tutti i test, compresi quelli sul database
reale, a ogni PR verso `main`.

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Su ogni PR verso `main` il workflow si avvia da solo e mostra l'esito.
- [ ] Una PR con un test fallito risulta in rosso.
- [ ] Il controllo è obbligatorio per il merge su `main`.

**Story Point**
Stima: 2

---

## [SC-15] - Avviso quando uno spazio si libera

**User Story**
Come studente
voglio essere avvisato quando uno spazio che mi interessava si libera
in modo da non perdere l'occasione di prenotarlo.

**Descrizione**
Lo studente si segna come interessato a una fascia occupata. Se la prenotazione viene annullata, riceve un
avviso nell'interfaccia (niente email reali).

**Criteri di accettazione**
La storia è considerata completata quando:
- [ ] Uno studente può segnarsi come interessato a una fascia occupata.
- [ ] Dopo l'annullamento della prenotazione gli interessati vedono un avviso.
- [ ] L'avviso non garantisce la fascia: serve comunque prenotarla.

**Story Point**
Stima: 8

---

## Fuori backlog: bonus agentico (facoltativo)

Le slide lo prevedono solo **dopo** aver verificato il risultato minimo e con l'accordo del PO: un solo
task, con una scheda che riporta task, autore, strumento, proposta, verifiche effettive, esito e revisore.
Da decidere più avanti.

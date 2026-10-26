# Analisi dei requisiti rispetto alle slide

> **Stato: bozza da rivedere insieme.** Ogni requisito è estratto dalle slide "Project Work SmartCampus"
> (il numero tra parentesi è la slide) e confrontato con quanto abbiamo oggi: analisi, architettura,
> backlog e processo.
>
> **Coperto**: c'è già una storia, un task o un documento che lo soddisfa. **Parziale**: c'è, ma manca
> qualcosa. **Mancante**: non c'è ancora.

## Riepilogo

| Area | Coperti | Parziali | Mancanti |
|---|---|---|---|
| A. Bisogno e prodotto | 6 | 4 | 2 |
| B. Scelte tecniche | 3 | 3 | 0 |
| C. Evidenze nel repository | 4 | 3 | 1 |
| D. Sicurezza e sostenibilità | 3 | 1 | 0 |
| E. Agile e Scrum | 3 | 2 | 4 |
| F. Presentazione e analisi finale | 2 | 2 | 2 |
| **Totale (45)** | **21** | **15** | **9** |

Il prodotto è ben coperto sul **prenotare senza conflitti**. Siamo più deboli su tre fronti:
**capire l'utilizzo** (che per le slide fa parte dell'obiettivo, non è un extra), **il punto di vista del
personale** e **le evidenze di processo** (Product Goal, Sprint Goal, accordi con il PO, costi).

---

## A. Bisogno e prodotto

| ID | Requisito (slide) | Stato | Dove / cosa manca |
|---|---|---|---|
| A1 | Trovare spazi adatti tra aule, laboratori e sale studio (6, 9) | Coperto | SC-04 |
| A2 | Prenotare senza impegni incompatibili (9) | Coperto | SC-06, SC-07, SC-16 |
| A3 | Le informazioni sulla disponibilità sono le stesse per tutti (6) | Coperto | SC-05: un'unica fonte, il servizio Bookings |
| A4 | Una conferma affidabile si ritrova facilmente (7) | Coperto | SC-08 |
| A5 | Una rinuncia raggiunge chi avrebbe bisogno dell'aula (7) | **Parziale** | SC-09 libera lo spazio, ma nessuno viene avvisato: SC-15 è solo Could |
| A6 | Uno spazio non resta "apparentemente occupato" se nessuno lo usa (7) | **Mancante** | Una prenotazione senza check-in blocca lo spazio fino alla fine. Proposta: **rilascio automatico** se nessuno fa check-in entro 15 minuti |
| A7 | Il personale non deve "ricostruire situazioni che nessuno vede per intero" (7) | **Mancante** | Nessuna vista d'insieme per il personale. Proposta: **calendario di tutte le prenotazioni** per giorno e per spazio |
| A8 | Distinguere uso effettivo, aspettative e impressioni; una prenotazione è un'intenzione (8) | Parziale | SC-12 (check-in) e SC-13 (report) esistono ma sono **Should** |
| A9 | Capire quali spazi sono "sempre richiesti" e quali "spesso vuoti" (8) | Parziale | SC-13 conta le prenotazioni ma non definisce un **tasso di utilizzo** con una formula spiegata |
| A10 | L'assenza di informazioni non dimostra che uno spazio fosse libero (8, 61) | Coperto | SC-13: nota sui limiti nel report |
| A11 | "Trovare **e prenotare** … **rendendone più comprensibile l'utilizzo**" è l'obiettivo del prodotto (9) | **Parziale** | Capire l'utilizzo è nell'obiettivo, ma noi l'abbiamo messo in Should. Va concordato con il PO (vedi E9) |
| A12 | Prodotto funzionante e integrato, non parti scollegate (9, 25) | Coperto | Scheletro integrato il primo giorno (SC-01) |

## B. Scelte tecniche

| ID | Requisito (slide) | Stato | Dove / cosa manca |
|---|---|---|---|
| B1 | Usare le conoscenze del corso: .NET/C#, cross-platform, SQL, NoSQL, cloud (11) | Parziale | .NET, SQL e cross-platform sì; **NoSQL e cloud non sono né usati né esclusi con una motivazione** (vedi [D4](../architecture.md#d4--database)) |
| B2 | Framework, database e forma dell'interfaccia sono scelte motivate (11) | Coperto | `docs/architecture.md`, D1–D10 |
| B3 | Doppia persistenza, telemetria e cloud non sono imposti: giustificare usi ed **esclusioni** (11) | Parziale | Manca la motivazione esplicita per l'esclusione di cloud e telemetria |
| B4 | Interfaccia utilizzabile negli ambienti proposti, portabilità dimostrabile (11, 59) | Parziale | Angular gira nel browser, ma nessun task prova la UI su più sistemi e su smartphone |
| B5 | Regole, architettura, dati, prove e documentazione sono scelte da motivare (10) | Coperto | Architettura e analisi |
| B6 | Concorrenza e persistenza provate sul database reale, non solo con mock (58) | Coperto | SC-07.T2, SC-16.T4 |

## C. Evidenze nel repository (12)

| ID | Requisito (slide) | Stato | Dove / cosa manca |
|---|---|---|---|
| C1 | Codice | Coperto | `src/` |
| C2 | Configurazione sicura | Coperto | `.env.example`, nessun segreto (SC-01.T2) |
| C3 | Istruzioni riproducibili di installazione, avvio e test (12, 59) | Coperto | README + SC-01.T4 |
| C4 | Analisi, **perimetro autorizzato**, backlog autonomo, criteri, DoD | **Parziale** | Il perimetro non è ancora stato validato dal PO né registrato |
| C5 | Regole, **schemi**, dati sintetici, prove eseguite, limiti noti | Parziale | Schemi solo come diagramma ER; mancano le regole di dominio scritte in un unico posto |
| C6 | Incrementi, **feedback del PO**, review, contributi individuali spiegabili | **Parziale** | Review sì (PR obbligatorie); nessun posto dove registrare feedback e decisioni del PO; incrementi non marcati |
| C7 | Esiti attesi delle prove definiti dal gruppo (12, 58) | Coperto | `docs/evidence/README.md` |
| C8 | Decisioni motivate tracciabili | **Mancante** | `docs/decisions/` è vuota: le scelte sono ancora "proposte" dentro l'architettura |

## D. Sicurezza e sostenibilità (13)

| ID | Requisito (slide) | Stato | Dove / cosa manca |
|---|---|---|---|
| D1 | Solo dati sintetici prodotti dal gruppo | Coperto | SC-02, SC-03.T3 |
| D2 | Segreti esclusi da repository, **log**, prompt e materiali condivisi | Parziale | Repository protetto (secret scanning attivo); manca una regola sui **log** e sull'uso degli strumenti AI. Vedi [sicurezza e test](../security-and-testing.md) |
| D3 | Dimostrazione locale sufficiente, nessuna spesa né account | Coperto | Tutto gira in locale |
| D4 | Sensori reali e infrastrutture di produzione fuori perimetro | Coperto | Analisi, sezione 3 |

## E. Agile e Scrum (14–33)

| ID | Requisito (slide) | Stato | Dove / cosa manca |
|---|---|---|---|
| E1 | **Product Goal** come riferimento del backlog (25, 26) | **Mancante** | Non è scritto da nessuna parte |
| E2 | **Sprint Goal** unico per ogni Sprint, non un elenco di task (26, 28) | **Mancante** | Abbiamo un piano per giorni, ma nessuno Sprint Goal |
| E3 | Product Backlog ordinato, unica fonte del lavoro (27) | Coperto | Issue + board, priorità e giorni |
| E4 | Storie (bisogno e valore) distinte dai task (lavoro tecnico) (27) | Coperto | Tipi Feature → User Story → Task |
| E5 | Definition of Done e criteri di accettazione concordati con il PO (31) | Parziale | Esistono, ma non sono ancora concordati con il PO |
| E6 | Increment utilizzabile, integrato e verificato (25) | Coperto | DoD + scheletro integrato |
| E7 | Eventi: Planning, Daily, Review, Retrospective (28–30) | **Mancante** | Nessuna traccia di quando e come li facciamo |
| E8 | **Adattamenti dichiarati**: ritmo, dimensione degli incrementi, responsabilità (32) | **Mancante** | Va scritto come adattiamo Scrum a 3 giorni |
| E9 | "Una riduzione unilaterale del perimetro non ridefinisce il risultato atteso" (10) | Parziale | Le nostre priorità Should/Could sono una **proposta**: vanno validate dal PO, non decise da noi |

## F. Presentazione e analisi finale (55–64)

| ID | Requisito (slide) | Stato | Dove / cosa manca |
|---|---|---|---|
| F1 | Breve presentazione: bisogno, perimetro concordato, prodotto funzionante (60) | Coperto | Issue #51 |
| F2 | Ogni componente spiega il proprio contributo (60) | Coperto | Issue #51 + PR personali |
| F3 | Misure di utilizzo spiegabili e verificabili, non formule prefissate (61) | Parziale | Va scritto **come** si calcola ogni numero del report |
| F4 | Dati sintetici: verificano il prototipo, non dimostrano benefici reali (61, 62) | Parziale | Da dire nei limiti noti e nella presentazione |
| F5 | **Valore e costi**: risorse, storage, traffico, log, manutenzione; ragioni per usare o escludere il cloud (62) | **Mancante** | Nessuna analisi dei costi |
| F6 | **Sviluppi futuri**: quando cambierebbe la scala (63), solo discussione | **Mancante** | Una pagina basta |

---

## Requisiti impliciti emersi dall'analisi

Non sono scritti nelle slide, ma servono a soddisfare il bisogno o a non introdurre problemi nuovi.

| ID | Requisito | Perché | Proposta |
|---|---|---|---|
| I1 | Orari coerenti tra browser, servizi e database | Una prenotazione alle 10:00 non deve diventare 09:00 per un fuso orario sbagliato | API e database in **UTC**, frontend in **Europe/Rome**; un test sul cambio dell'ora legale |
| I2 | Fasce orarie con regole chiare | Prenotazioni di 3 minuti o di 3 settimane rendono inutile il calendario | Granularità a 15 minuti, durata minima e massima, anticipo massimo: **da chiedere al PO** |
| I3 | Nessuno può bloccare il tempo di un altro | Con l'aggiunta diretta dei partecipanti, chiunque può "occupare" la fascia di un'altra persona | Il partecipante può uscire (SC-16.T3); proposta: limite di prenotazioni future per persona |
| I4 | Il personale vede chi ha fatto cosa | "Ricostruire situazioni" richiede uno storico | Data di creazione, annullamento e check-in salvate su ogni prenotazione |
| I5 | Ogni errore ha un messaggio comprensibile | "Le informazioni erano semplicemente diverse" (6) | Risposte `ProblemDetails` con un messaggio pensato per l'utente |

---

## Lacune principali e proposte

In ordine di importanza. Le modifiche al perimetro (1, 2, 3) vanno **proposte al PO**, non decise da noi.

| # | Lacuna | Proposta | Impatto sul backlog |
|---|---|---|---|
| 1 | Capire l'utilizzo è nell'obiettivo del prodotto ma è in Should (A8, A11, E9) | Chiedere al PO se un **report minimo** e il check-in fanno parte del risultato minimo | SC-12 e SC-13 da Should a Must, se il PO conferma |
| 2 | Uno spazio prenotato e non usato resta bloccato (A6) | **Rilascio automatico** della prenotazione senza check-in dopo 15 minuti dall'inizio | Nuova US in F-05 |
| 3 | Il personale non vede il quadro completo (A7) | **Calendario delle prenotazioni** per giorno e per spazio, riservato al personale | Nuova US in F-04 |
| 4 | Product Goal, Sprint Goal, eventi e adattamenti mancanti (E1, E2, E7, E8) | Documento `docs/process.md` con Product Goal, due Sprint con il loro Goal, eventi e adattamenti | Nuovo task |
| 5 | Accordi con il PO non registrati (C4, C6, E5) | `docs/analysis/po-decisions.md`: data, domanda, decisione, impatto | Nuovo task |
| 6 | Decisioni ancora "proposte" (C8, B1, B3) | Dopo la review, un ADR per ogni scelta, comprese le **esclusioni** di cloud e telemetria | Nuovo task |
| 7 | Costi, valore e sviluppi futuri (F5, F6) | `docs/analysis/value-and-costs.md`, una pagina | Nuovo task |
| 8 | Misure di utilizzo non definite (A9, F3) | Formula del tasso di utilizzo scritta e verificata a mano su dati sintetici | Criterio in SC-13 |
| 9 | Portabilità non provata (B4) | Prova dell'interfaccia su due sistemi operativi e su uno smartphone | Criterio in SC-01.T4 |
| 10 | Requisiti impliciti I1–I5 | I1 e I5 come criteri dei task esistenti; I2 e I3 come domande al PO | Criteri e domande |

Il flusso dell'applicativo con i punti deboli è in [application-flow.md](application-flow.md); la
sicurezza e i test per ogni componente sono in [security-and-testing.md](../security-and-testing.md).

---

## Copertura dopo l'aggiornamento del backlog

Tutti i **24 requisiti parziali o mancanti** hanno ora un'issue o un documento che li copre. Le modifiche di
priorità sono **proposte**: valgono dopo l'accordo con il PO (SC-21.T2, #85).

| ID | Stato prima | Coperto da |
|---|---|---|
| A5 | Parziale | SC-15 (#16) portata a Should, con avviso solo dentro l'app |
| A6 | Mancante | **SC-17 (#68)** rilascio automatico senza check-in |
| A7 | Mancante | **SC-18 (#72)** calendario delle prenotazioni per il personale |
| A8 | Parziale | SC-12 (#13) e SC-13 (#14) proposte in **Must** |
| A9 | Parziale | SC-13.T3 (#97) misure di utilizzo definite e verificate |
| A11 | Parziale | SC-12, SC-13 in Must + validazione con il PO (#85) |
| B1 | Parziale | D4: **MongoDB per Spaces**, PostgreSQL per Bookings e Users ([data-model.md](../data-model.md)); ADR in #89 |
| B3 | Parziale | ADR sulle esclusioni (#89) e analisi dei costi (#90) |
| B4 | Parziale | SC-23.T2 (#93) prova su due sistemi operativi e smartphone |
| C4 | Parziale | SC-21.T2 (#85) perimetro validato e registrato |
| C5 | Parziale | [data-model.md](../data-model.md): schemi e regole di dominio R1–R12 |
| C6 | Parziale | Registro delle decisioni del PO (#85), tag e release per incremento (#86) |
| C8 | Mancante | SC-22.T1 (#89) un ADR per ogni decisione |
| D2 | Parziale | [security-and-testing.md](../security-and-testing.md) (log e AI), SC-01.T7 (#94) |
| E1 | Mancante | SC-21.T1 (#84) Product Goal |
| E2 | Mancante | SC-21.T1 (#84) Sprint Goal per i due Sprint |
| E5 | Parziale | SC-21.T2 (#85) DoD e criteri concordati con il PO |
| E7 | Mancante | SC-21.T1 (#84) eventi; SC-21.T4 (#87) retrospettiva registrata |
| E8 | Mancante | SC-21.T1 (#84) adattamenti dichiarati |
| E9 | Parziale | Priorità marcate "proposta, da confermare con il PO"; decisione in #85 |
| F3 | Parziale | SC-13.T3 (#97) |
| F4 | Parziale | Criterio nella demo (#51) e in SC-23 (#91) |
| F5 | Mancante | SC-22.T2 (#90) valore e costi |
| F6 | Mancante | SC-22.T2 (#90) scenario di scala futura |

| Requisito implicito | Coperto da |
|---|---|
| I1 Orari coerenti | SC-06.T4 (#95) e criterio in SC-06 (#7) |
| I2 Regole sulle fasce | Regole R3–R4 in [data-model.md](../data-model.md#regole-di-dominio-in-un-solo-posto); valori da chiedere al PO |
| I3 Nessuno blocca il tempo di un altro | Limite di 5 prenotazioni di gruppo e uscita dal gruppo (#58, #60, #61) |
| I4 Storico per il personale | Date di creazione, annullamento e check-in, `added_by` ([data-model.md](../data-model.md#bookings-postgresql)); calendario #72 |
| I5 Messaggi comprensibili | `ProblemDetails` ([security-and-testing.md](../security-and-testing.md)) e messaggi nel frontend (#65) |

### Carico dopo l'aggiornamento

| Priorità | User story | Story point |
|---|---|---|
| Must | 16 | 56 |
| Should | 7 | 26 |

56 punti Must in tre giornate sono **molti**. È il motivo principale per portare subito al PO le priorità
proposte (SC-21.T2, #85): se il PO non conferma SC-12 e SC-13 in Must, il carico Must scende a 48.

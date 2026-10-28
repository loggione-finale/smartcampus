# Scaletta della presentazione e della demo

> **Stato: bozza aggiornata il 28/10 (Sprint 2 e chi ha fatto cosa)** (issue #92). Durata prevista 12 minuti più la demo. Le parti marcate **[da completare]** le
> scrive e le verifica chi è responsabile di quell'area: si presenta solo ciò che è stato provato davvero.
> Le slide chiedono che ogni componente sappia spiegare il proprio contributo: ognuno rilegge e fa proprio il suo pezzo.

Le slide corrispondenti sono in [presentation-slides.pptx](presentation-slides.pptx), con le note per chi parla in ogni slide.

## Scaletta

| # | Minuti | Tema | Cosa si dice | Chi |
|---|---|---|---|---|
| 1 | 1 | Il bisogno | Trovare e prenotare aule, laboratori e sale studio senza conflitti, anche per gruppi, e capire come vengono usati | **[da assegnare]** |
| 2 | 1 | Perimetro concordato | Cosa abbiamo concordato con il PO e cosa è fuori ([po-decisions.md](analysis/po-decisions.md)). Le decisioni ancora "in attesa" vanno chiuse prima di presentare | asprenolapo **[da completare]** |
| 3 | 2 | Architettura e scelte | Tre servizi (Spaces su MongoDB, Bookings e Users su PostgreSQL), REST sincrono, Angular. Perché: [architecture.md](architecture.md), scelte D1-D10 con pro e contro | **[da assegnare]** |
| 4 | 1 | Sprint 2: cosa abbiamo consegnato | Gruppi e inviti, commento finale, area admin, accesso e catalogo, regole di prenotazione, piattaforma; 76 PR mergiate in due giorni | **[da assegnare]** |
| 5 | 4 | Demo | Percorso scritto qui sotto, su dati sintetici | tutti, una parte ciascuno |
| 6 | 2 | Prove eseguite | Cosa abbiamo provato davvero e con che esito; sotto "Prove da citare" | Rhaastrake, FranDeSa456 |
| 7 | 1 | Limiti noti | Sotto "Limiti da dichiarare" | **[da assegnare]** |
| 8 | 1 | Costi e sviluppi futuri | Stime di `docs/analysis/value-and-costs.md`: zero licenze, demo in locale, esercizio stimato in 2 vCPU e 2-4 GB; sviluppi: email reali, SSO, più sedi, sensori | **[da assegnare]** |

## Percorso della demo (da provare a voce almeno una volta con tutto il gruppo)

Il percorso completo, con 15 passi, i dati e l'esito atteso di ognuno, è nel documento `docs/demo-path.md` (#91). Il 28/10
è stato eseguito sull'API con gli esiti attesi (`docs/evidence/2026-10-28-demo-rehearsal-api.md`, #51). Le slide ne
mostrano 8 blocchi:

| Blocco | Cosa si mostra | Dati |
|---|---|---|
| 1 | Prenotare Aula 101 in una fascia libera: conferma con codice leggibile | nuova fascia |
| 2 | Fascia sovrapposta: messaggio di conflitto (`409`) | prenotazione del passo 1 |
| 3 | Fascia adiacente: accettata | prenotazione del passo 1 |
| 4 | Annullare (solo l'organizzatore) e riprenotare la fascia con un altro utente | passo 1 |
| 5 | Aula mista piena: la persona successiva riceve "posti esauriti" | Aula 204, `SC-SEED-0012` e `0013` |
| 6 | Commento finale mancante: nuova prenotazione bloccata, poi sbloccata | Elena Greco, `SC-SEED-0011` |
| 7 | Gruppo con inviti: l'invitato conferma, un altro utente no | Lab 2, Sara Conti |
| 8 | Area admin: la dashboard solo per l'admin; con Spaces fermo la prenotazione risponde `503` | admin, Spaces |

Interfaccia provata su due sistemi operativi e su uno smartphone, con esito registrato: **[da fare]**, tabella in
`docs/demo-path.md`.

## Prove da citare (solo quelle eseguite)

| Prova | Esito | Dove |
|---|---|---|
| Concorrenza sul database: 16 richieste simultanee, una sola accettata sull'aula dedicata, capienza mai superata sull'aula mista | Superata il 27/10/2026 | [booking-concurrency.md](evidence/booking-concurrency.md) |
| Concorrenza via HTTP sull'aula mista: 30 richieste simultanee su 5 posti. In locale 5 accettate e 25 rifiutate (30 esecuzioni su 30); senza il lock il test fallisce (30 accettate, verifica manuale) | Superata in locale; un fallimento in CI (11 accettate su 5 posti), non riprodotto in 80 esecuzioni, #142 chiusa con un'ipotesi | test `MixedSpaceSeatsTests` (PR #133, #151) |
| Vincoli di esclusione su spazi e persone, anche a livello di database | Superata | test in `tests/SmartCampus.Bookings.Tests` (PR #136, #137) |
| Orari in UTC e in Europe/Rome nelle notti del cambio ora legale | Superata | test `TimeZoneTests` (PR #135) |
| Seed idempotente: due avvii consecutivi non duplicano i dati | Provata a mano e con test | PR #143 |
| Percorso della demo sull'API: 28 chiamate su Spaces, Users e Bookings in `Development` con PostgreSQL e MongoDB veri, tutte come atteso | Superata il 28/10/2026 (non sull'interfaccia) | `docs/evidence/2026-10-28-demo-rehearsal-api.md` (#51) |
| Avvio da zero la mattina della consegna | **[da fare giovedì]** | |

## Limiti da dichiarare

- **I dati sintetici verificano il prototipo ma non dimostrano benefici reali per il campus.** Nessun dato personale reale.
- La prova di concorrenza riguarda un solo servizio e un solo database PostgreSQL; con più istanze andrebbe ripetuta.
- Aula mista: in CI un'esecuzione del test di concorrenza ha accettato 11 prenotazioni su 5 posti (#142). Non l'abbiamo riprodotto in 80 esecuzioni e la causa probabile (una corsa tra test) non è dimostrata; il numero esatto di richieste accettate può inoltre variare tra esecuzioni.
- Il percorso della demo è provato solo sull'API, non sull'interfaccia né su due sistemi operativi e su uno smartphone.
- Con PostgreSQL fermo Bookings risponde dopo circa un minuto (nuovi tentativi di EF Core) con un `500` senza dettagli, e `/health` impiega lo stesso tempo: da correggere (#66).
- Le email di invito e di scadenza finiscono in una coda nel database; i file `.eml` in una cartella locale arrivano con #106 **[da confermare: Notifications]**.
- Il token JWT non contiene il nome dell'utente: nei dati delle prenotazioni si salva l'email.
- Le prenotazioni già terminate non si possono annullare (scelta del gruppo, da confermare con il PO).
- Le decisioni del PO ancora "in attesa" in [po-decisions.md](analysis/po-decisions.md) **[da chiudere]**.

## Chi spiega cosa

Il numero di PR è quello delle PR mergiate dal 26/10 al 28/10 (fonte: GitHub).

| Persona | PR | Contributo da saper spiegare |
|---|---|---|
| asprenolapo | 11 | Piattaforma: CI, MongoDB 7.0, ADR, prove di guasto, flusso agentico e controllo PR; incontro con il PO |
| AldAndrew0 | 10 | Spaces: catalogo e ricerca, indisponibilità, strumenti, pagine del catalogo |
| Rhaastrake | 16 | Bookings: schema, creazione, annullamento, vincoli, aule miste, fasce orarie, seed, demo |
| FranDeSa456 | 9 | Gruppi, inviti con scadenza, email `.eml`, mie prenotazioni, test di concorrenza |
| fedeericodl | 13 | Users e accesso, commento finale, disponibilità, dati sintetici |
| antoStr | 11 | Area admin: dashboard, avvisi, annullamento, statistiche, report, metriche d'uso |
| Leo-prova | 6 | Frontend: login, registrazione, form di prenotazione, mie prenotazioni |

## Prima di presentare

- [ ] Percorso della demo provato a voce da tutto il gruppo almeno una volta
- [ ] Avvio da zero provato la mattina di giovedì 29/10
- [ ] Interfaccia provata su due sistemi operativi e su uno smartphone, esito registrato
- [ ] Tutti i **[da completare]** di questa pagina risolti
- [ ] Ognuno sa spiegare il proprio contributo e le evidenze condivise

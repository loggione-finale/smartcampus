# Mappa del lavoro: chi ha fatto cosa e perché

> Fonte: le PR fuse su `main` dal 26/10 al 29/10 alle 10:30 (GitHub), con le issue e le decisioni che le hanno
> motivate. Serve alla presentazione: ognuno rilegge la propria sezione e sa spiegare il proprio contributo e il suo
> perché (slide 69, "evitare i silos"; [Chi spiega cosa](presentation-outline.md#chi-spiega-cosa)).
> **Da aggiornare dopo il freeze delle 11:15** con le PR dell'iper-sprint.

## In sintesi

103 PR fuse in quattro giorni. Le review contano le PR di altri approvate o rimandate con modifiche.

| Persona | Area | PR fuse | Review su PR di altri |
|---|---|---|---|
| asprenolapo | Coordinamento (facilitatore Scrum), piattaforma, CI, processo, prove di sistema | 22 | 21 |
| Rhaastrake | Bookings: prenotazioni, vincoli, fasce; demo e presentazione | 21 | 33 |
| FranDeSa456 | Gruppi e inviti, email, concorrenza, sicurezza di Bookings | 15 | 17 |
| antoStr | Area admin, dalle API alle pagine | 14 | 34 |
| fedeericodl | Users e accesso, disponibilità, commento finale, dati sintetici | 14 | 25 |
| AldAndrew0 | Spaces: catalogo, disponibilità, strumenti | 10 | 5 |
| Leo-prova | Frontend: struttura, registrazione, login, prenotazione | 7 | 7 |

Il numero di righe cambiate non è una misura del lavoro: comprende file generati, come `package-lock.json`.

## Il filo dei quattro giorni

| Giorno | Cosa si è fatto | Perché |
|---|---|---|
| lun 26 | Analisi, brainstorming, perimetro, scheletro della soluzione, database con Docker | Capire il bisogno e partire tutti dalla stessa base (decisioni 1–17) |
| mar 27 | Sprint 1: Spaces, Bookings con i vincoli nel database, Users con JWT, gruppi e inviti, CI | Sprint Goal 1: prenotare senza conflitti, da soli o in gruppo |
| mer 28 | Sprint 2: commento finale, area admin, pagine del frontend, email, prove di guasto e di sistema, agenti, Review con il PO | Sprint Goal 2 (decisione 33) e richiesta del professore sul flusso agentico (decisione 28) |
| gio 29 | Iper-sprint: richieste del PO del 28/10 ([piano](hyper-sprint-2026-10-29.md)) | Coerenza con le decisioni 29–32 prima della consegna |

## Per persona

### asprenolapo: coordinamento e piattaforma

| Cosa | PR | Perché |
|---|---|---|
| Analisi, architettura a microservizi, brainstorming e perimetro | #1, #52, #101 | Capire il bisogno e dividere il lavoro per aree, con un owner per area |
| Incontri con il PO e registro delle decisioni | #99, #140, #229, #237 | Il PO valida: ogni accordo deve essere tracciabile (slide 57) |
| Scheletro della soluzione, database con Docker Compose, CODEOWNERS | #120, #122, #121 | Una base comune e review per area fin dal primo giorno |
| CI su ogni PR e "Controllo PR" obbligatorio | #123, #172, #187 | Nessuna fusione senza build, test e regole minime |
| MongoDB 7.0 al posto della 8 | #152 | La CI falliva con il kernel Linux nuovo |
| Flusso agentico controllato: 7 ruoli, agenti Copilot con permessi scritti | #172, #187 | Richiesta del professore (decisione 28), senza segreti né spese |
| Product Goal, Sprint Goal, eventi, ADR | #174, #188 | Dichiarare come il gruppo ha adattato Scrum (slide 32) e perché ha scelto così |
| Prove di sistema: avvio da zero, isolamento dei database, prove di guasto | #126, #189, #190, #225 | Dimostrare ciò che si afferma, con l'esito atteso scritto prima |
| Avvio con un comando e chiave JWT nel README | #226 | Criterio di #2: il sistema si avvia con un comando |
| Test indipendente dall'ora del giorno | #209 | La CI falliva solo in certe ore |

### Rhaastrake: prenotazioni senza conflitti

| Cosa | PR | Perché |
|---|---|---|
| Schema di Bookings, migrazione, controllo di salute | #124 | Il cuore del prodotto: le prenotazioni |
| Creazione con verifica su Spaces, annullamento | #129, #130 | Sprint Goal 1 |
| Vincoli di esclusione su spazi e persone, posti nelle aule miste con lock | #133, #136, #137, #151 | "Nessuno spazio e nessuna persona prenotati due volte", garantito dal database |
| Durata, fasce orarie, una prenotazione per fascia e per giorno | #153 | Decisione 24 del PO |
| Orari in UTC e fuso di Roma nel cambio dell'ora legale | #135 | Evitare errori di un'ora nelle date di fine marzo e ottobre |
| Seed dei dati sintetici, una sola copia | #143, #159 | Demo e test con gli stessi dati |
| Annullamento sicuro con richieste simultanee, email dal claim giusto, `503` con PostgreSQL fermo | #215, #216, #218 | Bug trovati nelle review e nelle prove di guasto |
| Percorso della demo, prove sull'API, slide e scaletta, prova di tutto il sistema | #145, #192, #193, #198, #220, #236 | Presentare solo ciò che è stato provato |

### FranDeSa456: gruppi, inviti ed email

| Cosa | PR | Perché |
|---|---|---|
| Prova di concorrenza sulle prenotazioni e sulle persone | #128, #134, #196 | Dimostrare il vincolo con richieste simultanee vere |
| API delle mie prenotazioni | #146 | L'utente vede le proprie prenotazioni |
| Prenotazione di gruppo con le email dei partecipanti, conferma degli inviti, scadenza dopo due ore | #176, #175, #183 | Sprint Goal 2: i gruppi confermano entro due ore |
| Email come file `.eml` | #200 | Mostrare le email senza un server di posta e senza dati reali |
| Massimo 4 persone per gruppo nelle aule miste | #219 | Regola delle aule miste |
| Pagina di conferma dell'invito e link di registrazione corretto | #227, #235 | Criterio di #103: "registrazione se non registrato" |
| L'admin non prenota; test di sicurezza di Bookings | #232 | Regola del brainstorming (punto 9) e test di #96 |
| Data e ora nelle mail | #250 | Richiesta R1 del PO |
| Istruzioni sul numero dei commit | #141 | Correzione del processo |

### antoStr: area admin

| Cosa | PR | Perché |
|---|---|---|
| Dashboard: API e pagina | #178, #177, #194 | L'admin vede aule prenotate e libere (Sprint Goal 2) |
| Avvisi: API, avviso di ogni nuova prenotazione, pagina | #179, #186 | L'admin gestisce le eccezioni (brainstorming, punto 6) |
| Statistiche dai commenti finali, definizioni delle misure, report | #182, #184, #185 | Rendere comprensibile l'utilizzo degli spazi (slide 9) |
| Gestione degli spazi, annullamento admin con email | #180, #181 | Decisioni 9 e 10 del PO |
| Rotte admin protette, annullamento dal web | #206, #221 | Bug trovato in review; story #10 |
| Retrospettiva dello Sprint 1, due usi registrati dell'agente code-reviewer | #222 | Evento Scrum e prova del flusso agentico (#171) |
| Timeout di MongoDB a 3 secondi | #231 | Differenza delle prove di guasto (#224) |

### fedeericodl: accesso, disponibilità, commento finale

| Cosa | PR | Perché |
|---|---|---|
| Libreria `SmartCampus.Auth` con JWT e policy Admin | #125 | Una sola regola di accesso per i tre servizi |
| Registrazione e login con email o matricola | #132, #144, #163 | Chi prenota è un utente riconosciuto |
| Disponibilità di un'aula e spazi liberi in una fascia | #150, #161 | Trovare uno spazio libero prima di prenotare |
| Dati sintetici in `data/synthetic` | #156 | Una sola sorgente per demo e test |
| Commento finale: backend, pagina, blocco delle nuove prenotazioni | #167, #197, #191, #205 | Capire se lo spazio è stato usato davvero (brainstorming, slide 9) |
| Analisi del valore e dei costi, numeri dichiarati come stime | #199, #208 | Le slide chiedono valore e costi, senza inventare misure |
| Path ripulito nei log | #230 | Sicurezza: niente log forging |

### AldAndrew0: spazi e strumenti

| Cosa | PR | Perché |
|---|---|---|
| Modello degli spazi su MongoDB, indici, controllo di salute | #127 | Catalogo flessibile, con attributi diversi per tipo di spazio |
| Dati sintetici degli spazi e seed automatico | #147 | Demo con spazi realistici |
| Catalogo con filtri e dettaglio, pagina del catalogo | #162, #166 | Trovare lo spazio giusto |
| Spazio prenotabile, indisponibilità, disponibilità del giorno | #157, #168, #169 | Bookings chiede a Spaces se si può prenotare |
| Gestione spazi per l'admin con controllo del ruolo | #165 | Solo l'admin modifica gli spazi |
| Catalogo degli strumenti e richieste di strumenti con avviso all'admin | #170, #173 | Richiesta di strumenti per i gruppi (story #108) |

### Leo-prova: frontend

| Cosa | PR | Perché |
|---|---|---|
| Struttura del frontend con Angular Material | #131 | Una base grafica comune per tutte le pagine |
| Pagina di registrazione | #149 | Story #24 |
| Form di prenotazione, con strumenti solo per i gruppi | #164 | Prenotare dall'interfaccia (story #33); copre anche #111 |
| Login, sessione JWT, interceptor e guard | #195 | Accesso dall'interfaccia (#82) |
| Controllo delle email dei partecipanti rispetto ai posti | #210 | Errore chiaro prima di inviare (#63) |
| Pagina "Le mie prenotazioni" | #217 | Story #38 |
| Messaggio unico per i servizi non disponibili | #223 | Prove di guasto (#65) |

## Lavoro di tutti

- **Review incrociate**: ogni area ha un owner e un revisore fisso (`.github/CODEOWNERS`); nessuna fusione senza la
  review di un altro membro.
- **Prove**: concorrenza, guasti, isolamento, percorso della demo, avvio da zero, in [evidence/](evidence/README.md).
- **Review con il PO** del 28/10 e **iper-sprint** del 29/10: tutti e sette disponibili fino al freeze.

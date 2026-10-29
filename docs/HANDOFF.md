# SmartCampus: guida al progetto (hand-off)

> Il punto di partenza per chi prende in mano il progetto: valutatori, nuovi membri, noi stessi fra qualche mese.
> Riassume e rimanda ai documenti di dettaglio, senza ripeterli. Aggiornata il 29/10/2026 sul `main` dopo la release `v0.2`.

## 1. Il prodotto in breve

**Il problema.** Nel campus le informazioni sulla disponibilità degli spazi non sono le stesse per tutti: due gruppi si
presentano nella stessa aula, gli annullamenti si perdono e non si sa quanto gli spazi vengano usati davvero
([analisi](analysis/project-analysis.md), [brainstorming](analysis/brainstorming-2026-10-26.md)).

**Product Goal** (approvato dal PO, decisione 16): studenti e personale trovano e prenotano gli spazi del campus, anche per
un gruppo, con una conferma affidabile e senza conflitti; il personale capisce come gli spazi vengono usati, distinguendo
prenotazioni, uso dichiarato e dati mancanti.

**Due ruoli.**
- **Utente**: cerca, prenota (da solo o in gruppo), conferma gli inviti, annulla, lascia il commento finale.
- **Admin**: vede aule prenotate e libere, avvisi ed eccezioni, statistiche; gestisce gli spazi; annulla con motivo.
  **Non prenota.**

## 2. Stato alla consegna

| Area | Cosa funziona | Dove si vede |
|---|---|---|
| Spazi | Catalogo con filtri, dettaglio, disponibilità del giorno, spazi liberi in una fascia, indisponibilità, strumenti | Pagina Spazi; `GET /spaces` |
| Prenotazioni | Creazione con tutte le regole della sezione 4, annullamento, le mie prenotazioni | Form "Prenota"; "Le mie prenotazioni" |
| Gruppi | Invitati per email, conferma entro 2 ore, scadenza automatica, mail `.eml` | Pagina inviti; cartella `mail-outbox` |
| Commento finale | Obbligatorio dopo la fine; blocca le nuove prenotazioni finché manca | Pagina del commento finale |
| Admin | Dashboard, avvisi, report, statistiche, gestione spazi, annullamento con email | Area admin |
| Accesso | Registrazione, login con email o matricola, token JWT di 8 ore | Login e registrazione |
| Piattaforma | Avvio con un comando, CI su ogni PR, prove di sistema | `scripts/dev-up`; Actions |

Rilasci: [`v0.1`](https://github.com/loggione-finale/smartcampus/releases/tag/v0.1) (Sprint 1) e [`v0.2`](https://github.com/loggione-finale/smartcampus/releases/tag/v0.2) (Sprint 2 e
iper-sprint). Le PR fuse dopo la `v0.2` (#251, #269, #271–#276: interfaccia uniforme e correzioni) **non sono ancora in una release**: la `v0.2.1` si pubblica dopo le prove della consegna. Cosa è entrato con l'iper-sprint del 29/10: [piano](hyper-sprint-2026-10-29.md) e note
della `v0.2`.

## 3. Architettura in una pagina

```
Browser ── Angular (4200, proxy /api/...) ──┬── Spaces   (5101) ── MongoDB 7.0
                                            ├── Bookings (5102) ── PostgreSQL 17 (bookings)
                                            └── Users    (5103) ── PostgreSQL 17 (users)
Bookings ──REST──> Spaces (spazio prenotabile?)   Bookings ──REST──> Users (chi è registrato?)
```

- **Tre microservizi .NET 10** con confini per dominio, un database per servizio e un utente di database con privilegi
  minimi per servizio ([ADR 0001–0004](decisions/README.md), [architettura](architecture.md)).
- **Bookings** garantisce le regole nel database: vincoli di esclusione su spazio e persona, lock sull'aula per i posti
  delle aule miste, indice unico per fascia ([ADR 0005](decisions/0005-vincoli-di-esclusione.md),
  [modello dati](data-model.md)).
- **Chiamate sincrone REST** con timeout di 3 s: se Spaces o Users non rispondono, Bookings dà `503` con un messaggio
  leggibile ([ADR 0003](decisions/0003-rest-sincrono.md)).
- **Accesso**: Users firma il JWT, la libreria condivisa `src/Shared/SmartCampus.Auth` lo valida nei tre servizi con la
  stessa chiave (`JWT_SECRET`); policy `Admin` per le rotte riservate ([ADR 0008](decisions/0008-utenti-e-accesso.md)).
- **Email**: outbox nel database di Bookings, spedita come file `.eml` in
  `src/Services/Bookings/SmartCampus.Bookings.Api/mail-outbox/` (nessun server di posta).
- **Dati sintetici** in `data/synthetic/`, caricati da soli all'avvio in `Development`
  ([dati sintetici](../data/synthetic/README.md)).

## 4. Regole del dominio

Decise con il PO: [registro delle decisioni](analysis/po-decisions.md).

| Regola | Decisione |
|---|---|
| Durata da 30 minuti a 4 ore, a passi di 15 minuti | PO, 27/10 |
| Fasce, ora di Roma: mattina 08–14, pomeriggio 14–19, sera 19–23; niente prenotazioni a cavallo | 24, 29, 30 |
| Una prenotazione per fascia e per giorno per chi prenota, **e anche per i partecipanti** | 24, 31 (H8, #260) |
| Al massimo **15 giorni** di anticipo; l'inizio deve essere nel futuro | 32 (H1 #257, H2 #263) |
| Nessuno spazio e nessuna persona prenotati due volte, anche con richieste simultanee | Sprint Goal 1 |
| Singoli solo nelle aule miste; gruppi nelle dedicate o nelle miste fino a 4 persone; posti contati | 20 (#211) |
| Gruppi: invito per email; chi non conferma entro 2 ore è escluso, il posto si libera, organizzatore ed escluso ricevono una mail | 17, story #103 |
| Commento finale obbligatorio dopo la fine; blocca le nuove prenotazioni | Story SC-29 |
| Solo l'organizzatore annulla (`403` agli altri); l'admin annulla con motivo e mail | 10, #96 |
| L'admin non prenota (`403`) | Brainstorming, punto 9 |
| Orari salvati in UTC; le API accettano solo orari con `Z` | Scelta tecnica, [modello dati](data-model.md) |

## 5. Avviare, provare, testare

- **Avvio**: [README](../README.md), passi 1–3: `.env` dalle 5 password, poi `scripts/dev-up.ps1` (Windows) o
  `scripts/dev-up.sh` (Linux, provato su Fedora). Spegnimento: `scripts/dev-down`.
- **Account demo** e password: [data/synthetic/README.md](../data/synthetic/README.md).
- **Percorso della demo** in 15 passi, con l'esito atteso di ognuno: [demo-path.md](demo-path.md).
- **Test**: `dotnet test SmartCampus.slnx` e `npm test` in `src/web`.
  - I test d'integrazione usano i database veri. Servono le variabili `ConnectionStrings__BookingsTest`,
    `ConnectionStrings__UsersTest`, `ConnectionStrings__SpacesTest` e, per i test di concorrenza di Bookings,
    `BOOKINGS_TEST_CONNECTION_STRING`.
  - Senza queste variabili molti test falliscono per l'autenticazione: non è un difetto del codice.
  - **Non esportare `JWT_SECRET`** nel terminale dei test: i servizi la preferiscono alla chiave dei test e i test
    d'integrazione rispondono `401`.
- **Problemi frequenti**: tabella nel [README](../README.md#problemi-frequenti).

## 6. Mappa del repository

| Percorso | Cosa c'è |
|---|---|
| `src/Services/{Spaces,Bookings,Users}` | I tre servizi; ogni area in `Features/` |
| `src/Shared/SmartCampus.Auth` | Validazione JWT e policy condivise |
| `src/web` | Frontend Angular + Material |
| `tests/` | Test di ogni servizio e test di coerenza dei dati sintetici |
| `data/synthetic/` | Utenti, spazi e prenotazioni inventati |
| `infra/`, `docker-compose.yml` | Database e script di inizializzazione |
| `scripts/` | `dev-up` e `dev-down` |
| `.github/` | CI, controllo fisso delle PR, agenti Copilot, CODEOWNERS, template |
| `docs/analysis/` | Bisogno, requisiti, brainstorming, incontri e decisioni del PO, valore e costi, metriche d'uso |
| `docs/decisions/` | ADR 0001–0011 |
| `docs/evidence/` | Prove eseguite, con esito atteso e osservato |
| `docs/agents/` | Flusso agentico: ruoli, permessi, prompt |
| `docs/process.md` | Product Goal, Sprint, eventi, adattamenti di Scrum, retrospettiva |

## 7. Come lavoriamo

- **Scrum adattato** a quattro giorni: due Sprint, più un iper-sprint dopo la Review del 28/10. C'è un facilitatore
  (asprenolapo) e il PO è esterno al team ([process.md](process.md)).
- **Board** "Loggione Roadmap" con Status, Priority, Day e Story Point. Ogni lavoro è una issue.
- **Branch e PR**:
  - un branch `feature|fix|docs|test|chore/<issue>-descrizione` per issue;
  - una PR con `Closes #N` o `Refs #N` e la sezione "Come l'ho verificato";
  - squash merge fatto dall'autore ([CONTRIBUTING](../CONTRIBUTING.md)).
- **Protezione di `main`**: una review di un altro membro, e i controlli Backend (.NET), Frontend (Angular) e Controllo
  PR obbligatori. Ogni area ha owner e revisore in `.github/CODEOWNERS`.
- **Definition of Done**: [definition-of-done.md](definition-of-done.md).
- **Flusso agentico controllato** (decisione 28): un controllo fisso delle PR in CI e 6 agenti GitHub Copilot con permessi
  scritti. Niente orchestratore, segreti o spese ([docs/agents](agents/README.md)). Usi registrati in
  `docs/evidence/agentic/`.

## 8. Prove eseguite

Elenco completo: [evidence/README.md](evidence/README.md). Le principali:

- **concorrenza**: una sola prenotazione accettata su richieste simultanee, sia sugli spazi sia sulle persone;
- **prove di guasto** di ogni servizio e database, scenario 5 ripetuto dopo la correzione;
- **isolamento e persistenza** dei database;
- **avvio da zero** su Windows e Linux;
- **percorso della demo** sull'API e percorso dell'invito nel browser;
- **sicurezza** di Bookings.

## 9. Limiti noti e debito tecnico

| Limite | Perché resta | Dove |
|---|---|---|
| Dati solo sintetici: provano il funzionamento, non i benefici per il campus | Vincolo del progetto | [value-and-costs.md](analysis/value-and-costs.md) |
| Con PostgreSQL fermo il `503` arriva in circa 7 s invece di 5 | Timeout di 3 s più il nuovo tentativo; non cambiato dopo il freeze | [prove di guasto](evidence/2026-10-28-failure-tests.md) |
| Il limite per fascia dei partecipanti è controllato nell'applicazione, non nel database | Poco tempo; con richieste simultanee può essere superato | H8 #245 |
| `GET /users` è senza login: dice se un'email è registrata | Bookings chiama Users senza token; i dati personali sono stati tolti dalla risposta (H12, #267) | Sviluppo futuro: autenticazione tra servizi |
| Due registrazioni simultanee con la stessa email danno `500` invece di `409` | Caso raro, trovato nell'audit del 29/10 | Users, `RegisterUserHandler` |
| Outbox delle email e richieste di strumenti senza lock tra istanze | Oggi c'è una sola istanza | Bookings |
| Strumento "Altro" con nota e risposta dell'admin all'utente (R8) | Fuori tempo | Sviluppo futuro |
| La dashboard admin mostra solo le aule con almeno una prenotazione; nessun avviso né gestione delle chiusure per manutenzione | Controllo dei requisiti admin (H10, #243) | Le aule libere in una fascia si vedono dalla ricerca del catalogo |
| Avvio non provato su macOS; agente "implementer" mai usato | Tempo | [avvio da zero](evidence/2026-10-27-startup-from-scratch.md), [agents](agents/implementer.md) |

## 10. Prossimi passi consigliati

1. Autenticazione tra i servizi, così `GET /users` può richiedere il login.
2. Vincolo dei partecipanti per fascia nel database (come quello sulle persone).
3. Strumento "Altro" con nota e risposta dell'admin all'utente (R8).
4. Prova a voce della presentazione con tutto il gruppo, se non fatta prima della consegna (#51).
5. `409` sulla registrazione doppia; `503` con PostgreSQL fermo entro 5 s.
6. Misure reali d'uso con il campus, per verificare il valore dichiarato come stima.

## 11. A chi chiedere

| Area | Owner | Revisore |
|---|---|---|
| Coordinamento, piattaforma, CI, documentazione di processo | asprenolapo | Rhaastrake |
| Bookings: prenotazioni, vincoli, fasce | Rhaastrake | FranDeSa456 |
| Gruppi, inviti, email, mie prenotazioni | FranDeSa456 | Rhaastrake |
| Area admin (API e pagine) | antoStr | AldAndrew0 |
| Users, accesso, commento finale, disponibilità | fedeericodl | Leo-prova |
| Spaces e strumenti | AldAndrew0 | antoStr |
| Frontend | Leo-prova | antoStr |

Chi ha fatto cosa e perché, PR per PR: [team-work-map.md](team-work-map.md).

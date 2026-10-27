# Scaletta della presentazione e della demo

> **Stato: bozza** (issue #92). Durata prevista 12 minuti più la demo. Le parti marcate **[da completare]** le
> scrive e le verifica chi è responsabile di quell'area: si presenta solo ciò che è stato provato davvero.
> Le slide chiedono che ogni componente sappia spiegare il proprio contributo: ognuno rilegge e fa proprio il suo pezzo.

## Scaletta

| # | Minuti | Tema | Cosa si dice | Chi |
|---|---|---|---|---|
| 1 | 1 | Il bisogno | Trovare e prenotare aule, laboratori e sale studio senza conflitti, anche per gruppi, e capire come vengono usati | **[da assegnare]** |
| 2 | 1 | Perimetro concordato | Cosa abbiamo concordato con il PO e cosa è fuori ([po-decisions.md](analysis/po-decisions.md)). Le decisioni ancora "in attesa" vanno chiuse prima di presentare | asprenolapo **[da completare]** |
| 3 | 2 | Architettura e scelte | Tre servizi (Spaces su MongoDB, Bookings e Users su PostgreSQL), REST sincrono, Angular. Perché: [architecture.md](architecture.md), scelte D1-D10 con pro e contro | **[da assegnare]** |
| 4 | 4 | Demo | Percorso scritto qui sotto, su dati sintetici | tutti, una parte ciascuno |
| 5 | 2 | Prove eseguite | Cosa abbiamo provato davvero e con che esito; sotto "Prove da citare" | Rhaastrake, FranDeSa456 |
| 6 | 1 | Limiti noti | Sotto "Limiti da dichiarare" | **[da assegnare]** |
| 7 | 1 | Costi e sviluppi futuri | Costi di sviluppo e gestione **[da completare: FranDeSa456]**; cosa faremmo con più tempo | **[da assegnare]** |

## Percorso della demo (da provare a voce almeno una volta con tutto il gruppo)

Avvio da zero: `docker compose up -d`, `dotnet run` dei tre servizi, `ng serve`. Il servizio Bookings in `Development`
carica da solo 13 prenotazioni sintetiche ([bookings-seed.md](../data/synthetic/bookings-seed.md)).

| Passo | Cosa si mostra | Dati | Risultato atteso |
|---|---|---|---|
| 1 | Accesso come Giulia Rossi | seed | Accesso riuscito **[da completare: login]** |
| 2 | Prenotare Aula 101 in una fascia libera | nuova fascia | Conferma con codice di riferimento leggibile |
| 3 | Riprovare una fascia sovrapposta | `SC-SEED-0001` | Messaggio di conflitto (`409`) |
| 4 | Fascia adiacente alla precedente | `SC-SEED-0001` e `0002` | Accettata: nessun conflitto |
| 5 | Annullare la propria prenotazione e riprenotare la fascia con un altro utente | `SC-SEED-0003` e `0004` | La fascia torna libera |
| 6 | Aula mista quasi piena | Aula 204 e `SC-SEED-0012` | Una persona entra, la successiva riceve "posti esauriti" |
| 7 | Accesso come Elena Greco, che ha un commento finale mancante | `SC-SEED-0011` | Nuova prenotazione bloccata, con il codice da commentare |
| 8 | Gruppo con inviti | `SC-SEED-0005` | Inviti in tutti gli stati **[da completare: Invitations]** |
| 9 | Vista admin: avvisi ed eccezioni | `SC-SEED-0005`, `0010` | Richiesta di strumenti e problema segnalato **[da completare: Admin]** |

Interfaccia provata su due sistemi operativi e su uno smartphone, con esito registrato in
[docs/evidence](evidence/README.md) **[da fare]**.

## Prove da citare (solo quelle eseguite)

| Prova | Esito | Dove |
|---|---|---|
| Concorrenza sul database: 16 richieste simultanee, una sola accettata sull'aula dedicata, capienza mai superata sull'aula mista | Superata il 27/10/2026 | [booking-concurrency.md](evidence/booking-concurrency.md) |
| Concorrenza via HTTP sull'aula mista: 30 richieste simultanee su 5 posti. In locale 5 accettate e 25 rifiutate (30 esecuzioni su 30); senza il lock il test fallisce (30 accettate, verifica manuale) | Superata in locale; in CI un fallimento (11 accettate su 5 posti), indagine in #142 | test `MixedSpaceSeatsTests` (PR #133) |
| Vincoli di esclusione su spazi e persone, anche a livello di database | Superata | test in `tests/SmartCampus.Bookings.Tests` (PR #136, #137) |
| Orari in UTC e in Europe/Rome nelle notti del cambio ora legale | Superata | test `TimeZoneTests` (PR #135) |
| Seed idempotente: due avvii consecutivi non duplicano i dati | Provata a mano e con test | PR #143 |
| Avvio da zero la mattina della consegna | **[da fare giovedì]** | |

## Limiti da dichiarare

- **I dati sintetici verificano il prototipo ma non dimostrano benefici reali per il campus.** Nessun dato personale reale.
- La prova di concorrenza riguarda un solo servizio e un solo database PostgreSQL; con più istanze andrebbe ripetuta.
- Aula mista: in CI un'esecuzione del test di concorrenza ha accettato 11 prenotazioni su 5 posti (#142). Finché il difetto non è chiarito, la capienza con richieste simultanee non è dimostrata in modo affidabile; il numero esatto di richieste accettate può inoltre variare tra esecuzioni.
- Le email sono file `.eml` in una cartella locale, non messaggi reali **[da confermare: Notifications]**.
- Il token JWT non contiene il nome dell'utente: nei dati delle prenotazioni si salva l'email.
- Le prenotazioni già terminate non si possono annullare (scelta del gruppo, da confermare con il PO).
- Le decisioni del PO ancora "in attesa" in [po-decisions.md](analysis/po-decisions.md) **[da chiudere]**.

## Chi spiega cosa

| Persona | Contributo da saper spiegare |
|---|---|
| asprenolapo | Piattaforma: soluzione, Docker Compose, CI, verifica del token; incontro con il PO |
| AldAndrew0 | Spaces su MongoDB: catalogo, indisponibilità, strumenti |
| Rhaastrake | Bookings: schema, creazione, annullamento, vincoli, aule miste, orari UTC, seed dei dati sintetici |
| FranDeSa456 | Bookings: inviti, email, mie prenotazioni, test di concorrenza |
| fedeericodl | Users e accesso, commento finale, dati sintetici |
| antoStr | Admin: dashboard, avvisi, eccezioni; frontend dell'area admin |
| Leo-prova | Frontend utente: accesso, prenotazione, gestione errori |

## Prima di presentare

- [ ] Percorso della demo provato a voce da tutto il gruppo almeno una volta
- [ ] Avvio da zero provato la mattina di giovedì 29/10
- [ ] Interfaccia provata su due sistemi operativi e su uno smartphone, esito registrato
- [ ] Tutti i **[da completare]** di questa pagina risolti
- [ ] Ognuno sa spiegare il proprio contributo e le evidenze condivise

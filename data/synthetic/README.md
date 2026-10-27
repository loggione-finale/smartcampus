# Dati sintetici e riproducibili (SmartCampus)

Questo archivio contiene l'insieme completo dei **dati sintetici riproducibili** utilizzati dal progetto SmartCampus per lo sviluppo locale, l'esecuzione dei test e le dimostrazioni ([SC-02], [SC-02.T1]).

> [!IMPORTANT]
> **Garanzia di riservatezza e conformità:**
> - Tutti i dati presenti in questa cartella (`users.json`, `spaces.json`, `bookings.json`) sono **esclusivamente inventati**.
> - Nessun nome, cognome, matricola, numero di telefono o indirizzo email corrisponde a persone fisiche reali.
> - Tutti i domini email utilizzano il dominio riservato `@smartcampus.test`.
> - I dati sintetici permettono di verificare il corretto funzionamento del prototipo in modo deterministico e riproducibile, senza mai trattare dati personali reali.

---

## 1. Identificativi condivisi tra i microservizi

Poiché i tre microservizi (`Spaces`, `Bookings`, `Users`) hanno ciascuno il proprio database indipendente e non condividono chiavi esterne fisiche, gli identificativi UUID condivisi consentono la correlazione logica:

### Utenti (`users.json`)

| Nome | Identificativo UUID | Matricola | Email | Ruolo |
|---|---|---|---|---|
| **Admin Campus** | `b7e1c2d3-0000-4000-8000-000000000001` | `0000000001` | `admin@smartcampus.test` | `Admin` |
| **Giulia Rossi** | `b7e1c2d3-0000-4000-8000-000000000002` | `1000000001` | `giulia.rossi@smartcampus.test` | `User` (Studente) |
| **Marco Bianchi** | `b7e1c2d3-0000-4000-8000-000000000003` | `1000000002` | `marco.bianchi@smartcampus.test` | `User` (Studente) |
| **Sara Conti** | `b7e1c2d3-0000-4000-8000-000000000004` | `1000000003` | `sara.conti@smartcampus.test` | `User` (Studente) |
| **Luca Ferrari** | `b7e1c2d3-0000-4000-8000-000000000005` | `1000000004` | `luca.ferrari@smartcampus.test` | `User` (Studente) |
| **Elena Greco** | `b7e1c2d3-0000-4000-8000-000000000006` | `1000000005` | `elena.greco@smartcampus.test` | `User` (Studente) |

### Spazi del campus (`spaces.json`)

| Nome | Identificativo UUID | Tipo | Modalità | Edificio | Piano | Capienza |
|---|---|---|---|---|---|---|
| **Aula 101** | `3f2a9c1e-0000-4000-8000-000000000001` | `Classroom` | `Dedicated` | Edificio A | 1 | 30 |
| **Laboratorio Informatica 2** | `3f2a9c1e-0000-4000-8000-000000000002` | `Lab` | `Dedicated` | Edificio B | 1 | 24 |
| **Sala Studio Piano Terra** | `3f2a9c1e-0000-4000-8000-000000000003` | `StudyRoom` | `Mixed` | Edificio A | 0 | 12 |
| **Aula 204** | `3f2a9c1e-0000-4000-8000-000000000004` | `Classroom` | `Mixed` | Edificio A | 2 | 6 |
| **Aula Magna** | `3f2a9c1e-0000-4000-8000-000000000005` | `Classroom` | `Dedicated` | Edificio Centrale | 0 | 150 |
| **Aula Mista Seminari** | `3f2a9c1e-0000-4000-8000-000000000006` | `Classroom` | `Mixed` | Edificio A | 1 | 30 |
| **Laboratorio Multimediale** | `3f2a9c1e-0000-4000-8000-000000000007` | `Lab` | `Dedicated` | Edificio B | 2 | 20 |
| **Aula 205 (Manutenzione)** | `3f2a9c1e-0000-4000-8000-000000000008` | `Classroom` | `Dedicated` | Edificio A | 2 | 35 |

---

## 2. Credenziali demo documentate per l'accesso

Tutti gli account pre-caricati sono utilizzabili per accedere via frontend o via endpoint `/auth/login`:

| Email | Matricola | Password Demo | Ruolo | Scenario di test |
|---|---|---|---|---|
| `admin@smartcampus.test` | `0000000001` | `AdminPass123` | `Admin` | Accesso alle viste riservate admin, gestione segnalazioni e approvazione attrezzature. |
| `giulia.rossi@smartcampus.test` | `1000000001` | `Password123` | `User` | Organizzatrice di prenotazione singola, di gruppo e in aula mista. |
| `marco.bianchi@smartcampus.test` | `1000000002` | `Password123` | `User` | Utente con prenotazione adiacente e storico concluso con commento positivo. |
| `sara.conti@smartcampus.test` | `1000000003` | `Password123` | `User` | Utente con prenotazione annullata e storico concluso con problema segnalato. |
| `luca.ferrari@smartcampus.test` | `1000000004` | `Password123` | `User` | Organizzatore di gruppo che satura la capienza di Aula 204. |
| `elena.greco@smartcampus.test` | `1000000005` | `Password123` | `User` | **Blocco commento finale:** ha la prenotazione terminata `SC-SEED-0011` non commentata; non può creare prenotazioni finché non inserisce il commento. |

---

## 3. Casi limite coperti dal dataset sintetico

> [!NOTE]
> Tutti gli orari indicati negli scenari e nei dataset (es. 09:00–11:00) sono intesi in ora locale di Roma (`Europe/Rome`), coerentemente con la gestione dei fusi orari adottata nel servizio Bookings.

Il dataset include una serie di scenari complessi per validare le regole di business:

| Riferimento | Caso limite / Regola di dominio | Dettagli dello scenario |
|---|---|---|
| `SC-SEED-0001` & `SC-SEED-0002` | **Prenotazioni adiacenti** su aula dedicata | Due prenotazioni consecutive (09:00–11:00 e 11:00–13:00) su Aula 101: entrambe accettate senza falsi conflitti di range `[start, end)`. |
| `SC-SEED-0003` & `SC-SEED-0004` | **Annullamento e riassegnazione fascia** | Sara annulla la prenotazione (09:00–10:00); Luca prenota con successo la stessa fascia resa di nuovo disponibile. |
| `SC-SEED-0005` | **Inviti in tutti gli stati e strumenti** | Prenotazione di gruppo con inviti `Accepted`, `Invited`, `Left`, `Expired`, utente esterno invitato prima della registrazione e richiesta videoproiettore con alert admin. |
| `SC-SEED-0006`, `0007`, `0008` | **Aula mista con sovrapposizioni parziali** | Tre studenti prenotano singolarmente posti in Sala Studio in orari parzialmente sovrapposti, senza superare la capienza di 12 posti. |
| `SC-SEED-0009` | **Storico e commento regolare** | Prenotazione passata completata con esito positivo e commento regolare senza anomalie. |
| `SC-SEED-0010` | **Segnalazione guasto e alert admin** | Prenotazione passata con commento che segnala guasto ("postazioni non accese") e genera un alert automatico per l'amministratore. |
| `SC-SEED-0011` | **Blocco per commento obbligatorio mancante** | Prenotazione passata priva di commento: impedisce a Elena Greco di creare qualsiasi nuova prenotazione (restituisce 409 con codice da commentare). |
| `SC-SEED-0012` & `SC-SEED-0013` | **Saturazione esatta capienza aula mista** | Aula 204 (capienza 6 posti): gruppo da 5 persone + prenotazione singola di 1 posto = 6/6 posti occupati. Eventuali ulteriori richieste ricevono 409 "Not enough seats". |
| `3f2a9c1e-...0008` | **Indisponibilità e manutenzione spazio** | Aula 205 contrassegnata con manutenzione impianto elettrico creata dall'Admin: il sistema blocca le prenotazioni per quella fascia. |

---

## 4. Modalità di caricamento all'avvio (Seed)

In ambiente `Development`, ciascun microservizio carica automaticamente la propria porzione di dati all'avvio se il rispettivo database è vuoto:

- **Bookings (`SmartCampus.Bookings.Api`)**: `BookingsSeeder.SeedIfEmptyAsync()` popola prenotazioni, inviti, commenti e avvisi.
- **Spaces (`SmartCampus.Spaces.Api`)**: carica gli 8 spazi da `spaces.json` se la collezione MongoDB è vuota.
- **Users (`SmartCampus.Users.Api`)**: `UsersDbSeeder.SeedAsync()` inserisce gli account utente e admin con password con hash sicuro tramite `IPasswordHasher<UserProfile>`.

Tutte le operazioni di seed sono **idempotenti**: riavviare il servizio con database già popolato ignora il seed e non genera duplicati.

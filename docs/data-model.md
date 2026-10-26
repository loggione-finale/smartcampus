# Modello dati, regole di dominio e sicurezza dei database

> **Stato: bozza da rivedere insieme.** Persistenza scelta (D4 in [architecture.md](architecture.md)):
> **Spaces su MongoDB**, **Bookings e Users su PostgreSQL**. Ogni servizio possiede il proprio database e
> nessun altro lo legge.

## Vista d'insieme

| Servizio | Motore | Database | Accesso dal codice | Schema |
|---|---|---|---|---|
| Spaces | MongoDB | `spaces` (collezione `spaces`) | `MongoDB.Driver` | Documenti; indici creati all'avvio |
| Bookings | PostgreSQL | `bookings` | EF Core + Npgsql | Tabelle; migrazioni EF Core |
| Users | PostgreSQL | `users` | EF Core + Npgsql | Tabelle; migrazioni EF Core |

Tutti gli orari sono salvati in **UTC** (`timestamptz` su PostgreSQL, `Date` su MongoDB).

---

## Spaces (MongoDB)

**Perché un documento:** ogni tipo di spazio ha caratteristiche diverse (un laboratorio ha postazioni e
software, un'aula proiettore e lavagna, una sala studio prese e livello di silenzio). Le indisponibilità
appartengono allo spazio e si leggono sempre insieme a lui, quindi stanno dentro il documento.

```json
{
  "_id": "3f2a9c1e-7b4d-4e8a-9c1f-2d5e6a7b8c9d",
  "name": "Laboratorio Informatica 2",
  "type": "Lab",
  "building": "Edificio B",
  "floor": 1,
  "capacity": 24,
  "isActive": true,
  "features": {
    "workstations": 24,
    "software": ["Visual Studio Code", "Docker"],
    "projector": true
  },
  "unavailabilities": [
    {
      "id": "8c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f",
      "start": "2026-10-28T08:00:00Z",
      "end": "2026-10-28T12:00:00Z",
      "reason": "Manutenzione impianto elettrico",
      "createdBy": "b7e1c2d3-0000-4000-8000-000000000002"
    }
  ],
  "updatedAt": "2026-10-26T14:00:00Z"
}
```

| Campo | Regole |
|---|---|
| `_id` | UUID generato dal servizio (stringa), usato come `space_id` da Bookings |
| `type` | Uno tra `Classroom`, `Lab`, `StudyRoom` |
| `capacity` | Intero da 1 a 500 |
| `features` | Oggetto libero, ma solo chiavi previste per quel `type` (validate nel servizio) |
| `unavailabilities[].end` | Sempre dopo `start` |
| `isActive` | Gli spazi non si cancellano mai, si disattivano |

**Indici** (creati all'avvio dal servizio, idempotenti):

| Indice | Serve per |
|---|---|
| `{ type: 1, capacity: 1, isActive: 1 }` | Ricerca con filtri |
| `{ "features.software": 1 }` | Ricerca per software installato |
| `{ name: "text" }` | Ricerca per nome |

## Bookings (PostgreSQL)

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE TABLE bookings (
    id                  uuid PRIMARY KEY,
    space_id            uuid        NOT NULL,          -- riferimento logico a Spaces
    space_name_snapshot     text    NOT NULL,       -- copie dei dati dello spazio al momento della prenotazione
    space_type_snapshot     text    NOT NULL,
    space_building_snapshot text    NOT NULL,
    organizer_id        uuid        NOT NULL,          -- riferimento logico a Users
    period              tstzrange   NOT NULL,
    status              text        NOT NULL CHECK (status IN ('Confirmed', 'Cancelled', 'Released')),
    checked_in_at       timestamptz NULL,
    checked_in_by       uuid        NULL,
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now(),
    cancelled_at        timestamptz NULL,
    CONSTRAINT bookings_period_valid CHECK (lower(period) < upper(period)),
    CONSTRAINT bookings_space_no_overlap
        EXCLUDE USING gist (space_id WITH =, period WITH &&) WHERE (status = 'Confirmed')
);

CREATE TABLE booking_participants (
    booking_id            uuid        NOT NULL REFERENCES bookings (id),
    user_id               uuid        NOT NULL,        -- riferimento logico a Users
    display_name_snapshot text        NOT NULL,
    role                  text        NOT NULL CHECK (role IN ('Organizer', 'Participant')),
    period                tstzrange   NOT NULL,        -- copia di bookings.period
    status                text        NOT NULL CHECK (status IN ('Confirmed', 'Cancelled', 'Released', 'Left')),
    added_by              uuid        NOT NULL,
    PRIMARY KEY (booking_id, user_id),
    CONSTRAINT booking_participants_person_no_overlap
        EXCLUDE USING gist (user_id WITH =, period WITH &&) WHERE (status = 'Confirmed')
);

CREATE INDEX ix_bookings_space_period ON bookings USING gist (space_id, period);
CREATE INDEX ix_bookings_organizer ON bookings (organizer_id);
CREATE INDEX ix_participants_user ON booking_participants (user_id);
```

| Stato | Significato |
|---|---|
| `Confirmed` | Prenotazione valida: occupa spazio e persone |
| `Cancelled` | Annullata dall'organizzatore o dal personale |
| `Released` | Rilasciata in automatico: nessun check-in entro 15 minuti dall'inizio |
| `Left` | Solo per un partecipante: è uscito dalla prenotazione |

Le righe non si cancellano mai: lo storico serve al personale e alle statistiche.

## Users (PostgreSQL)

```sql
CREATE TABLE user_profiles (
    id            uuid PRIMARY KEY,
    display_name  text    NOT NULL,
    role          text    NOT NULL CHECK (role IN ('Student', 'Staff')),
    email         text    NOT NULL UNIQUE,      -- fittizia, dominio @smartcampus.test
    password_hash text    NULL,                 -- solo con SC-20: account pre-caricati, nessuna registrazione
    is_active     boolean NOT NULL DEFAULT true
);

CREATE INDEX ix_user_profiles_display_name ON user_profiles (lower(display_name));
```

---

**Nessuna registrazione.** I profili (e, con SC-20, le loro password demo) arrivano solo dal seed dei dati
sintetici, che simula l'anagrafica della scuola: studenti e personale hanno già le loro credenziali. Il servizio
Users non espone API per creare utenti.

---

## Aggregare i dati di servizi diversi

Spaces è su MongoDB e Bookings su PostgreSQL, ma il punto vero è che sono **due database di due servizi**:
nessuna query li unisce, con qualsiasi motore. Ogni vista che ha bisogno di dati di entrambi usa una di due
tecniche.

| Vista | Dati necessari | Come li otteniamo |
|---|---|---|
| Creare una prenotazione | Spazio (attivo, capienza, indisponibilità) + prenotazioni | Bookings chiede a Spaces via API, poi salva le **copie** di nome, tipo ed edificio |
| Le mie prenotazioni | Prenotazioni + nome dello spazio + nomi dei partecipanti | Solo Bookings, grazie alle copie |
| Disponibilità di uno spazio | Dettagli dello spazio + fasce occupate | Il frontend compone: dettagli da Spaces, fasce da Bookings |
| Spazi adatti **e liberi** in una fascia | Filtri sugli spazi + fasce libere | Il frontend chiede a Spaces gli spazi che rispettano i filtri, poi a Bookings quali sono liberi (`POST /bookings/availability` con l'elenco degli id) |
| Calendario del personale con filtri per edificio e tipo | Prenotazioni + edificio e tipo | Solo Bookings, filtrando sulle copie |
| Statistiche per spazio, tipo o edificio | Prenotazioni + check-in + tipo ed edificio | Solo Bookings, `GROUP BY` sulle copie |
| Gestione spazi del personale | Solo spazi | Solo Spaces |

**Regole delle copie**

- Si scrivono una volta, quando la prenotazione viene creata, e non si aggiornano: descrivono lo spazio **com'era
  quando è stato prenotato**, come deve fare uno storico.
- Se uno spazio cambia nome, tipo o edificio, le nuove prenotazioni avranno i nuovi valori. Le statistiche
  restano corrette rispetto a ciò che era stato prenotato; il limite va dichiarato nelle misure di utilizzo.
- Per i dati che devono essere **attuali** (spazio attivo, capienza, indisponibilità) non si usano mai le copie:
  Bookings chiede sempre a Spaces.

---

## Regole di dominio (in un solo posto)

| # | Regola | Dove è garantita |
|---|---|---|
| R1 | Uno spazio non ha due prenotazioni `Confirmed` sovrapposte | Vincolo `bookings_space_no_overlap` |
| R2 | Una persona non è in due prenotazioni `Confirmed` sovrapposte | Vincolo `booking_participants_person_no_overlap` |
| R3 | Una fascia ha fine dopo l'inizio, non è nel passato, è multipla di 15 minuti | Vincolo `bookings_period_valid` + validazione in Bookings |
| R4 | Durata minima, massima e anticipo massimo | Validazione in Bookings (valori **da chiedere al PO**) |
| R5 | Organizzatore + partecipanti ≤ capienza dello spazio | Bookings, leggendo la capienza da Spaces |
| R6 | Si prenota solo uno spazio attivo e non indisponibile in quella fascia | Bookings, chiedendo a Spaces |
| R7 | Un organizzatore ha al massimo 5 prenotazioni di gruppo future | Bookings |
| R8 | Solo l'organizzatore annulla; un partecipante può solo uscire | Bookings |
| R9 | Check-in da 10 minuti prima a 15 minuti dopo l'inizio, solo da organizzatore o partecipante | Bookings |
| R10 | Senza check-in entro 15 minuti dall'inizio la prenotazione diventa `Released` e lo spazio si libera | Servizio in background di Bookings |
| R11 | Spazi e profili non si cancellano, si disattivano | Spaces e Users non espongono `DELETE` |
| R12 | Disattivare uno spazio annulla le sue prenotazioni future (se il PO conferma) | Spaces chiede a Bookings, che annulla in una transazione |

---

## Sicurezza dei database

| Controllo | PostgreSQL | MongoDB |
|---|---|---|
| **Autenticazione attiva** | Utente amministratore con password da `.env` | Autenticazione attiva (`MONGO_INITDB_ROOT_USERNAME/PASSWORD` da `.env`) |
| **Un utente per servizio, minimo privilegio** | `bookings_app` con permessi solo su `bookings`, `users_app` solo su `users`; nessuno dei due è superuser | `spaces_app` con ruolo `readWrite` solo sul database `spaces` |
| **Porte non esposte alla rete** | `127.0.0.1:5432:5432` in `docker-compose.yml` | `127.0.0.1:27017:27017` |
| **Segreti** | Password solo in `.env` (non versionato) o `dotnet user-secrets` | Come PostgreSQL |
| **Iniezione** | EF Core genera query parametrizzate; vietato `FromSqlRaw` con stringhe concatenate | Solo filtri tipizzati (`Builders<Space>.Filter`); vietato costruire query JSON da stringhe dell'utente |
| **Modifiche allo schema** | Solo tramite migrazioni EF Core, versionate nel repository | Indici creati dal codice all'avvio, versionati nel repository |
| **Dati iniziali** | Seed solo in ambiente `Development`, solo dati sintetici | Come PostgreSQL |
| **Persistenza e backup** | Volume Docker con nome; `pg_dump` prima della demo | Volume Docker con nome; `mongodump` prima della demo |
| **Riavvio** | `restart: unless-stopped` e `healthcheck` in Compose; `EnableRetryOnFailure` in EF Core | `restart: unless-stopped` e `healthcheck` in Compose |

## Test sui database

| Test | Database |
|---|---|
| Vincoli R1 e R2 con richieste simultanee | PostgreSQL reale, database `bookings_test` |
| Stati e storico (R8, R10) | PostgreSQL reale |
| Ricerca con filtri e indici, documento con `features` diverse per tipo | MongoDB reale, database `spaces_test` |
| Un servizio con l'utente applicativo non riesce a leggere il database di un altro | Prova manuale, registrata in `evidence/` |
| Dopo `docker compose down` e `up` i dati restano (volume) | Prova manuale, registrata in `evidence/` |

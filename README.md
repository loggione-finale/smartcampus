# SmartCampus

Prodotto per trovare e prenotare gli spazi del campus (aule, laboratori, sale studio) senza impegni
incompatibili, rendendone più comprensibile l'utilizzo.

Project Work ITS Bari – Novembre 2026.

## Stato

Progetto appena avviato: il perimetro è ancora da concordare con il Product Owner. Le decisioni prese
vanno registrate in [`docs/decisions/`](docs/decisions/).

Consegna: **29/10/2026**.

- **[Promemoria: che cosa stiamo facendo e come](docs/project-overview.md)**
- **[Brainstorming del 26/10: nuovo perimetro e divisione del lavoro](docs/analysis/brainstorming-2026-10-26.md)**
- [Analisi del progetto](docs/analysis/project-analysis.md)
- [Analisi dei requisiti rispetto alle slide](docs/analysis/requirements-analysis.md)
- [Flusso dell'applicativo e punti deboli](docs/analysis/application-flow.md)
- [Architettura proposta (microservizi)](docs/architecture.md)
- [Modello dati e sicurezza dei database](docs/data-model.md)
- [Sicurezza e test per ogni componente](docs/security-and-testing.md)
- [Bozza del backlog](docs/backlog/backlog-draft.md)

**Stack:** tre microservizi ASP.NET Core Web API (Spaces su MongoDB; Bookings e Users su PostgreSQL con EF Core),
frontend Angular + Angular Material.

## Struttura del repository

| Percorso | Contenuto |
|---|---|
| `src/Services/` | I tre microservizi .NET: Spaces, Bookings, Users |
| `src/web/` | Frontend Angular |
| `docker-compose.yml`, `infra/` | MongoDB e PostgreSQL, un database per servizio |
| `tests/` | Test automatici, inclusi quelli sul database reale scelto |
| `data/synthetic/` | Dati sintetici prodotti dal gruppo e script per generarli |
| `docs/analysis/` | Analisi del bisogno, ipotesi, domande aperte, perimetro concordato col PO |
| `docs/architecture.md` | Servizi, scelte tecniche con pro e contro |
| `docs/backlog/` | Bozza del backlog prima della creazione delle issue |
| `docs/decisions/` | Decisioni architetturali e tecniche motivate (ADR) |
| `docs/evidence/` | Prove eseguite, esiti osservati e limiti noti |
| `docs/definition-of-done.md` | Definition of Done del gruppo |
| `.github/` | Template di issue e pull request, CODEOWNERS, workflow CI |

Il backlog vive nelle **Issue** e nel progetto **Loggione Roadmap** dell'organizzazione.

## Installazione, avvio e test

### Requisiti

| Strumento | Versione | A cosa serve |
|---|---|---|
| .NET SDK | 10 (fissata da `global.json`) | Servizi e test |
| Docker Desktop | qualsiasi recente, **avviato** | Database |
| Node.js e Angular CLI | Node LTS, `npm install -g @angular/cli` | Solo per il frontend |
| Git | qualsiasi recente | |

### 1. Scarica il repository

```bash
git clone https://github.com/loggione-finale/smartcampus.git
cd smartcampus
git config core.longpaths true   # solo su Windows
```

### 2. Avvia i database

```bash
cp .env.example .env              # solo la prima volta: rilanciarlo cancella le tue password
notepad .env                      # compila le 5 password vuote e salva (Mac/Linux: nano .env)
docker compose up -d --wait       # PostgreSQL e MongoDB, attesi "Healthy"
```

Dettagli, utenti e comandi utili: [infra/README.md](infra/README.md).

### 3. Imposta le stringhe di connessione (una volta per PC)

Le password stanno solo nel tuo `.env` e nei *user secrets* di .NET, mai in `appsettings*.json`.
Per ogni servizio, con le password del tuo `.env`:

```bash
dotnet user-secrets set "ConnectionStrings:Bookings" "Host=localhost;Port=5432;Database=bookings;Username=bookings_app;Password=<BOOKINGS_DB_PASSWORD>" --project src/Services/Bookings/SmartCampus.Bookings.Api
```

| Servizio | Chiavi | Progetto |
|---|---|---|
| Bookings | `ConnectionStrings:Bookings`, `ConnectionStrings:BookingsTest` | `src/Services/Bookings/SmartCampus.Bookings.Api` |
| Users | `ConnectionStrings:Users`, `ConnectionStrings:UsersTest` | `src/Services/Users/SmartCampus.Users.Api` |
| Spaces | `ConnectionStrings:Spaces`, `ConnectionStrings:SpacesTest` | `src/Services/Spaces/SmartCampus.Spaces.Api` |

I valori completi sono in [infra/README.md](infra/README.md). In CI le stesse chiavi arrivano come variabili
d'ambiente (`ConnectionStrings__BookingsTest`…).

### 4. Avvia i servizi

Un terminale per servizio:

```bash
dotnet run --project src/Services/Spaces/SmartCampus.Spaces.Api      # http://localhost:5101
dotnet run --project src/Services/Bookings/SmartCampus.Bookings.Api  # http://localhost:5102
dotnet run --project src/Services/Users/SmartCampus.Users.Api        # http://localhost:5103
```

Per ogni servizio: `/health` risponde `Healthy` e `/swagger` mostra le API.

### 5. Avvia il frontend

```bash
cd src/web
npm ci
ng serve                          # http://localhost:4200
```

### 6. Esegui i test

```bash
docker compose up -d --wait       # i test di integrazione usano i database reali
dotnet test SmartCampus.slnx
cd src/web && npm test -- --watch=false
```

### Problemi frequenti

| Messaggio | Causa e soluzione |
|---|---|
| `failed to connect to the docker API` | Docker Desktop non è avviato |
| `required variable ... is missing a value: Imposta ... nel file .env` | Manca una password nel `.env`: aprilo e compila tutte e 5 le password |
| Porta 5432 o 27017 già occupata | Cambia `POSTGRES_PORT` o `MONGO_PORT` nel `.env` e la porta nella stringa di connessione |
| `Filename too long` (Windows) | `git config core.longpaths true` |
| Hai rilanciato `cp .env.example .env` o cambiato le password dopo il primo avvio | `docker compose down -v` e di nuovo `up` (cancella i dati locali): utenti e password dei database si creano solo al primo avvio |
| `Could not find a UserSecretsId` | Il proprietario del servizio non ha ancora eseguito `dotnet user-secrets init`: va fatto una volta, nel suo task |

Prove di avvio da zero: [docs/evidence/2026-10-27-startup-from-scratch.md](docs/evidence/2026-10-27-startup-from-scratch.md).

## Come contribuire

Leggi [CONTRIBUTING.md](CONTRIBUTING.md) prima di aprire la tua prima pull request.

## Regole di sicurezza

- Solo dati sintetici: nessun dato personale reale.
- Nessun segreto (password, token, connection string) in repository, log o materiali condivisi.
  Usa `dotnet user-secrets` o variabili d'ambiente.

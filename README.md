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
| `.github/` | Template di issue e pull request, CODEOWNERS |

Il backlog vive nelle **Issue** e nel progetto **Loggione Roadmap** dell'organizzazione.

## Requisiti

- .NET SDK 10
- Node.js LTS e Angular CLI (`npm install -g @angular/cli`)
- Docker Desktop (o equivalente), solo per i database

## Installazione, avvio e test

_Da completare e provare appena esiste il primo incremento (task SC-01)._ Le istruzioni devono permettere
a chiunque di ricostruire configurazione, dipendenze, dati sintetici e avvio **senza segreti nel
repository**. Schema previsto:

```bash
cp .env.example .env               # poi imposta una password locale
docker compose up -d               # MongoDB (spaces) e PostgreSQL (bookings, users)
dotnet run --project src/Services/Spaces/SmartCampus.Spaces.Api
dotnet run --project src/Services/Bookings/SmartCampus.Bookings.Api
dotnet run --project src/Services/Users/SmartCampus.Users.Api
cd src/web && npm install && ng serve   # http://localhost:4200
```

Test:

```bash
dotnet test                        # servono i database avviati
cd src/web && ng test
```

## Come contribuire

Leggi [CONTRIBUTING.md](CONTRIBUTING.md) prima di aprire la tua prima pull request.

## Regole di sicurezza

- Solo dati sintetici: nessun dato personale reale.
- Nessun segreto (password, token, connection string) in repository, log o materiali condivisi.
  Usa `dotnet user-secrets` o variabili d'ambiente.

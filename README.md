# SmartCampus

Prodotto per trovare e prenotare gli spazi del campus (aule, laboratori, sale studio) senza impegni
incompatibili, rendendone più comprensibile l'utilizzo.

Project Work ITS Bari – Novembre 2026.

## Stato

Progetto appena avviato: il perimetro è ancora da concordare con il Product Owner. Le decisioni prese
vanno registrate in [`docs/decisions/`](docs/decisions/).

Consegna: **29/10/2026**.

- [Analisi del progetto](docs/analysis/project-analysis.md)
- [Architettura proposta (microservizi)](docs/architecture.md)
- [Bozza del backlog](docs/backlog/backlog-draft.md)

**Stack:** tre microservizi ASP.NET Core Web API (Spaces, Bookings, Users) con EF Core e PostgreSQL,
frontend Angular + Angular Material.

## Struttura del repository

| Percorso | Contenuto |
|---|---|
| `src/Services/` | I tre microservizi .NET: Spaces, Bookings, Users |
| `src/web/` | Frontend Angular |
| `docker-compose.yml`, `infra/` | PostgreSQL con un database per servizio |
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
- Docker Desktop (o equivalente), solo per PostgreSQL

## Installazione, avvio e test

_Da completare e provare appena esiste il primo incremento (task SC-01)._ Le istruzioni devono permettere
a chiunque di ricostruire configurazione, dipendenze, dati sintetici e avvio **senza segreti nel
repository**. Schema previsto:

```bash
cp .env.example .env               # poi imposta una password locale
docker compose up -d               # PostgreSQL con i database spaces, bookings, users
dotnet run --project src/Services/Spaces/SmartCampus.Spaces.Api
dotnet run --project src/Services/Bookings/SmartCampus.Bookings.Api
dotnet run --project src/Services/Users/SmartCampus.Users.Api
cd src/web && npm install && ng serve   # http://localhost:4200
```

Test:

```bash
dotnet test                        # serve PostgreSQL avviato
cd src/web && ng test
```

## Come contribuire

Leggi [CONTRIBUTING.md](CONTRIBUTING.md) prima di aprire la tua prima pull request.

## Regole di sicurezza

- Solo dati sintetici: nessun dato personale reale.
- Nessun segreto (password, token, connection string) in repository, log o materiali condivisi.
  Usa `dotnet user-secrets` o variabili d'ambiente.

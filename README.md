# SmartCampus

Prodotto per trovare e prenotare gli spazi del campus (aule, laboratori, sale studio) senza impegni
incompatibili, rendendone più comprensibile l'utilizzo.

Project Work ITS Bari – Novembre 2026.

## Stato

Progetto appena avviato: perimetro, architettura e stack di dettaglio sono ancora da concordare
con il Product Owner. Le decisioni prese vanno registrate in [`docs/decisioni/`](docs/decisioni/).

## Struttura del repository

| Percorso | Contenuto |
|---|---|
| `src/` | Codice dell'applicazione (.NET / C#) |
| `tests/` | Test automatici, inclusi quelli sul database reale scelto |
| `data/synthetic/` | Dati sintetici prodotti dal gruppo e script per generarli |
| `docs/analisi/` | Analisi del bisogno, ipotesi, domande aperte, perimetro concordato col PO |
| `docs/decisioni/` | Decisioni architetturali e tecniche motivate (ADR) |
| `docs/prove/` | Prove eseguite, esiti osservati e limiti noti |
| `docs/definition-of-done.md` | Definition of Done del gruppo |
| `.github/` | Template di issue e pull request, CODEOWNERS |

Il backlog vive nelle **Issue** e nel progetto **Loggione Roadmap** dell'organizzazione.

## Requisiti

- .NET SDK 10
- _Database: da definire_

## Installazione, avvio e test

_Da completare appena esiste il primo incremento._ Le istruzioni devono permettere a chiunque di
ricostruire configurazione, dipendenze, dati sintetici e avvio **senza segreti nel repository**.

```bash
dotnet restore
dotnet build
dotnet test
```

## Come contribuire

Leggi [CONTRIBUTING.md](CONTRIBUTING.md) prima di aprire la tua prima pull request.

## Regole di sicurezza

- Solo dati sintetici: nessun dato personale reale.
- Nessun segreto (password, token, connection string) in repository, log o materiali condivisi.
  Usa `dotnet user-secrets` o variabili d'ambiente.

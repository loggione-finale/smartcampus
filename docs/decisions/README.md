# Decisioni (ADR)

Ogni scelta tecnica rilevante (framework, database, forma dell'interfaccia, uso o esclusione del cloud)
ha un file `NNNN-titolo.md`, ad esempio `0001-scelta-database.md`, con questa struttura:

```markdown
# NNNN – Titolo

- Data:
- Stato: proposta | accettata | sostituita da NNNN

## Contesto
Quale problema o bisogno richiede una decisione.

## Decisione
Cosa abbiamo scelto.

## Alternative considerate
Cosa abbiamo scartato e perché.

## Conseguenze
Cosa diventa più facile, cosa più difficile, quali rischi restano.
```

## Elenco

Le decisioni D1–D10 di [architecture.md](../architecture.md), più gli strumenti esclusi (#89).

| ADR | Decisione |
|---|---|
| [0001](0001-microservizi.md) | Stile architetturale: tre microservizi |
| [0002](0002-confini-dei-servizi.md) | Confini dei servizi: Spaces, Bookings, Users |
| [0003](0003-rest-sincrono.md) | Comunicazione tra servizi: REST sincrono |
| [0004](0004-database.md) | Database: Spaces su MongoDB, Bookings e Users su PostgreSQL |
| [0005](0005-vincoli-di-esclusione.md) | Nessuna sovrapposizione: vincoli di esclusione in PostgreSQL |
| [0006](0006-frontend-angular.md) | Frontend: Angular e Angular Material |
| [0007](0007-avvio-locale.md) | Avvio in locale: Docker Compose per i database |
| [0008](0008-utenti-e-accesso.md) | Utenti e accesso: registrazione e login con token firmato |
| [0009](0009-test.md) | Test: xUnit sui database reali |
| [0010](0010-integrazione-continua.md) | Integrazione continua: GitHub Actions su ogni PR |
| [0011](0011-strumenti-esclusi.md) | Strumenti esclusi: cloud, telemetria, Aspire, YARP, RabbitMQ, Blazor |

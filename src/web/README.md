# Frontend web

Frontend Angular del progetto SmartCampus, generato con Angular CLI 20 e Angular Material.

## Server di sviluppo

Dalla cartella `src/web` avvia il server:

```bash
npm start
```

Apri `http://localhost:4200/`. Il comando carica automaticamente `proxy.conf.json`, che inoltra le
richieste locali ai tre servizi:

| Percorso del browser | Servizio | Porta |
| --- | --- | --- |
| `/api/spaces/*` | Spaces | `5101` |
| `/api/bookings/*` | Bookings | `5102` |
| `/api/users/*` | Users | `5103` |

Per esempio, `/api/spaces/health` viene inoltrato a `http://localhost:5101/health`.

## Generazione del codice

Per generare un nuovo componente:

```bash
ng generate component component-name
```

Per l’elenco completo degli schematics disponibili:

```bash
ng generate --help
```

## Build

```bash
ng build
```

Gli artefatti vengono salvati nella cartella `dist/`, esclusa dal repository.

## Test unitari

```bash
npm test -- --watch=false
```

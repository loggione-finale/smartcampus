# Prova: tutto il sistema su `main`, API e frontend (#91)

| Campo | Contenuto |
|---|---|
| Cosa verifica | Che i tre servizi, i due database e il frontend funzionino insieme su `main` e che il percorso della demo (`docs/demo-path.md`) dia gli esiti attesi, comprese le funzioni arrivate dopo la prima prova (email `.eml`, statistiche e avvisi admin, divieto di prenotare all'admin, annullamento di prenotazioni del seed) |
| Come | PostgreSQL 17 e MongoDB `v7.0.45` con gli script di `infra/`; i tre servizi in `Development` con i dati sintetici; copione di chiamate HTTP con `curl` che segue il percorso della demo; frontend: `npm ci`, `npm run build`, `npm test` con Chrome headless, poi `npm start` (porta 4200, proxy verso i servizi) e schermate con Chrome headless a 1280x800 e 390x844 |
| Esito atteso | Tutte le chiamate con il codice e il messaggio scritti in `docs/demo-path.md`; build e test del frontend senza errori; le pagine si caricano e si adattano alla larghezza di un telefono |
| Esito osservato | Come atteso, vedi sotto |
| Ambiente | Fedora Linux 44, .NET SDK 10.0.113, Node 24, Podman 5.8.9, Chrome headless |
| Commit di `main` | `1d42f2b` |
| Chi l'ha eseguita | Rhaastrake, 28/10/2026, con un copione scritto e lanciato con Claude Code: non è un uso registrato del flusso agentico con Copilot |

## Percorso della demo sull'API

35 chiamate con un esito più 6 accessi, tutte come atteso. Rispetto alla prova delle 11 (`2026-10-28-demo-rehearsal-api.md`):

| Novità | Atteso | Osservato |
|---|---|---|
| L'admin prova a prenotare | `403` | `403` |
| Spazi liberi in una fascia (`POST /bookings/availability`, Aula 101 e Aula 204) | `200`, entrambi liberi | `200`, 2 liberi |
| Giulia annulla `SC-SEED-0001` (organizzatore con il nome nel dato del seed) | `200`, `Cancelled` | `200`, `Cancelled` |
| Le mie prenotazioni dopo due annullamenti | `200`, 2 annullate visibili | `200`, 4 prenotazioni di cui 2 annullate |
| Elena scrive il commento con un problema segnalato | `200` | `200`; l'avviso admin è tra i 9 avvisi |
| Admin: statistiche per edificio; utente: statistiche | `200` e `403` | `200` (2 gruppi) e `403` |
| Admin: avvisi | `200` | `200`, 9 avvisi |

Gli altri passi (accesso, catalogo, conflitto `409`, adiacenza, annullamento altrui `403`, fascia già usata `409`, regole su fasce e durata `400`, senza accesso `401`, aula mista piena `409`, commento mancante `409` e sblocco, gruppo con inviti e conferma, `403` sull'invito di un altro, Spaces fermo `503`) hanno dato gli stessi esiti della prova delle 11.

**Email:** dopo il percorso erano stati scritti 10 file `.eml` in `src/Services/Bookings/SmartCampus.Bookings.Api/mail-outbox/` (6 prenotazioni create, 2 inviti, 2 annullamenti), tutti con stato `Sent` nel database. Controllato il mittente, il destinatario e l'oggetto di un file (`Prenotazione creata ... - Aula 204`, a un'invitata).

## Frontend

- `npm run build`: riuscita; bundle iniziale 870 kB senza compressione e 205 kB trasferiti (il budget di 600 kB dà solo un avviso).
- `npm test`: 180 test su 180 passati.
- Server di sviluppo su `localhost:4200`: `200` sulla pagina; il proxy porta l'accesso (`/api/users/auth/login`, token ricevuto) e il catalogo (`/api/spaces/spaces`, `200`).
- Schermate con Chrome headless di `/login` e `/spaces` a 1280x800 e a 390x844 (larghezza da telefono): le pagine si caricano, i campi e i filtri restano leggibili e a una colonna sul telefono. Le immagini non sono nel repository.

## Limiti

- **Non è la prova dell'interfaccia di #91.** Nessuno ha cliccato i passi nel browser: c'è solo il rendering di due pagine. Restano le prove a mano su due sistemi operativi e su uno smartphone vero, con la tabella di `docs/demo-path.md`.
- Un solo PC (Linux), Chrome headless, un solo giro.
- Con PostgreSQL fermo il comportamento è stato corretto con #218, già su `main`, e qui non è provato. È stato ripetuto il 29/10 nelle [prove di guasto](2026-10-28-failure-tests.md#ripetizione-dopo-le-correzioni-2910): `503` leggibile in circa 7 s.
- Il copione non è nel repository: ognuno ripete i passi a mano o scrive il suo.
- Non prova la prova a voce con il gruppo, che resta da fare.

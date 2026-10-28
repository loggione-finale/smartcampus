# Prova: percorso della demo sull'API (#51)

| Campo | Contenuto |
|---|---|
| Cosa verifica | Che il percorso della demo (15 passi, vedi `docs/demo-path.md`, #91) dia gli esiti attesi su Spaces, Users e Bookings in `Development` con i dati sintetici del seed |
| Come | Database avviati con gli script di `infra/` (PostgreSQL 17, MongoDB `v7.0.45`); `dotnet build`; i tre servizi in `Development` (porte 5101, 5102, 5103) con le stesse variabili d'ambiente per la chiave JWT e per le stringhe di connessione; un copione di chiamate HTTP (`curl`) che esegue i passi in ordine con gli account demo e scrive il codice HTTP di ogni chiamata |
| Esito atteso | Scritto prima, nella colonna "Esito atteso" di `docs/demo-path.md` e qui sotto |
| Esito osservato | Tutte le 28 chiamate con un esito e i 6 accessi come atteso, vedi la tabella |
| Ambiente | Fedora Linux 44, kernel 7.2.11, .NET SDK 10.0.113, Podman 5.8.9 |
| Commit di `main` | `6589b43` |
| Chi l'ha eseguita | Rhaastrake, 28/10/2026, con un copione scritto e lanciato con Claude Code: non è un uso registrato del flusso agentico con Copilot ([agents/README.md](../agents/README.md)) |

## Esiti

| Passo | Chiamata | Atteso | Osservato |
|---|---|---|---|
| 1 | Accesso di 6 account demo | Token | Sei token |
| 1 | Accesso con password sbagliata | `401` | `401` |
| 2 | `GET /spaces?type=Lab` | `200`, due laboratori | `200`, 2 spazi |
| 3 | Disponibilità di Aula 101 fra 12 giorni | `200`, nessuna fascia | `200`, 0 fasce |
| 4 | Giulia prenota Aula 101 09:00-11:00 | `201` con codice | `201`, `SC-261109-VJQ9` |
| 5 | Marco prenota 10:00-12:00 (sovrapposta) | `409` | `409` "Space already booked" |
| 6 | Marco prenota 11:00-13:00 (adiacente) | `201` | `201` |
| 7 | Sara annulla la prenotazione di Giulia | `403` | `403` "Only the organizer can cancel a booking" |
| 7 | Giulia annulla la propria | `200` `Cancelled` | `200`, `Cancelled` |
| 8 | Sara prenota la fascia liberata | `201` | `201` |
| 9 | Marco, seconda prenotazione nella stessa fascia | `409` | `409` "Time band already used" |
| 10 | 07:00-08:00 | `400` | `400` |
| 10 | 13:00-15:00 (a cavallo di due fasce) | `400` | `400` |
| 10 | 15 minuti | `400` | `400` |
| 10 | Senza accesso | `401` | `401` |
| 11 | Sara prenota Aula 204 nella fascia piena | `409`, 0 posti | `409` "Not enough seats", `freeSeats` 0 |
| 11 | Sara prenota Aula 204 in una fascia libera | `201` | `201` |
| 12 | Elena prenota con il commento mancante | `409` con il codice | `409` "Closing comment pending", `SC-SEED-0011` |
| 12 | Elena scrive il commento finale di `SC-SEED-0011` | `200` | `200` |
| 12 | Elena prenota di nuovo | `201` | `201` |
| 13 | Luca prenota il Lab 2 con due invitati | `201` | `201`, `SC-261110-ABCE` |
| 13 | Inviti di Sara | `200`, il nuovo invito tra gli altri | `200`, 3 inviti |
| 13 | Sara conferma l'invito | `200` `Accepted` | `200`, `Accepted` |
| 13 | Marco prova a confermare l'invito di Sara | `403` | `403` |
| 14 | Le prenotazioni di Giulia | `200` | `200`, 4 prenotazioni |
| 14 | Admin: dashboard | `200` | `200` |
| 14 | Sara (utente): dashboard | `403` | `403` |
| 15 | Prenotazione con Spaces fermo | `503` in pochi secondi | `503` "Spaces unavailable" |
| 15 | Le mie prenotazioni con Spaces fermo | `200` | `200` |

## Limiti

- **È una prova sull'API, non sull'interfaccia.** Non dice nulla su schermate, messaggi in italiano, due sistemi operativi o
  smartphone: sono richieste di #91 e vanno registrate a parte.
- **Una persona, un PC, un'istanza per servizio**, con un solo giro. Non prova la concorrenza (vedi
  `docs/evidence/booking-concurrency.md`) né il comportamento con PostgreSQL fermo (#66).
- Le email di invito sono solo in coda nel database: i file `.eml` non erano ancora su `main` (#106).
- Il copione non è nel repository: il percorso scritto è `docs/demo-path.md`; chi vuole ripetere la prova lo fa a mano,
  passo per passo, o scrive un suo script.
- Il giro è stato fatto con i dati appena caricati; con prenotazioni lasciate da altre prove gli esiti dei passi 5 e 9
  cambiano se la fascia scelta è già occupata.

## Da fare giovedì 29/10 (criteri di #51)

- [ ] Avvio da zero provato al mattino su un PC pulito (`docker compose up -d --wait`, tre servizi, frontend), con
  l'esito in `docs/evidence/`.
- [ ] Percorso della demo provato a voce da tutto il gruppo almeno una volta.
- [ ] Prova dell'interfaccia su due sistemi operativi e su uno smartphone (tabella in `docs/demo-path.md`).
- [ ] Presentazione breve: bisogno, perimetro concordato, prodotto funzionante.
- [ ] Dichiarato che i dati sintetici verificano il prototipo ma non dimostrano benefici reali per il campus.
- [ ] Ognuno sa spiegare il proprio contributo e le evidenze condivise.
- [ ] Solo correzioni: nessuna nuova funzione.

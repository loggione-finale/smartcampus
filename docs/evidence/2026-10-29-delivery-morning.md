# Prova della mattina della consegna (#51)

> **Stato:** procedura ed esiti attesi scritti prima, come chiede [README.md](README.md).
> - **Sezione 1 eseguita il 29/10 alle 14:53, nel pomeriggio e non la mattina**, sulla `v0.2.1`: tutti i passi come atteso, con le due note scritte nelle righe 1 e 2.
> - **Sezioni 2 e 3 non compilate riga per riga.** La prova a voce con tutto il gruppo è stata fatta il 29/10 (conferma di asprenolapo, #51); la prova dell'interfaccia è nella tabella di [demo-path.md](../demo-path.md).

| Campo | Valore |
|---|---|
| Cosa verifica | Due criteri di #51: "avvio da zero provato la mattina della consegna" e "demo con prove effettivamente eseguite"; la presentazione nei tempi |
| Ambiente | Windows 11, .NET SDK 10.0.401, Docker 29.8.0, Node 24.19.0 |
| Commit di `main` | `ed0e521` (tag `v0.2.1`) |
| Chi l'ha eseguita | asprenolapo, con Claude Code |
| Data e ora | 29/10/2026, 14:53–14:58 |

## 1. Avvio da zero (prima della daily delle 9:30)

Su una cartella nuova, senza riusare quella di lavoro, così si prova davvero il README.

| # | Passo | Esito atteso | Esito osservato |
|---|---|---|---|
| 1 | `git clone` del repository in una cartella nuova, poi `git log -1 --format=%h` | Clone riuscito; l'hash è quello di `main` su GitHub | `ed0e521`, uguale a `main`. **Nota:** in una cartella con un percorso lungo il checkout si è fermato con `Filename too long`, perché `core.longpaths` va passato già al clone. Recuperato con `git config core.longpaths true` e `git reset`; README corretto (`git clone -c core.longpaths=true`) |
| 2 | `docker compose down -v` nella cartella di lavoro di prima, per partire da database vuoti | Volumi `smartcampus_*` rimossi | **Variante:** per non cancellare i dati locali, `docker compose down` senza `-v` (container tolti, volumi tenuti) e avvio del clone con `COMPOSE_PROJECT_NAME=smartcampus-zero`. Volumi nuovi `smartcampus-zero_*`, quindi database vuoti |
| 3 | `.env` da `.env.example`, con 5 password nuove e `JWT_SECRET` vuota (README, passo 2) | Compilato con 5 password nuove generate a caso; `JWT_SECRET` vuota |
| 4 | `scripts/dev-up` (README, passo 3), cronometrato | 4 `OK` e "Sistema avviato" in **60 s**, compresi build e `npm ci`. `JWT_SECRET` generata nel `.env`. Le immagini Docker erano già scaricate sul PC |
| 5 | `http://localhost:4200`, accesso come Giulia Rossi | Accesso di Giulia riuscito; 15 spazi e le sue prenotazioni del seed (`SC-SEED-0001`, `0005`, `0013`); frontend `200`, favicon nuova servita |
| 6 | `dotnet test SmartCampus.slnx` e `npm test -- --watch=false` in `src/web` | `dotnet test`: **513/513** (Bookings 285, Users 110, Spaces 109, dati sintetici 9); `ng test`: **200/200**. **Nota:** se nel terminale è esportata `JWT_SECRET` (per esempio caricando tutto il `.env`), i test d'integrazione falliscono con `401`, perché i servizi la preferiscono alla chiave dei test. Va esportata solo la stringa di connessione dei test |
| 7 | CI di `main` su GitHub | CI e CodeQL verdi su `ed0e521` |

Se un passo fallisce: si annota l'errore esatto, si apre un Bug e si decide in daily se la demo usa il commit
precedente (ultimo tag) invece di `main`.

## 2. Prova della demo

Sul sistema avviato al punto 1, con il percorso di [demo-path.md](../demo-path.md).

| # | Controllo | Esito atteso | Esito osservato |
|---|---|---|---|
| 1 | Passi 1–15 del percorso, ognuno da chi è nella colonna "Chi" | Ogni passo con l'esito atteso; i passi non pronti si tolgono dal percorso | |
| 2 | Prova dell'interfaccia su un secondo sistema e su smartphone ([demo-path.md](../demo-path.md), "Prova dell'interfaccia") | Righe della tabella compilate | |
| 3 | Durata della demo | Entro il tempo dato dal professore, con 2 minuti di margine | |
| 4 | Piano B: video o schermate del percorso, se il PC della demo non parte | Pronto prima della consegna, sul PC di un secondo membro | |

## 3. Prova della presentazione

| # | Controllo | Esito atteso | Esito osservato |
|---|---|---|---|
| 1 | Slide ([presentation-outline.md](../presentation-outline.md)): bisogno, perimetro concordato con il PO, prodotto funzionante | Una prova completa, cronometrata | |
| 2 | Ogni componente spiega il proprio contributo e mostra la sua evidenza ([demo-path.md](../demo-path.md), ultima sezione) | Tutti e 7 | |
| 3 | Dichiarazioni obbligatorie: dati sintetici, che non dimostrano benefici reali; cosa è provato e cosa è solo ipotizzato | Dette a voce e presenti nelle slide | |
| 4 | Limiti noti e bug aperti detti come tali (Bug ancora aperti su GitHub) | Elenco aggiornato la mattina | |
| 5 | Decisioni della Review con il PO ([po-meeting-2026-10-29.md](../analysis/po-meeting-2026-10-29.md)) registrate | Righe nuove in `po-decisions.md` | |

## Dopo la prova

- Rilascio `v0.2` sul commit usato per la demo (#86): tag, poi GitHub Release.
- Questa scheda compilata e fusa con una PR, come le altre prove.

## Limiti

- Una sola prova da zero, su un PC: non sostituisce la prova sul secondo sistema operativo (#2).
- I tempi dipendono dalla rete (download delle immagini Docker e dei pacchetti npm al primo avvio).
- Token, password e stringhe di connessione non vanno copiati in questo file né negli screenshot.

# Prova della mattina della consegna (#51)

> **Stato: da eseguire il 29/10/2026.** Procedura ed esiti attesi sono scritti prima della prova, come chiede
> [README.md](README.md). Si compila la colonna "Esito osservato" durante la prova.

| Campo | Valore |
|---|---|
| Cosa verifica | Due criteri di #51: "avvio da zero provato la mattina della consegna" e "demo con prove effettivamente eseguite"; la presentazione nei tempi |
| Ambiente | _sistema operativo, .NET SDK, Docker, Node_ |
| Commit di `main` | _hash del commit usato per la demo_ |
| Chi l'ha eseguita | _nome_ |
| Data e ora | _29/10/2026, ora_ |

## 1. Avvio da zero (prima della daily delle 9:30)

Su una cartella nuova, senza riusare quella di lavoro, così si prova davvero il README.

| # | Passo | Esito atteso | Esito osservato |
|---|---|---|---|
| 1 | `git clone` del repository in una cartella nuova, poi `git log -1 --format=%h` | Clone riuscito; l'hash è quello di `main` su GitHub | |
| 2 | `docker compose down -v` nella cartella di lavoro di prima, per partire da database vuoti | Volumi `smartcampus_*` rimossi | |
| 3 | `.env` da `.env.example`, con 5 password nuove e `JWT_SECRET` vuota (README, passo 2) | File compilato | |
| 4 | `scripts/dev-up` (README, passo 3), cronometrato | 4 `OK` e "Sistema avviato"; il tempo totale si scrive qui | |
| 5 | `http://localhost:4200`, accesso come Giulia Rossi | Catalogo visibile; seed caricato (spazi e prenotazioni sintetiche) | |
| 6 | `dotnet test SmartCampus.slnx` e `npm test -- --watch=false` in `src/web` | Tutti i test superati, con il numero scritto qui | |
| 7 | CI di `main` su GitHub | Ultimo run verde su Backend, Frontend e Controllo PR | |

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

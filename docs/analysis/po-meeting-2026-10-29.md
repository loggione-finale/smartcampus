# Incontro con il Product Owner – giovedì 29/10/2026

> Materiale per la Sprint Review con il PO del giorno di consegna (#83). Recupera la Review dello Sprint 1, saltata
> il 27/10 ([process.md](../process.md)), e fa la Review dello Sprint 2. Le decisioni prese si registrano in
> [po-decisions.md](po-decisions.md) dalla riga 29 in poi. Durata indicativa: **20 minuti**.
>
> Il PO **concorda e valida**. Noi portiamo l'incremento provato e **proposte motivate**, non decisioni già prese.

## Scaletta

| Minuti | Punto | Chi presenta |
|---|---|---|
| 0–2 | Product Goal e cosa è cambiato dal 27/10 | asprenolapo |
| 2–10 | **Incremento**: percorso della demo, passi 1–15 di [demo-path.md](../demo-path.md), sul sistema avviato con `scripts/dev-up` | chi ha il passo nella colonna "Chi" |
| 10–16 | **Domande** 1–5 qui sotto | asprenolapo |
| 16–18 | Cosa resta incompleto e i limiti noti | asprenolapo |
| 18–20 | Riepilogo delle decisioni, letto ad alta voce | chi scrive il verbale |

Uno presenta, uno scrive le decisioni in diretta nella colonna "Decisione del PO" qui sotto. Subito dopo si copiano
in [po-decisions.md](po-decisions.md) (vedi "Dopo l'incontro").

## Domande per il PO

| # | Domanda | Proposta del gruppo | Perché | Se il PO dice no | Decisione del PO |
|---|---|---|---|---|---|
| 1 | **Orari delle fasce** (decisione 25) | Mattina 08:00–14:00, pomeriggio 14:00–19:00, sera 19:00–23:00, ora di Roma | Coprono l'orario delle lezioni e lo studio serale; sono già nel codice (#153) | Si cambiano le costanti delle fasce e i test collegati: lavoro piccolo, ma dopo la consegna | |
| 2 | **Prenotazione a cavallo di due fasce** (decisione 26) | Non ammessa: una prenotazione sta dentro una sola fascia | Rende semplice la regola "una prenotazione per fascia e per giorno" | Va ripensata la regola della fascia: sviluppo futuro | |
| 3 | **Limite per fascia anche per i partecipanti?** (decisione 27) | No, solo per chi prenota | I partecipanti sono già protetti dal vincolo "nessuna persona prenotata due volte" | Nuovo vincolo nel database: sviluppo futuro | |
| 4 | **Anticipo massimo** di una prenotazione | 14 giorni | Evita che pochi occupino il calendario con settimane di anticipo | Senza limite resta com'è oggi: va dichiarato come limite | |
| 5 | **Sprint Goal 2**, riformulato senza il rilascio automatico | La versione in [process.md](../process.md) | Il rilascio automatico è uscito dal perimetro con il brainstorming (decisione 17) | Si scrive la versione del PO | |

## Cosa portiamo come incremento

- **Sprint 1, rilascio `v0.1`**: catalogo e disponibilità, prenotazione singola e di gruppo senza conflitti, regole
  garantite dal database (nessuna sovrapposizione, una prenotazione per fascia e per giorno, capienza).
- **Sprint 2, rilascio `v0.2`** (bozza delle note in #86): login nel frontend, le mie prenotazioni, commento finale
  con blocco delle nuove prenotazioni, area admin (dashboard, avvisi, report, statistiche, gestione spazi,
  annullamento con email), inviti che scadono dopo due ore, email come file `.eml`, messaggi di servizio non
  disponibile, avvio con un comando (se #226 è su `main`).
- **Prove**: test automatici in CI, prove di concorrenza, di guasto e di isolamento dei database, percorso della
  demo sull'API ([evidence/](../evidence/README.md)).

## Cosa resta incompleto (da dire, non da nascondere)

Da aggiornare la mattina con lo stato di `main`:

- **Prove dell'interfaccia** su due sistemi operativi e su smartphone: si fanno il 29/10 ([demo-path.md](../demo-path.md)).
- **Flusso agentico** (#171): gli agenti sono nel repository; gli usi reali sono registrati solo se fatti dal gruppo.
- Lavori ancora in PR al momento dell'incontro (per esempio conferma dell'invito #107, annullamento dal web #40,
  strumenti nel form #111): se non sono su `main`, non si mostrano.
- Bug aperti: #224 (Spaces risponde `503` dopo 5,1 s con MongoDB fermo).
- Dichiarazione: i dati sono sintetici; verificano il prototipo e non dimostrano benefici reali per il campus.

## Richieste di revisione

Se il PO chiede di cambiare qualcosa, si scrive qui e poi in "Richieste di revisione dopo le review" di
[po-decisions.md](po-decisions.md), con chi se ne occupa. Il 29/10 non si aggiungono funzioni: le richieste diventano
issue per gli sviluppi futuri, salvo correzioni piccole che il gruppo accetta in daily.

| Richiesta | Da chi | Issue | Chi |
|---|---|---|---|
| | | | |

## Dopo l'incontro

1. Una riga nuova in [po-decisions.md](po-decisions.md) per ogni risposta (dalla 29). Le decisioni 25–27 non si
   cancellano: si aggiunge la riga che le conferma o le sostituisce.
2. Togliere da "Questioni aperte" le questioni chiuse.
3. In [process.md](../process.md): Sprint Goal 2 approvato e stato "Review fatta il 29/10" per i due Sprint.
4. Se cambia una regola: issue con la decisione del PO, owner dell'area, Priority e Day.
5. Rilascio `v0.2` dopo la Review (#86).

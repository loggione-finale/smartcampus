# Come lavoriamo

> Product Goal, Sprint e loro obiettivi, eventi e adattamenti di Scrum scelti dal gruppo (#84). Le decisioni prese
> con il PO sono nel [registro](analysis/po-decisions.md); la qualità minima di ogni incremento è nella
> [Definition of Done](definition-of-done.md).

## Product Goal

> Studenti e personale trovano e prenotano gli spazi del campus, anche per un gruppo, con una conferma affidabile
> e senza conflitti; il personale capisce come gli spazi vengono usati, distinguendo prenotazioni, uso dichiarato
> e dati mancanti.

Approvato dal PO il 27/10 (decisione 16).

## Sprint

| Sprint | Date | Sprint Goal | Stato |
|---|---|---|---|
| 1 | lun 26 – mar 27 | Uno studente trova uno spazio e lo prenota, da solo o in gruppo, con la certezza che nessuno spazio e nessuna persona siano prenotati due volte | Approvato dal PO (decisione 16). Review con il PO non ancora fatta: vedi sotto |
| 2 | mer 28 – gio 29 mattina | I gruppi confermano la partecipazione entro due ore, chi ha prenotato lascia il commento finale, e l'admin vede aule prenotate e libere, avvisi e statistiche d'uso, in modo sicuro e verificato | **Proposta da confermare con il PO**: la versione approvata citava il rilascio automatico, uscito dal perimetro con il brainstorming (decisione 17) |

## Eventi

| Evento | Quando | Durata | Chi | Esce |
|---|---|---|---|---|
| Sprint Planning | Inizio di ogni Sprint, dopo la Daily | 30 min | Tutto il gruppo | Sprint Goal e task del giorno sulla board (colonna Day) |
| Daily Scrum | Ogni giorno alle **9:30** | 15 min | Tutto il gruppo | Impedimenti e carichi ridistribuiti; rapporto del controllore del backlog ([docs/agents](agents/backlog-checker.md)) |
| Sprint Review | Fine di ogni Sprint, con il PO | 20 min | Gruppo e PO | Incremento ispezionato; decisioni e richieste di revisione nel [registro](analysis/po-decisions.md) |
| Retrospective | Dopo la Review | 20 min | Tutto il gruppo | Cosa tenere e cosa cambiare, con azioni assegnate (sezione sotto, #87) |
| Rilascio dell'incremento | Dopo la Review | — | asprenolapo | Tag `v0.N` e GitHub Release con cosa è incluso, incompleto e provato (#86) |

## Adattamenti rispetto a Scrum

Dichiarati come chiede la slide 32 ("pratiche adattate non equivalgono all'adozione integrale di Scrum").

| Scrum | Noi | Perché |
|---|---|---|
| Sprint di durata fissa, fino a un mese | Due Sprint di circa un giorno e mezzo | Il project work dura quattro giorni |
| Un Product Owner nel team | Un PO comune a tutti i gruppi, esterno al team | Dato dal contesto (slide 6 e 32) |
| Scrum Master | asprenolapo fa da **facilitatore**: organizza gli eventi, cura board e repository, aiuta a rimuovere gli impedimenti | Il gruppo può scegliere un facilitatore (slide 6) |
| Sprint Review alla fine dello Sprint | Si fa quando il PO è disponibile: se salta, la si recupera al primo confronto | Lo Sprint 1 si è chiuso senza Review per una lezione in aula (27/10) |
| Stima | Story point con una prima proposta, rivotati insieme | Poco tempo per il planning poker completo |
| Lavoro del team | Ogni area ha un owner e un revisore fisso (`.github/CODEOWNERS`); nessun merge senza la review di un altro membro | Evitare i silos (slide 69) |
| Strumenti | Board GitHub Projects con Status, Priority, Day e Story Point; agenti con permessi e controlli scritti ([docs/agents](agents/README.md)) | Trasparenza del lavoro; richiesta del professore (decisione 28) |

## Sprint 1: Review e Retrospective

**Review con il PO:** non fatta il 27/10, perché il professore ha usato la giornata per il Modulo 3 (agenti e
workflow). Da recuperare al primo confronto con il PO, insieme a queste questioni aperte (scaletta del 29/10 in
[po-meeting-2026-10-29.md](analysis/po-meeting-2026-10-29.md)):

- conferma delle fasce orarie e delle regole 25–27 (già nel codice con la #153);
- anticipo massimo di una prenotazione (proposta: 14 giorni);
- nuova formulazione dello Sprint Goal 2 (sopra).

**Retrospective (#87):** _da compilare dopo la retrospettiva del gruppo_.

| Cosa ha funzionato | Cosa cambiare | Azione | Chi |
|---|---|---|---|
| | | | |

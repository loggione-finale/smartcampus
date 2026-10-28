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
| 1 | lun 26 – mar 27 | Uno studente trova uno spazio e lo prenota, da solo o in gruppo, con la certezza che nessuno spazio e nessuna persona siano prenotati due volte | Approvato dal PO (decisione 16). Review fatta con il PO il 28/10 sera, insieme a quella dello Sprint 2 |
| 2 | mer 28 | I gruppi confermano la partecipazione entro due ore, chi ha prenotato lascia il commento finale, e l'admin vede aule prenotate e libere, avvisi e statistiche d'uso, in modo sicuro e verificato | **Approvato dal PO il 28/10** (decisione 33), a condizione che tutto sia coerente con le decisioni 29–32. Review fatta il 28/10 sera |
| 3 (iper-sprint) | mer 28 sera – gio 29 tarda mattinata | Il prodotto rispetta le regole confermate dal PO il 28/10 e la demo mostra solo ciò che è stato provato | Proposta del gruppo, da confermare nella PR del piano ([hyper-sprint-2026-10-29.md](hyper-sprint-2026-10-29.md)) |

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
| Sprint di durata fissa, fino a un mese | Due Sprint di circa un giorno e mezzo, più un iper-sprint di una notte e una mattina dopo la Review del 28/10 | Il project work dura quattro giorni; la Review con il PO è arrivata la sera prima della consegna |
| Un Product Owner nel team | Un PO comune a tutti i gruppi, esterno al team | Dato dal contesto (slide 6 e 32) |
| Scrum Master | asprenolapo fa da **facilitatore**: organizza gli eventi, cura board e repository, aiuta a rimuovere gli impedimenti | Il gruppo può scegliere un facilitatore (slide 6) |
| Sprint Review alla fine dello Sprint | Si fa quando il PO è disponibile: se salta, la si recupera al primo confronto | Lo Sprint 1 si è chiuso senza Review per una lezione in aula (27/10) |
| Stima | Story point con una prima proposta, rivotati insieme | Poco tempo per il planning poker completo |
| Lavoro del team | Ogni area ha un owner e un revisore fisso (`.github/CODEOWNERS`); nessun merge senza la review di un altro membro | Evitare i silos (slide 69) |
| Strumenti | Board GitHub Projects con Status, Priority, Day e Story Point; agenti con permessi e controlli scritti ([docs/agents](agents/README.md)) | Trasparenza del lavoro; richiesta del professore (decisione 28) |

## Sprint 1: Review e Retrospective

**Review con il PO:** non fatta il 27/10, perché il professore ha usato la giornata per il Modulo 3 (agenti e
workflow). **Recuperata il 28/10 sera**, insieme a quella dello Sprint 2, con la scaletta di
[po-meeting-2026-10-29.md](analysis/po-meeting-2026-10-29.md):

- fasce orarie e prenotazioni a cavallo confermate (decisioni 29 e 30);
- il limite per fascia vale anche per i partecipanti (decisione 31, cambia la proposta del gruppo);
- anticipo massimo di 15 giorni (decisione 32);
- Sprint Goal 2 approvato (decisione 33);
- nove richieste di revisione (R1–R9 nel [registro](analysis/po-decisions.md)), pianificate
  nell'[iper-sprint](hyper-sprint-2026-10-29.md).

**Retrospective (#87):** svolta il 27/10/2026 dal gruppo. Sintesi di cosa ha funzionato, dei punti critici emersi e delle azioni correttive concordate e applicate nello Sprint 2.

| Cosa ha funzionato | Cosa cambiare | Azione | Chi | Stato Sprint 2 |
|---|---|---|---|---|
| Dati sintetici condivisi hanno accelerato lo sviluppo parallelo | Seed duplicati e disallineati tra Bookings e Users | Unificare generatore e sorgente unica del seed di test (`#158`, `#187`) | fedeericodl, Rhaastrake | Applicata |
| Suite di test d'integrazione con database reali (PostgreSQL, MongoDB) | Test fragili per dipendenza dall'ora del giorno e isolamento dello stato | Rendere i test indipendenti dall'orario di sistema e garantire isolamento (`#154`, `#155`, `#207`) | FranDeSa456, fedeericodl | Applicata |
| Flusso di PR snello e rapido | PR con descrizioni eterogenee, checklist mancanti o formattazione alterata | Automatizzare il controllo fisso delle PR in CI con regole sintattiche e di processo (`check-pr.sh`, `#187`) | asprenolapo | Applicata |
| Componenti Angular modulari | Disallineamento tra validazioni client e contratti API backend (regole aule miste, formati) | Allineamento sistematico dei contratti OpenAPI/DTO e validazioni contestuali prima del merge (`#160`, `#163`, `#210`) | AldAndrew0, Leo-prova | Applicata |
| Adozione di strumenti di supporto AI per velocizzare review e test | Uso non regolamentato con rischio di allucinazioni o introduzione di regressioni | Definire un flusso agentico controllato con 7 ruoli, permessi espliciti, no segreti e schede di evidenza in `docs/evidence/agentic/` (`#171`, `#187`) | asprenolapo, Rhaastrake, antoStr | Applicata |

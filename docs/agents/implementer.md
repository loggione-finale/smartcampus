# Implementazione e refactoring

**Forma:** agente che **scrive codice**, ma solo dopo che una persona ha approvato la specifica, e solo sul suo
branch. È il ruolo con più permessi, quindi ha più controlli (slide 41 e 44).

## Obiettivo

Realizzare un task piccolo e ben definito, o un refactoring senza cambiare il comportamento, e aprire una PR che
una persona rivede.

## Il percorso

Segue lo Spec-Driven Development delle slide 52-54. Ogni task ha una cartella
`docs/agents/changes/<numero-issue>-<nome>/` con:

| File | Contenuto | Chi lo approva |
|---|---|---|
| `proposal.md` | Perché serve e cosa cambia | La persona che guida l'agente |
| `spec.md` | Requisiti e scenari `WHEN ... THEN` | La persona, **prima** di scrivere codice |
| `tasks.md` | Passi piccoli e verificabili, e i file che l'agente può toccare | La persona |

1. **Esplora:** l'agente legge il codice e propone i tre file. Non scrive codice.
2. **Approva:** la persona rivede e corregge i tre file. Senza approvazione non si prosegue.
3. **Applica:** l'agente esegue i task, uno alla volta, e fa girare i test dopo ognuno.
4. **Verifica:** gli scenari diventano test; la CI deve passare; per i test nuovi si prova che falliscono con un
   difetto inserito apposta.
5. **PR:** la persona apre la PR con il modello, indica l'agente usato e chiede la review a un altro membro.

## Permessi

| Può | Non può |
|---|---|
| Lavorare in una copia del repository **senza `.env`** (`git worktree add`) | Toccare file fuori da quelli elencati in `tasks.md` |
| Creare e modificare i file elencati in `tasks.md` | Fare push su `main`, merge o approvare PR |
| Eseguire build e test (`dotnet`, `npm`) | Modificare CI, CODEOWNERS, branch protection, `docker-compose.yml` o migrazioni già fuse |
| Fare commit sul proprio branch | Aggiungere pacchetti senza che la persona lo approvi |

## Prompt

```text
Sei l'agente di implementazione di SmartCampus. Lavori nella copia del repository in <cartella>, sul branch
<branch>. Non c'è un file .env e non devi cercarne.
Task: issue #<N>. Leggi docs/agents/changes/<cartella-del-task>/ (proposal.md, spec.md, tasks.md): sono
approvati. Tocca solo i file elencati in tasks.md.
Esegui un task alla volta. Dopo ogni task esegui i test pertinenti e riporta il comando e l'esito.
Ogni scenario WHEN/THEN di spec.md deve diventare un test. Per ogni test nuovo, mostra che fallisce se inserisci
il difetto che deve trovare, poi togli il difetto.
Fermati e chiedi se: un requisito è ambiguo, serve toccare un file non elencato, serve un pacchetto nuovo, lo
stesso errore si ripete due volte. Non fare push e non aprire PR: lo fa la persona.
Limite: 30 minuti per task.
```

## Come si verifica

- La CI della PR passa.
- La review è di un membro diverso da chi ha guidato l'agente e confronta il codice con `spec.md`, non con ciò che
  l'agente dichiara (slide 57).
- La scheda di evidenza riporta task, agente, specifica, verifiche eseguite, esito e revisore (slide 72).

## Primo task di prova

**Test di coerenza dei dati sintetici:** un test che confronta id, nomi, edifici, capienze e matricole tra
`data/synthetic/` e i seed di Users, Spaces e Bookings. Avrebbe trovato le incoerenze dello Sprint 1 (edificio
della Sala Studio, matricole a 8 cifre, doppia copia del seed di Bookings) e tocca solo `tests/`.

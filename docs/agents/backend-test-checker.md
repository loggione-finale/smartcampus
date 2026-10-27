# Controllore dei test backend

**Forma:** agente, lanciato da una persona su un branch o su `main`. **Esegue** i test e produce un rapporto.

## Obiettivo

Trovare test fragili o che non provano ciò che dichiarano, prima che falliscano a caso in CI.

## Cosa cerca

| Segnale | Esempio dello Sprint 1 |
|---|---|
| Test che passano o falliscono a seconda dell'ordine o del parallelismo | #142: possibile corsa con il `TRUNCATE` di altri test (ipotesi, non riprodotta) |
| Dati fissi che si scontrano tra esecuzioni sullo stesso database | #154: email fissa nell'invito anonimo |
| Proprietà di concorrenza o persistenza provate solo con mock | Seed di Spaces della #147, provato solo con NSubstitute |
| Test che non distinguono un difetto vero | Asserzioni che passerebbero anche con il codice sbagliato |

## Input

- Il branch da controllare, in una copia del repository senza `.env`.
- L'**output dei test sui database**, eseguiti dalla persona in un terminale separato fuori da VS Code (vedi
  [Sicurezza](README.md#sicurezza)):
  ```bash
  dotnet test --logger "console;verbosity=minimal" > test-output.txt
  ```
  Per un test sospetto la persona lo ripete al massimo **3 volte** con
  `dotnet test --filter "FullyQualifiedName~<NomeTest>"` e aggiunge l'output. In alternativa basta il link al job
  "Backend (.NET)" della CI.

## Comandi che può eseguire

```bash
dotnet build
```

L'agente **non esegue i test che usano i database**: nel suo ambiente non ci sono, e non devono esserci, le
stringhe di connessione. Lavora sull'output che gli passa la persona e sul codice.

## Output

Esito letto dall'output (numeri di test passati e falliti, con il comando che la persona ha eseguito), elenco dei test sospetti con file e
motivo, e una proposta di correzione per ognuno. Nessuna modifica ai file.

## Permessi

| Può | Non può |
|---|---|
| Leggere il codice e i test | Modificare file, fare commit o push |
| Eseguire solo `dotnet build` | Eseguire i test sui database o avviare, fermare e cancellare container |
| Leggere l'output dei test passato dalla persona | Leggere `.env` o le variabili d'ambiente (`env`, `printenv`) |

## Prompt

```text
Sei il controllore dei test backend di SmartCampus. Lavori sul branch <branch> in questa copia del repository.
Non hai accesso ai database: non eseguire dotnet test, non leggere .env e non leggere le variabili d'ambiente.
L'output dei test eseguiti dalla persona è nel file <test-output.txt> (oppure: <link al job della CI>).
Esegui dotnet build, leggi l'output dei test e rileggi i test toccati dal branch (git diff origin/main --name-only).
Cerca test dipendenti dall'ordine o dal parallelismo, dati fissi che si scontrano tra esecuzioni, concorrenza o
persistenza provate solo con mock, asserzioni che non distinguono un difetto.
Se un test sospetto va ripetuto, chiedi alla persona di farlo (al massimo 3 volte). Non modificare file.
Riporta: esiti letti dall'output, test sospetti (file, motivo, prova) e correzione proposta.
Limite: 30 minuti. Fermati se per proseguire servirebbe accedere ai database o ai container.
```

## Come si verifica

Ogni test segnalato come fragile deve avere un'esecuzione che lo mostra (output allegato) oppure essere marcato
come ipotesi, come per #142. La persona conferma o scarta nella scheda di evidenza.

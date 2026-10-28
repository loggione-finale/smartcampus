# Controllore delle PR

**Forma:** controllo fisso in CI, senza LLM: workflow [`pr-check.yml`](../../.github/workflows/pr-check.yml),
job "Controllo PR". Non è un agente: le regole sono sempre le stesse e non serve ragionare per applicarle
(slide 50).

## Obiettivo

Fermare subito le PR con errori di forma che oggi scopriamo solo in review.

## Cosa controlla

| Regola | Perché | Esito se manca |
|---|---|---|
| Il titolo inizia con un tipo (`feat:`, `fix:`, `test:`, `docs:`, `chore:`, `refactor:`, `ci:`) | Storia dei commit leggibile dopo lo squash | Errore |
| Il corpo collega una issue con `Closes #N` o `Refs #N` | Board e issue si aggiornano da sole | Errore |
| Il corpo non contiene il segnaposto `Closes #` senza numero | Modello lasciato com'era | Errore |
| Il corpo non contiene `` `n `` o `` `r `` | Testo rovinato da PowerShell (#149) | Errore |
| Il branch segue `<tipo>/<numero-issue>-<descrizione>` in minuscolo | `CONTRIBUTING.md`, sezione 2 | Errore |
| Branch, titolo e messaggi di commit non contengono parole offensive ([elenco](../../.github/scripts/blocked-words.txt)) | Nomi che restano nella storia del repository | Errore |
| Il corpo non ha nomi tra `` o caratteri di controllo al posto dei backtick | Testo passato da PowerShell (#128, #175) | Avviso |
| "Closes #N" non compare dentro una frase | Con lo squash il corpo diventa il messaggio di commit e chiude la issue (#171) | Avviso |
| C'è una sezione con le verifiche eseguite o almeno una voce spuntata | DoD: test eseguiti, non solo scritti | Avviso |

La regola sulle verifiche è un avviso e non un errore: provata sulle ultime 40 PR, come errore ne avrebbe bloccate 13
che descrivevano le verifiche con parole loro. Sulle ultime 45 PR le regole di errore fermano solo due branch che
davvero non seguono la convenzione (`docs/evidence-booking-concurrency-36`, `feature/agentic-workflow`) e nessun'altra
PR; gli avvisi segnalano solo corpi davvero rovinati (#141, #175) e la frase che ha chiuso per sbaglio la #171.

L'elenco delle parole vietate si confronta su parole intere: "cazzuola" o "shift" non fanno scattare il controllo.
Per aggiungere una parola si modifica `.github/scripts/blocked-words.txt` con una PR. Il confronto usa la locale
UTF-8, quindi le lettere accentate contano come lettere e non come separatori.

**Branch dei bot:** i branch che iniziano con `dependabot/` sono esclusi dalla regola sul nome, così Dependabot
funziona se un giorno lo attiviamo. Il cloud agent di Copilot non si usa, quindi i branch `copilot/...` restano
vietati: se comparissero, il controllo li fermerebbe.

## Permessi

Legge solo titolo, corpo, nome del branch e messaggi di commit della PR, passati come variabili d'ambiente. Non ha token con permessi di
scrittura e non commenta la PR: l'esito si legge nel job.

## Quando fermarsi

Il controllo non decide niente oltre a queste regole. Se una regola blocca una PR per un buon motivo (per esempio
una PR senza issue), si discute nel gruppo e si cambia la regola con una PR, non si aggira.

## Come si verifica

Lo script si prova anche in locale, senza GitHub:

```bash
PR_TITLE="feat: prova" PR_BODY="Closes #" bash .github/scripts/check-pr.sh
```

Un corpo con il modello non compilato deve fallire; lo stesso corpo con `Closes #24` e una verifica spuntata deve
passare.

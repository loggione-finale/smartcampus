# Come contribuire a SmartCampus

Queste regole valgono per tutti i membri del team, owner compresi.

## 1. Flusso di lavoro

1. **Parti da una issue.** Ogni lavoro nasce da una issue del backlog (storia, task o bug).
   Assegnatela e spostala in **In Progress** sulla board
   [Loggione Roadmap](https://github.com/orgs/loggione-finale/projects/1).
   Quando apri la PR spostala in **In Review**; dopo il merge va in **Done**.
2. **Crea un branch da `main` aggiornato.**
   ```bash
   git switch main
   git pull
   git switch -c feature/42-room-search
   ```
3. **Lavora con commit piccoli** e fai push del branch.
4. **Apri una pull request verso `main`** e collega la issue scrivendo `Closes #42` nella descrizione.
5. **Chiedi la review** a un altro membro del team.
6. **Dopo l'approvazione**, l'autore fa il merge con **Squash and merge**.
   Il branch viene cancellato automaticamente.

Su `main` non si fa mai push diretto: GitHub lo blocca.

### Lingua

- **Nomi in inglese**: file, cartelle, progetti, namespace, classi, metodi, variabili, rotte API, tabelle
  e colonne, branch, messaggi di commit, label.
- **Testi in italiano**: documentazione, issue, descrizioni delle PR, commenti di review, testi dell'interfaccia.

## 2. Nomi dei branch

`<tipo>/<numero-issue>-<descrizione-breve>`, tutto minuscolo con trattini.

| Tipo | Uso |
|---|---|
| `feature/` | Nuova funzionalità |
| `fix/` | Correzione di un difetto |
| `docs/` | Solo documentazione |
| `test/` | Solo test o dati sintetici |
| `chore/` | Configurazione, dipendenze, manutenzione |

Esempio: `fix/57-double-booking-same-room`.

## 3. Messaggi di commit e titolo della PR

Con lo squash, **il titolo della PR diventa il messaggio del commit su `main`**: deve essere chiaro.
Usiamo il formato [Conventional Commits](https://www.conventionalcommits.org/it/):

```
feat: search free rooms by time slot
fix: prevent double booking of the same room
docs: add Definition of Done
test: synthetic data for overlapping bookings
```

## 4. Pull request

- Una PR = un argomento. Se cresce troppo, dividila.
- Compila il template: cosa cambia, perché, come l'hai verificato, limiti noti.
- La PR deve compilare e i test devono passare **in locale** prima di chiedere la review.
- Se la PR non è pronta, aprila come **Draft**.

## 5. Review

- **Ogni PR richiede almeno 1 approvazione di un membro diverso dall'autore.**
  GitHub non permette di approvare la propria PR e blocca il merge senza approvazione.
- Se dopo l'approvazione arrivano nuovi commit, l'approvazione decade e serve una nuova review.
  Chi ha fatto l'ultimo push non può essere chi approva.
- Chi fa la review:
  - legge il diff, non solo la descrizione;
  - verifica che i criteri di accettazione della issue siano rispettati;
  - se può, esegue i test in locale;
  - usa **Request changes** quando qualcosa va corretto, **Approve** quando è pronto.
- Ruotiamo i revisori: non sempre la stessa persona.

## 6. Merge

- Si usa **solo Squash and merge** (le altre modalità sono disattivate).
- Il merge lo fa l'autore, dopo l'approvazione.
- Il branch remoto viene cancellato automaticamente; cancella anche quello locale:
  ```bash
  git switch main
  git pull
  git branch -d feature/42-room-search
  ```
- Se GitHub segnala conflitti, aggiorna il branch con `main` prima del merge.

## 7. Definition of Done

Una storia è "Done" solo se rispetta la [Definition of Done](docs/definition-of-done.md).
Un elemento non conforme alla DoD non fa parte dell'incremento.

## 8. Sicurezza e dati

- **Solo dati sintetici** prodotti dal gruppo. Nessun dato personale reale.
- **Nessun segreto** nel repository: password, token, connection string vanno in
  `dotnet user-secrets` o in variabili d'ambiente. Fornisci un file di esempio senza valori reali.
- Se pubblichi per errore un segreto, avvisa subito il team: va revocato, non basta cancellare il commit.

## 9. Uso di strumenti AI

Consentito, ma la responsabilità resta di chi apre la PR: deve saper spiegare ogni riga e aver
eseguito davvero le verifiche dichiarate. Un "test passa" va dimostrato, non affermato.

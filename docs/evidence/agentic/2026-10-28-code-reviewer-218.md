# Uso di un agente: code-reviewer su PR #218

| Campo | Valore |
|---|---|
| Data | 28/10/2026 |
| Ruolo | [code-reviewer](../../agents/code-reviewer.md) |
| Task | PR #218, revisione del gestore 503 per indisponibilità PostgreSQL (#213) |
| Chi ha guidato l'agente | antoStr |
| Strumento | GitHub Copilot CLI / Claude 3.5 Sonnet |
| Durata | 15 minuti |
| Consumo | non disponibile |

## Input dato all'agente

- Diff della PR: `git diff origin/main...origin/fix/213-database-unavailable-503`
- Issue #213 e scenario 5 delle prove di guasto (#66)
- File `DatabaseUnavailableExceptionHandler.cs`, `Program.cs`, test unitari e di integrazione
- Nessun segreto né dato personale reale presente nei file o comandi forniti

## Cosa ha proposto

L'agente ha analizzato il diff verificando:
1. Impostazione del timeout di connessione (3s) e configurazione della policy di retry (2 tentativi, max 1s) in `Program.cs`.
2. Implementazione di `DatabaseUnavailableExceptionHandler` con mapping dei codici SQL e verifica dell'header `Retry-After: 5`.
3. Isolamento delle eccezioni di query/constraint (codici `23505`, `23P01`, `42P01`) che devono rimanere non gestite (500) per evitare mascheramenti.
4. Esito dei test unitari e di integrazione che simulano l'interruzione della connessione tramite interceptor.

## Verifiche eseguite

| Affermazione dell'agente | Come l'ho controllata | Esito |
|---|---|---|
| Le eccezioni di violazione vincolo (23505, 23P01) non vengono catturate dal gestore | Ispezione di `IsDatabaseUnavailable` e test `Constraint_and_query_errors_are_not_treated_as_unavailable` | Confermata |
| Il timeout di 3 secondi viene applicato solo se non già presente nella stringa di connessione | Ispezione di `Program.cs` in Bookings e Users | Confermata |
| I test automatici passano e il tempo di risposta con DB disconnesso è inferiore a 8s | Esecuzione test in CI su GitHub Actions | Confermata |
| L'header `Retry-After: 5` è presente nella risposta HTTP 503 | Ispezione del metodo `TryHandleAsync` in `DatabaseUnavailableExceptionHandler.cs` | Confermata |

## Esito

- **Accettato:** Approvazione formale della PR #218 con sintesi tecnica delle verifiche effettuate su resilienza, codici SQL e suite di test.
- **Scartato:** Suggerimento di fattorizzare l'handler in una libreria condivisa comune: scartato per evitare modifiche a `SmartCampus.Auth` a ridosso della consegna.
- **Interruzioni:** Nessuna.

## Revisore

Rhaastrake (autore della PR e del commit)

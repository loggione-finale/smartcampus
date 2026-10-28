# 0008 – Utenti e accesso: registrazione e login con token firmato

- Data: 27/10/2026
- Stato: accettata

## Contesto
La proposta iniziale (in [architecture.md](../architecture.md), D8) era un profilo sintetico scelto all'ingresso, senza registrazione. Nell'incontro del 27/10 il PO ha approvato il perimetro del brainstorming, che comprende registrazione e login (decisioni 14 e 17), con telefono obbligatorio (18) e matricola di esattamente 10 cifre (19).

## Decisione
- **Registrazione** nel servizio Users con validazione di matricola, nome, cognome, email, telefono e password; password salvata solo come hash.
- **Login con email o matricola** e token **JWT firmato** valido 8 ore, con ruolo `User` o `Admin`; la libreria `SmartCampus.Auth` valida il token in tutti i servizi e definisce la policy Admin.
- **Gli admin non si registrano:** arrivano dal seed dei dati sintetici, come gli utenti demo.

## Alternative considerate
- **Profili sintetici scelti all'ingresso** (header `X-User-Id`): nessuna sicurezza vera; superata dalla decisione 17.
- **Account esterni o login con un provider:** esclusi dalle slide (nessun account o servizio esterno).

## Conseguenze
- Più facile: ruoli e permessi veri su ogni endpoint (`401`/`403` provati nei test).
- Più difficile: la chiave di firma va gestita come segreto (`dotnet user-secrets` o variabili d'ambiente, mai nel repository).
- Da aggiornare: la sezione D8 di `architecture.md` descrive ancora i profili sintetici e i ruoli `Student`/`Staff`.

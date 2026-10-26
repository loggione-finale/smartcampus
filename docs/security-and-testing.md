# Sicurezza e test per ogni componente

> **Stato: bozza da rivedere insieme.** Per ogni parte del codice: quali rischi ci sono, come li riduciamo
> e quali test dimostrano che funziona. Solo strumenti di base di .NET e Angular.
> Ogni prova eseguita va registrata in [`evidence/`](evidence/) con esito atteso e osservato.

## Regole valide per tutto il codice

| Regola | Come |
|---|---|
| **Nessun segreto nel repository, nei log o nei prompt** | Password e stringhe di connessione in `.env` (non versionato) o `dotnet user-secrets`; `.env.example` senza valori reali. Secret scanning e push protection di GitHub già attivi. Non incollare segreti negli strumenti AI |
| **Solo dati sintetici** | Nessun nome, email o dato reale, nemmeno nei test e negli screenshot |
| **Input sempre validato sul server** | DTO dedicati con Data Annotations (`[Required]`, `[Range]`, `[StringLength]`); il frontend valida per comodità, il server per sicurezza |
| **Mai esporre le entità del database** | Le API ricevono e restituiscono DTO: niente campi modificabili per sbaglio (ad esempio `status` o `organizerId` inviati dal client) |
| **Errori senza dettagli interni** | Risposte `ProblemDetails` con un messaggio per l'utente; niente stack trace fuori da `Development` |
| **Log utili ma puliti** | Si registrano id, operazione ed esito; mai header di autorizzazione, password o stringhe di connessione |
| **Dipendenze controllate** | Dependabot attivo; in CI `dotnet list package --vulnerable` e `npm audit --audit-level=high` |
| **Orari in UTC** | `timestamptz` nel database, UTC nelle API, conversione in Europe/Rome solo nel frontend |

## Tipi di test

| Tipo | Che cosa verifica | Strumento | Dove gira |
|---|---|---|---|
| **Unitario** | Regole di dominio in isolamento: fascia valida, capienza, chi può annullare | xUnit | Locale e CI |
| **Integrazione** | API + database **reale** (database `*_test` sul PostgreSQL di Docker Compose) | xUnit con `WebApplicationFactory` di ASP.NET Core; se non l'abbiamo visto nel corso, test sulle classi di servizio con il `DbContext` reale | Locale e CI |
| **Concorrenza** | Molte richieste simultanee: ne passa esattamente una (spazio e persona) | xUnit con `Task.WhenAll` sul database reale | Locale e CI |
| **Tra servizi** | Comportamento quando l'altro servizio risponde, sbaglia o non risponde | xUnit con un `HttpMessageHandler` finto al posto di Spaces/Users | Locale e CI |
| **Sicurezza** | Permessi per ruolo, accesso a dati altrui, input malevoli | xUnit (integrazione) | Locale e CI |
| **Frontend** | Servizi Angular, guard, interceptor, componenti principali | `ng test` | Locale e CI |
| **Guasto** | Che cosa succede se un servizio o il database si ferma | Manuale, con checklist | Locale, esito in `evidence/` |
| **Manuale end-to-end** | I percorsi principali dall'interfaccia, su due sistemi operativi e su smartphone | Checklist scritta | Locale, esito in `evidence/` |

---

## Frontend (Angular + Angular Material)

**Sicurezza**

| Rischio | Contromisura |
|---|---|
| XSS: testo inserito da un utente (nome di uno spazio, motivo di indisponibilità) eseguito come codice | Usare solo l'interpolazione di Angular (`{{ }}`), che fa l'escape automatico; **mai** `[innerHTML]` né `bypassSecurityTrust*` |
| Funzioni del personale visibili a uno studente | Route guard per ruolo. È solo comodità: **il controllo vero è sul server** |
| Utente e ruolo falsificabili dal browser (W1) | Accesso con password demo e **token firmato** emesso da Users (SC-20, #79); il ruolo arriva dal token, non dal browser. Finché SC-20 non è fatta: limite dichiarato |
| Chiamate a servizi esterni non previsti | Il frontend chiama solo `/api/...` tramite il proxy; nessuna URL assoluta nel codice |
| Dipendenze npm vulnerabili | `npm audit` in CI; versioni bloccate da `package-lock.json` |

**Test**

| Cosa | Esempi | Tipo |
|---|---|---|
| Interceptor | Aggiunge `X-User-Id` e `X-User-Role` a ogni richiesta | `ng test` |
| Guard | Uno studente viene respinto dalle pagine del personale | `ng test` |
| Gestione errori | `503`, `409` e timeout producono il messaggio giusto | `ng test` |
| Form di prenotazione | Fine prima dell'inizio, campi vuoti, troppi partecipanti | `ng test` |
| Conversione orari | 10:00 a Roma diventa 09:00 UTC (08:00 in estate) e viceversa | `ng test` |
| Portabilità | Percorsi principali su due sistemi operativi e su uno smartphone | Manuale |

## Servizio Spaces

**Sicurezza**

| Rischio | Contromisura |
|---|---|
| Uno studente crea, modifica o disattiva spazi | Controllo del ruolo `Staff` su ogni endpoint di scrittura (`403` altrimenti) |
| Dati non validi (capienza negativa, nome vuoto, testo enorme) | Data Annotations sui DTO, `400` con `ProblemDetails` |
| Iniezione nelle query (SQL o MongoDB) | EF Core usa query parametrizzate; con MongoDB solo filtri tipizzati (`Builders<T>.Filter`), mai query costruite concatenando stringhe |
| Cancellazione che rompe i riferimenti di Bookings | Nessun `DELETE`: solo disattivazione |
| Filtri di ricerca abusati (pagine enormi) | Paginazione con dimensione massima |

**Test**

| Cosa | Tipo |
|---|---|
| Filtri combinati per tipo, capienza e attrezzatura | Integrazione |
| Spazio disattivato escluso dalla ricerca ma ancora leggibile per id | Integrazione |
| Scrittura con ruolo `Student`: `403`; senza header: `401` | Sicurezza |
| Capienza `0` o negativa, nome di 10.000 caratteri: `400` | Sicurezza |
| Indisponibilità con fine prima dell'inizio: `400` | Unitario |
| Seed: due avvii non duplicano i dati | Integrazione |

## Servizio Bookings (il più critico)

**Sicurezza**

| Rischio | Contromisura |
|---|---|
| **Accesso a prenotazioni altrui** (IDOR): cambiare l'id nella URL per vedere, annullare o fare check-in su una prenotazione non propria | Ogni operazione verifica che l'utente sia organizzatore o partecipante; altrimenti `404` (non si rivela che la prenotazione esiste) |
| Un partecipante annulla l'intera prenotazione | Solo `organizer_id` può annullare; un partecipante può solo uscire |
| Il client invia campi che non gli spettano (`status`, `organizerId`, `checkedInAt`) | DTO di input senza questi campi; l'organizzatore è sempre l'utente corrente |
| Prenotazioni sovrapposte per richieste simultanee | Vincoli di esclusione nel database (spazio e persona) |
| Abuso delle prenotazioni di gruppo (W6) | Il partecipante può uscire; proposta: limite di prenotazioni future per persona |
| Statistiche viste da uno studente | `GET /bookings/stats` solo per `Staff` |
| Dati personali nelle statistiche | Le statistiche restituiscono conteggi, non elenchi di persone |
| Servizio lento o giù che blocca Bookings | Timeout di 3 s sulle chiamate a Spaces e Users, `503` chiaro |

**Test**

| Cosa | Tipo |
|---|---|
| Fascia con fine prima dell'inizio, nel passato, di durata zero: `400` | Unitario |
| Prenotazione su una fascia libera: `201`; su una fascia occupata: `409` | Integrazione |
| **Molte richieste simultanee sullo stesso spazio e fascia: esattamente una accettata** | Concorrenza |
| **Molte prenotazioni simultanee con la stessa persona: esattamente una accettata** | Concorrenza |
| Prenotazioni adiacenti (10–11 e 11–12): entrambe accettate | Integrazione |
| Gruppo oltre la capienza: `422`; partecipante inesistente: `422` | Integrazione + tra servizi |
| Spaces non risponde: `503`; Users non risponde: `503` solo per i gruppi | Tra servizi |
| Annullare o fare check-in su una prenotazione altrui: `404` | Sicurezza |
| Un partecipante prova ad annullare: `403`; l'organizzatore prova a uscire: `400` | Sicurezza |
| Statistiche con ruolo `Student`: `403` | Sicurezza |
| Statistiche su dati sintetici noti: numeri uguali a quelli calcolati a mano | Integrazione |
| Prenotazione nella notte del cambio dell'ora legale | Unitario |
| Il test di concorrenza **fallisce** se si rimuove il vincolo | Verifica del test stesso |

## Servizio Users

**Sicurezza**

| Rischio | Contromisura |
|---|---|
| Elenco dei profili esposto a tutti | Accettato: sono profili sintetici; si restituiscono solo id, nome e ruolo, mai l'email agli studenti |
| Ricerca usata per scaricare tutti i profili | Ricerca con almeno 2 caratteri e massimo 20 risultati |
| Profili creati o modificati da chiunque | Nessuna API di scrittura: i profili arrivano solo dal seed |

**Test**

| Cosa | Tipo |
|---|---|
| Ricerca per nome, minimo 2 caratteri, massimo 20 risultati | Integrazione |
| `GET /users?ids=` con un id inesistente: lo segnala | Integrazione |
| Nessun dato reale nel seed (controllo sul file) | Unitario |

## Database e infrastruttura

**Sicurezza**

| Rischio | Contromisura |
|---|---|
| Un servizio legge i dati di un altro | Ogni servizio ha la propria stringa di connessione; se c'è tempo, un utente PostgreSQL per database con permessi solo sul proprio |
| Database raggiungibile dalla rete | In `docker-compose.yml` la porta è esposta solo su `127.0.0.1` |
| Password predefinite | Password scelta in `.env`, diversa per ogni macchina |

**Test**

| Cosa | Tipo |
|---|---|
| `docker compose up -d` da zero crea i database | Manuale, in `evidence/` |
| Migrazioni e vincoli presenti dopo l'avvio | Integrazione |
| PostgreSQL fermo: i servizi rispondono `503` e l'health check fallisce | Guasto |

## Repository e CI

| Controllo | Stato |
|---|---|
| Protezione di `main`: PR, 1 approvazione di un altro membro, niente force push | Attivo |
| Secret scanning e push protection | Attivo |
| Dependabot alert e aggiornamenti di sicurezza | Attivo |
| Build e test di tutti i servizi e del frontend su ogni PR | Da fare (SC-14) |
| Controllo delle dipendenze vulnerabili in CI | Da fare (SC-14) |

---

## Copertura minima prima della consegna

Non contiamo le righe coperte: contiamo i **comportamenti** dimostrati. Prima del 29/10 devono esistere,
eseguiti e registrati:

1. i due test di concorrenza (spazio e persona) sul database reale;
2. i test di sicurezza di Bookings (accesso altrui, ruoli, campi non consentiti);
3. i test tra servizi (Spaces e Users che non rispondono);
4. le prove di guasto per ogni servizio;
5. il percorso manuale completo su due sistemi operativi e su uno smartphone.

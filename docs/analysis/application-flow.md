# Flusso dell'applicativo e punti deboli

> **Stato: bozza da rivedere insieme.** Il flusso segue l'architettura proposta in
> [architecture.md](../architecture.md). I punti deboli (W1–W8) sono ordinati lungo il percorso dell'utente.

## Flusso completo

```mermaid
flowchart TD
    A([Utente apre il frontend]) --> B[Sceglie il profilo<br/>GET /api/users]
    B -->|Studente| C[Cerca e filtra gli spazi<br/>GET /api/spaces]
    B -->|Personale| P1[Gestisce spazi e indisponibilità<br/>Spaces]
    C --> D[Guarda la disponibilità del giorno<br/>GET /api/bookings/...]
    D --> E{Singola o di gruppo?}
    E -->|Singola| F[POST /api/bookings]
    E -->|Gruppo| E2[Cerca i partecipanti<br/>GET /api/users?search=] --> F
    F --> G[Bookings valida la fascia]
    G --> H[Bookings chiede a Spaces:<br/>esiste, è attivo, capienza, indisponibilità]
    H -->|Gruppo| I[Bookings chiede a Users:<br/>i partecipanti esistono]
    H -->|Singola| J
    I --> J[INSERT prenotazione + partecipanti<br/>una transazione]
    J --> K{Vincoli di esclusione}
    K -->|Spazio occupato| X1[409 fascia occupata]
    K -->|Persona impegnata| X2[409 Mario Rossi è già impegnato]
    K -->|Libero| L[201 confermata]
    L --> M[Le mie prenotazioni<br/>organizzatore o partecipante]
    M --> N1[Annulla<br/>solo organizzatore]
    M --> N2[Esci dal gruppo<br/>solo partecipante]
    M --> O[Check-in all'inizio]
    P1 --> P2[Report di utilizzo<br/>GET /api/bookings/stats]
    O -.->|alimenta| P2
    N1 -.->|alimenta| P2

    classDef weak fill:#FCEBEB,stroke:#A32D2D,color:#501313
    W1[W1 identità non verificata]:::weak -.- B
    W2[W2 servizio giù]:::weak -.- H
    W3[W3 fusi orari]:::weak -.- D
    W4[W4 controllo poi modifica]:::weak -.- J
    W5[W5 PostgreSQL unico]:::weak -.- K
    W6[W6 tempo altrui bloccato]:::weak -.- E2
    W7[W7 prenotato ma vuoto]:::weak -.- O
    W8[W8 dati dichiarati]:::weak -.- P2
```

## Punti deboli e risoluzioni

| ID | Dove | Problema | Risoluzione | Issue | Rischio residuo |
|---|---|---|---|---|---|
| **W1** | Scelta del profilo | Utente e ruolo arrivano da header `X-User-*` che chiunque può falsificare | Accesso con password demo; Users rilascia un **token firmato (JWT)** con id e ruolo; i servizi accettano solo token validi | SC-20 (#79) | Password demo note a tutti: va bene per una demo con dati sintetici. Finché SC-20 non è fatta, limite dichiarato |
| **W2** | Chiamate tra servizi | Bookings ha bisogno di Spaces (e di Users per i gruppi) per creare una prenotazione | Timeout di 3 s, `503` con messaggio chiaro, pagine indipendenti nel frontend, prove di guasto | #32, #60, #65, #66 | Con Spaces giù non si prenota: scelta voluta per non confermare prenotazioni sbagliate |
| **W3** | Disponibilità e prenotazione | Fusi orari e ora legale spostano le fasce | UTC in database e API, Europe/Rome solo nel frontend, fasce da 15 minuti, test sul cambio d'ora | SC-06.T4 (#95), criterio in SC-06 (#7) | Nessuno, se il test sul cambio d'ora passa |
| **W4** | Tra la verifica e il salvataggio | Uno spazio disattivato mentre una prenotazione è in corso passa lo stesso | Spaces **prima disattiva** lo spazio, poi chiede a Bookings di annullare le prenotazioni future; se Bookings non risponde, ripristina e rifiuta | SC-19 (#75) | Una prenotazione verificata pochi millisecondi prima della disattivazione può sfuggire: limite dichiarato. Da confermare con il PO (domanda 4) |
| **W5** | Database | Un solo container PostgreSQL per Bookings e Users | MongoDB separato per Spaces; riavvio automatico, health check e volumi in Compose; `EnableRetryOnFailure`; un utente per servizio | SC-01.T7 (#94), [data-model.md](../data-model.md#sicurezza-dei-database) | Se PostgreSQL cade, prenotazioni e profili si fermano finché non riparte: accettabile in locale |
| **W6** | Prenotazione di gruppo | Chi organizza può bloccare la fascia di altre persone aggiungendole | Il partecipante vede chi l'ha aggiunto e può uscire; massimo 5 prenotazioni di gruppo future per organizzatore | SC-16 (#58), SC-16.T2 (#60), SC-16.T3 (#61) | Un abuso resta possibile fino al limite; gli inviti da accettare restano un'alternativa da proporre al PO |
| **W7** | Dopo la prenotazione | Prenotazione senza check-in: spazio "apparentemente occupato" (slide 7) | **Rilascio automatico** 15 minuti dopo l'inizio senza check-in | SC-17 (#68) | Chi arriva in ritardo di oltre 15 minuti perde la prenotazione: soglia da confermare con il PO |
| **W8** | Report | Il check-in è dichiarato, nessuno verifica che sia vero | Il report lo dice esplicitamente e conta a parte le prenotazioni rilasciate; misure documentate e verificate a mano | SC-13 (#14), SC-13.T3 (#97) | L'uso reale non è misurabile senza sensori, che sono fuori perimetro |

## Che cosa ci dice il flusso

- Il punto più solido è il **salvataggio**: le due regole critiche (spazio e persona) sono garantite dal database
  in una sola transazione, anche con richieste simultanee.
- Tutti gli otto punti deboli hanno ora una **risoluzione nel backlog**. Restano dei rischi residui, che vanno
  **dichiarati** nei limiti noti e nella presentazione.
- W4, W6 e W7 dipendono da decisioni del **PO** (domanda 4, inviti, soglia dei 15 minuti): vanno portate
  all'incontro previsto in SC-21.T2 (#85).

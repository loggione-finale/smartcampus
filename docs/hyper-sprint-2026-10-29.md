# Iper-sprint: mercoledì 28/10 sera – giovedì 29/10 tarda mattinata

> **Proposta da discutere con tutto il gruppo, non un piano già deciso.** La Review con il PO del 28/10 sera ha
> confermato le regole principali e ha chiesto nove correzioni (R1–R9). Questo documento spiega cosa è emerso, cosa
> riusciamo a far rientrare prima della consegna, chi fa cosa e cosa dichiariamo come sviluppo futuro.
> Commentate la PR **entro le 21:45** con la vostra disponibilità, le obiezioni e le idee (vedi
> [la sezione 8](#8-le-vostre-idee)). asprenolapo conferma il piano alle 21:45 e apre le issue.

## 1. Cosa è emerso

### Decisioni del PO (registro: [po-decisions.md](analysis/po-decisions.md), righe 29–33)

| # | Decisione | Cosa cambia nel codice |
|---|---|---|
| 29 | Fasce confermate: 08–14, 14–19, 19–23 | Niente nelle prenotazioni; il catalogo cerca in finestre diverse (09–11, 14–16, 19–21) e va allineato |
| 30 | Niente prenotazioni a cavallo di due fasce | Niente |
| 31 | **Il limite di una prenotazione per fascia vale anche per i partecipanti** | Nuovo controllo in Bookings: oggi vale solo per chi prenota |
| 32 | **Al massimo 15 giorni di anticipo**; l'inizio deve essere nel futuro | Nuova regola: oggi c'è solo "inizio nel futuro" |
| 33 | Sprint Goal 2 approvato, se tutto è coerente con le decisioni sopra | `process.md` |

### Richieste del PO (R1–R9) e difetti trovati da noi

| Rif. | Cosa | Stato nel codice di `main` (`16be065`) |
|---|---|---|
| R1 | Mail con data **e** ora | Data e ora della prenotazione ci sono già; la scadenza dell'invito ha solo l'ora ("entro 19:49") |
| R2 | L'admin non vede il form di prenotazione ma un messaggio specifico | Il backend risponde `403` (#232); il form è visibile all'admin e mostra un errore generico |
| R3 | Card dell'invito con aula, data, orario, durata e partecipanti | L'API `/invitations/mine` restituisce solo `bookingId` |
| R4 | Lo slider della durata segue le ore scelte | Lo slider legge la durata con un metodo, non con un segnale legato al form |
| R5 | Dopo l'accesso "Registrati" sparisce dal menu | I link del menu sono fissi (`app.ts`, `navLinks`) |
| R6 | Più aule | Il seed ha 8 spazi |
| R7 | Cercare e filtrare gli spazi quando si prenota | Il catalogo ha i filtri, il form ha solo una tendina e dal catalogo non si arriva al form |
| R8 | Strumento non in elenco: segnalazione con nota; l'admin risponde all'utente | Il backend accetta già "Altro" con nota e crea l'avviso; il form non lo mostra; chiudere l'avviso non manda nessuna mail all'utente |
| R9 | Pagina admin controllata rispetto ai requisiti | Ci sono dashboard, avvisi, report e gestione spazi: manca il confronto con i requisiti |
| D1 | Il link della mail di registrazione non scorre al form (prova nel browser del 28/10) | Né il router né la home scorrono verso `#registrazione` |
| D2 | L'etichetta "Email dei partecipanti" si sovrappone al chip (prova nel browser del 28/10) | Da vedere nel CSS del form |
| D3 | Le finestre di ricerca del catalogo sono diverse dalle fasce (#236) | `spaces-catalog.page.ts:65-67` |

Già verificato e funzionante su `main`: il percorso completo dell'invito nel browser (prenotazione di gruppo,
`.eml`, registrazione, login, conferma, "Le mie prenotazioni") e il percorso della demo sull'API su Linux (#236).

## 2. Sprint Goal 3 (proposta)

> Il prodotto rispetta le regole confermate dal PO il 28/10 e la demo mostra solo ciò che è stato provato.

## 3. Regole: modifiche chirurgiche

1. **Una issue, un branch, una PR.** Branch `fix/<issue>-<descrizione>`, `Closes #<issue>` nella descrizione.
2. **Solo i file della riga del task.** Niente refactor, rinomine, riformattazioni o dipendenze nuove. Se serve
   toccare altro, lo si dice in chat prima.
3. **Un test per ogni comportamento cambiato**; `dotnet test` o `ng test` in locale e CI verde prima della review.
4. **Review entro 30 minuti** dal revisore indicato, scrivendo cosa ha controllato (niente approvazioni "a fiducia").
   Se il revisore non risponde, asprenolapo riassegna.
5. **Chi apre la PR la fonde** (squash), come da CONTRIBUTING.
6. **Dopo il freeze delle 11:15 si fonde solo un blocker**, con l'ok di asprenolapo. Un task non pronto si toglie e
   si dichiara come limite: meglio una cosa in meno che una cosa a metà.
7. Due task sugli stessi file non vanno in parallelo: l'ordine è nella colonna "Dopo".

## 4. Chi fa cosa

asprenolapo resta fuori dai task: coordina, fa le review di riserva, prepara repository, rilascio e presentazione.
Le slide restano quelle su `main` (#220): **niente lavoro nuovo sulle slide**.

### Onda A: stanotte, PR entro le 00:00 (Must)

| Task | Cosa | File | Come | Perché | Chi | Revisore | Dopo |
|---|---|---|---|---|---|---|---|
| H1 | Massimo 15 giorni di anticipo (backend) | `BookingRequestValidator.cs`, test del validatore e di `POST /bookings` | Errore `400` sul campo `Start`, per esempio "A booking can start at most 15 days from now." | Decisione 32 | Rhaastrake | FranDeSa456 | — |
| H2 | Form di prenotazione: data massima a +15 giorni; messaggio per l'admin al posto del form; slider legato alle ore; etichetta sovrapposta | `booking-form.component.ts/.html/.scss/.spec.ts` | `max` sull'input data; `session.isAdmin()` mostra "Gli amministratori non prenotano: gestiscono le eccezioni dall'area admin"; durata come `computed` sui valori del form; correzione della `mat-form-field` dei chip | Decisione 32, R2, R4, D2 | Leo-prova | antoStr | — |
| H3 | Data e ora nelle mail di invito e di scadenza | `NotificationTemplates.cs`, `PickupDirectoryEmailSenderTests.cs`, `OutboxEmailE2ETests.cs` | `dd/MM/yyyy HH:mm` al posto di `HH:mm` nelle quattro righe della scadenza | R1 | FranDeSa456 | Rhaastrake | — |
| H4 | "Registrati" nascosto dopo l'accesso; il link della mail scorre al form | `app.ts`, `app.html`, `app.config.ts`, `app.spec.ts` | `navLinks` filtrati con `session.isAuthenticated()`; scorrimento a `#registrazione` dopo il primo rendering | R5, D1 | fedeericodl | Leo-prova | — |
| H5 | Finestre di ricerca del catalogo uguali alle fasce | `spaces-catalog.page.ts` e il suo spec | 08–14, 14–19, 19–23 (la ricerca non ha limite di durata, verificato in `AvailabilityController`) | Decisione 29, D3 | AldAndrew0 | Leo-prova | — |
| H10 | Controllo della pagina admin rispetto ai requisiti | Solo lettura, commento sull'issue | Lista dei requisiti (dashboard con aule prenotate e libere, avviso di ogni prenotazione, eccezioni strumenti e manutenzione, annullamento con motivo e mail, statistiche, gestione spazi, telefono visibile solo all'admin), con "c'è / manca / come si prova" | R9 | antoStr | asprenolapo | — |

### Onda B: domattina 09:45–11:15 (Should, se l'onda A è fusa)

| Task | Cosa | File | Come | Perché | Chi | Revisore | Dopo |
|---|---|---|---|---|---|---|---|
| H6 | Più aule nei dati sintetici | `data/synthetic/spaces.json` (e il seed di Spaces, se non lo legge da lì) | 6–8 spazi nuovi, di tipi ed edifici diversi; **nessun id esistente cambia**, perché il seed delle prenotazioni li usa; test di coerenza dei dati verdi | R6 | AldAndrew0 | antoStr | H5 |
| H7 | Card dell'invito completa | `MyInvitationsService.cs`, `InvitationResponse`, pagina e modelli in `src/web/src/app/invitations/`, test | Aggiungere alla risposta spazio, inizio, fine e numero di partecipanti; la card li mostra in ora di Roma | R3 | FranDeSa456 | Leo-prova | H3 |
| H8 | Limite per fascia anche per i partecipanti | `BookingService.cs`, `AcceptInvitationService.cs`, test | `409` "Time band already used" se un partecipante registrato ha già, quel giorno e in quella fascia, una prenotazione come organizzatore o come partecipante confermato | Decisione 31 | Rhaastrake | FranDeSa456 | H1 |
| H9 | Prenotare partendo dalla ricerca del catalogo | Card del catalogo (AldAndrew0), lettura dello spazio scelto nel form (Leo-prova) | Pulsante "Prenota" sulla card → `/?spazio=<id>#prenota`; il form preseleziona lo spazio. Si riusano i filtri del catalogo, senza duplicarli nel form | R7 | AldAndrew0, Leo-prova | antoStr | H2, H5 |
| H10b | Correzioni piccole emerse da H10 | Solo `src/web/src/app/admin/` | Solo ciò che si chiude in meno di un'ora; il resto si dichiara | R9 | antoStr | AldAndrew0 | H10 |

### Fuori dall'iper-sprint (sviluppo futuro dichiarato)

| Task | Cosa | Perché fuori |
|---|---|---|
| H11 | Strumento "Altro" con nota nel form, e mail all'utente quando l'admin chiude l'avviso | Tocca form, area admin e mail: troppo per una mattina. Il backend accetta già "Altro" con nota; lo diciamo in demo |
| — | Vincolo sui partecipanti garantito dal database | H8 lo controlla nell'applicazione; con richieste simultanee resta un limite, da dichiarare |

Se qualcuno ha tempo e vuole prendere H11 al posto di un task dell'onda B, lo dica nella PR.

## 5. Tempi

| Quando | Cosa | Chi |
|---|---|---|
| mer 21:00–21:45 | Lettura di questa PR, commenti con disponibilità e idee | Tutti |
| mer 21:45 | Piano confermato, issue aperte e assegnate, board aggiornata | asprenolapo |
| mer 22:00–00:00 | Onda A: PR aperte | Owner dei task |
| entro 00:30 | Review incrociate e fusioni dell'onda A | Revisori e autori |
| gio 08:30 | Avvio da zero su `main` ([scheda della mattina](evidence/2026-10-29-delivery-morning.md)) | asprenolapo |
| gio 09:30 | Daily: stato dell'onda A, conferma dell'onda B | Tutti |
| gio 09:45–11:15 | Onda B | Owner dei task |
| gio 11:15 | **Freeze**: si fondono solo i blocker | — |
| gio 11:15–11:45 | Prova finale nel browser su due sistemi e sul telefono (#93) | Windows: asprenolapo; Linux: Rhaastrake; telefono: fedeericodl |
| gio 11:45 | Tag `v0.2` e release (#86) | asprenolapo |
| gio 12:00 | Prova a voce della presentazione (#51) | Tutti |

L'orario della consegna va confermato: se è prima, freeze e prove si anticipano di conseguenza.

## 6. Cosa resta da chiudere e come

| Issue | Perché è aperta | Come si chiude | Chi |
|---|---|---|---|
| #103 Conferma entro 2 ore | Le sub-issue sono chiuse; la story no | Controllare i criteri (il percorso è stato provato nel browser il 28/10) e chiuderla; poi si chiude #56 | FranDeSa456 |
| #2 Avvio riproducibile | Manca il secondo sistema operativo per `dev-up` | Riga Linux nella scheda di avvio, poi si chiudono #2 e #53 | Rhaastrake |
| #83 Perimetro e accordi | Mancavano le decisioni del PO | Si chiude con questa PR | asprenolapo |
| #86 Tag e note | Si rilascia dopo il freeze | `v0.2` alle 11:45 | asprenolapo |
| #93 Portabilità | Manca la prova nel browser su due sistemi e sul telefono | Prova delle 11:15, nella tabella di `demo-path.md` | vedi sezione 5 |
| #92, #91, #51 | Presentazione | Slide come su `main`; prova a voce alle 12:00 | Tutti |
| #67 | Contenitore | Si chiude con #83 e #91 | asprenolapo |

Da sistemare nella documentazione (asprenolapo): `docs/security-and-testing.md` allineato al `403` per
l'annullamento altrui (#96), le due note della #236 e lo scenario 5 delle prove di guasto ripetuto dopo la #218,
come chiesto da Rhaastrake.

## 7. Come si dichiara quello che non entra

Ogni task non fuso al freeze va in tre posti:

- nella release `v0.2`, sotto "Cosa è incompleto";
- nelle slide, sotto "Limiti" (a voce, se le slide non si toccano);
- nel registro del PO, come richiesta di revisione con stato "Sviluppo futuro".

## 8. Le vostre idee

Nella PR, ognuno risponde a quattro domande:

1. Quando sei disponibile stanotte e domattina?
2. Il task che ti è assegnato va bene? Se no, quale preferisci?
3. Cosa cambieresti nelle priorità? Per esempio: H11 al posto di H9.
4. Hai visto altri difetti provando l'applicazione? Vanno nella tabella della sezione 1.

# Incontro con il Product Owner – lunedì 26/10/2026

> Materiale per l'incontro previsto dal task #85 (SC-21.T2). Le decisioni prese si registrano in
> [po-decisions.md](po-decisions.md). Durata indicativa: **30 minuti**.
>
> Il PO **concorda e valida**: chiarisce il bisogno, concorda priorità e risultati attesi, autorizza perimetro
> e variazioni (slide 10). Noi portiamo **proposte motivate**, non decisioni già prese.

## Scaletta

| Minuti | Punto | Chi presenta |
|---|---|---|
| 0–3 | Come abbiamo letto il bisogno (tre problemi, due tipi di utenti) | _da assegnare_ |
| 3–6 | Proposta di **Product Goal** e dei due **Sprint Goal** | _da assegnare_ |
| 6–14 | **Perimetro e priorità**: cosa consideriamo risultato minimo (domande 1–2) | _da assegnare_ |
| 14–24 | **Regole del prodotto** da chiarire (domande 3–12) | _da assegnare_ |
| 24–27 | Criteri di accettazione e Definition of Done (domanda 13) | _da assegnare_ |
| 27–30 | Prossimi incontri e riepilogo delle decisioni | _da assegnare_ |

Uno di noi presenta, uno scrive le decisioni in diretta nella tabella di [po-decisions.md](po-decisions.md).

## 1. Il bisogno, come lo abbiamo capito

1. Le informazioni sulla disponibilità sono diverse per persone diverse: due gruppi si presentano nella stessa aula.
2. Annullamenti e conferme si perdono: spazi "apparentemente occupati" mentre sono vuoti.
3. Il personale non distingue uso effettivo, aspettative e impressioni; e un'assenza di dati non prova che uno spazio fosse libero.

## 2. Proposta di Product Goal e Sprint Goal

**Product Goal (proposta)**
> Studenti e personale trovano e prenotano gli spazi del campus, anche per un gruppo, con una conferma
> affidabile e senza conflitti; il personale capisce come gli spazi vengono usati, distinguendo prenotazioni,
> uso dichiarato e dati mancanti.

| Sprint | Date | Sprint Goal (proposta) | Review con il PO |
|---|---|---|---|
| 1 | lun 26 – mar 27 | Uno studente trova uno spazio e lo prenota, da solo o in gruppo, con la certezza che nessuno spazio e nessuna persona siano prenotati due volte | Fine di martedì 27 (_orario da concordare_) |
| 2 | mer 28 – gio 29 mattina | Gli spazi non usati tornano liberi e il personale vede il quadro delle prenotazioni e l'utilizzo, in modo sicuro e verificato | Presentazione di giovedì 29 |

## 3. Domande per il PO

Per ogni domanda: la nostra proposta e cosa cambia nel backlog. **SP** = story point.

### Perimetro e priorità

**1. Il risultato minimo che proponiamo va bene?**
Oggi le storie **Must** sono 16 per **56 SP**, le **Should** 7 per **26 SP**. Per tre giornate è un carico alto.

| Must (proposta) | SP |
|---|---|
| Avvio riproducibile (SC-01), dati sintetici (SC-02), scelta del profilo (SC-03) | 10 |
| Catalogo (SC-04), disponibilità (SC-05), prenotazione (SC-06), nessuna sovrapposizione (SC-07) | 16 |
| Le mie prenotazioni (SC-08), annullamento (SC-09), gestione spazi (SC-10), prenotazione di gruppo (SC-16) | 13 |
| Check-in (SC-12), report di utilizzo (SC-13) | 8 |
| Perimetro e accordi (SC-21), decisioni e costi (SC-22), presentazione (SC-23) | 9 |

Should: indisponibilità (SC-11), CI (SC-14), avviso spazio liberato (SC-15), rilascio automatico (SC-17),
calendario del personale (SC-18), disattivazione con prenotazioni (SC-19), accesso con token (SC-20).

- **Chiediamo:** confermare il Must; se il PO lo ritiene troppo ampio, **che cosa togliere per primo**.

**2. Capire l'utilizzo fa parte del risultato minimo?**
La slide 9 lo mette nell'obiettivo ("rendendone più comprensibile l'utilizzo"). Proponiamo check-in (SC-12) e un
report minimo (SC-13) in **Must**. Se il PO dice di no, il Must scende a **48 SP**.

### Regole delle prenotazioni

**3. Quali spazi rientrano?** Proposta: aule, laboratori e sale studio, tutti prenotabili sia da studenti sia dal personale.

**4. Che limiti hanno le prenotazioni?** Proposta: fasce da **15 minuti**, durata da **30 minuti a 4 ore**, al
massimo **14 giorni** in anticipo. Cambia solo la validazione in Bookings (regole R3–R4).

**5. Partecipanti: aggiunti direttamente o con invito?** Proposta: **direttamente** dall'organizzatore, con
la possibilità di uscire e un massimo di **5 prenotazioni di gruppo future** per organizzatore. Con gli
inviti da accettare la storia SC-16 cresce di circa 3 SP.

**6. Uno studente può aggiungere un membro del personale come partecipante?** Proposta: sì, sono tutti profili del campus.

**7. Nel gruppo, chi fa il check-in?** Proposta: l'organizzatore **o** un qualsiasi partecipante, una volta per tutta la prenotazione.

**8. Rilascio automatico: 15 minuti senza check-in vanno bene?** Proposta: sì, 15 minuti (SC-17, Should).
Risolve lo spazio "apparentemente occupato" della slide 7.

**9. Se uno spazio viene disattivato o va in manutenzione, le prenotazioni future si annullano?**
Proposta: sì, con conferma del personale che vede quante prenotazioni sono coinvolte (SC-19, Should).

**10. Il personale può annullare o spostare prenotazioni altrui?** Proposta: solo annullare, e solo tramite
la disattivazione o la manutenzione di uno spazio (domanda 9); niente spostamenti.

**11. Chi va avvisato quando una prenotazione viene annullata?** Proposta: nessun avviso esterno; i
partecipanti vedono lo stato "annullata" nelle proprie prenotazioni. L'avviso a chi aspettava lo spazio è SC-15 (Should).

### Report e interfaccia

**12. Che cosa deve mostrare, al minimo, il report di utilizzo?** Proposta: per spazio e periodo,
prenotazioni, annullamenti, check-in, prenotazioni rilasciate e persone coinvolte, con la nota che il check-in
è uso **dichiarato**, non misurato.

**13. In quali ambienti va usata l'interfaccia?** Proposta: browser recenti su computer (Windows, macOS,
Linux) e su smartphone. Niente app da installare.

**14. Accesso:** basta scegliere un profilo sintetico (limite dichiarato), o serve l'accesso con password demo
e token firmato (SC-20, 5 SP)?

### Qualità

**15. Criteri di accettazione e Definition of Done vanno bene?** Portiamo la DoD (`docs/definition-of-done.md`)
e chiediamo al PO se aggiungerebbe o toglierebbe qualcosa.

## 4. Da chiedere in chiusura

- Quando facciamo la **review dello Sprint 1** (martedì 27)?
- Il PO preferisce vedere la demo dal vivo o anche le prove registrate in `docs/evidence/`?

## Dopo l'incontro

1. Compilare [po-decisions.md](po-decisions.md) con data, decisione e impatto.
2. Aggiornare priorità e criteri sulla board e nelle issue (le storie con "proposta, da confermare con il PO").
3. Scrivere Product Goal e Sprint Goal concordati in `docs/process.md` (#84).
4. Comunicare le decisioni al gruppo con un commento sull'issue #98.

---

## Aggiornamento dopo il brainstorming del gruppo (26/10 pomeriggio)

Il gruppo ha rivisto il prodotto: vedi [brainstorming-2026-10-26.md](brainstorming-2026-10-26.md). All'incontro
si porta **quel perimetro**, con il suo ordine di taglio (sezione 7).

**Domande superate** (il brainstorming propone già una risposta da validare):

| Domanda | Nuova proposta |
|---|---|
| 5 – partecipanti diretti o con invito | Invito per email, conferma entro 2 ore, anche per non registrati |
| 7 – chi fa il check-in | Niente check-in: commento finale obbligatorio con presenze |
| 8 – soglia del rilascio automatico | Niente rilascio automatico |
| 9, 10 – spazio disattivato, annullamenti del personale | Gestiti come eccezioni dall'admin, con mail ai partecipanti |
| 14 – accesso | Registrazione e login con password, nessun mock |

**Domande nuove** (sezione 9 del brainstorming): telefono necessario?, formato della matricola, chi prenota
aule dedicate e miste, scadenza delle conferme per prenotazioni imminenti, elenco degli strumenti, email
come file `.eml` locali.

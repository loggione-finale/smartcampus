# Promemoria: che cosa stiamo facendo e come

> Una pagina da rileggere prima di ogni giornata di lavoro. I dettagli sono nei documenti linkati.
>
> **Aggiornamento 26/10 pomeriggio:** il [brainstorming del gruppo](analysis/brainstorming-2026-10-26.md) cambia
> accesso (registrazione e login), gruppi (inviti per email con conferma entro 2 ore), aule (dedicate e miste),
> admin e divisione del lavoro. Questa pagina verrà aggiornata dopo la validazione del PO.

## Che cosa

**SmartCampus**: trovare e prenotare gli spazi del campus (aule, laboratori, sale studio) senza conflitti,
anche per un gruppo, e capire come vengono usati davvero. Project Work ITS Bari, **consegna giovedì 29/10/2026**.

Il cuore del prodotto in tre regole:
1. uno spazio non ha mai due prenotazioni sovrapposte;
2. una persona non è mai in due prenotazioni sovrapposte;
3. un'assenza di dati non è un'assenza di utilizzo.

## Come è fatto

| Parte | Tecnologia |
|---|---|
| Spaces (catalogo, indisponibilità) | ASP.NET Core Web API + **MongoDB** |
| Bookings (prenotazioni, gruppi, check-in, statistiche) | ASP.NET Core Web API + EF Core + **PostgreSQL** |
| Users (profili sintetici, accesso) | ASP.NET Core Web API + EF Core + **PostgreSQL** |
| Frontend | **Angular + Angular Material**, con proxy di sviluppo verso i servizi |
| Avvio | `docker compose up -d` per i database, `dotnet run` per i servizi, `ng serve` per il frontend |

Solo strumenti visti nel corso. Perché: [architecture.md](architecture.md). Dati: [data-model.md](data-model.md).

**Accesso:** nessuna registrazione (studenti e personale hanno già i loro account). Si parte dal mock, cioè la
scelta di un profilo sintetico; il login con account pre-caricati (SC-20) si fa solo se avanza tempo.

**Dati di servizi diversi:** nessuna query unisce MongoDB e PostgreSQL. Bookings salva una copia di nome, tipo
ed edificio dello spazio; dove serve il dato attuale, il frontend chiede a entrambi i servizi.

## Come lavoriamo

1. **Prendi un Task** dalla [board](https://github.com/orgs/loggione-finale/projects/1) (sei già assegnato) e spostalo in **In Progress**.
2. **Branch da `main`**: `feature/<numero>-<descrizione>` (nomi in inglese).
3. **PR verso `main`** con `Closes #<numero>`; spostala in **In Review**.
4. **Review di un altro membro**: obbligatoria, GitHub blocca il merge senza.
5. **Squash and merge**: il branch si cancella da solo; il task va in **Done**.

Regole: nomi in inglese, testi in italiano; niente segreti né dati reali; ogni PR rispetta la
[Definition of Done](definition-of-done.md), compresi sicurezza e test della componente
([security-and-testing.md](security-and-testing.md)).

## Il backlog

**Feature → User Story → Task** (sub-issue), formato del docente. 6 Feature, 23 User Story, 67 Task.

| Feature | User story |
|---|---|
| F-01 Piattaforma e avvio | SC-01, SC-02, SC-14 |
| F-02 Utenti e ruoli | SC-03, SC-20 |
| F-03 Catalogo degli spazi | SC-04, SC-10, SC-11, SC-19 |
| F-04 Prenotazioni senza conflitti | SC-05, SC-06, SC-07, SC-08, SC-09, SC-15, SC-16, SC-18 |
| F-05 Utilizzo degli spazi | SC-12, SC-13, SC-17 |
| F-06 Processo, decisioni e presentazione | SC-21, SC-22, SC-23 |

**Priorità proposte:** 16 storie Must (56 punti), 7 Should (26 punti). Le priorità diventano valide solo
dopo l'incontro con il PO (#85).

## Chi fa cosa

| Persona | Area principale |
|---|---|
| asprenolapo | Piattaforma: soluzione, Docker Compose, sicurezza dei database, CI, verifica del token; incontro con il PO |
| AldAndrew0 | Spaces su MongoDB: catalogo, gestione, indisponibilità, disattivazione; test del rilascio automatico; prova di portabilità |
| Rhaastrake | Bookings: modello, creazione, vincoli, annullamento, gruppi, orari UTC; tag e release; presentazione |
| FranDeSa456 | Bookings: disponibilità, mie prenotazioni, test di concorrenza e di sicurezza, calendario; valore e costi |
| fedeericodl | Users e accesso; partecipanti; check-in, statistiche, rilascio automatico; dati sintetici |
| antoStr | Frontend: progetto Angular, catalogo, disponibilità, gestione spazi, report, calendario; ADR; retrospettiva |
| Leo-prova | Frontend: accesso, prenotazione, partecipanti, mie prenotazioni, check-in, errori; `docs/process.md` |

## Il piano

| Quando | Che cosa |
|---|---|
| **Lun 26** | Merge della PR della documentazione; **incontro con il PO** (#85); scheletro: soluzione (#17), Docker Compose (#18), Angular (#19), Users (#64); Product Goal e Sprint Goal (#84) |
| **Mar 27** | Sprint 1: catalogo, disponibilità, prenotazione singola e di gruppo, vincoli e test di concorrenza; fine giornata: retrospettiva (#87) e tag `v0.1` (#86) |
| **Mer 28** | Sprint 2: annullamento, check-in, statistiche, rilascio automatico, sicurezza, prove di guasto e portabilità; ADR e costi |
| **Gio 29** | Solo correzioni; prova della demo (#51); presentazione |

## Da decidere con il PO

1. Perimetro e priorità: SC-12 e SC-13 in Must?
2. Spazio disattivato con prenotazioni già confermate: le annulliamo? (SC-19)
3. Soglia per il rilascio automatico: 15 minuti vanno bene? (SC-17)
4. Partecipanti aggiunti direttamente o con invito da accettare? (W6)
5. Durata minima e massima, anticipo massimo di una prenotazione.
6. Chi fa il check-in in un gruppo; se i partecipanti vanno avvisati di un annullamento.

## Attenzione

- **Molti documenti sono stati preparati con l'aiuto di uno strumento AI.** Le slide chiedono che ogni
  componente sappia spiegare il proprio contributo: chi è responsabile di un'area **rilegge, corregge e fa
  proprio** il documento relativo prima della presentazione (vedi CONTRIBUTING, sezione "Uso di strumenti AI").
- Il carico Must è alto per tre giornate: meglio **finire e integrare** poche storie che iniziarne molte.
- Prima di partire, su ogni PC: .NET SDK 10, Node.js LTS con Angular CLI, Docker Desktop.

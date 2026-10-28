# Misure di utilizzo degli spazi

Questo documento spiega cosa significa ogni numero del report `GET /admin/stats` (servizio Bookings, solo ruolo Admin),
mostra come si calcola a mano su dati sintetici e dichiara i limiti. Il codice di riferimento è
`AdminStatsService` in `src/Services/Bookings/SmartCampus.Bookings.Api/Features/Admin/`.

## Come si interroga

`GET /admin/stats?from=AAAA-MM-GG&to=AAAA-MM-GG&groupBy=Space|Type|Building`

- `from` e `to` sono **inclusivi** e si riferiscono alla data di prenotazione del campus (`BookingDate`), non all'ora di creazione.
  Se `from` è dopo `to` la risposta è `400`. Un utente senza ruolo Admin riceve `403`.
- `groupBy` raggruppa per spazio, tipo di spazio o edificio (se omesso, per spazio). Si usano le **copie salvate nella
  prenotazione** (nome, tipo, edificio al momento della prenotazione). Per spazio il gruppo è l'id dello spazio: se è stato
  rinominato resta un solo gruppo, con l'etichetta della prenotazione più recente nel periodo.
- La risposta è una lista ordinata per etichetta, un elemento per gruppo che ha almeno una prenotazione nel periodo.

## Misure

Ogni elemento della risposta contiene:

| Campo | Cosa conta |
|---|---|
| `totalBookings` | Tutte le prenotazioni del gruppo nel periodo, in qualunque stato (annullate comprese). |
| `confirmed` | Prenotazioni in stato `Confirmed`: future, oppure già finite ma **senza commento finale**. |
| `cancelled` | Prenotazioni in stato `Cancelled`. |
| `closed` | Prenotazioni **concluse**: in stato `Closed`, cioè con commento finale inviato. |
| `spaceUsed` | Commenti finali con "spazio usato" = sì. |
| `spaceNotUsed` | Commenti finali con "spazio usato" = no. |
| `withoutComment` | Prenotazioni **concluse senza commento**: non annullate, con fine già passata e senza commento finale. |
| `totalAttendees` | Somma delle **presenze dichiarate** nei commenti finali (0 se lo spazio non è stato usato). |
| `issuesReported` | Commenti finali che segnalano un problema. |

Relazioni sempre vere: `totalBookings = confirmed + cancelled + closed` e `closed = spaceUsed + spaceNotUsed`
(ogni prenotazione chiusa ha esattamente un commento). Inoltre `withoutComment <= confirmed + closed`: sono le
prenotazioni non annullate già finite, quasi sempre ancora `Confirmed`.

### Misure derivate (non sono nella risposta)

Si calcolano dai campi sopra:

- **Prenotazioni usate su concluse**: `spaceUsed / closed`. Dice quante delle prenotazioni concluse hanno davvero usato lo spazio.
  Se `closed` è 0 la misura non è definita (non è 0%).
- **Presenze medie per prenotazione usata**: `totalAttendees / spaceUsed`.

### Presenze e partecipanti previsti

Il report contiene solo le **presenze dichiarate** (`totalAttendees`). La misura "presenze rispetto ai previsti" **non è
disponibile**: manca il denominatore. I **partecipanti previsti** non sono nel report:
nella prenotazione c'è `SeatsRequested`, ma per le aule a uso dedicato indica la prenotazione intera (nei dati sintetici
vale 1 anche se poi i presenti sono 18), quindi non è un conteggio di persone. Il confronto previsti/presenti non si
può fare in modo affidabile con i dati attuali.

## Esempio 1: i dati del test del report

I dati di `AdminStatsEndpointTests` (un solo giorno, un solo edificio). Sono 5 prenotazioni:

| Spazio | Stato | Commento finale |
|---|---|---|
| Lab | Closed | usato, 10 presenti, nessun problema |
| Lab | Closed | usato, 4 presenti, problema segnalato |
| Lab | Closed | non usato, 0 presenti |
| Lab | Cancelled | nessuno |
| Aula | Confirmed | nessuno |

Calcolo per edificio (tutte e 5 le prenotazioni):

- `totalBookings` = 5
- `confirmed` = 1 (l'aula), `cancelled` = 1, `closed` = 3, e 1 + 1 + 3 = 5
- `spaceUsed` = 2 (10 e 4), `spaceNotUsed` = 1, e 2 + 1 = 3 = `closed`
- `withoutComment` = 0 (l'unica senza commento non annullata è l'aula, ma è nel futuro)
- `totalAttendees` = 10 + 4 + 0 = 14
- `issuesReported` = 1
- derivata: usate su concluse = 2 / 3 = 67% (arrotondato)

Per solo laboratorio (`groupBy=Space`): `totalBookings` 4, `confirmed` 0, `totalAttendees` 14. Per il tipo dell'aula:
`totalBookings` 1, `confirmed` 1, `spaceUsed` 0, `totalAttendees` 0. Sono gli stessi valori che il test verifica.

Un test a parte verifica `withoutComment` su uno spazio con 5 prenotazioni: una `Confirmed` passata senza commento, una
`Closed` passata senza commento, una `Closed` con commento, una `Cancelled`, una `Confirmed` futura. Nel giorno passato
`totalBookings` = 4 e `withoutComment` = 2 (le prime due); la futura e la annullata non contano, quella con commento
nemmeno. Nel giorno della futura `withoutComment` = 0.

## Esempio 2: i dati del seed di Bookings

I dati sono in `data/synthetic/bookings.json` (13 prenotazioni, descritte in `data/synthetic/bookings-seed.md`). Le date
sono relative a oggi: la colonna "giorno" è `daysFromToday`. Spazi: Aula 101, Sala Studio Piano Terra e Aula 204 sono in
Edificio A; Laboratorio Informatica 2 è in Edificio B.

| Codice | Spazio | Giorno | Stato | Commento finale |
|---|---|---|---|---|
| SC-SEED-0001 | Aula 101 | +3 | Confirmed | - |
| SC-SEED-0002 | Aula 101 | +3 | Confirmed | - |
| SC-SEED-0003 | Aula 101 | +4 | Cancelled | - |
| SC-SEED-0004 | Aula 101 | +4 | Confirmed | - |
| SC-SEED-0005 | Lab 2 | +5 | Confirmed | - |
| SC-SEED-0006 | Sala Studio | +2 | Confirmed | - |
| SC-SEED-0007 | Sala Studio | +2 | Confirmed | - |
| SC-SEED-0008 | Sala Studio | +2 | Confirmed | - |
| SC-SEED-0009 | Aula 101 | -7 | Closed | usato, 18 presenti, nessun problema |
| SC-SEED-0010 | Lab 2 | -3 | Closed | usato, 9 presenti, problema segnalato |
| SC-SEED-0011 | Aula 101 | -1 | Confirmed (finita) | - |
| SC-SEED-0012 | Aula 204 | +6 | Confirmed | - |
| SC-SEED-0013 | Aula 204 | +6 | Confirmed | - |

**Periodo "ultimi 7 giorni"**: `from` = oggi - 7, `to` = oggi, `groupBy=Building`. Entrano 0009, 0010 e 0011.

| Edificio | total | confirmed | cancelled | closed | spaceUsed | spaceNotUsed | withoutComment | totalAttendees | issuesReported |
|---|---|---|---|---|---|---|---|---|---|
| Edificio A | 2 | 1 | 0 | 1 | 1 | 0 | 1 | 18 | 0 |
| Edificio B | 1 | 0 | 0 | 1 | 1 | 0 | 0 | 9 | 1 |

Passaggi per Edificio A: prenotazioni 0009 (Closed) e 0011 (Confirmed) danno `total` = 2, `confirmed` = 1, `closed` = 1.
Solo 0009 ha il commento: `spaceUsed` = 1, `totalAttendees` = 18, nessun problema. La 0011 è finita e senza commento:
`withoutComment` = 1. Per Edificio B: solo 0010, `total` = 1,
`closed` = 1, `spaceUsed` = 1, `withoutComment` = 0, `totalAttendees` = 9, `issuesReported` = 1.
Stesso periodo con `groupBy=Type`: `Classroom` ha gli stessi numeri di Edificio A, `Lab` quelli di Edificio B.
Con `groupBy=Space` si ottengono Aula 101 (come Edificio A) e Laboratorio Informatica 2 (come Edificio B).

Derivate: usate su concluse = 1/1 per entrambi gli edifici. La prenotazione senza commento è la 0011, finita ieri (è
quella che blocca le nuove prenotazioni di Elena Greco): ora il report la conta in `withoutComment`.

**Periodo completo** (da oggi - 7 a oggi + 6, tutte le 13): Edificio A ha `total` 11, `confirmed` 9, `cancelled` 1,
`closed` 1, `spaceUsed` 1, `withoutComment` 1, `totalAttendees` 18; Edificio B ha `total` 2, `confirmed` 1, `closed` 1,
`withoutComment` 0, `totalAttendees` 9, `issuesReported` 1. Qui `confirmed` mescola 8 prenotazioni future con la 0011:
`withoutComment` le distingue (solo la 0011 è finita).

Verifica: i numeri dell'esempio 1 coincidono con le asserzioni di `AdminStatsEndpointTests` (7 test su 7 superati in
locale, su PostgreSQL reale; 174/174 sull'intero progetto di test). L'esempio 2 è calcolato a mano dal file JSON; non è stato confrontato con una chiamata
all'endpoint sul seed.

## Limiti

- **Dati dichiarati, non misurati.** Presenze e "spazio usato" li scrive chi ha prenotato nel commento finale; nessun sensore
  o controllo accessi li conferma. Possono essere sbagliati o arrotondati.
- **Senza commento non vuol dire non usato.** Le prenotazioni senza commento non hanno `spaceUsed` né presenze: non
  vengono contate né come usate né come non usate, ma separatamente in `withoutComment`. L'assenza di dati non prova che lo spazio fosse libero.
- **Il commento arriva dopo.** Una prenotazione appena finita resta in `confirmed` finché non viene chiusa; i numeri di
  `closed`, `spaceUsed` e `totalAttendees` dei giorni recenti possono crescere nei giorni successivi.
- **Le annullate fanno parte di `totalBookings`.** Per un tasso di utilizzo conviene usare `closed` come denominatore.
- **Presenze rispetto ai previsti non disponibile.** Il report ha solo le presenze dichiarate; senza un numero di
  partecipanti previsti affidabile il rapporto non si può calcolare.
- **Nessuna occupazione in ore o posti.** Il report conta prenotazioni, non ore prenotate né posti occupati sulla capienza.
- **Dati sintetici.** Gli esempi usano utenti e spazi inventati; non descrivono nessun campus reale.

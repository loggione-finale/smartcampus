# Percorso della demo

> Percorso scritto della demo su dati sintetici (#91). Ogni passo ha l'esito atteso dall'API: il 28/10/2026 è stato
> eseguito chiamata per chiamata su tre servizi e due database veri, con l'esito scritto in
> `docs/evidence/2026-10-28-demo-rehearsal-api.md` (#51, in una PR separata). L'interfaccia non è stata
> ancora provata: vedi "Prova dell'interfaccia" in fondo.

## Prima di iniziare

1. Database, servizi e frontend come nel [README](../README.md) (sezioni 2 e 3): `scripts/dev-up`, che avvia i servizi in
   `Development`. All'avvio si caricano da soli utenti, spazi e prenotazioni sintetiche
   ([bookings-seed.md](../data/synthetic/bookings-seed.md)); se i database non sono vuoti il seed si salta.
2. Account demo (email e password in [data/synthetic/README.md](../data/synthetic/README.md)): Giulia Rossi, Marco Bianchi,
   Sara Conti, Luca Ferrari, Elena Greco e l'admin.
3. Le date dei dati sintetici sono relative a oggi, quindi i casi sono sempre gli stessi: Elena ha una prenotazione finita
   ieri senza commento (`SC-SEED-0011`) e l'Aula 204 è piena in una fascia fra 6 giorni (`SC-SEED-0012` e `0013`).
4. Per le fasce libere si usano giorni a partire da una settimana: nessuna prenotazione sintetica ci arriva.

## Il percorso

Gli orari sono in ora di Roma. La colonna "Chi" è una proposta in base all'area di ognuno, da confermare alla daily.

| # | Chi (proposta) | Azione | Esito atteso |
|---|---|---|---|
| 1 | fedeericodl | Accesso come Giulia Rossi; prova con una password sbagliata | Accesso riuscito; con la password sbagliata `401` |
| 2 | AldAndrew0 | Catalogo: filtro per laboratori; dettaglio di Aula 101 | Due laboratori; il dettaglio dello spazio |
| 3 | FranDeSa456 | Disponibilità di Aula 101 in un giorno libero | Nessuna fascia occupata |
| 4 | Rhaastrake | Giulia prenota Aula 101, 09:00-11:00 | Prenotazione confermata con un codice leggibile (es. `SC-261109-VJQ9`) |
| 5 | Rhaastrake | Marco prenota Aula 101, 10:00-12:00, sovrapposta | Rifiutata: "Space already booked" (`409`) |
| 6 | Rhaastrake | Marco prenota Aula 101, 11:00-13:00, adiacente | Confermata: una finisce quando inizia l'altra |
| 7 | Rhaastrake | Sara prova ad annullare la prenotazione di Giulia; poi la annulla Giulia | Sara `403`, "solo l'organizzatore"; Giulia: stato `Cancelled` |
| 8 | Rhaastrake | Sara prenota la fascia liberata, 09:00-11:00 | Confermata |
| 9 | Rhaastrake | Marco prova una seconda prenotazione nella stessa fascia (mattina) dello stesso giorno | Rifiutata: "Time band already used" (`409`) |
| 10 | Rhaastrake | Prenotazioni 07:00-08:00, 13:00-15:00 (a cavallo di due fasce) e di 15 minuti | Tutte `400`; senza accesso `401` |
| 11 | Rhaastrake | Sara prenota Aula 204 nella fascia di `SC-SEED-0012` e `0013` (sei posti su sei) | Rifiutata: "Not enough seats", posti liberi 0; in un'altra fascia confermata |
| 12 | fedeericodl | Elena prova a prenotare; poi scrive il commento finale di `SC-SEED-0011`; poi prenota di nuovo | `409` "Closing comment pending" con il codice da commentare; commento `200`; poi prenotazione confermata |
| 13 | FranDeSa456 | Luca prenota il Lab 2 con due invitati (Sara e un ospite non registrato); Sara vede l'invito e lo conferma; Marco prova a confermare quello di Sara | Gruppo confermato; invito in stato `Accepted` per Sara; Marco `403` |
| 14 | antoStr | Come admin, dashboard; Sara prova la stessa pagina | Admin `200`; Sara (utente) `403` |
| 15 | asprenolapo | Fermare il servizio Spaces e provare a prenotare; poi le mie prenotazioni | Prenotazione `503` "Spaces unavailable" in pochi secondi; le mie prenotazioni funzionano |

I passi 5, 9 e 11 mostrano le regole garantite dal database: nessuna sovrapposizione, una prenotazione per fascia e per
giorno, mai oltre la capienza. I test di concorrenza sono in
[evidence/booking-concurrency.md](evidence/booking-concurrency.md).

## Cose da non promettere in demo

- Le email di invito e di scadenza finiscono in una coda del database (outbox): i file `.eml` in una cartella locale
  arrivano con #106. Se non sono ancora su `main`, non mostrarli.
- Le statistiche d'uso per l'admin (`GET /admin/stats`, #47) e le definizioni delle misure (#97) sono mostrabili solo se
  quelle PR sono su `main`.
- Gli inviti scadono dopo due ore: per mostrare la scadenza serve aspettare o cambiare l'orologio; meglio dirla a voce.

## Dichiarazioni obbligatorie

- Tutti i dati sono sintetici: verificano il prototipo e non dimostrano benefici reali per il campus.
- Quello che si mostra è stato provato; quello che è solo ipotizzato si dice come tale (limiti nelle slide e in
  [presentation-outline.md](presentation-outline.md)).

## Prova dell'interfaccia (da compilare)

Il criterio di #91 (task #93) chiede la prova su due sistemi operativi e su uno smartphone, con l'esito registrato.
Finora la prova è solo sull'API. Si fa il 29/10, dopo l'avvio da zero della mattina.

### Come si prova

1. **PC** (Windows e un secondo sistema: Linux o macOS). Sistema avviato con `scripts/dev-up` (README, passo 3), poi
   `http://localhost:4200`. Si seguono i passi 1–14 nel browser; il passo 15 (Spaces fermo) basta su un sistema.
2. **Smartphone**, collegato alla stessa rete Wi-Fi del PC che fa girare il sistema:
   - sul PC il frontend va avviato in ascolto sulla rete: con `dev-up` già lanciato, ferma solo il frontend e lancia
     `npm start -- --host 0.0.0.0` in `src/web`; senza `dev-up`, lancia solo quel comando dopo i servizi;
   - indirizzo del PC: `ipconfig` (Windows, voce "Indirizzo IPv4") o `ip -4 addr` (Linux); sul telefono
     `http://<indirizzo-del-PC>:4200`. Le API passano dal proxy del PC, quindi i servizi restano su `localhost`;
   - su Windows, al primo avvio il firewall chiede se permettere a Node.js l'accesso alle reti private: lo decide chi
     usa il PC. Se il telefono non si collega, è quasi sempre questo o una rete Wi-Fi che isola i dispositivi (per
     esempio una rete ospiti): in quel caso usa l'hotspot del telefono;
   - sul telefono: accesso (passo 1), catalogo con filtro (2), disponibilità (3), una prenotazione (4), un conflitto
     (5), le mie prenotazioni e il commento finale (12). Si controllano orari in ora di Roma, testo leggibile senza
     zoom, pulsanti e menu raggiungibili e nessuno scorrimento orizzontale.
3. Ogni differenza dall'esito atteso diventa un Bug con il numero del passo, come per le prove di guasto.

Controllo fatto il 28/10 (asprenolapo, Windows 11): con `npm start -- --host 0.0.0.0` la pagina risponde `200` anche
dall'indirizzo di rete del PC (`http://192.168.x.x:4200`). Il telefono non è ancora stato provato.

### Esiti

Una riga a persona, dopo aver seguito il percorso nel browser:

| Sistema | Browser | Chi | Data | Passi riusciti | Differenze | Bug aperti |
|---|---|---|---|---|---|---|
| Windows 11 | | | | | | |
| Linux o macOS | | | | | | |
| Smartphone (ora di Roma e layout stretto) | | | | | | |

## Ogni componente spiega il proprio contributo

Ognuno rilegge la propria riga e può mostrare l'evidenza collegata (test, scheda in `docs/evidence/`, PR). Se un passo
non è pronto il giorno della demo, si toglie dal percorso invece di mostrarlo a metà.

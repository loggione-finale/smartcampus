# Analisi del valore, costi e sviluppi futuri

> Documento di analisi per la User Story **SC-22** (issue #88, task tecnico **SC-22.T2**, issue #90).
> Collegato alle decisioni architetturali descritte in [`docs/decisions/`](../decisions/README.md) (in particolare [ADR 0011](../decisions/0011-strumenti-esclusi.md)).

---

## 1. Premessa metodologica sui dati sintetici

> [!IMPORTANT]
> **Dichiarazione obbligatoria**: Tutti i dati utilizzati nel prototipo (utenti, credenziali, prenotazioni e spazi) sono **dati sintetici generati artificialmente** ([`data/synthetic/`](../../data/synthetic/README.md)). Essi servono unicamente a verificare la correttezza logica del software, i vincoli di concorrenza e l'integrazione tra i servizi in ambiente di sviluppo e test. **Nessun beneficio economico, risparmio di tempo o tasso di utilizzo presentato in questa analisi è dimostrato o misurato dai dati sintetici**, i quali non rappresentano una sperimentazione reale sul campo né sostituiscono un'indagine empirica su studenti e personale.

---

## 2. Valore atteso e benefici qualitativi

Il valore del sistema si misura nella capacità di risolvere le inefficienze rilevate nella gestione degli spazi didattici e di studio prima dell'introduzione del software ([`docs/analysis/requirements-analysis.md`](requirements-analysis.md)).

### 2.1 Per gli studenti e la comunità universitaria
* **Certezza del posto prima della trasferta**: la consultazione preventiva della disponibilità reale delle aule e dei laboratori evita viaggi a vuoto o il girovagare tra edifici alla ricerca di posti liberi.
* **Trasparenza su dotazioni e capienze**: conoscere in anticipo la presenza di videoproiettori, lavagne multimediali, prese elettriche o kit audio consente di pianificare sessioni di studio individuale o di gruppo in aule adeguate.
* **Collaborazione e gruppi di studio**: la funzionalità di invito per partecipanti registrati e contatti esterni permette di coordinare il lavoro comune senza sovrapposizioni e con un unico responsabile della prenotazione.
* **Equità nell'accesso alle risorse**: i vincoli di un'unica prenotazione per fascia oraria per persona ([ADR 0005](../decisions/0005-vincoli-di-esclusione.md)) e la durata massima contingentata prevengono l'accaparramento seriale di posti da parte di singoli utenti a scapito degli altri.
* **Responsabilizzazione dell'uso**: l'obbligo del commento finale di chiusura ([SC-29](https://github.com/loggione-finale/smartcampus/issues/112)) induce a rilasciare informazioni trasparenti sull'effettivo utilizzo e a segnalare tempestivamente guasti per i colleghi successivi.

### 2.2 Per il personale e l'amministrazione del campus
* **Supervisione operativa centralizzata**: la dashboard in tempo reale (`GET /admin/dashboard`) offre una vista aggregata per edificio e tipologia delle aule occupate, dei partecipanti e dei responsabili, superando fogli cartacei o registri frammentati.
* **Manutenzione reattiva e tracciamento guasti**: la segnalazione di problemi nel commento finale (`IssuesReported`) genera in automatico avvisi prioritari per l'amministrazione (`admin_alerts`), riducendo i tempi tra l'insorgenza di un malfunzionamento e il suo inoltro all'ufficio tecnico.
* **Pianificazione basata su evidenze dichiarate**: le statistiche d'uso aggregate (`GET /admin/stats`) permettono di quantificare l'interesse relativo per specifici edifici o tipologie di aula e di individuare aule frequentemente prenotate ma rimaste inutilizzate, fornendo indicazioni utili per gli orari di apertura, i turni di sorveglianza e le pulizie.
* **Gestione trasparente delle eccezioni**: gli amministratori possono annullare prenotazioni per ragioni istituzionali o di emergenza con motivazione esplicita e notifica contestuale ai partecipanti.

---

## 3. Analisi dei costi infrastrutturali e operativi

L'architettura realizzata comprende tre servizi backend in .NET 10 (Bookings, Spaces, Users), un'applicazione frontend Angular e due istanze di database (PostgreSQL 17 e MongoDB 7.0). Di seguito si riporta una **stima teorica e preliminare** dei costi operativi e del fabbisogno hardware per un ateneo o polo universitario di medie dimensioni (2.000–5.000 utenti registrati, 50–100 aule e spazi prenotabili).

> [!NOTE]
> In conformità con la premessa metodologica, tutti i valori quantitativi (RAM, traffico, log, ore/uomo) sono **stime progettuali basate su ipotesi di carico e ordini di grandezza tipici**, non dati empirici misurati in produzione. Dove disponibili, sono esplicitate le misure reali rilevate nei test di sviluppo in locale.

### 3.1 Risorse di calcolo (Compute)
* **Microservizi .NET (Spaces, Users, Bookings)**:
  * In assenza di profilazione sotto stress prolungato, stimiamo a riposo un'allocazione prudenziale di **circa 80–150 MB di RAM per container** (ordine di grandezza tipico per microservizi .NET 10 Web API con runtime containerizzato su Alpine/Debian-slim), con consumo CPU trascurabile durante la normale ricezione di richieste JSON.
  * In uno scenario di dimensionamento per un campus singolo, stimiamo che un'allocazione complessiva di **2 vCPU e 2 GB di RAM** per i tre microservizi sia adeguata a gestire decine di richieste al secondo nei picchi dei cambi d'ora (ipotesi di progetto da convalidare con benchmark).
* **Database PostgreSQL 17 (Bookings e Users)**:
  * Misura reale in locale (`docker stats --no-stream`, 28/10/2026): container `smartcampus-postgres` a riposo consuma **~51 MiB di RAM**.
  * Per un ambiente di esercizio stimiamo un fabbisogno di **2 vCPU e 2–4 GB di RAM** con buffer di memoria dimensionati per mantenere in cache gli indici GiST su intervalli temporali (`tstzrange`) e gestire pool di connessioni tipici di EF Core (~50–100 connessioni simultanee).
* **Database MongoDB 7.0 (Spaces)**:
  * Misura reale in locale (`docker stats --no-stream`, 28/10/2026): container `smartcampus-mongo` a riposo con dati sintetici consuma **~191 MiB di RAM**.
  * Il volume documentale del catalogo spazi e delle indisponibilità manutentive è molto contenuto; stimiamo che **1–2 vCPU e 2 GB di RAM** (con cache WiredTiger limitata) garantiscano tempi di risposta nell'ordine di pochi millisecondi in rete locale per letture per `spaceId`, senza necessità di cache esterna.
* **Frontend Web (Angular SPA)**:
  * I file statici (HTML, JS, CSS) vengono distribuiti da un web server Nginx o servizio statico; l'impatto di CPU/RAM lato server è trascurabile.

### 3.2 Storage e persistenza
* **PostgreSQL**:
  * Ipotizzando un volume indicativo di 50.000 prenotazioni/anno (stima di ~250 prenotazioni al giorno su 200 giorni accademici, ciascuna con inviti, audit log e commenti finali), si stima un incremento di storage compreso tra **500 MB e 2 GB** annui (ipotizzando prudenzialmente 10–40 kB per prenotazione comprensivi di indici `tstzrange` e B-Tree).
  * La dimensione dei log di transazione (WAL) dipende dalla frequenza dei checkpoint; uno storage disco dedicato di **20–50 GB** rappresenta una stima precauzionale adeguata per gestire snapshot, indici e storico per più anni.
* **MongoDB**:
  * Per un ateneo con 100 spazi e la registrazione di indisponibilità manutentive, stimiamo che l'ingombro su disco rimanga contenuto entro poche centinaia di megabyte (**< 500 MB**).
* **Politiche di backup e archiviazione**:
  * I backup giornalieri logici (`pg_dump` e `mongodump` compressi) sono stimati nell'ordine di decine di megabyte al giorno; i costi di archiviazione (su storage NAS interno o secondario) sono minimi.

### 3.3 Traffico di rete (Network/Bandwidth)
* **Traffico client-server**:
  * Le chiamate API sono compatti scambi JSON REST sincroni (in media 1–5 kB per richiesta/risposta).
  * Misura da build di produzione (`npm run build`, 28/10/2026): il bundle frontend iniziale pesa **~814 kB** (comprensivo di Angular Material e runtime) e viene scaricato solo al primo caricamento nel browser grazie al caching HTTP. I moduli caricati in lazy loading hanno dimensioni ridotte: `closure` (~43 kB), `spaces` (~35 kB), `admin` (~40 kB).
  * Per un'ipotesi di 50.000 prenotazioni/anno e relative consultazioni (ipotizzando 5–10 visualizzazioni per prenotazione), stimiamo teoricamente un traffico di rete in uscita nell'ordine di **15–30 GB/mese** (stima teorica derivata da frequenza presunta, non da misurazione effettiva).
* **Traffico interno (inter-service)**:
  * Le comunicazioni sincrone HTTP tra Bookings e Spaces/Users avvengono su rete interna/container (`127.0.0.1` o Docker network) e non generano consumo di banda verso l'esterno.

### 3.4 Log, diagnostica e tracciabilità
* **Volumi di log**:
  * I tre microservizi producono log strutturati su console. Al livello informativo standard (`Information`), ipotizzando 5–8 righe per prenotazione (ricezione, validazione, transazione DB, outbox), stimiamo una produzione teorica di circa **10–20 MB di log di testo al giorno** in periodi di normale attività (stima dedotta dal flusso applicativo, non da telemetria su carichi reali).
  * La gestione è affidata a rotazione locale standard (`logrotate`) o inoltro a uno stack leggero (Vector/Loki/Syslog).
* **Esclusione di telemetria pesante**:
  * Come deciso nell'[ADR 0011](../decisions/0011-strumenti-esclusi.md), non vengono adottati agenti APM commerciali o sistemi di distributed tracing ad alto campionamento (OpenTelemetry con backend SaaS), azzerando i costi di licenza o di storage ingestion.

### 3.5 Manutenzione e supporto operativo
* **Aggiornamento e patching**:
  * Applicazione periodica di aggiornamenti semestrali di runtime (.NET e Angular) e aggiornamento delle immagini dei database.
* **Operazioni ordinarie**:
  * Monitoraggio degli endpoint `/health`, controllo dello spazio disco e rotazione annuale della chiave di firma dei token JWT.
  * **Stima presunta dell'impegno**: circa **2–4 ore/mese** di personale IT di ateneo già in organico (stima di massima per interventi ordinari, soggetta a verifica operativa).

---

## 4. Cloud vs On-Premises: motivazioni e trade-off

La scelta architetturale adottata ([ADR 0007](../decisions/0007-avvio-locale.md), [ADR 0011](../decisions/0011-strumenti-esclusi.md)) privilegia l'esecuzione in locale e on-premises. Di seguito vengono analizzate le motivazioni e le condizioni di convenienza.

| Criterio | Approccio On-Premises (Attuale/Consigliato) | Approccio Cloud (IaaS/PaaS) |
|---|---|---|
| **Costi vivi** | **Quasi nulli**: sfrutta infrastrutture di virtualizzazione già esistenti in ateneo (VMware, Proxmox, server fisici di polo). | **Costi mensili fissi e a consumo** per VM, managed database (RDS/Cosmos), egress network e IP pubblici. |
| **Complessità operativa** | Semplice: `docker-compose.yml` riproducibile su qualsiasi server Linux locale con Docker Engine. | Maggiore: configurazione IAM, Virtual Private Cloud (VPC), security group, certificati gestiti e vincoli di governance. |
| **Privacy e GDPR** | **Massima sovranità**: nessun dato (nomi, email, matricole, orari di presenza) lascia la rete di ateneo. | Richiede accordi DPA (Data Processing Agreement), verifica della localizzazione europea dei datacenter e valutazione di impatto privacy. |
| **Latenza** | Minima: traffico instradato interamente sulla rete LAN/Wi-Fi del campus. | Dipendente dalla connettività internet dell'ateneo verso la regione cloud del provider. |

### Quando considerare la migrazione al Cloud
L'adozione del cloud diverrebbe economicamente e tecnicamente giustificata solo se:
1. L'ateneo dispone di **convenzioni ed accordi quadro gratuiti o convenzionati** (es. crediti universitari AWS/Azure/GCP o convenzioni GARR/Consip) che azzerano la spesa.
2. Manca personale sistemistico interno in grado di garantire continuità operativa e si preferisce delegare la manutenzione dei database a servizi gestiti (PostgreSQL Managed, MongoDB Atlas).
3. Si richiede disponibilità distribuita multi-datacenter (Multi-AZ Disaster Recovery) per garantire continuità anche in caso di guasto elettrico o di connettività dell'intero polo locale.

---

## 5. Scenari futuri e variazione di scala

L'architettura attuale è modulare e disaccoppiata per contesto ([ADR 0002](../decisions/0002-confini-dei-servizi.md)), ma è stata dimensionata per un singolo campus e per interazioni dichiarative. In uno sviluppo futuro di scala superiore andranno considerati due scenari evolutivi:

### 5.1 Scenario Multi-Campus (scalabilità orizzontale)
* **Descrizione**: un ateneo con 10 o più sedi/poli universitari dislocati in diverse città (20.000–50.000 studenti).
* **Impatto sul modello dati**:
  * Introduzione del partizionamento per `CampusId` in Spaces e Bookings.
  * Federazione dell'autenticazione tramite Single Sign-On istituzionale (SPID, CIE, SAML/Shibboleth, EduGAIN) integrata nel servizio Users.
* **Impatto infrastrutturale**:
  * I microservizi backend possono scalare orizzontalmente dietro un reverse proxy o un Ingress Controller (Kubernetes/K8s).
  * Il database PostgreSQL di Bookings può beneficiare di repliche di lettura (Read Replicas) per assorbire le query di consultazione disponibilità e statistiche, lasciando al nodo primario solo le transazioni di creazione/annullamento con lock.

### 5.2 Scenario IoT e Big Data (sensori di presenza reali)
* **Descrizione**: superare il dato meramente *dichiarato* dal commento finale tramite l'installazione di sensori fisici nelle aule (sensori PIR sotto le sedute, varchi di conteggio persone a infrarossi/telecamera, badge reader all'ingresso).
* **Impatto architetturale (cambio di paradigma)**:
  * **Dal modello REST sincrono a un'architettura Event-Driven**: l'invio continuo di telemetria da centinaia di sensori (es. un segnale ogni 30 secondi) saturerebbe le connessioni HTTP dirette verso i database relazionali.
  * Sarebbe necessario introdurre un broker di messaggi leggero e ad alta velocità (MQTT / Apache Kafka / RabbitMQ) per raccogliere gli eventi di passaggio e presenza.
  * I dati storici di presenza confluirebbero in un database per serie temporali (TimescaleDB / InfluxDB) per analisi Big Data su flussi di movimento e consumo energetico (riscaldamento/climatizzazione).
* **Valore aggiunto reale**:
  * **Rilascio automatico certificato (No-Show Cancellation)**: se l'aula non registra presenze fisiche entro i primi 15 minuti dalla fascia oraria, la prenotazione viene revocata d'ufficio e lo spazio torna immediatamente disponibile per altri studenti, eliminando l'inefficienza degli spazi "bloccati sulla carta ma vuoti nella realtà".

---

## 6. Indicatori di revisione architetturale

Le scelte correnti del sistema SmartCampus rimangono valide ed economicamente convenienti finché si mantengono i seguenti parametri operativi (espressi come **soglie stimate di progetto**, non come limiti massimi provati su test di stress estremi):

| Parametro / Metrica | Soglia stimata dell'architettura attuale | Segnale di revisione / Nuovo approccio |
|---|---|---|
| **Volume utenti contemporanei** | Stima: < 500 connessioni simultanee | Oltre tale soglia: scalare Bookings orizzontalmente con Load Balancer. |
| **Tempo di risposta del catalogo** | Obiettivo stimato: < 100 ms su rete locale | Se supera 500 ms: caching distribuito (Redis) per la disponibilità giornaliera. |
| **Volume telemetrico sensori** | Nessun sensore (solo commenti manuali) | Con sensori IoT: broker MQTT e serie temporali, superando il solo REST sincrono. |
| **Distribuzione geografica** | Polo singolo / Campus locale | Sedi multiple con latenza elevata: partizionamento database per sede o cluster managed. |

---

## 7. Sintesi per il Product Owner

L'architettura sviluppata garantisce **costi bassi per la demo e per un primo pilota** (da verificare poi sul campo con dati reali):
1. **Investimento iniziale e ricorrente ridotto al minimo**: zero licenze software, zero costi di account cloud per la demo e l'esercizio pilota locale, adozione di tecnologie open-source e standard (.NET 10, PostgreSQL, MongoDB, Angular).
2. **Affidabilità e consistenza**: i vincoli di non sovrapposizione e capienza sono garantiti nativamente dal motore relazionale di PostgreSQL ([ADR 0005](../decisions/0005-vincoli-di-esclusione.md)), senza la fragilità di complessi meccanismi di sincronizzazione distribuiti.
3. **Evolutività trasparente**: la separazione netta tra i tre contesti di dominio consente di estendere singole funzionalità (come l'integrazione di sensori o il passaggio al Single Sign-On) senza riscrivere l'intero sistema.

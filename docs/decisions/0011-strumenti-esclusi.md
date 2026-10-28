# 0011 – Strumenti esclusi: cloud, telemetria, Aspire, YARP, RabbitMQ, Blazor

- Data: 26/10/2026
- Stato: accettata

## Contesto
Le slide dicono che nessun framework, database o servizio è imposto e che va motivata ogni inclusione ed esclusione. Il cloud è facoltativo e solo senza spese; una telemetria artificiale è una scelta possibile, non un obbligo; il Big Data resta uno sviluppo futuro.

## Decisione
Restano fuori dal progetto:

| Strumento | Perché no |
|---|---|
| **Cloud** (deploy) | La demo locale è sufficiente; un deploy richiederebbe account e potenzialmente spese, escluse dalle slide |
| **Telemetria artificiale** | Non serve al bisogno: le statistiche si basano su prenotazioni e commenti finali, con i limiti dichiarati |
| **.NET Aspire** | Mai usato nel corso; Docker Compose e `dotnet run` bastano per tre servizi |
| **YARP** (gateway) | Il proxy di sviluppo di Angular fa lo stesso lavoro in locale |
| **RabbitMQ** con outbox | Nessuna funzione lo richiede ([0003](0003-rest-sincrono.md)) |
| **Blazor** | Il gruppo conosce Angular ([0006](0006-frontend-angular.md)) |
| **Testcontainers** | Il database di Docker Compose è già reale ([0009](0009-test.md)) |

## Alternative considerate
- **Cloud:** al suo posto la demo in locale con Docker Compose ([0007](0007-avvio-locale.md)).
- **Telemetria:** al suo posto le statistiche calcolate da prenotazioni e commenti finali, con i limiti dichiarati.
- Per gli altri strumenti l'alternativa scelta è nell'ADR collegato nella tabella.

## Conseguenze
- Più facile: meno strumenti nuovi da imparare in quattro giorni, nessuna spesa, nessun account.
- Più difficile: niente deploy da mostrare; il prodotto gira solo in locale.
- Restano possibili sviluppi futuri da presentare: deploy cloud senza spese, scenario Big Data con molti campus e sensori.

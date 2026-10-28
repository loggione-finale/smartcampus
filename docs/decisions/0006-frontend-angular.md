# 0006 – Frontend: Angular e Angular Material

- Data: 26/10/2026
- Stato: accettata

## Contesto
Le slide chiedono un'interfaccia utilizzabile negli ambienti proposti e una portabilità dimostrabile. Il PO ha indicato browser su computer e smartphone (decisione 13).

## Decisione
Angular con Angular Material, in `src/web`, con un proxy di sviluppo verso i tre servizi (`/api/spaces`, `/api/bookings`, `/api/users`).

## Alternative considerate
- **Blazor:** tutto in C#, ma mai usato nel corso.
- **.NET MAUI:** app nativa, ma build ed emulatori lenti da configurare su 7 macchine.

## Conseguenze
- Più facile: componenti pronti (tabelle, form, selettori di data, messaggi), funziona in qualsiasi browser anche da smartphone, il proxy evita un gateway.
- Più difficile: un secondo linguaggio (TypeScript) e Node.js da installare.
- Da provare: portabilità su due sistemi operativi e su uno smartphone (#93).

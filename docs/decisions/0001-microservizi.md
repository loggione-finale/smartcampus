# 0001 – Stile architetturale: tre microservizi

- Data: 26/10/2026
- Stato: accettata

## Contesto
Sette persone lavorano in parallelo per quattro giorni sullo stesso prodotto. Serve un modo di dividere il lavoro con pochi conflitti, e ogni parte deve potersi avviare e provare da sola. Le slide non impongono un'architettura, ma chiedono di motivarla.

## Decisione
Microservizi, ma **solo tre** (Spaces, Bookings, Users), con gli strumenti già visti nel corso: ASP.NET Core Web API con controller, comunicazione solo REST, la regola critica (nessuna sovrapposizione) dentro un solo servizio.

## Alternative considerate
- **Monolite modulare:** un solo processo e un solo database, avvio, debug e transazioni più semplici. Scartato perché i confini restano affidati alla disciplina, i conflitti di merge tra 7 persone aumentano e un guasto ferma tutto.
- **Più di tre servizi** (per esempio un servizio Usage separato per le statistiche): scartato in [0002](0002-confini-dei-servizi.md).

## Conseguenze
- Più facile: lavoro in parallelo per area (`.github/CODEOWNERS`), un servizio si avvia e si prova da solo, un guasto resta limitato a una parte del prodotto.
- Più difficile: più processi da avviare, chiamate di rete che possono fallire, nessuna foreign key né transazione tra servizi.
- Rischio che resta: tempi di avvio e di integrazione più lunghi; mitigato con un solo `docker compose` per i database e le istruzioni del README.

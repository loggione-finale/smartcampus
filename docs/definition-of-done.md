# Definition of Done

> Bozza iniziale: il gruppo la discute, la modifica e la concorda. Ogni modifica passa da una PR.

Un elemento del backlog è **Done** quando:

- [ ] i criteri di accettazione concordati con il PO sono soddisfatti e dimostrabili;
- [ ] il codice è integrato in `main` tramite PR approvata da un membro diverso dall'autore;
- [ ] il progetto compila senza errori;
- [ ] esistono test pertinenti e sono stati **eseguiti**, con esito registrato;
- [ ] le proprietà di concorrenza e persistenza sono provate sul **database reale**, non solo con mock;
- [ ] i dati sintetici necessari sono nel repository o generabili da script;
- [ ] nessun segreto e nessun dato personale reale sono stati aggiunti;
- [ ] i controlli di sicurezza e i test previsti per la componente in
      [security-and-testing.md](security-and-testing.md) sono presenti e passano;
- [ ] README e documentazione pertinente sono aggiornati;
- [ ] i limiti noti sono dichiarati in [`docs/evidence/`](evidence/).

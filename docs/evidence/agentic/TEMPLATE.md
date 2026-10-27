# Uso di un agente: <ruolo> su <PR o issue>

> Copia questo file come `AAAA-MM-GG-<ruolo>-<numero>.md` e compilalo subito dopo l'uso. Campi richiesti dalla
> slide 72: task, strumento, proposta, verifiche eseguite, esito e revisore.

| Campo | Valore |
|---|---|
| Data | <GG/MM/AAAA> |
| Ruolo | <scheda usata, per esempio [code-reviewer](../../agents/code-reviewer.md)> |
| Task | <PR #N o issue #N, con una riga su cosa doveva fare l'agente> |
| Chi ha guidato l'agente | <nome GitHub> |
| Strumento | <nome dello strumento autorizzato e modello, se lo mostra> |
| Durata | <minuti> |
| Consumo | <token o richieste, se lo strumento lo mostra; altrimenti "non disponibile"> |

## Input dato all'agente

<File, comandi e testo forniti. Conferma che non c'erano segreti né dati personali reali.>

## Cosa ha proposto

<Riassunto del rapporto, della bozza di review o della modifica. Allega o collega l'output completo.>

## Verifiche eseguite

| Affermazione dell'agente | Come l'ho controllata | Esito |
|---|---|---|
| <per esempio "il test X dipende dall'ordine"> | <comando eseguito o file letto> | Confermata / Scartata / Non verificata |

## Esito

- **Accettato:** <cosa è entrato nella PR, nella review o nella board>
- **Scartato:** <cosa non era corretto, con il motivo>
- **Interruzioni:** <se l'agente si è fermato, perché>

## Revisore

<Nome GitHub di chi ha rivisto il risultato, diverso da chi ha guidato l'agente quando il risultato entra in
`main`.>

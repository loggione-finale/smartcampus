# Prove e limiti noti

Per ogni prova significativa registriamo:

| Campo | Contenuto |
|---|---|
| Cosa verifica | Il comportamento o criterio di accettazione |
| Come | Comando eseguito, dati usati, ambiente |
| Esito atteso | Definito dal gruppo prima dell'esecuzione |
| Esito osservato | Cosa è successo davvero, con data |
| Limiti | Cosa questa prova **non** dimostra |

Distinguiamo sempre ciò che è stato provato da ciò che è soltanto ipotizzato.

## Limiti noti

- **Prenotazioni**: la prova di concorrenza (`booking-concurrency.md`, #36) copre solo il livello database, non l'endpoint HTTP; la verifica negativa (senza vincolo o lock la prova fallisce) è solo manuale, e nel caso dell'aula mista il numero esatto di accettate non è deterministico (garantito solo il tetto di capienza).

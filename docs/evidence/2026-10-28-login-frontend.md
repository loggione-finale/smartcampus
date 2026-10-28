# Login, interceptor e guard nel frontend (#82)

| Campo | Contenuto |
|---|---|
| Cosa verifica | Accesso con email o matricola, sessione in `sessionStorage`, `Authorization: Bearer`, guard utente/admin, logout e ritorno al login con token scaduto |
| Come | `npm run build` e `npm test -- --watch=false --browsers=ChromeHeadless` in `src/web`; lettura del contratto `POST /api/users/auth/login` in `AuthController.cs` |
| Esito atteso | Build senza errori, test verdi, utente `User` respinto da `/admin`, sessione cancellata su `401` |
| Esito osservato | Build completata; 89 test su 89 passati il 28/10/2026 dopo l’allineamento a `main`. Warning: chunk iniziale 814,53 kB (budget 600 kB) e `app.scss` 11,99 kB (soglia di warning 10 kB). |
| Limiti | Vedi sotto |

## Prove automatiche

- `auth-session.service.spec.ts`: salvataggio, ripristino, scadenza, storage corrotto, ruolo dal token, chiavi legacy, logout.
- `auth.service.spec.ts`: body `{ username, password }`, token salvato solo su risposta valida.
- `auth.interceptor.spec.ts`: Bearer sulle API protette, esclusione di login/registrazione, `401` singolo e concorrente, `403` senza logout, token scaduto.
- `auth.guard.spec.ts`: anonimo, sessione valida, sessione scaduta, `User` su admin, `Admin` su admin.
- `login.page.spec.ts`: form invalido, login User/Admin, ritorno all’URL richiesto, `401` generico, `5xx`, `returnUrl` non sicuro, avviso di sessione scaduta.
- `account.page.spec.ts`: dati mostrati e logout. La dashboard admin della issue #74 resta coperta da `admin/dashboard.page.spec.ts`.

## Limiti noti

- **XSS**: il token è in `sessionStorage` ed è leggibile da script nella stessa origine. Le difese sono l’interpolazione Angular, l’assenza di `innerHTML` e i controlli sulle dipendenze; un XSS comprometterebbe comunque il token.
- **Ruolo lato client**: la UI legge il ruolo dai claim del token, ma il browser non può verificarne la firma. La schermata admin è solo comodità: il controllo vero è la policy `Admin` del backend, che risponde `403`.
- **Logout**: è solo client-side. Il JWT resta valido fino alla scadenza di 8 ore; non esiste revoca server-side.
- **Scadenza**: viene riconosciuta alla prima navigazione o chiamata API successiva; non c’è un timer che sposta l’utente mentre la pagina è ferma.
- **Multi-scheda**: `sessionStorage` è per scheda; login e logout non si propagano tra schede.
- **Pagine admin**: `/admin` monta la dashboard della issue #74 ed è protetta da `adminGuard`; le chiavi legacy restano sincronizzate perché `admin-api.service.ts` legge ancora `smartcampus.token` tramite `admin-session.ts`, da rimuovere quando tutte le pagine admin useranno `AuthSessionService`.
- **Proxy**: il frontend usa `/api/users/auth/login`; il rewrite di `proxy.conf.json` lo porta a `/auth/login`. Un deploy senza lo stesso rewrite richiede una configurazione equivalente.

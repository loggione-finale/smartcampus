// Eseguito da MongoDB solo al primo avvio, quando il volume è vuoto.
// Crea l'utente del servizio Spaces, con accesso solo ai propri database (normale e di test).
// La password arriva dal file .env tramite docker-compose.yml.

db.getSiblingDB('spaces').createUser({
  user: 'spaces_app',
  pwd: process.env.SPACES_DB_PASSWORD,
  roles: [
    { role: 'readWrite', db: 'spaces' },
    { role: 'readWrite', db: 'spaces_test' },
  ],
});

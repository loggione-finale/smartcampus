#!/bin/bash
# Eseguito da PostgreSQL solo al primo avvio, quando il volume è vuoto.
# Crea un utente per servizio, proprietario solo dei propri database (normale e di test).
# Le password arrivano dal file .env tramite docker-compose.yml.

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  -v bookings_pw="$BOOKINGS_DB_PASSWORD" \
  -v users_pw="$USERS_DB_PASSWORD" <<'EOSQL'
CREATE ROLE bookings_app LOGIN PASSWORD :'bookings_pw';
CREATE ROLE users_app LOGIN PASSWORD :'users_pw';

CREATE DATABASE bookings OWNER bookings_app;
CREATE DATABASE bookings_test OWNER bookings_app;
CREATE DATABASE users OWNER users_app;
CREATE DATABASE users_test OWNER users_app;

-- Nessuno si collega ai database degli altri: resta solo il proprietario
REVOKE CONNECT ON DATABASE bookings, bookings_test, users, users_test FROM PUBLIC;
EOSQL

export type ServiceArea = 'spaces' | 'bookings' | 'users' | 'unknown';

const HEALTH_CHECK = /^\/api\/(?:spaces|bookings|users)\/health(?:[?#]|$)/;

const UNAVAILABLE_MESSAGES: Readonly<Record<ServiceArea, string>> = {
  spaces: 'Catalogo momentaneamente non disponibile. Riprova tra poco.',
  bookings: 'Prenotazioni momentaneamente non disponibili. Riprova tra poco.',
  users: 'Accesso momentaneamente non disponibile. Riprova tra poco.',
  unknown: 'Servizio momentaneamente non disponibile. Riprova tra poco.',
};

const UNREACHABLE_MESSAGES: Readonly<Record<ServiceArea, string>> = {
  spaces: 'Il catalogo degli spazi non è raggiungibile. Controlla la connessione e riprova.',
  bookings: 'Il servizio prenotazioni non è raggiungibile. Controlla la connessione e riprova.',
  users: 'Il servizio di accesso non è raggiungibile. Controlla la connessione e riprova.',
  unknown: 'Il servizio non è raggiungibile. Controlla la connessione e riprova.',
};

const TIMEOUT_MESSAGES: Readonly<Record<ServiceArea, string>> = {
  spaces: 'Il catalogo degli spazi non risponde in tempo. Riprova tra poco.',
  bookings: 'Il servizio prenotazioni non risponde in tempo. Riprova tra poco.',
  users: 'Il servizio di accesso non risponde in tempo. Riprova tra poco.',
  unknown: 'Il servizio non risponde in tempo. Riprova tra poco.',
};

/** Maps an API URL to the service that owns it. */
export function serviceAreaFor(url: string): ServiceArea {
  const path = url.split(/[?#]/, 1)[0];
  if (path === '/api/spaces' || path.startsWith('/api/spaces/')) {
    return 'spaces';
  }
  if (
    path === '/api/bookings' ||
    path.startsWith('/api/bookings/') ||
    path === '/api/invitations' ||
    path.startsWith('/api/invitations/')
  ) {
    return 'bookings';
  }
  if (path === '/api/users' || path.startsWith('/api/users/')) {
    return 'users';
  }
  return 'unknown';
}

/**
 * Health checks feed the service matrix on the home page, which already shows
 * "Non raggiungibile": a snackbar for each one would only add noise.
 */
export function isHealthCheck(url: string): boolean {
  return HEALTH_CHECK.test(url);
}

/**
 * Message for the snackbar. The caller passes `0` for an unreachable service
 * (offline, connection refused) and `504` for a request that timed out.
 */
export function serviceUnavailableMessage(url: string, status: number): string {
  const area = serviceAreaFor(url);
  if (status === 0) {
    return UNREACHABLE_MESSAGES[area];
  }
  if (status === 504) {
    return TIMEOUT_MESSAGES[area];
  }
  return UNAVAILABLE_MESSAGES[area];
}

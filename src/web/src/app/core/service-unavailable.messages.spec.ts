import {
  isHealthCheck,
  serviceAreaFor,
  serviceUnavailableMessage,
} from './service-unavailable.messages';

describe('serviceUnavailableMessage', () => {
  it('maps each API path to its service', () => {
    expect(serviceAreaFor('/api/spaces/spaces?type=Study')).toBe('spaces');
    expect(serviceAreaFor('/api/spaces/space-1/bookable')).toBe('spaces');
    expect(serviceAreaFor('/api/bookings/bookings/mine')).toBe('bookings');
    expect(serviceAreaFor('/api/invitations/mine')).toBe('bookings');
    expect(serviceAreaFor('/api/users/auth/login')).toBe('users');
    expect(serviceAreaFor('/api/users?emails=a@smartcampus.test')).toBe('users');
    expect(serviceAreaFor('https://example.test/api/spaces')).toBe('unknown');
    expect(serviceAreaFor('/api/other')).toBe('unknown');
  });

  it('recognises the health checks of the three services', () => {
    expect(isHealthCheck('/api/spaces/health')).toBeTrue();
    expect(isHealthCheck('/api/bookings/health?full=true')).toBeTrue();
    expect(isHealthCheck('/api/users/health')).toBeTrue();
    expect(isHealthCheck('/api/bookings/bookings/mine')).toBeFalse();
    expect(isHealthCheck('/api/users/health-details')).toBeFalse();
  });

  it('names the service that is temporarily unavailable', () => {
    expect(serviceUnavailableMessage('/api/spaces/spaces', 503)).toBe(
      'Catalogo momentaneamente non disponibile. Riprova tra poco.',
    );
    expect(serviceUnavailableMessage('/api/bookings/bookings/mine', 503)).toBe(
      'Prenotazioni momentaneamente non disponibili. Riprova tra poco.',
    );
    expect(serviceUnavailableMessage('/api/invitations/mine', 503)).toBe(
      'Prenotazioni momentaneamente non disponibili. Riprova tra poco.',
    );
    expect(serviceUnavailableMessage('/api/users/auth/login', 503)).toBe(
      'Accesso momentaneamente non disponibile. Riprova tra poco.',
    );
    expect(serviceUnavailableMessage('/api/other', 503)).toBe(
      'Servizio momentaneamente non disponibile. Riprova tra poco.',
    );
  });

  it('distinguishes an unreachable service from a timeout', () => {
    expect(serviceUnavailableMessage('/api/spaces/spaces', 0)).toBe(
      'Il catalogo degli spazi non è raggiungibile. Controlla la connessione e riprova.',
    );
    expect(serviceUnavailableMessage('/api/bookings/bookings/mine', 0)).toBe(
      'Il servizio prenotazioni non è raggiungibile. Controlla la connessione e riprova.',
    );
    expect(serviceUnavailableMessage('/api/users/auth/login', 0)).toBe(
      'Il servizio di accesso non è raggiungibile. Controlla la connessione e riprova.',
    );
    expect(serviceUnavailableMessage('/api/spaces/spaces', 504)).toBe(
      'Il catalogo degli spazi non risponde in tempo. Riprova tra poco.',
    );
    expect(serviceUnavailableMessage('/api/bookings/bookings/mine', 504)).toBe(
      'Il servizio prenotazioni non risponde in tempo. Riprova tra poco.',
    );
    expect(serviceUnavailableMessage('/api/other', 504)).toBe(
      'Il servizio non risponde in tempo. Riprova tra poco.',
    );
  });
});

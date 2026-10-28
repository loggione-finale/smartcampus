import { TestBed } from '@angular/core/testing';
import {
  AUTH_EXPIRES_AT_STORAGE_KEY,
  AUTH_TOKEN_STORAGE_KEY,
  AUTH_USER_STORAGE_KEY,
  LEGACY_AUTH_ROLE_STORAGE_KEY,
  LEGACY_AUTH_TOKEN_STORAGE_KEY,
} from './auth-session';
import { AuthSessionService } from './auth-session.service';
import { LoginResponse } from './auth.models';

function unsignedToken(payload: Record<string, unknown>): string {
  const encode = (value: Record<string, unknown>): string =>
    btoa(JSON.stringify(value)).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
  return `${encode({ alg: 'none' })}.${encode(payload)}.signature`;
}

const LOGIN_RESPONSE: LoginResponse = {
  token: 'token-123',
  tokenType: 'Bearer',
  expiresIn: 28_800,
  expiresAt: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString(),
  user: {
    id: 'b7e1c2d3-0000-4000-8000-000000000002',
    matricola: '1000000001',
    firstName: 'Giulia',
    lastName: 'Rossi',
    displayName: 'Giulia Rossi',
    email: 'giulia.rossi@smartcampus.test',
    phoneNumber: '+393331122334',
    role: 'User',
    createdAt: '2026-10-27T10:00:00Z',
  },
};

describe('AuthSessionService', () => {
  let service: AuthSessionService;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({});
    service = TestBed.inject(AuthSessionService);
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  it('stores the token, user and expiry in sessionStorage and keeps legacy keys in sync', () => {
    service.store(LOGIN_RESPONSE);

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.isAdmin()).toBeFalse();
    expect(service.user()?.email).toBe('giulia.rossi@smartcampus.test');
    expect(sessionStorage.getItem(AUTH_TOKEN_STORAGE_KEY)).toBe('token-123');
    expect(sessionStorage.getItem(AUTH_USER_STORAGE_KEY)).toContain('Giulia Rossi');
    expect(sessionStorage.getItem(AUTH_EXPIRES_AT_STORAGE_KEY)).not.toBeNull();
    expect(sessionStorage.getItem(LEGACY_AUTH_TOKEN_STORAGE_KEY)).toBe('token-123');
    expect(sessionStorage.getItem(LEGACY_AUTH_ROLE_STORAGE_KEY)).toBe('User');
  });

  it('restores a valid session from sessionStorage', () => {
    service.store(LOGIN_RESPONSE);

    const restored = new AuthSessionService();

    expect(restored.isAuthenticated()).toBeTrue();
    expect(restored.user()?.displayName).toBe('Giulia Rossi');
  });

  it('rejects and clears an expired session', () => {
    service.store({
      ...LOGIN_RESPONSE,
      expiresAt: new Date(Date.now() - 1000).toISOString(),
    });

    expect(service.hasValidSession()).toBeFalse();
    expect(service.hasSession()).toBeFalse();
    expect(sessionStorage.getItem(AUTH_TOKEN_STORAGE_KEY)).toBeNull();
  });

  it('clears corrupted storage instead of crashing', () => {
    sessionStorage.setItem(AUTH_TOKEN_STORAGE_KEY, 'token-123');
    sessionStorage.setItem(AUTH_USER_STORAGE_KEY, '{not-json');
    sessionStorage.setItem(AUTH_EXPIRES_AT_STORAGE_KEY, String(Date.now() + 60_000));

    const restored = new AuthSessionService();

    expect(restored.isAuthenticated()).toBeFalse();
    expect(sessionStorage.getItem(AUTH_TOKEN_STORAGE_KEY)).toBeNull();
  });

  it('clears canonical and legacy keys on logout', () => {
    service.store(LOGIN_RESPONSE);

    service.clear();

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.user()).toBeNull();
    expect(sessionStorage.getItem(AUTH_TOKEN_STORAGE_KEY)).toBeNull();
    expect(sessionStorage.getItem(AUTH_USER_STORAGE_KEY)).toBeNull();
    expect(sessionStorage.getItem(AUTH_EXPIRES_AT_STORAGE_KEY)).toBeNull();
    expect(sessionStorage.getItem(LEGACY_AUTH_TOKEN_STORAGE_KEY)).toBeNull();
    expect(sessionStorage.getItem(LEGACY_AUTH_ROLE_STORAGE_KEY)).toBeNull();
  });

  it('reads the role from the token instead of the mutable user field', () => {
    const token = unsignedToken({ role: 'User', sub: LOGIN_RESPONSE.user.id });

    service.store({
      ...LOGIN_RESPONSE,
      token,
      user: { ...LOGIN_RESPONSE.user, role: 'Admin' },
    });

    expect(service.isAdmin()).toBeFalse();
    expect(service.role()).toBe('User');
  });

  it('accepts an Admin role signed in the token', () => {
    const token = unsignedToken({ role: 'Admin', sub: LOGIN_RESPONSE.user.id });

    service.store({
      ...LOGIN_RESPONSE,
      token,
      user: { ...LOGIN_RESPONSE.user, role: 'User' },
    });

    expect(service.isAdmin()).toBeTrue();
  });

  it('flags a session that was already expired when the service started', () => {
    sessionStorage.setItem(AUTH_TOKEN_STORAGE_KEY, 'token-123');
    sessionStorage.setItem(AUTH_USER_STORAGE_KEY, JSON.stringify(LOGIN_RESPONSE.user));
    sessionStorage.setItem(AUTH_EXPIRES_AT_STORAGE_KEY, String(Date.now() - 1000));

    const restored = new AuthSessionService();

    expect(restored.isAuthenticated()).toBeFalse();
    expect(restored.consumeExpiredNotice()).toBeTrue();
    expect(restored.consumeExpiredNotice()).toBeFalse();
  });

  it('rejects an incomplete login response without creating a session', () => {
    const stored = service.store({ ...LOGIN_RESPONSE, token: '' });

    expect(stored).toBeFalse();
    expect(service.isAuthenticated()).toBeFalse();
    expect(sessionStorage.getItem(AUTH_TOKEN_STORAGE_KEY)).toBeNull();
  });
});

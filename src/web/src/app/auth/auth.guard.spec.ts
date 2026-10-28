import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { adminGuard, authGuard } from './auth.guard';
import { AuthSessionService } from './auth-session.service';
import { LoginResponse } from './auth.models';

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

const ADMIN_RESPONSE: LoginResponse = {
  ...LOGIN_RESPONSE,
  user: { ...LOGIN_RESPONSE.user, role: 'Admin' },
};

describe('auth guards', () => {
  let router: Router;
  let session: AuthSessionService;

  const route = {} as ActivatedRouteSnapshot;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    router = TestBed.inject(Router);
    session = TestBed.inject(AuthSessionService);
  });

  afterEach(() => {
    sessionStorage.clear();
  });

  function runGuard(guard: typeof authGuard, url: string) {
    const state = { url } as RouterStateSnapshot;
    return TestBed.runInInjectionContext(() => guard(route, state));
  }

  it('redirects an anonymous visitor to login with the requested URL', () => {
    const result = runGuard(authGuard, '/account');

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/login?returnUrl=%2Faccount');
  });

  it('allows a user with a valid session', () => {
    session.store(LOGIN_RESPONSE);

    expect(runGuard(authGuard, '/account')).toBeTrue();
  });

  it('clears an expired session and redirects to login with reason=expired', () => {
    session.store({
      ...LOGIN_RESPONSE,
      expiresAt: new Date(Date.now() - 1000).toISOString(),
    });

    const result = runGuard(authGuard, '/account');

    expect(session.isAuthenticated()).toBeFalse();
    expect(router.serializeUrl(result as UrlTree)).toBe(
      '/login?returnUrl=%2Faccount&reason=expired',
    );
  });

  it('sends a User away from admin routes', () => {
    session.store(LOGIN_RESPONSE);

    const result = runGuard(adminGuard, '/admin');

    expect(result).toBeInstanceOf(UrlTree);
    expect(router.serializeUrl(result as UrlTree)).toBe('/forbidden');
  });

  it('allows an Admin into admin routes', () => {
    session.store(ADMIN_RESPONSE);

    expect(runGuard(adminGuard, '/admin')).toBeTrue();
  });

  it('sends an anonymous visitor from admin routes to login', () => {
    const result = runGuard(adminGuard, '/admin');

    expect(router.serializeUrl(result as UrlTree)).toBe('/login?returnUrl=%2Fadmin');
  });
});

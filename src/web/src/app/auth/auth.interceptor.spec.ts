import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthSessionService } from './auth-session.service';
import { authInterceptor } from './auth.interceptor';
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

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpTesting: HttpTestingController;
  let session: AuthSessionService;
  let router: Router;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
    session = TestBed.inject(AuthSessionService);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);
  });

  afterEach(() => {
    httpTesting.verify();
    sessionStorage.clear();
  });

  it('adds the Bearer token to protected API calls', () => {
    session.store(LOGIN_RESPONSE);

    http.get('/api/spaces/spaces').subscribe();

    const request = httpTesting.expectOne('/api/spaces/spaces');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-123');
    request.flush([]);
  });

  it('does not add the Bearer token to login or registration', () => {
    session.store(LOGIN_RESPONSE);

    http.post('/api/users/auth/login', {}).subscribe();
    http.post('/api/users/auth/register', {}).subscribe();

    const loginRequest = httpTesting.expectOne('/api/users/auth/login');
    const registerRequest = httpTesting.expectOne('/api/users/auth/register');
    expect(loginRequest.request.headers.has('Authorization')).toBeFalse();
    expect(registerRequest.request.headers.has('Authorization')).toBeFalse();
    loginRequest.flush({});
    registerRequest.flush({});
  });

  it('recognises auth endpoints with a trailing slash or query string', () => {
    session.store(LOGIN_RESPONSE);

    http.post('/api/users/auth/login/?next=1', {}).subscribe();

    const request = httpTesting.expectOne('/api/users/auth/login/?next=1');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });

  it('clears the session and redirects to login on a 401 from a protected call', () => {
    session.store(LOGIN_RESPONSE);

    http.get('/api/spaces/spaces').subscribe({ error: () => undefined });
    const request = httpTesting.expectOne('/api/spaces/spaces');
    request.flush(
      { title: 'Unauthorized' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(session.isAuthenticated()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: jasmine.objectContaining({ reason: 'expired' }),
    });
  });

  it('does not redirect when the login endpoint returns 401', () => {
    session.store(LOGIN_RESPONSE);

    http.post('/api/users/auth/login', {}).subscribe({ error: () => undefined });
    const request = httpTesting.expectOne('/api/users/auth/login');
    request.flush(
      { title: 'Unauthorized' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('keeps the session on 403 responses', () => {
    session.store(LOGIN_RESPONSE);

    http.get('/api/bookings/admin/dashboard').subscribe({ error: () => undefined });
    const request = httpTesting.expectOne('/api/bookings/admin/dashboard');
    request.flush(
      { title: 'Forbidden' },
      { status: 403, statusText: 'Forbidden' },
    );

    expect(session.isAuthenticated()).toBeTrue();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('redirects only once when several protected calls return 401', () => {
    session.store(LOGIN_RESPONSE);

    http.get('/api/spaces/spaces').subscribe({ error: () => undefined });
    http.get('/api/bookings/bookings').subscribe({ error: () => undefined });

    httpTesting
      .expectOne('/api/spaces/spaces')
      .flush({}, { status: 401, statusText: 'Unauthorized' });
    httpTesting
      .expectOne('/api/bookings/bookings')
      .flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(session.isAuthenticated()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledTimes(1);
  });

  it('clears an expired session after the server rejects the request', () => {
    session.store({
      ...LOGIN_RESPONSE,
      expiresAt: new Date(Date.now() - 1000).toISOString(),
    });

    http.get('/api/spaces/spaces').subscribe({ error: () => undefined });
    const request = httpTesting.expectOne('/api/spaces/spaces');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(session.isAuthenticated()).toBeFalse();
    expect(router.navigate).toHaveBeenCalledTimes(1);
  });
});

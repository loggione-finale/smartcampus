import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthSessionService } from './auth-session.service';
import { AuthService } from './auth.service';
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

describe('AuthService', () => {
  let service: AuthService;
  let session: AuthSessionService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    session = TestBed.inject(AuthSessionService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    sessionStorage.clear();
  });

  it('posts the identifier and password to the Users login endpoint', () => {
    service.login({ identifier: ' 1000000001 ', password: 'Password123' }).subscribe();

    const request = httpTesting.expectOne('/api/users/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      username: '1000000001',
      password: 'Password123',
    });
    request.flush(LOGIN_RESPONSE);

    expect(session.isAuthenticated()).toBeTrue();
    expect(session.token()).toBe('token-123');
  });

  it('does not store a session when the server rejects the credentials', () => {
    service.login({ identifier: 'giulia.rossi@smartcampus.test', password: 'wrong' }).subscribe({
      error: () => undefined,
    });

    const request = httpTesting.expectOne('/api/users/auth/login');
    request.flush(
      { title: 'Unauthorized', detail: 'Invalid credentials.' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(session.isAuthenticated()).toBeFalse();
  });
});

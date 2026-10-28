import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { AuthSessionService } from './auth-session.service';
import { LoginPage } from './login.page';
import { LoginResponse } from './auth.models';

type LoginPageTest = {
  loginForm: FormGroup<{
    identifier: FormControl<string>;
    password: FormControl<string>;
  }>;
  submit(): void;
  showPassword(): boolean;
  togglePassword(): void;
};

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

describe('LoginPage', () => {
  let fixture: ComponentFixture<LoginPage>;
  let httpTesting: HttpTestingController;
  let router: Router;
  let session: AuthSessionService;
  let routeParams: Record<string, string>;

  beforeEach(async () => {
    sessionStorage.clear();
    routeParams = {};

    await TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            get snapshot() {
              return { queryParamMap: convertToParamMap(routeParams) };
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginPage);
    httpTesting = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    session = TestBed.inject(AuthSessionService);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);
  });

  afterEach(() => {
    httpTesting.verify();
    sessionStorage.clear();
  });

  function page(): LoginPageTest {
    return fixture.componentInstance as unknown as LoginPageTest;
  }

  function fillForm(identifier = 'giulia.rossi@smartcampus.test', password = 'Password123'): void {
    page().loginForm.setValue({ identifier, password });
  }

  it('does not call the API when the form is invalid', () => {
    fixture.detectChanges();

    page().submit();

    expect(httpTesting.match('/api/users/auth/login').length).toBe(0);
    expect(page().loginForm.controls.identifier.touched).toBeTrue();
  });

  it('logs in, stores the session and returns to the requested URL', () => {
    routeParams = { returnUrl: '/account' };
    fixture.detectChanges();
    fillForm(' 1000000001 ', 'Password123');

    page().submit();

    const request = httpTesting.expectOne('/api/users/auth/login');
    expect(request.request.body).toEqual({
      username: '1000000001',
      password: 'Password123',
    });
    request.flush(LOGIN_RESPONSE);

    expect(session.isAuthenticated()).toBeTrue();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/account');
  });

  it('sends a User to the home page when there is no return URL', () => {
    fixture.detectChanges();
    fillForm();

    page().submit();
    httpTesting.expectOne('/api/users/auth/login').flush(LOGIN_RESPONSE);

    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('sends an Admin to the admin area when there is no return URL', () => {
    fixture.detectChanges();
    fillForm();

    page().submit();
    httpTesting.expectOne('/api/users/auth/login').flush(ADMIN_RESPONSE);

    expect(router.navigateByUrl).toHaveBeenCalledWith('/admin');
  });

  it('shows a generic message for wrong credentials and clears the password', () => {
    fixture.detectChanges();
    fillForm();

    page().submit();
    httpTesting
      .expectOne('/api/users/auth/login')
      .flush({ detail: 'Invalid credentials.' }, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain(
      'Email o matricola e password non corretti.',
    );
    expect(fixture.nativeElement.textContent).not.toContain('Invalid credentials');
    expect(page().loginForm.controls.password.value).toBe('');
    expect(router.navigateByUrl).not.toHaveBeenCalled();
    expect(session.isAuthenticated()).toBeFalse();
  });

  it('shows a temporary-service message for 5xx errors', () => {
    fixture.detectChanges();
    fillForm();

    page().submit();
    httpTesting
      .expectOne('/api/users/auth/login')
      .flush({ title: 'Error' }, { status: 503, statusText: 'Service Unavailable' });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Errore temporaneo del servizio');
  });

  it('ignores an unsafe return URL and falls back to the home page', () => {
    routeParams = { returnUrl: '//evil.example' };
    fixture.detectChanges();
    fillForm();

    page().submit();
    httpTesting.expectOne('/api/users/auth/login').flush(LOGIN_RESPONSE);

    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('shows the expired-session notice from the query string', () => {
    routeParams = { reason: 'expired' };
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('La sessione è scaduta');
  });

  it('redirects an already authenticated user away from the login page', () => {
    session.store(LOGIN_RESPONSE);
    fixture.detectChanges();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('toggles password visibility', () => {
    fixture.detectChanges();

    expect(page().showPassword()).toBeFalse();
    page().togglePassword();
    expect(page().showPassword()).toBeTrue();
  });
});

import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../app.routes';
import { AuthSessionService } from './auth-session.service';
import { LoginResponse } from './auth.models';
import { RegisterPage } from './register.page';

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

type RegistrationTestApp = {
  registrationForm: FormGroup<{
    matricola: FormControl<string>;
    firstName: FormControl<string>;
    lastName: FormControl<string>;
    email: FormControl<string>;
    phoneNumber: FormControl<string>;
    password: FormControl<string>;
  }>;
  submitRegistration(): void;
};

describe('RegisterPage', () => {
  let httpTesting: HttpTestingController;
  let harness: RouterTestingHarness;

  async function openRegister(query = '') {
    harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl(`/register${query}`, RegisterPage);
    harness.detectChanges();
    return { fixture: harness.fixture, app: page as unknown as RegistrationTestApp };
  }

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter(routes)],
    });
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    sessionStorage.clear();
  });

  it('keeps the registration form invalid until all server rules are met', async () => {
    const { fixture, app } = await openRegister();

    app.registrationForm.patchValue({
      matricola: '123',
      firstName: 'A',
      lastName: '',
      email: 'not-an-email',
      phoneNumber: '3331234567',
      password: 'short',
    });
    app.submitRegistration();
    fixture.detectChanges();

    expect(app.registrationForm.invalid).toBeTrue();
    expect(fixture.nativeElement.querySelectorAll('mat-error').length).toBeGreaterThan(0);
  });

  it('shows a duplicate email response on the email field', async () => {
    const { fixture, app } = await openRegister();

    app.registrationForm.setValue({
      matricola: '1234567890',
      firstName: 'Mario',
      lastName: 'Rossi',
      email: 'mario@example.test',
      phoneNumber: '+393331234567',
      password: 'Password123',
    });
    app.submitRegistration();

    const request = httpTesting.expectOne('/api/users/auth/register');
    expect(request.request.method).toBe('POST');
    request.flush(
      { detail: 'Email is already registered.' },
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Email is already registered.');
    expect(fixture.nativeElement.textContent).toContain('Esiste già un account');
  });

  it('navigates to the login with returnUrl after registration without a session', async () => {
    // Link dalla mail all'invitato non registrato (#233): la query sta prima del frammento.
    const { fixture, app } = await openRegister('?returnUrl=%2Finvitations%2Fabc');

    app.registrationForm.setValue({
      matricola: '1234567890',
      firstName: 'Mario',
      lastName: 'Rossi',
      email: 'mario@example.test',
      phoneNumber: '+393331234567',
      password: 'Password123',
    });
    app.submitRegistration();

    const request = httpTesting.expectOne('/api/users/auth/register');
    expect(request.request.method).toBe('POST');
    // La rotta /invitations è protetta: senza sessione l'authGuard manda al login con returnUrl.
    request.flush({}, { status: 201, statusText: 'Created' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(TestBed.inject(Router).url).toBe('/login?returnUrl=%2Finvitations%2Fabc');
  });

  it('navigates to the invitation after registration with a session', async () => {
    // Come sopra, ma con la sessione attiva: si atterra direttamente sull'invito.
    const session = TestBed.inject(AuthSessionService);
    session.store(LOGIN_RESPONSE);
    const { fixture, app } = await openRegister('?returnUrl=%2Finvitations%2Fabc');

    app.registrationForm.setValue({
      matricola: '1234567890',
      firstName: 'Mario',
      lastName: 'Rossi',
      email: 'mario@example.test',
      phoneNumber: '+393331234567',
      password: 'Password123',
    });
    app.submitRegistration();

    const request = httpTesting.expectOne('/api/users/auth/register');
    expect(request.request.method).toBe('POST');
    request.flush({}, { status: 201, statusText: 'Created' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(TestBed.inject(Router).url).toBe('/invitations/abc');
    // La pagina inviti caricata dalla rotta fa la sua GET: la chiudiamo qui.
    httpTesting.match((r) => r.url.endsWith('/api/invitations/mine')).forEach((r) => r.flush([]));
  });

  it('stays on the page after registration when returnUrl is external', async () => {
    // returnUrl esterno (//host): safeReturnUrl lo scarta, si resta sulla pagina con la conferma.
    const { fixture, app } = await openRegister('?returnUrl=%2F%2Fevil.example');

    app.registrationForm.setValue({
      matricola: '1234567890',
      firstName: 'Mario',
      lastName: 'Rossi',
      email: 'mario@example.test',
      phoneNumber: '+393331234567',
      password: 'Password123',
    });
    app.submitRegistration();

    const request = httpTesting.expectOne('/api/users/auth/register');
    expect(request.request.method).toBe('POST');
    request.flush({}, { status: 201, statusText: 'Created' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(TestBed.inject(Router).url).toBe('/register?returnUrl=%2F%2Fevil.example');
    expect(fixture.nativeElement.textContent).toContain('Il tuo account è stato creato');
  });
});

import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthSessionService } from './auth/auth-session.service';
import { LoginResponse } from './auth/auth.models';
import { App } from './app';
import { routes } from './app.routes';
import { SpacesApiService } from './spaces/spaces-api.service';
import { EquipmentCatalogItemDto, SpaceAvailabilityDto, SpaceDto } from './spaces/spaces.models';

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
  token: 'admin-token',
  user: { ...LOGIN_RESPONSE.user, role: 'Admin' },
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

const spacesApiStub = {
  searchSpaces: () => of<SpaceDto[]>([]),
  getEquipmentCatalog: () => of<EquipmentCatalogItemDto[]>([]),
  getSpaceDailyAvailability: () =>
    of<SpaceAvailabilityDto>({
      spaceId: 'space-3',
      date: '2026-10-29',
      accessMode: 'Mixed',
      capacity: 8,
      slots: [],
    }),
};

describe('App', () => {
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(routes),
        { provide: SpacesApiService, useValue: spacesApiStub },
      ],
    }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.match((request) => request.url.endsWith('/health')).forEach((request) => request.flush({}));
    httpTesting.verify();
    sessionStorage.clear();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the SmartCampus shell', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Prenota.');
    expect(compiled.querySelectorAll('.space').length).toBe(4);
    expect(compiled.querySelectorAll('.service').length).toBe(3);
  });

  it('renders the booking form in its own component', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.querySelector('app-booking-form #prenota')).not.toBeNull();
    expect(compiled.querySelector('#booking-title')?.textContent).toContain('Prenota senza sorprese');
  });

  it('keeps the registration form invalid until all server rules are met', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as RegistrationTestApp;
    fixture.detectChanges();

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

  it('shows a duplicate email response on the email field', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as RegistrationTestApp;
    fixture.detectChanges();

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
    // Link from the email to an unregistered invitee (#233): query precedes the fragment.
    window.history.replaceState({}, '', '/?returnUrl=%2Finvitations%2Fabc');
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as RegistrationTestApp;
    fixture.detectChanges();

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
    // The /invitations route is protected: without a session, authGuard redirects to login with returnUrl.
    request.flush({}, { status: 201, statusText: 'Created' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(TestBed.inject(Router).url).toBe('/login?returnUrl=%2Finvitations%2Fabc');
    window.history.replaceState({}, '', '/');
  });

  it('navigates to the invitation after registration with a session', async () => {
    // As above, but with an active session: lands directly on the invitation.
    const session = TestBed.inject(AuthSessionService);
    session.store(LOGIN_RESPONSE);
    window.history.replaceState({}, '', '/?returnUrl=%2Finvitations%2Fabc');
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as RegistrationTestApp;
    fixture.detectChanges();

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
    // The invitations page loaded by the route performs its GET request: resolve it here.
    httpTesting.match((r) => r.url.endsWith('/api/invitations/mine')).forEach((r) => r.flush([]));
    window.history.replaceState({}, '', '/');
  });

  it('stays on the home after registration when returnUrl is external', async () => {
    // External returnUrl (//host): safeReturnUrl rejects it, staying on home.
    window.history.replaceState({}, '', '/?returnUrl=%2F%2Fevil.example');
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as unknown as RegistrationTestApp;
    fixture.detectChanges();

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

    expect(TestBed.inject(Router).url).toBe('/');
    expect(fixture.nativeElement.textContent).toContain('Il tuo account è stato creato');
    window.history.replaceState({}, '', '/');
  });

  it('shows the login entry when anonymous and the personal area when authenticated', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Accedi');

    const session = TestBed.inject(AuthSessionService);
    session.store(LOGIN_RESPONSE);
    fixture.detectChanges();

    const authenticatedText = fixture.nativeElement.textContent as string;
    expect(authenticatedText).toContain('Area personale');
    expect(authenticatedText).toContain('Esci');
    expect(authenticatedText).not.toContain('Accedi');
    expect(authenticatedText).not.toContain('Admin');

    session.store(ADMIN_RESPONSE);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Admin');
  });
});

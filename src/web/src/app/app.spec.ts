import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
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
    // The homepage no longer exposes the services' health endpoints
    expect(compiled.querySelector('#servizi')).toBeNull();
    expect(compiled.querySelector('a[href*="/health"]')).toBeNull();
  });

  it('previews the booking on the home and continues on /book', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.querySelector('app-booking-teaser #prenota')).not.toBeNull();
    expect(compiled.querySelector('app-booking-form')).toBeNull();

    // Without a session the booking page sends to the login, keeping the way back.
    compiled.querySelector('app-booking-teaser form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/login?returnUrl=%2Fbook');
  });

  it('keeps registration off the home and forwards old #registrazione links to /register', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#registrazione')).toBeNull();

    const router = TestBed.inject(Router);
    await router.navigateByUrl('/?returnUrl=%2Finvitations%2Fabc#registrazione');
    expect(router.url).toBe('/register?returnUrl=%2Finvitations%2Fabc');
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

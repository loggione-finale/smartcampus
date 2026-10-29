import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { routes } from '../app.routes';
import { AuthSessionService } from '../auth/auth-session.service';
import { LoginResponse } from '../auth/auth.models';
import { SpacesApiService } from '../spaces/spaces-api.service';
import { SpaceDto } from '../spaces/spaces.models';
import { BookPage } from './book.page';
import { BookingFormComponent } from './booking-form.component';

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

const space = (id: string, name: string): SpaceDto => ({
  id,
  name,
  type: 'Classroom',
  mode: 'Dedicated',
  building: 'Edificio A',
  floor: 1,
  capacity: 30,
  isActive: true,
  features: {},
  equipment: [],
  unavailabilities: [],
  updatedAt: '2026-01-01T00:00:00Z',
});

describe('BookPage', () => {
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(routes),
        {
          provide: SpacesApiService,
          useValue: {
            searchSpaces: () => of([space('space-1', 'Aula 101'), space('space-2', 'Aula 204')]),
            getEquipmentCatalog: () => of([]),
          },
        },
      ],
    });
  });

  afterEach(() => sessionStorage.clear());

  it('prefills the form from the Prenota link in the route', async () => {
    TestBed.inject(AuthSessionService).store(LOGIN_RESPONSE);
    const start = new Date(Date.now() + 2 * 86400000);
    start.setUTCHours(9, 0, 0, 0);
    const end = new Date(start.getTime() + 90 * 60000);

    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(
      `/book?spazio=space-2&inizio=${start.toISOString()}&fine=${end.toISOString()}`,
      BookPage,
    );
    const form = harness.fixture.debugElement.query(By.directive(BookingFormComponent))
      .componentInstance as any;

    expect(form.bookingForm.controls.spaceId.value).toBe('space-2');
    expect(form.bookingDuration()).toBe(90);
  });

  it('asks for the login before showing the booking page', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/book?spazio=space-2');

    expect(TestBed.inject(Router).url).toBe('/login?returnUrl=%2Fbook%3Fspazio%3Dspace-2');
    expect(harness.routeNativeElement?.textContent ?? '').toContain('Entra con il tuo profilo');
  });
});

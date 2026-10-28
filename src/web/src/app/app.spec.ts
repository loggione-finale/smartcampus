import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { FormControl, FormGroup } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthSessionService } from './auth/auth-session.service';
import { LoginResponse } from './auth/auth.models';
import { App } from './app';
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

const SPACES: SpaceDto[] = [
  {
    id: 'space-1',
    name: 'Aula 101',
    type: 'Classroom',
    mode: 'Dedicated',
    building: 'Edificio A',
    floor: 1,
    capacity: 30,
    isActive: true,
    features: {},
    equipment: ['videoprojector'],
    unavailabilities: [],
    updatedAt: '2026-01-01T00:00:00Z',
  },
  {
    id: 'space-2',
    name: 'Sala studio chiusa',
    type: 'StudyRoom',
    mode: 'Mixed',
    building: 'Edificio B',
    floor: 0,
    capacity: 12,
    isActive: false,
    features: {},
    equipment: [],
    unavailabilities: [],
    updatedAt: '2026-01-01T00:00:00Z',
  },
  {
    id: 'space-3',
    name: 'Sala studio condivisa',
    type: 'StudyRoom',
    mode: 'Mixed',
    building: 'Edificio B',
    floor: 0,
    capacity: 8,
    isActive: true,
    features: {},
    equipment: [],
    unavailabilities: [],
    updatedAt: '2026-01-01T00:00:00Z',
  },
  {
    id: 'space-4',
    name: 'Aula mini',
    type: 'Classroom',
    mode: 'Dedicated',
    building: 'Edificio C',
    floor: 1,
    capacity: 2,
    isActive: true,
    features: {},
    equipment: [],
    unavailabilities: [],
    updatedAt: '2026-01-01T00:00:00Z',
  },
];

const EQUIPMENT: EquipmentCatalogItemDto[] = [
  { code: 'videoprojector', name: 'Videoproiettore', description: '', isStandard: true, requiresNotes: false },
  { code: 'interactive-whiteboard', name: 'Lavagna interattiva', description: '', isStandard: true, requiresNotes: false },
  { code: 'audio-kit', name: 'Kit audio', description: '', isStandard: true, requiresNotes: false },
  { code: 'microphone', name: 'Microfono', description: '', isStandard: true, requiresNotes: false },
  { code: 'other', name: 'Altro', description: '', isStandard: false, requiresNotes: true },
];

const spacesApiStub = {
  searchSpaces: () => of(SPACES),
  getEquipmentCatalog: () => of(EQUIPMENT),
  getSpaceDailyAvailability: () =>
    of<SpaceAvailabilityDto>({
      spaceId: 'space-3',
      date: '2026-10-29',
      accessMode: 'Mixed',
      capacity: 8,
      slots: [],
    }),
};

// The form validates time bands in Europe/Rome, while the inputs are in the device time zone.
// Tests build the local values from a Rome wall clock so they pass in CI too (UTC).
function romeParts(date: Date): { date: string; minutes: number } {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Europe/Rome',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(date);
  const value = (type: string) => parts.find((part) => part.type === type)?.value ?? '';
  return {
    date: `${value('year')}-${value('month')}-${value('day')}`,
    minutes: Number(value('hour')) * 60 + Number(value('minute')),
  };
}

function romeOffsetMs(instant: Date): number {
  const parts = romeParts(instant);
  const [year, month, day] = parts.date.split('-').map(Number);
  const asUtc = Date.UTC(year, month - 1, day, Math.floor(parts.minutes / 60), parts.minutes % 60);
  return asUtc - instant.getTime();
}

function localValue(instant: Date): { date: string; time: string } {
  const local = new Date(instant.getTime() - instant.getTimezoneOffset() * 60000)
    .toISOString()
    .slice(0, 16);
  return { date: local.slice(0, 10), time: local.slice(11, 16) };
}

function localSlotFromRome(romeDate: string, startRome: string, endRome: string) {
  const [year, month, day] = romeDate.split('-').map(Number);
  const instantFor = (wallClock: string) => {
    const [hour, minute] = wallClock.split(':').map(Number);
    const guess = new Date(Date.UTC(year, month - 1, day, hour, minute));
    return new Date(guess.getTime() - romeOffsetMs(guess));
  };
  const start = localValue(instantFor(startRome));
  const end = localValue(instantFor(endRome));
  return { date: start.date, start: start.time, end: end.time };
}

function romeDayOffset(days: number): string {
  return romeParts(new Date(Date.now() + days * 86400000)).date;
}

describe('App', () => {
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
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

  function createApp(): any {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  function fillValidBooking(app: any): void {
    app.bookingForm.patchValue({
      spaceId: 'space-1',
      ...localSlotFromRome(romeDayOffset(1), '10:00', '11:00'),
      equipment: [],
    });
  }

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

  it('starts from a booking slot in the future and inside a valid band', () => {
    const app = createApp();
    const { date, start, end } = app.bookingForm.getRawValue();

    expect(new Date(`${date}T${start}`).getTime()).toBeGreaterThan(Date.now());
    expect(app.bookingDuration()).toBe(60);
    expect(end).not.toBe(start);
  });

  it('loads bookable spaces from the Spaces service and skips inactive ones', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();

    expect(app.bookingSpaces().map((space: { id: string }) => space.id)).toEqual([
      'space-1',
      'space-3',
      'space-4',
    ]);
    expect(app.bookingForm.controls.spaceId.value).toBe('space-1');
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

  it('does not submit an incomplete booking form and shows the field error', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    app.bookingForm.controls.date.setValue('');

    app.submitBooking();
    fixture.detectChanges();

    expect(app.bookingState()).toBe('error');
    expect(app.bookingFieldError('date')).toContain('Scegli una data');
    expect(fixture.nativeElement.querySelector('mat-error')?.textContent).toContain('Scegli una data');
    httpTesting.expectNone('/api/bookings');
  });

  it('does not submit without a space and shows the field error', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);
    app.bookingForm.controls.spaceId.setValue('');

    app.submitBooking();
    fixture.detectChanges();

    expect(app.bookingFieldError('spaceId')).toContain('Scegli uno spazio');
    expect(fixture.nativeElement.textContent).toContain('Scegli uno spazio');
    httpTesting.expectNone('/api/bookings');
  });

  it('rejects a start time in the past before calling the service', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);
    app.bookingForm.patchValue(localSlotFromRome(romeDayOffset(-1), '10:00', '11:00'));

    app.submitBooking();
    fixture.detectChanges();

    expect(app.bookingFieldError('start')).toContain('futuro');
    expect(fixture.nativeElement.textContent).toContain('futuro');
    httpTesting.expectNone('/api/bookings');
  });

  it('rejects a booking that crosses a time band', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);
    app.bookingForm.patchValue(localSlotFromRome(romeDayOffset(1), '13:30', '14:30'));

    app.submitBooking();

    expect(app.bookingFieldError('start')).toContain('fascia');
    httpTesting.expectNone('/api/bookings');
  });

  it('rejects a duration below 30 minutes', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);
    app.bookingForm.patchValue({ start: '10:00', end: '10:15' });

    app.submitBooking();

    expect(app.bookingFieldError('end')).toContain('30 minuti');
    httpTesting.expectNone('/api/bookings');
  });

  it('rejects start times that are not a multiple of 15 minutes', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);
    app.bookingForm.patchValue({ start: '10:07', end: '11:07' });

    app.submitBooking();

    expect(app.bookingFieldError('start')).toContain('15 minuti');
    httpTesting.expectNone('/api/bookings');
  });

  it('shows the first validation message returned by Bookings', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);

    app.submitBooking();
    const request = httpTesting.expectOne('/api/bookings');
    request.flush(
      { errors: { Start: ['The start must be in the future.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    expect(app.bookingState()).toBe('error');
    expect(app.bookingFieldError('start')).toBe('The start must be in the future.');
    expect(fixture.nativeElement.textContent).toContain('The start must be in the future.');
  });

  it('updates the end time from the duration slider and stops at the band end', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);

    expect(fixture.nativeElement.querySelector('mat-slider')).not.toBeNull();
    app.setBookingDuration(90);
    expect(app.bookingDuration()).toBe(90);

    app.bookingForm.patchValue(localSlotFromRome(romeDayOffset(1), '13:00', '13:30'));
    app.setBookingDuration(240);
    expect(app.bookingDuration()).toBe(60);
  });

  it('shows the conflict returned by Bookings', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);

    app.submitBooking();
    httpTesting
      .expectOne('/api/bookings')
      .flush({ title: 'Space already booked' }, { status: 409, statusText: 'Conflict' });

    expect(app.bookingState()).toBe('error');
    expect(app.bookingMessage()).toContain('già occupata');
  });

  it('asks for a login when the service answers 401', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);

    app.submitBooking();
    httpTesting.expectOne('/api/bookings').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(app.bookingMessage()).toContain('accesso');
  });

  it('shows the reference code after a successful booking', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);

    app.submitBooking();
    httpTesting
      .expectOne('/api/bookings')
      .flush({ referenceCode: 'SC-261027-ABCD' }, { status: 201, statusText: 'Created' });

    expect(app.bookingState()).toBe('success');
    expect(app.bookingReference()).toBe('SC-261027-ABCD');
  });

  it('validates participant emails and shows equipment only for groups', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();

    const chipInput = { clear: jasmine.createSpy('clear') };
    app.addParticipant({ value: 'group@example.test', chipInput } as any);
    fixture.detectChanges();

    expect(app.participantEmails()).toEqual(['group@example.test']);
    expect(fixture.nativeElement.querySelectorAll('.equipment-field mat-checkbox').length).toBe(4);

    app.addParticipant({ value: 'group@example.test', chipInput } as any);
    fixture.detectChanges();
    expect(app.participantError()).toContain('già stata aggiunta');
    expect(fixture.nativeElement.textContent).toContain('già stata aggiunta');

    app.removeParticipant('group@example.test');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.equipment-field')).toBeNull();
  });

  it('shows the participant total and the capacity of the selected space', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    const chipInput = { clear() {} };

    app.addParticipant({ value: 'one@example.test', chipInput } as any);
    app.addParticipant({ value: 'two@example.test', chipInput } as any);
    fixture.detectChanges();

    expect(app.participantTotal()).toBe(3);
    expect(app.participantSummary()).toContain('responsabile + 2 invitati');
    expect(fixture.nativeElement.textContent).toContain('Partecipanti totali: 3');
    expect(fixture.nativeElement.textContent).toContain('Capienza: 30 posti');
  });

  it('loads the free seats when the first bookable space is mixed', () => {
    spyOn(spacesApiStub, 'searchSpaces').and.returnValue(of([SPACES[2]]));
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();

    expect(app.bookingForm.controls.spaceId.value).toBe('space-3');
    expect(fixture.nativeElement.textContent).toContain('Posti liberi nella fascia: 8 su 8');
  });

  it('does not accept the organizer email as a participant', () => {
    const fixture = TestBed.createComponent(App);
    const session = TestBed.inject(AuthSessionService);
    session.store(LOGIN_RESPONSE);
    fixture.detectChanges();
    const app = fixture.componentInstance as any;
    const chipInput = { clear() {} };

    app.addParticipant({ value: 'GIULIA.ROSSI@smartcampus.test', chipInput } as any);
    fixture.detectChanges();

    expect(app.participantEmails()).toEqual([]);
    expect(app.participantError()).toContain('responsabile');
    expect(fixture.nativeElement.textContent).toContain('non aggiungere la tua email');
  });

  it('blocks a group larger than the capacity of a dedicated space', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);
    app.bookingForm.controls.spaceId.setValue('space-4');
    const chipInput = { clear() {} };
    app.addParticipant({ value: 'one@example.test', chipInput } as any);
    app.addParticipant({ value: 'two@example.test', chipInput } as any);
    fixture.detectChanges();

    expect(app.participantSeatWarning()).toContain('Aula mini ha 2 posti');

    app.submitBooking();
    fixture.detectChanges();

    expect(app.bookingState()).toBe('error');
    expect(fixture.nativeElement.textContent).toContain('Aula mini ha 2 posti');
    httpTesting.expectNone('/api/bookings');

    app.removeParticipant('two@example.test');
    fixture.detectChanges();

    expect(app.participantTotal()).toBe(2);
    expect(app.participantSeatWarning()).toBe('');
    expect(app.bookingState()).toBe('idle');
    expect(fixture.nativeElement.textContent).not.toContain('Aula mini ha 2 posti');
  });

  it('blocks a group larger than the free seats in a mixed space', () => {
    const slot = localSlotFromRome(romeDayOffset(1), '10:00', '11:00');
    const startIso = new Date(`${slot.date}T${slot.start}`).toISOString();
    const endIso = new Date(`${slot.date}T${slot.end}`).toISOString();
    spyOn(spacesApiStub, 'getSpaceDailyAvailability').and.returnValue(
      of<SpaceAvailabilityDto>({
        spaceId: 'space-3',
        date: slot.date,
        accessMode: 'Mixed',
        capacity: 8,
        slots: [
          { start: startIso, end: endIso, occupiedSeats: 6, freeSeats: 2, isAvailable: true },
        ],
      }),
    );

    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);
    app.bookingForm.patchValue({ spaceId: 'space-3' });
    const chipInput = { clear() {} };
    for (const name of ['one', 'two', 'three']) {
      app.addParticipant({ value: `${name}@example.test`, chipInput } as any);
    }
    fixture.detectChanges();

    expect(app.participantTotal()).toBe(4);
    expect(app.participantSeatWarning()).toContain('restano 2 posti su 8');
    expect(fixture.nativeElement.textContent).toContain('Posti liberi nella fascia: 2 su 8');

    app.submitBooking();
    fixture.detectChanges();

    expect(app.bookingState()).toBe('error');
    expect(fixture.nativeElement.textContent).toContain('restano 2 posti su 8');
    httpTesting.expectNone('/api/bookings');
  });

  it('accepts a mixed group that fits in the free seats', () => {
    const slot = localSlotFromRome(romeDayOffset(1), '10:00', '11:00');
    const startIso = new Date(`${slot.date}T${slot.start}`).toISOString();
    const endIso = new Date(`${slot.date}T${slot.end}`).toISOString();
    spyOn(spacesApiStub, 'getSpaceDailyAvailability').and.returnValue(
      of<SpaceAvailabilityDto>({
        spaceId: 'space-3',
        date: slot.date,
        accessMode: 'Mixed',
        capacity: 8,
        slots: [
          { start: startIso, end: endIso, occupiedSeats: 4, freeSeats: 4, isAvailable: true },
        ],
      }),
    );

    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);
    app.bookingForm.patchValue({ spaceId: 'space-3' });
    const chipInput = { clear() {} };
    app.addParticipant({ value: 'one@example.test', chipInput } as any);
    app.addParticipant({ value: 'two@example.test', chipInput } as any);
    fixture.detectChanges();

    expect(app.participantSeatWarning()).toBe('');

    app.submitBooking();

    const request = httpTesting.expectOne('/api/bookings');
    expect(request.request.body.participantEmails).toEqual([
      'one@example.test',
      'two@example.test',
    ]);
    request.flush({ referenceCode: 'SC-261027-MIXD' }, { status: 201, statusText: 'Created' });
  });

  it('sends participant emails and equipment codes with a group booking', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);
    app.addParticipant({ value: 'group@example.test', chipInput: { clear() {} } } as any);
    app.toggleEquipment('videoprojector', true);

    app.submitBooking();

    const request = httpTesting.expectOne('/api/bookings');
    expect(request.request.body).toEqual(
      jasmine.objectContaining({
        participantEmails: ['group@example.test'],
        equipmentRequests: [{ equipmentType: 'videoprojector' }],
      }),
    );
    request.flush({ referenceCode: 'SC-261027-WXYZ' }, { status: 201, statusText: 'Created' });
  });

  it('sends the stored token when one is available', () => {
    spyOn(window.sessionStorage, 'getItem').and.callFake((key: string) =>
      key === 'smartcampus.auth.token' ? 'test-token' : null,
    );

    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(app);

    app.submitBooking();

    const request = httpTesting.expectOne('/api/bookings');
    expect(request.request.headers.get('Authorization')).toBe('Bearer test-token');
    request.flush({ referenceCode: 'SC-261027-TOKEN' }, { status: 201, statusText: 'Created' });
  });
});

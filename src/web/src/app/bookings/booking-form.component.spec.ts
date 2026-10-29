import { isSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { AuthSessionService } from '../auth/auth-session.service';
import { LoginResponse } from '../auth/auth.models';
import { SpacesApiService } from '../spaces/spaces-api.service';
import { EquipmentCatalogItemDto, SpaceAvailabilityDto, SpaceDto } from '../spaces/spaces.models';
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

const ADMIN_LOGIN_RESPONSE: LoginResponse = {
  ...LOGIN_RESPONSE,
  user: { ...LOGIN_RESPONSE.user, role: 'Admin' },
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

describe('BookingFormComponent', () => {
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [BookingFormComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SpacesApiService, useValue: spacesApiStub },
      ],
    }).compileComponents();
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    sessionStorage.clear();
  });

  function createForm(): any {
    const fixture = TestBed.createComponent(BookingFormComponent);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  function fillValidBooking(form: any): void {
    form.bookingForm.patchValue({
      spaceId: 'space-1',
      ...localSlotFromRome(romeDayOffset(1), '10:00', '11:00'),
      equipment: [],
    });
  }

  it('should create the booking form', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('shows a specific message instead of the form for administrators', () => {
    const session = TestBed.inject(AuthSessionService);
    session.store(ADMIN_LOGIN_RESPONSE);

    const fixture = TestBed.createComponent(BookingFormComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Gli amministratori non prenotano');
    expect(fixture.nativeElement.textContent).toContain('area admin');
  });

  it('limits the booking date to 15 days from today', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();

    const dateInput = fixture.nativeElement.querySelector('input[type="date"]');
    expect(dateInput.getAttribute('min')).toBe(form.minimumBookingDate);
    expect(dateInput.getAttribute('max')).toBe(form.maximumBookingDate);
    const days =
      (Date.parse(`${form.maximumBookingDate}T00:00:00Z`) -
        Date.parse(`${form.minimumBookingDate}T00:00:00Z`)) /
      86400000;
    expect(days).toBe(15);

    fillValidBooking(form);
    const [year, month, day] = form.maximumBookingDate.split('-').map(Number);
    const beyond = new Date(year, month - 1, day + 1);
    form.bookingForm.patchValue({
      date: new Date(beyond.getTime() - beyond.getTimezoneOffset() * 60000)
        .toISOString()
        .slice(0, 10),
      start: '10:00',
      end: '11:00',
    });

    form.submitBooking();
    fixture.detectChanges();

    expect(form.bookingFieldError('date')).toContain('15 giorni');
    expect(fixture.nativeElement.textContent).toContain('15 giorni');
    httpTesting.expectNone('/api/bookings');
  });

  it('starts from a booking slot in the future and inside a valid band', () => {
    const form = createForm();
    const { date, start, end } = form.bookingForm.getRawValue();

    expect(new Date(`${date}T${start}`).getTime()).toBeGreaterThan(Date.now());
    expect(form.bookingDuration()).toBe(60);
    expect(end).not.toBe(start);
  });

  it('loads bookable spaces from the Spaces service and skips inactive ones', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();

    expect(form.bookingSpaces().map((space: { id: string }) => space.id)).toEqual([
      'space-1',
      'space-3',
      'space-4',
    ]);
    expect(form.bookingForm.controls.spaceId.value).toBe('space-1');
  });

  it('does not submit an incomplete booking form and shows the field error', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    form.bookingForm.controls.date.setValue('');

    form.submitBooking();
    fixture.detectChanges();

    expect(form.bookingState()).toBe('error');
    expect(form.bookingFieldError('date')).toContain('Scegli una data');
    expect(fixture.nativeElement.querySelector('mat-error')?.textContent).toContain('Scegli una data');
    httpTesting.expectNone('/api/bookings');
  });

  it('does not submit without a space and shows the field error', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);
    form.bookingForm.controls.spaceId.setValue('');

    form.submitBooking();
    fixture.detectChanges();

    expect(form.bookingFieldError('spaceId')).toContain('Scegli uno spazio');
    expect(fixture.nativeElement.textContent).toContain('Scegli uno spazio');
    httpTesting.expectNone('/api/bookings');
  });

  it('rejects a start time in the past before calling the service', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);
    form.bookingForm.patchValue(localSlotFromRome(romeDayOffset(-1), '10:00', '11:00'));

    form.submitBooking();
    fixture.detectChanges();

    expect(form.bookingFieldError('start')).toContain('futuro');
    expect(fixture.nativeElement.textContent).toContain('futuro');
    httpTesting.expectNone('/api/bookings');
  });

  it('rejects a booking that crosses a time band', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);
    form.bookingForm.patchValue(localSlotFromRome(romeDayOffset(1), '13:30', '14:30'));

    form.submitBooking();

    expect(form.bookingFieldError('start')).toContain('fascia');
    httpTesting.expectNone('/api/bookings');
  });

  it('rejects a duration below 30 minutes', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);
    form.bookingForm.patchValue({ start: '10:00', end: '10:15' });

    form.submitBooking();

    expect(form.bookingFieldError('end')).toContain('30 minuti');
    httpTesting.expectNone('/api/bookings');
  });

  it('rejects start times that are not a multiple of 15 minutes', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);
    form.bookingForm.patchValue({ start: '10:07', end: '11:07' });

    form.submitBooking();

    expect(form.bookingFieldError('start')).toContain('15 minuti');
    httpTesting.expectNone('/api/bookings');
  });

  it('shows the first validation message returned by Bookings', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);

    form.submitBooking();
    const request = httpTesting.expectOne('/api/bookings');
    request.flush(
      { errors: { Start: ['The start must be in the future.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    expect(form.bookingState()).toBe('error');
    expect(form.bookingFieldError('start')).toBe('The start must be in the future.');
    expect(fixture.nativeElement.textContent).toContain('The start must be in the future.');
  });

  it('updates the end time from the duration slider and stops at the band end', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);

    expect(fixture.nativeElement.querySelector('mat-slider')).not.toBeNull();
    form.setBookingDuration(90);
    expect(form.bookingDuration()).toBe(90);

    form.bookingForm.patchValue(localSlotFromRome(romeDayOffset(1), '13:00', '13:30'));
    form.setBookingDuration(240);
    expect(form.bookingDuration()).toBe(60);
  });

  it('derives the duration from the start and end fields', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);

    expect(isSignal(form.bookingDuration)).toBeTrue();

    form.bookingForm.patchValue(localSlotFromRome(romeDayOffset(1), '10:00', '12:30'));
    fixture.detectChanges();

    expect(form.bookingDuration()).toBe(150);
    expect(fixture.nativeElement.textContent).toContain('150 minuti');
  });

  it('shows the conflict returned by Bookings', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);

    form.submitBooking();
    httpTesting
      .expectOne('/api/bookings')
      .flush({ title: 'Space already booked' }, { status: 409, statusText: 'Conflict' });

    expect(form.bookingState()).toBe('error');
    expect(form.bookingMessage()).toContain('già occupata');
  });

  it('asks for a login when the service answers 401', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);

    form.submitBooking();
    httpTesting.expectOne('/api/bookings').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(form.bookingMessage()).toContain('accesso');
  });

  it('shows the reference code after a successful booking', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);

    form.submitBooking();
    httpTesting
      .expectOne('/api/bookings')
      .flush({ referenceCode: 'SC-261027-ABCD' }, { status: 201, statusText: 'Created' });

    expect(form.bookingState()).toBe('success');
    expect(form.bookingReference()).toBe('SC-261027-ABCD');
  });

  it('validates participant emails and shows equipment only for groups', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();

    const chipInput = { clear: jasmine.createSpy('clear') };
    form.addParticipant({ value: 'group@example.test', chipInput } as any);
    fixture.detectChanges();

    expect(form.participantEmails()).toEqual(['group@example.test']);
    expect(fixture.nativeElement.querySelectorAll('.equipment-field mat-checkbox').length).toBe(4);

    form.addParticipant({ value: 'group@example.test', chipInput } as any);
    fixture.detectChanges();
    expect(form.participantError()).toContain('già stata aggiunta');
    expect(fixture.nativeElement.textContent).toContain('già stata aggiunta');

    form.removeParticipant('group@example.test');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.equipment-field')).toBeNull();
  });

  it('floats the participants label above the chips', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();

    form.addParticipant({ value: 'one@example.test', chipInput: { clear() {} } } as any);
    fixture.detectChanges();

    const label = fixture.nativeElement.querySelector(
      '.participant-field label.mdc-floating-label',
    );
    expect(label).not.toBeNull();
    expect(label.classList.contains('mdc-floating-label--float-above')).toBeTrue();
    expect(fixture.nativeElement.querySelectorAll('.participant-field mat-chip-row').length).toBe(1);
  });

  it('shows the participant total and the capacity of the selected space', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    const chipInput = { clear() {} };

    form.addParticipant({ value: 'one@example.test', chipInput } as any);
    form.addParticipant({ value: 'two@example.test', chipInput } as any);
    fixture.detectChanges();

    expect(form.participantTotal()).toBe(3);
    expect(form.participantSummary()).toContain('responsabile + 2 invitati');
    expect(fixture.nativeElement.textContent).toContain('Partecipanti totali: 3');
    expect(fixture.nativeElement.textContent).toContain('Capienza: 30 posti');
  });

  it('loads the free seats when the first bookable space is mixed', () => {
    spyOn(spacesApiStub, 'searchSpaces').and.returnValue(of([SPACES[2]]));
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();

    expect(form.bookingForm.controls.spaceId.value).toBe('space-3');
    expect(fixture.nativeElement.textContent).toContain('Posti liberi nella fascia: 8 su 8');
  });

  it('does not accept the organizer email as a participant', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const session = TestBed.inject(AuthSessionService);
    session.store(LOGIN_RESPONSE);
    fixture.detectChanges();
    const form = fixture.componentInstance as any;
    const chipInput = { clear() {} };

    form.addParticipant({ value: 'GIULIA.ROSSI@smartcampus.test', chipInput } as any);
    fixture.detectChanges();

    expect(form.participantEmails()).toEqual([]);
    expect(form.participantError()).toContain('responsabile');
    expect(fixture.nativeElement.textContent).toContain('non aggiungere la tua email');
  });

  it('blocks a group larger than the capacity of a dedicated space', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);
    form.bookingForm.controls.spaceId.setValue('space-4');
    const chipInput = { clear() {} };
    form.addParticipant({ value: 'one@example.test', chipInput } as any);
    form.addParticipant({ value: 'two@example.test', chipInput } as any);
    fixture.detectChanges();

    expect(form.participantSeatWarning()).toContain('Aula mini ha 2 posti');

    form.submitBooking();
    fixture.detectChanges();

    expect(form.bookingState()).toBe('error');
    expect(fixture.nativeElement.textContent).toContain('Aula mini ha 2 posti');
    httpTesting.expectNone('/api/bookings');

    form.removeParticipant('two@example.test');
    fixture.detectChanges();

    expect(form.participantTotal()).toBe(2);
    expect(form.participantSeatWarning()).toBe('');
    expect(form.bookingState()).toBe('idle');
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

    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);
    form.bookingForm.patchValue({ spaceId: 'space-3' });
    const chipInput = { clear() {} };
    for (const name of ['one', 'two', 'three']) {
      form.addParticipant({ value: `${name}@example.test`, chipInput } as any);
    }
    fixture.detectChanges();

    expect(form.participantTotal()).toBe(4);
    expect(form.participantSeatWarning()).toContain('restano 2 posti su 8');
    expect(fixture.nativeElement.textContent).toContain('Posti liberi nella fascia: 2 su 8');

    form.submitBooking();
    fixture.detectChanges();

    expect(form.bookingState()).toBe('error');
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

    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);
    form.bookingForm.patchValue({ spaceId: 'space-3' });
    const chipInput = { clear() {} };
    form.addParticipant({ value: 'one@example.test', chipInput } as any);
    form.addParticipant({ value: 'two@example.test', chipInput } as any);
    fixture.detectChanges();

    expect(form.participantSeatWarning()).toBe('');

    form.submitBooking();

    const request = httpTesting.expectOne('/api/bookings');
    expect(request.request.body.participantEmails).toEqual([
      'one@example.test',
      'two@example.test',
    ]);
    request.flush({ referenceCode: 'SC-261027-MIXD' }, { status: 201, statusText: 'Created' });
  });

  it('blocks a mixed-space group larger than 4 people', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);
    form.bookingForm.patchValue({ spaceId: 'space-3' });
    const chipInput = { clear() {} };
    for (const name of ['one', 'two', 'three', 'four']) {
      form.addParticipant({ value: `${name}@example.test`, chipInput } as any);
    }
    fixture.detectChanges();

    expect(form.participantTotal()).toBe(5);
    expect(form.participantMixedSizeWarning()).toContain('al massimo 4 persone');

    form.submitBooking();
    fixture.detectChanges();

    expect(form.bookingState()).toBe('error');
    expect(fixture.nativeElement.textContent).toContain('al massimo 4 persone');
    httpTesting.expectNone('/api/bookings');

    form.removeParticipant('four@example.test');
    fixture.detectChanges();

    expect(form.participantTotal()).toBe(4);
    expect(form.participantMixedSizeWarning()).toBe('');
    expect(form.bookingState()).toBe('idle');
  });

  it('accepts a mixed-space group of exactly 4 people', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);
    form.bookingForm.patchValue({ spaceId: 'space-3' });
    const chipInput = { clear() {} };
    for (const name of ['one', 'two', 'three']) {
      form.addParticipant({ value: `${name}@example.test`, chipInput } as any);
    }
    fixture.detectChanges();

    expect(form.participantTotal()).toBe(4);
    expect(form.participantMixedSizeWarning()).toBe('');

    form.submitBooking();

    const request = httpTesting.expectOne('/api/bookings');
    expect(request.request.body.participantEmails.length).toBe(3);
    request.flush({ referenceCode: 'SC-261027-MIX4' }, { status: 201, statusText: 'Created' });
    expect(form.bookingState()).toBe('success');
  });

  it('shows the mixed group limit message when Bookings answers 409 Group too large', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);
    form.bookingForm.patchValue({ spaceId: 'space-3' });

    form.submitBooking();
    httpTesting
      .expectOne('/api/bookings')
      .flush(
        { title: 'Group too large', maxGroupSize: 4 },
        { status: 409, statusText: 'Conflict' },
      );
    fixture.detectChanges();

    expect(form.bookingState()).toBe('error');
    expect(form.bookingMessage()).toContain('al massimo 4 persone');
    expect(fixture.nativeElement.textContent).toContain('al massimo 4 persone');
  });

  it('sends participant emails and equipment codes with a group booking', () => {
    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);
    form.addParticipant({ value: 'group@example.test', chipInput: { clear() {} } } as any);
    form.toggleEquipment('videoprojector', true);

    form.submitBooking();

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

    const fixture = TestBed.createComponent(BookingFormComponent);
    const form = fixture.componentInstance as any;
    fixture.detectChanges();
    fillValidBooking(form);

    form.submitBooking();

    const request = httpTesting.expectOne('/api/bookings');
    expect(request.request.headers.get('Authorization')).toBe('Bearer test-token');
    request.flush({ referenceCode: 'SC-261027-TOKEN' }, { status: 201, statusText: 'Created' });
  });
});

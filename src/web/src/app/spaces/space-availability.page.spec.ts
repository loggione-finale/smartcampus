import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { BOOKINGS_CHANGED_EVENT, SpaceAvailabilityPage } from './space-availability.page';
import { SpaceAvailabilityDto, SpaceDto, buildDailyTimeline } from './spaces.models';

const DEDICATED_SPACE: SpaceDto = {
  id: '3f2a9c1e-0000-4000-8000-000000000001',
  name: 'Aula 101',
  type: 'Classroom',
  mode: 'Dedicated',
  building: 'Edificio A',
  floor: 1,
  capacity: 30,
  isActive: true,
  features: { blackboard: true, projector: true },
  equipment: ['videoprojector'],
  unavailabilities: [],
  updatedAt: '2026-10-26T08:00:00Z',
};

const MIXED_SPACE: SpaceDto = {
  id: '3f2a9c1e-0000-4000-8000-000000000003',
  name: 'Sala Studio Piano Terra',
  type: 'StudyRoom',
  mode: 'Mixed',
  building: 'Edificio A',
  floor: 0,
  capacity: 12,
  isActive: true,
  features: { powerOutlets: true, quietZone: true },
  equipment: [],
  unavailabilities: [],
  updatedAt: '2026-10-26T08:00:00Z',
};

describe('buildDailyTimeline', () => {
  it('splits a dedicated space day into free and occupied Rome-time bands', () => {
    const availability: SpaceAvailabilityDto = {
      spaceId: DEDICATED_SPACE.id,
      date: '2026-10-28',
      accessMode: 'Dedicated',
      capacity: 30,
      slots: [
        {
          // 09:00Z-11:00Z = 10:00-12:00 Europe/Rome (CET, UTC+1)
          start: '2026-10-28T09:00:00Z',
          end: '2026-10-28T11:00:00Z',
          occupiedSeats: 30,
          freeSeats: 0,
          isAvailable: false,
        },
      ],
    };

    const segments = buildDailyTimeline(DEDICATED_SPACE, availability, '2026-10-28');
    expect(segments.length).toBe(3);

    expect(segments[0].startTimeRome).toBe('08:00');
    expect(segments[0].endTimeRome).toBe('10:00');
    expect(segments[0].status).toBe('free');

    expect(segments[1].startTimeRome).toBe('10:00');
    expect(segments[1].endTimeRome).toBe('12:00');
    expect(segments[1].status).toBe('occupied');

    expect(segments[2].startTimeRome).toBe('12:00');
    expect(segments[2].endTimeRome).toBe('23:00');
    expect(segments[2].status).toBe('free');
  });

  it('computes partial and full seat counts for mixed spaces and maintenance reasons', () => {
    const availability: SpaceAvailabilityDto = {
      spaceId: MIXED_SPACE.id,
      date: '2026-10-28',
      accessMode: 'Mixed',
      capacity: 12,
      slots: [
        {
          // 08:00Z-10:00Z = 09:00-11:00 Rome -> 4 seats occupied, 8 free
          start: '2026-10-28T08:00:00Z',
          end: '2026-10-28T10:00:00Z',
          occupiedSeats: 4,
          freeSeats: 8,
          isAvailable: true,
        },
        {
          // 14:00Z-16:00Z = 15:00-17:00 Rome -> 12 seats occupied, 0 free
          start: '2026-10-28T14:00:00Z',
          end: '2026-10-28T16:00:00Z',
          occupiedSeats: 12,
          freeSeats: 0,
          isAvailable: false,
        },
      ],
    };

    const segments = buildDailyTimeline(MIXED_SPACE, availability, '2026-10-28');
    const partial = segments.find((s) => s.status === 'partial');
    const full = segments.find((s) => s.status === 'occupied');

    expect(partial).toBeTruthy();
    expect(partial?.startTimeRome).toBe('09:00');
    expect(partial?.endTimeRome).toBe('11:00');
    expect(partial?.freeSeats).toBe(8);
    expect(partial?.occupiedSeats).toBe(4);

    expect(full).toBeTruthy();
    expect(full?.startTimeRome).toBe('15:00');
    expect(full?.endTimeRome).toBe('17:00');
    expect(full?.freeSeats).toBe(0);

    // Maintenance window replaces synthetic 24h block with exact hours & reason
    const maintenanceSpace: SpaceDto = {
      ...DEDICATED_SPACE,
      unavailabilities: [
        {
          id: 'u-1',
          start: '2026-10-28T08:00:00Z',
          end: '2026-10-28T12:00:00Z',
          reason: 'Manutenzione impianto elettrico',
        },
      ],
    };
    const syntheticDayBlock: SpaceAvailabilityDto = {
      spaceId: maintenanceSpace.id,
      date: '2026-10-28',
      accessMode: 'Dedicated',
      capacity: 30,
      slots: [
        {
          start: '2026-10-28T00:00:00Z',
          end: '2026-10-29T00:00:00Z',
          occupiedSeats: 30,
          freeSeats: 0,
          isAvailable: false,
        },
      ],
    };

    const maintTimeline = buildDailyTimeline(maintenanceSpace, syntheticDayBlock, '2026-10-28');
    expect(maintTimeline.length).toBe(3);
    expect(maintTimeline[1].status).toBe('unavailable');
    expect(maintTimeline[1].startTimeRome).toBe('09:00');
    expect(maintTimeline[1].endTimeRome).toBe('13:00');
    expect(maintTimeline[1].reason).toBe('Manutenzione impianto elettrico');
  });
});

describe('SpaceAvailabilityPage', () => {
  let fixture: ComponentFixture<SpaceAvailabilityPage>;
  let httpTesting: HttpTestingController;
  let paramMap$: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  let queryParamMap$: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  beforeEach(async () => {
    paramMap$ = new BehaviorSubject(convertToParamMap({ id: MIXED_SPACE.id }));
    queryParamMap$ = new BehaviorSubject(convertToParamMap({ date: '2026-10-28' }));

    await TestBed.configureTestingModule({
      imports: [SpaceAvailabilityPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: paramMap$.asObservable(),
            queryParamMap: queryParamMap$.asObservable(),
          },
        },
      ],
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SpaceAvailabilityPage);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('displays free and partially occupied bands for a mixed space on the selected day', () => {
    fixture.detectChanges();

    httpTesting.expectOne(`/api/spaces/spaces/${MIXED_SPACE.id}`).flush(MIXED_SPACE);
    httpTesting
      .expectOne(
        (req) =>
          req.url === `/api/bookings/availability/${MIXED_SPACE.id}` &&
          req.params.get('date') === '2026-10-28',
      )
      .flush({
        spaceId: MIXED_SPACE.id,
        date: '2026-10-28',
        accessMode: 'Mixed',
        capacity: 12,
        slots: [
          {
            start: '2026-10-28T08:00:00Z',
            end: '2026-10-28T10:00:00Z',
            occupiedSeats: 5,
            freeSeats: 7,
            isAvailable: true,
          },
        ],
      } satisfies SpaceAvailabilityDto);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[data-testid="space-title"]')?.textContent).toContain(
      'Sala Studio Piano Terra',
    );
    expect(host.textContent).toContain('09:00 – 11:00');
    expect(host.textContent).toContain('7 posti liberi su 12 (5 occupati)');
    expect(host.textContent).toContain('Tutti i 12 posti sono liberi');
  });

  it('refreshes the timeline automatically after a booking or cancellation event', () => {
    paramMap$.next(convertToParamMap({ id: DEDICATED_SPACE.id }));
    fixture.detectChanges();

    // Initial state: 10:00-12:00 Rome is occupied by an active booking
    httpTesting.expectOne(`/api/spaces/spaces/${DEDICATED_SPACE.id}`).flush(DEDICATED_SPACE);
    httpTesting
      .expectOne(
        (req) =>
          req.url === `/api/bookings/availability/${DEDICATED_SPACE.id}` &&
          req.params.get('date') === '2026-10-28',
      )
      .flush({
        spaceId: DEDICATED_SPACE.id,
        date: '2026-10-28',
        accessMode: 'Dedicated',
        capacity: 30,
        slots: [
          {
            start: '2026-10-28T09:00:00Z',
            end: '2026-10-28T11:00:00Z',
            occupiedSeats: 30,
            freeSeats: 0,
            isAvailable: false,
          },
        ],
      } satisfies SpaceAvailabilityDto);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelectorAll('[data-testid="timeline-segment"]').length).toBe(3);
    expect(host.textContent).toContain('Aula già prenotata per l’intera fascia');

    // Simulate booking cancellation: dispatch BOOKINGS_CHANGED_EVENT
    window.dispatchEvent(new Event(BOOKINGS_CHANGED_EVENT));

    httpTesting.expectOne(`/api/spaces/spaces/${DEDICATED_SPACE.id}`).flush(DEDICATED_SPACE);
    httpTesting
      .expectOne(
        (req) =>
          req.url === `/api/bookings/availability/${DEDICATED_SPACE.id}` &&
          req.params.get('date') === '2026-10-28',
      )
      .flush({
        spaceId: DEDICATED_SPACE.id,
        date: '2026-10-28',
        accessMode: 'Dedicated',
        capacity: 30,
        slots: [],
      } satisfies SpaceAvailabilityDto);
    fixture.detectChanges();

    // Cancelled booking no longer appears as occupied; entire 08:00-23:00 day is free
    const segmentsAfterCancel = host.querySelectorAll('[data-testid="timeline-segment"]');
    expect(segmentsAfterCancel.length).toBe(1);
    expect(segmentsAfterCancel[0].textContent).toContain('08:00 – 23:00');
    expect(segmentsAfterCancel[0].textContent).toContain('Aula interamente libera e prenotabile');
  });

  it('shows a clear alert when the space does not exist (404) or service is unavailable (503)', () => {
    fixture.detectChanges();

    const spaceReq = httpTesting.expectOne(`/api/spaces/spaces/${MIXED_SPACE.id}`);
    const availReq = httpTesting.expectOne(
      (req) => req.url === `/api/bookings/availability/${MIXED_SPACE.id}`,
    );

    spaceReq.flush({ title: 'Space not found' }, { status: 404, statusText: 'Not Found' });
    expect(availReq.cancelled).toBeTrue();
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[data-testid="availability-error"]')?.textContent).toContain(
      'Lo spazio richiesto non esiste',
    );

    const refreshBtn = host.querySelector(
      '[data-testid="refresh-availability-btn"]',
    ) as HTMLButtonElement;
    refreshBtn.click();

    const retrySpaceReq = httpTesting.expectOne(`/api/spaces/spaces/${MIXED_SPACE.id}`);
    const retryAvailReq = httpTesting.expectOne(
      (req) => req.url === `/api/bookings/availability/${MIXED_SPACE.id}`,
    );
    retrySpaceReq.flush(MIXED_SPACE);
    retryAvailReq.flush(
      { title: 'Service unavailable' },
      { status: 503, statusText: 'Service Unavailable' },
    );
    fixture.detectChanges();

    expect(host.querySelector('[data-testid="availability-error"]')?.textContent).toContain(
      'uno dei servizi non risponde',
    );
  });
});

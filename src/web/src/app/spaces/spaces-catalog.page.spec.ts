import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SpacesCatalogPage } from './spaces-catalog.page';
import {
  SpaceDto,
  hasOverlappingUnavailability,
  romeLocalToUtcIso,
  utcIsoToRomeTime,
} from './spaces.models';

const SAMPLE_SPACES: SpaceDto[] = [
  {
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
  },
  {
    id: '3f2a9c1e-0000-4000-8000-000000000002',
    name: 'Laboratorio Informatica 2',
    type: 'Lab',
    mode: 'Dedicated',
    building: 'Edificio B',
    floor: 1,
    capacity: 24,
    isActive: true,
    features: { workstations: 24, software: ['Docker', '.NET SDK'] },
    equipment: ['videoprojector', 'audio-kit'],
    unavailabilities: [],
    updatedAt: '2026-10-26T08:00:00Z',
  },
  {
    id: '3f2a9c1e-0000-4000-8000-000000000008',
    name: 'Aula 205 (Manutenzione)',
    type: 'Classroom',
    mode: 'Mixed',
    building: 'Edificio A',
    floor: 2,
    capacity: 35,
    isActive: true,
    features: { projector: true },
    equipment: ['videoprojector'],
    unavailabilities: [
      {
        id: '8c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e01',
        start: '2026-10-28T08:00:00Z',
        end: '2026-10-28T12:00:00Z',
        reason: 'Manutenzione impianto elettrico',
      },
    ],
    updatedAt: '2026-10-26T08:00:00Z',
  },
];

describe('Spaces time & availability helpers', () => {
  it('converts 10:00 Europe/Rome to 09:00 UTC in winter and 08:00 UTC in summer', () => {
    expect(romeLocalToUtcIso('2026-01-15', '10:00')).toBe('2026-01-15T09:00:00.000Z');
    expect(romeLocalToUtcIso('2026-07-15', '10:00')).toBe('2026-07-15T08:00:00.000Z');

    expect(utcIsoToRomeTime('2026-01-15T09:00:00Z')).toBe('10:00');
    expect(utcIsoToRomeTime('2026-07-15T08:00:00Z')).toBe('10:00');
  });

  it('detects overlapping maintenance windows and permits adjacent intervals', () => {
    const maintenanceSpace = SAMPLE_SPACES[2];

    expect(
      hasOverlappingUnavailability(
        maintenanceSpace,
        '2026-10-28T09:00:00.000Z',
        '2026-10-28T10:00:00.000Z',
      ),
    ).toBeTrue();

    expect(
      hasOverlappingUnavailability(
        maintenanceSpace,
        '2026-10-28T12:00:00.000Z',
        '2026-10-28T14:00:00.000Z',
      ),
    ).toBeFalse();
  });
});

describe('SpacesCatalogPage', () => {
  let fixture: ComponentFixture<SpacesCatalogPage>;
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SpacesCatalogPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SpacesCatalogPage);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('loads and displays active spaces with Italian labels on initialization', () => {
    fixture.detectChanges();

    const req = httpTesting.expectOne('/api/spaces');
    expect(req.request.method).toBe('GET');
    req.flush(SAMPLE_SPACES);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const cards = host.querySelectorAll('[data-testid="space-card"]');
    expect(cards.length).toBe(3);
    expect(host.querySelector('[data-testid="results-count"]')?.textContent).toContain(
      '3 spazi trovati',
    );
    expect(host.textContent).toContain('Laboratorio Informatica 2');
    expect(host.textContent).toContain('Uso dedicato');
    expect(host.textContent).toContain('Uso misto');
    expect(host.textContent).toContain('Videoproiettore');
    expect(host.textContent).toContain('24 postazioni PC');
  });

  it('falls back transparently to /api/spaces/spaces when dev proxy strips /api/spaces', () => {
    fixture.detectChanges();

    const primaryReq = httpTesting.expectOne('/api/spaces');
    primaryReq.flush(null, { status: 404, statusText: 'Not Found' });

    const fallbackReq = httpTesting.expectOne('/api/spaces/spaces');
    expect(fallbackReq.request.method).toBe('GET');
    fallbackReq.flush([SAMPLE_SPACES[0]]);
    fixture.detectChanges();

    const cards = (fixture.nativeElement as HTMLElement).querySelectorAll(
      '[data-testid="space-card"]',
    );
    expect(cards.length).toBe(1);
    expect(cards[0].textContent).toContain('Aula 101');
  });

  it('sends combined filter query parameters to GET /api/spaces', () => {
    fixture.detectChanges();
    httpTesting.expectOne('/api/spaces').flush(SAMPLE_SPACES);
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      filterForm: SpacesCatalogPage['filterForm'];
      applyFilters(): void;
    };

    component.filterForm.patchValue({
      search: 'Informatica',
      type: 'Lab',
      mode: 'Dedicated',
      minCapacity: 20,
      building: 'Edificio B',
      equipment: ['videoprojector', 'audio-kit'],
      software: 'Docker',
    });

    component.applyFilters();

    const filteredReq = httpTesting.expectOne((request) => request.url === '/api/spaces');
    expect(filteredReq.request.params.get('search')).toBe('Informatica');
    expect(filteredReq.request.params.get('type')).toBe('Lab');
    expect(filteredReq.request.params.get('mode')).toBe('Dedicated');
    expect(filteredReq.request.params.get('minCapacity')).toBe('20');
    expect(filteredReq.request.params.get('building')).toBe('Edificio B');
    expect(filteredReq.request.params.get('software')).toBe('Docker');
    expect(filteredReq.request.params.getAll('equipment')).toEqual([
      'videoprojector',
      'audio-kit',
    ]);

    filteredReq.flush([SAMPLE_SPACES[1]]);
    fixture.detectChanges();

    const cards = (fixture.nativeElement as HTMLElement).querySelectorAll(
      '[data-testid="space-card"]',
    );
    expect(cards.length).toBe(1);
    expect(cards[0].textContent).toContain('Laboratorio Informatica 2');
  });

  it('composes Spaces + Bookings availability and excludes spaces under maintenance in that slot', () => {
    fixture.detectChanges();
    httpTesting.expectOne('/api/spaces').flush(SAMPLE_SPACES);
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      filterForm: SpacesCatalogPage['filterForm'];
      applyFilters(): void;
    };

    // 2026-10-28 09:00-11:00 in Europe/Rome (CET, UTC+1) -> 08:00Z-10:00Z
    // SAMPLE_SPACES[2] has maintenance 08:00Z-12:00Z, so only [0] and [1] are queried on Bookings
    component.filterForm.patchValue({
      date: '2026-10-28',
      startTime: '09:00',
      endTime: '11:00',
    });

    component.applyFilters();

    const spacesReq = httpTesting.expectOne('/api/spaces');
    spacesReq.flush(SAMPLE_SPACES);

    const availabilityReq = httpTesting.expectOne('/api/bookings/availability');
    expect(availabilityReq.request.method).toBe('POST');
    expect(availabilityReq.request.body).toEqual({
      spaceIds: [
        '3f2a9c1e-0000-4000-8000-000000000001',
        '3f2a9c1e-0000-4000-8000-000000000002',
      ],
      start: '2026-10-28T08:00:00.000Z',
      end: '2026-10-28T10:00:00.000Z',
    });

    // Bookings reports only Laboratorio Informatica 2 as free
    availabilityReq.flush(['3f2a9c1e-0000-4000-8000-000000000002']);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const cards = host.querySelectorAll('[data-testid="space-card"]');
    expect(cards.length).toBe(1);
    expect(cards[0].textContent).toContain('Laboratorio Informatica 2');
    expect(cards[0].textContent).toContain('Libero nella fascia');
    expect(host.querySelector('[data-testid="active-slot-pill"]')?.textContent).toContain(
      '2026-10-28 · 09:00–11:00',
    );
  });

  it('validates invalid or incomplete time slot before calling backend APIs', () => {
    fixture.detectChanges();
    httpTesting.expectOne('/api/spaces').flush(SAMPLE_SPACES);
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      filterForm: SpacesCatalogPage['filterForm'];
      applyFilters(): void;
    };

    component.filterForm.patchValue({
      date: '2026-10-28',
      startTime: '14:00',
      endTime: '12:00',
    });

    component.applyFilters();
    fixture.detectChanges();

    httpTesting.expectNone('/api/spaces');
    httpTesting.expectNone('/api/bookings/availability');

    const alert = (fixture.nativeElement as HTMLElement).querySelector(
      '[data-testid="slot-error"]',
    );
    expect(alert?.textContent).toContain(
      'L’ora di fine deve essere successiva all’ora di inizio.',
    );
  });

  it('shows a clear empty-state message when no space matches and allows resetting filters', () => {
    fixture.detectChanges();
    httpTesting.expectOne('/api/spaces').flush(SAMPLE_SPACES);
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      filterForm: SpacesCatalogPage['filterForm'];
      applyFilters(): void;
    };

    component.filterForm.patchValue({ minCapacity: 400 });
    component.applyFilters();

    const emptyReq = httpTesting.expectOne(
      (req) => req.url === '/api/spaces' && req.params.get('minCapacity') === '400',
    );
    emptyReq.flush([]);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const emptyState = host.querySelector('[data-testid="empty-state"]');
    expect(emptyState).toBeTruthy();
    expect(emptyState?.textContent).toContain('Nessuna aula corrisponde ai filtri selezionati.');

    const resetBtn = host.querySelector('[data-testid="empty-reset-btn"]') as HTMLButtonElement;
    resetBtn.click();

    const reloadReq = httpTesting.expectOne(
      (req) => req.url === '/api/spaces' && req.params.keys().length === 0,
    );
    reloadReq.flush(SAMPLE_SPACES);
    fixture.detectChanges();

    expect(host.querySelectorAll('[data-testid="space-card"]').length).toBe(3);
  });

  it('displays clear Italian alerts when Spaces or Bookings services return 503', () => {
    fixture.detectChanges();

    const initialReq = httpTesting.expectOne('/api/spaces');
    initialReq.flush(
      { title: 'Spaces database unavailable' },
      { status: 503, statusText: 'Service Unavailable' },
    );
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[data-testid="error-banner"]')?.textContent).toContain(
      'Il catalogo degli spazi non è al momento raggiungibile',
    );

    const component = fixture.componentInstance as unknown as {
      filterForm: SpacesCatalogPage['filterForm'];
      applyFilters(): void;
    };

    component.filterForm.patchValue({
      date: '2026-10-28',
      startTime: '14:00',
      endTime: '16:00',
    });
    component.applyFilters();

    httpTesting.expectOne('/api/spaces').flush([SAMPLE_SPACES[0]]);
    httpTesting
      .expectOne('/api/bookings/availability')
      .flush({ title: 'Bookings unavailable' }, { status: 503, statusText: 'Service Unavailable' });
    fixture.detectChanges();

    expect(host.querySelector('[data-testid="error-banner"]')?.textContent).toContain(
      'il servizio prenotazioni non risponde',
    );
  });
});

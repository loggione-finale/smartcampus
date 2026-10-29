import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
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
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(SpacesCatalogPage);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('loads and displays active spaces with Italian labels and links to daily availability', () => {
    fixture.detectChanges();

    const req = httpTesting.expectOne('/api/spaces/spaces');
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

    const firstLink = host.querySelector(
      '[data-testid="open-availability-link"]',
    ) as HTMLAnchorElement | null;
    expect(firstLink?.getAttribute('href')).toBe('/spaces/3f2a9c1e-0000-4000-8000-000000000001');
  });

  it('shows a Prenota link on every card pointing to the home booking form with the space', () => {
    fixture.detectChanges();
    httpTesting.expectOne('/api/spaces/spaces').flush(SAMPLE_SPACES);
    fixture.detectChanges();

    const links = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLAnchorElement>(
        '[data-testid="book-space-link"]',
      ),
    );
    expect(links.length).toBe(SAMPLE_SPACES.length);
    expect(links[0].textContent?.trim()).toBe('Prenota');
    expect(links.map((l) => l.getAttribute('href'))).toEqual(
      SAMPLE_SPACES.map((s) => `/?spazio=${s.id}#prenota`),
    );
  });

  it('sends combined filter query parameters to GET /api/spaces/spaces', () => {
    fixture.detectChanges();
    httpTesting.expectOne('/api/spaces/spaces').flush(SAMPLE_SPACES);
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

    const filteredReq = httpTesting.expectOne((request) => request.url === '/api/spaces/spaces');
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
    httpTesting.expectOne('/api/spaces/spaces').flush(SAMPLE_SPACES);
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      filterForm: SpacesCatalogPage['filterForm'];
      applyFilters(): void;
    };

    component.filterForm.patchValue({
      date: '2026-10-28',
      startTime: '09:00',
      endTime: '11:00',
    });

    component.applyFilters();

    const spacesReq = httpTesting.expectOne('/api/spaces/spaces');
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

    availabilityReq.flush(['3f2a9c1e-0000-4000-8000-000000000002']);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const cards = host.querySelectorAll('[data-testid="space-card"]');
    expect(cards.length).toBe(1);
    expect(cards[0].textContent).toContain('Laboratorio Informatica 2');
    expect(cards[0].textContent).toContain('Libero nella fascia');

    const detailLink = cards[0].querySelector(
      '[data-testid="open-availability-link"]',
    ) as HTMLAnchorElement | null;
    expect(detailLink?.getAttribute('href')).toBe(
      '/spaces/3f2a9c1e-0000-4000-8000-000000000002?date=2026-10-28',
    );
  });

  it('offers quick time bands aligned with the booking bands (08-14, 14-19, 19-23)', () => {
    fixture.detectChanges();
    httpTesting.expectOne('/api/spaces/spaces').flush(SAMPLE_SPACES);
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const chips = () =>
      Array.from(host.querySelectorAll<HTMLButtonElement>('.preset-chip:not(.preset-clear)'));
    expect(chips().map((chip) => chip.textContent?.trim())).toEqual([
      'Mattina (08:00–14:00)',
      'Pomeriggio (14:00–19:00)',
      'Sera (19:00–23:00)',
    ]);

    // Winter date (CET, UTC+1): each band must reach the availability API as the whole band.
    const expected = [
      ['2026-01-15T07:00:00.000Z', '2026-01-15T13:00:00.000Z'],
      ['2026-01-15T13:00:00.000Z', '2026-01-15T18:00:00.000Z'],
      ['2026-01-15T18:00:00.000Z', '2026-01-15T22:00:00.000Z'],
    ];
    const component = fixture.componentInstance as unknown as {
      filterForm: SpacesCatalogPage['filterForm'];
    };
    component.filterForm.patchValue({ date: '2026-01-15' });

    expected.forEach(([start, end], index) => {
      chips()[index].click();
      httpTesting.expectOne('/api/spaces/spaces').flush(SAMPLE_SPACES);
      const req = httpTesting.expectOne('/api/bookings/availability');
      expect(req.request.body.start).toBe(start);
      expect(req.request.body.end).toBe(end);
      req.flush([]);
      fixture.detectChanges();
    });
  });

  it('validates invalid or incomplete time slot before calling backend APIs', () => {
    fixture.detectChanges();
    httpTesting.expectOne('/api/spaces/spaces').flush(SAMPLE_SPACES);
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

    httpTesting.expectNone('/api/spaces/spaces');
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
    httpTesting.expectOne('/api/spaces/spaces').flush(SAMPLE_SPACES);
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      filterForm: SpacesCatalogPage['filterForm'];
      applyFilters(): void;
    };

    component.filterForm.patchValue({ minCapacity: 400 });
    component.applyFilters();

    const emptyReq = httpTesting.expectOne(
      (req) => req.url === '/api/spaces/spaces' && req.params.get('minCapacity') === '400',
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
      (req) => req.url === '/api/spaces/spaces' && req.params.keys().length === 0,
    );
    reloadReq.flush(SAMPLE_SPACES);
    fixture.detectChanges();

    expect(host.querySelectorAll('[data-testid="space-card"]').length).toBe(3);
  });

  it('displays clear Italian alerts when Spaces or Bookings services return 503', () => {
    fixture.detectChanges();

    const initialReq = httpTesting.expectOne('/api/spaces/spaces');
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

    httpTesting.expectOne('/api/spaces/spaces').flush([SAMPLE_SPACES[0]]);
    httpTesting
      .expectOne('/api/bookings/availability')
      .flush({ title: 'Bookings unavailable' }, { status: 503, statusText: 'Service Unavailable' });
    fixture.detectChanges();

    expect(host.querySelector('[data-testid="error-banner"]')?.textContent).toContain(
      'il servizio prenotazioni non risponde',
    );
  });
});

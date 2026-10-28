import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DashboardPage } from './dashboard.page';
import { DashboardDto } from './admin.models';

const DASHBOARD: DashboardDto = {
  date: '2026-10-28',
  rooms: [
    {
      spaceId: 'a',
      name: 'Aula 101',
      building: 'Edificio A',
      type: 'Classroom',
      mode: 'Dedicated',
      capacity: 30,
      cells: [
        { start: '2026-10-28T08:00:00Z', end: '2026-10-28T10:00:00Z', status: 'Free', freeSeats: 30 },
        { start: '2026-10-28T10:00:00Z', end: '2026-10-28T12:00:00Z', status: 'Booked', freeSeats: 0 },
      ],
    },
    {
      spaceId: 'b',
      name: 'Sala Studio',
      building: 'Edificio A',
      type: 'StudyRoom',
      mode: 'Mixed',
      capacity: 12,
      cells: [
        { start: '2026-10-28T08:00:00Z', end: '2026-10-28T10:00:00Z', status: 'Partial', freeSeats: 5 },
        { start: '2026-10-28T10:00:00Z', end: '2026-10-28T12:00:00Z', status: 'Unavailable', freeSeats: 0 },
      ],
    },
  ],
};

describe('DashboardPage', () => {
  let fixture: ComponentFixture<DashboardPage>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('requests the dashboard and renders rooms by time slot with free seats', () => {
    http.expectOne((r) => r.url === '/api/bookings/admin/dashboard').flush(DASHBOARD);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelectorAll('tr[mat-row]').length).toBe(2);
    expect(el.querySelectorAll('th[mat-header-cell]').length).toBe(3);
    expect(el.textContent).toContain('Posti liberi: 5');
    expect(el.textContent).toContain('Prenotata');
  });

  it('does not offer any way to create a booking', () => {
    http.expectOne((r) => r.url === '/api/bookings/admin/dashboard').flush(DASHBOARD);
    fixture.detectChanges();

    const labels = Array.from(fixture.nativeElement.querySelectorAll('button, a')).map(
      (e) => (e as HTMLElement).textContent?.toLowerCase() ?? '',
    );
    expect(labels.some((t) => t.includes('prenota'))).toBeFalse();
  });

  it('sends the selected filters and shows an error when the call fails', () => {
    http.expectOne((r) => r.url === '/api/bookings/admin/dashboard').flush(DASHBOARD);
    fixture.componentInstance['filters'].patchValue({ building: 'Edificio B', type: 'Lab' });
    fixture.componentInstance['load']();

    const req = http.expectOne((r) => r.url === '/api/bookings/admin/dashboard');
    expect(req.request.params.get('building')).toBe('Edificio B');
    expect(req.request.params.get('type')).toBe('Lab');
    req.flush('err', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeTruthy();
  });
});

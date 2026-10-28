import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DashboardSpace } from './admin.models';
import { DashboardPage } from './dashboard.page';

const URL = '/api/bookings/admin/dashboard';

const SPACES: DashboardSpace[] = [
  {
    spaceId: 'a',
    spaceName: 'Aula 101',
    spaceType: 'Classroom',
    spaceBuilding: 'Edificio A',
    capacity: 30,
    bookings: [
      {
        referenceCode: 'BK-0001',
        organizer: 'Mario Rossi',
        start: '2026-10-28T08:00:00Z',
        end: '2026-10-28T10:00:00Z',
        status: 'Confirmed',
        occupiedSeats: 4,
        participants: [],
      },
    ],
  },
  {
    spaceId: 'b',
    spaceName: 'Sala Studio',
    spaceType: 'StudyRoom',
    spaceBuilding: 'Edificio A',
    capacity: 12,
    bookings: [
      {
        referenceCode: 'BK-0002',
        organizer: 'Anna Bianchi',
        start: '2026-10-28T10:00:00',
        end: '2026-10-28T12:00:00',
        status: 'Cancelled',
        occupiedSeats: 0,
        participants: [],
      },
    ],
  },
];

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

  afterEach(() => {
    http.verify();
    sessionStorage.removeItem('smartcampus.token');
  });

  it('renders one column per time slot and the free seats of each booking', () => {
    http.expectOne((r) => r.url === URL).flush(SPACES);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelectorAll('tr[mat-row]').length).toBe(2);
    expect(el.querySelectorAll('th[mat-header-cell]').length).toBe(3);
    expect(el.textContent).toContain('BK-0001');
    expect(el.textContent).toContain('4/30 posti, liberi 26');
    expect(el.textContent).toContain('Annullata');
  });

  it('does not offer any way to create a booking', () => {
    http.expectOne((r) => r.url === URL).flush(SPACES);
    fixture.detectChanges();

    const labels = Array.from(fixture.nativeElement.querySelectorAll('button, a')).map(
      (e) => (e as HTMLElement).textContent?.toLowerCase() ?? '',
    );
    expect(labels.some((t) => t.includes('prenota'))).toBeFalse();
  });

  it('sends filters and bearer token, and shows an error when the call fails', () => {
    http.expectOne((r) => r.url === URL).flush(SPACES);
    sessionStorage.setItem('smartcampus.token', 'abc');
    fixture.componentInstance['filters'].patchValue({ building: 'Edificio B', type: 'Lab' });
    fixture.componentInstance['load']();

    const req = http.expectOne((r) => r.url === URL);
    expect(req.request.params.get('building')).toBe('Edificio B');
    expect(req.request.params.get('type')).toBe('Lab');
    expect(req.request.headers.get('Authorization')).toBe('Bearer abc');
    req.flush('err', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeTruthy();
  });
});

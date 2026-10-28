import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { StatsGroup } from './admin.models';
import { ReportPage } from './report.page';

const URL = '/api/bookings/admin/stats';
const ROWS: StatsGroup[] = [
  {
    key: 'Classroom',
    label: 'Classroom',
    totalBookings: 10,
    confirmed: 4,
    cancelled: 2,
    closed: 4,
    spaceUsed: 3,
    spaceNotUsed: 1,
    withoutComment: 7,
    totalAttendees: 25,
    issuesReported: 2,
  },
];

describe('ReportPage', () => {
  let http: HttpTestingController;

  function create() {
    const fixture = TestBed.createComponent(ReportPage);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ReportPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    sessionStorage.clear();
  });

  it('always shows the note about the limits of the data', () => {
    const fixture = create();
    http.expectOne((r) => r.url === URL).flush([]);
    fixture.detectChanges();

    const note = fixture.nativeElement.querySelector('[role="note"]');
    expect(note.textContent).toContain("l'assenza di dati non dimostra che lo spazio fosse libero");
  });

  it('requests the stats with the chosen range and grouping and renders the rows', () => {
    const fixture = create();
    http.expectOne((r) => r.url === URL).flush([]);
    fixture.componentInstance['filters'].patchValue({
      from: '2026-10-01',
      to: '2026-10-07',
      groupBy: 'Type',
    });
    fixture.componentInstance['load']();

    const req = http.expectOne((r) => r.url === URL);
    expect(req.request.params.get('from')).toBe('2026-10-01');
    expect(req.request.params.get('to')).toBe('2026-10-07');
    expect(req.request.params.get('groupBy')).toBe('Type');
    req.flush(ROWS);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelectorAll('tr[mat-row]').length).toBe(1);
    expect(el.textContent).toContain('Aula');
    expect(el.textContent).toContain('25');
    expect(el.textContent).toContain('Senza commento');
    expect(el.querySelectorAll('td[mat-cell]')[7].textContent).toContain('7');
  });

  it('does not call the API when the range is inverted', () => {
    const fixture = create();
    http.expectOne((r) => r.url === URL).flush([]);
    fixture.componentInstance['filters'].patchValue({ from: '2026-10-08', to: '2026-10-01' });
    fixture.componentInstance['load']();
    fixture.detectChanges();

    http.expectNone((r) => r.url === URL);
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeTruthy();
  });

  it('shows an error when the call fails', () => {
    const fixture = create();
    http.expectOne((r) => r.url === URL).flush('err', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeTruthy();
  });
});

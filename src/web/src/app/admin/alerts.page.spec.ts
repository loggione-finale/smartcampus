import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AlertsPage } from './alerts.page';
import { AdminAlert } from './admin.models';

const ALERTS: AdminAlert[] = [
  {
    id: '1',
    kind: 'ReportedIssue',
    bookingId: 'b1',
    bookingReferenceCode: 'SC-AAA',
    message: 'Proiettore guasto',
    status: 'Open',
    createdAt: '2026-10-28T08:00:00Z',
    handledAt: null,
    handlingNote: null,
  },
  {
    id: '2',
    kind: 'EquipmentRequest',
    bookingId: null,
    bookingReferenceCode: null,
    message: 'Richiesta microfono',
    status: 'Handled',
    createdAt: '2026-10-27T08:00:00Z',
    handledAt: '2026-10-27T09:00:00Z',
    handlingNote: 'Fatto',
  },
];

describe('AlertsPage', () => {
  let fixture: ComponentFixture<AlertsPage>;
  let http: HttpTestingController;
  let el: HTMLElement;

  const loadAlerts = () => {
    http.expectOne((r) => r.url === '/api/bookings/admin/alerts').flush(ALERTS);
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AlertsPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AlertsPage);
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('asks for open alerts first and highlights them', () => {
    const req = http.expectOne((r) => r.url === '/api/bookings/admin/alerts');
    expect(req.request.params.get('status')).toBe('Open');
    req.flush(ALERTS);
    fixture.detectChanges();

    const items = el.querySelectorAll('li.alert');
    expect(items.length).toBe(2);
    expect(items[0].classList).toContain('alert-open');
    expect(items[1].classList).not.toContain('alert-open');
  });

  it('filters by kind without calling the API again', () => {
    loadAlerts();
    fixture.componentInstance['kindFilter'].set('EquipmentRequest');
    fixture.detectChanges();

    const items = el.querySelectorAll('li.alert');
    expect(items.length).toBe(1);
    expect(items[0].textContent).toContain('Richiesta microfono');
  });

  it('resolves an alert with the note and reloads the list', () => {
    loadAlerts();
    fixture.componentInstance['notes']['1'] = ' Sostituito ';
    fixture.componentInstance['resolve'](ALERTS[0]);

    const req = http.expectOne('/api/bookings/admin/alerts/1/resolve');
    expect(req.request.body).toEqual({ note: 'Sostituito' });
    req.flush({ ...ALERTS[0], status: 'Handled' });
    http.expectOne((r) => r.url === '/api/bookings/admin/alerts').flush([]);
    fixture.detectChanges();

    expect(el.textContent).toContain('Avviso segnato come gestito.');
  });

  it('keeps "annulla prenotazione" disabled until a reason is typed', () => {
    loadAlerts();
    const button = () => el.querySelector<HTMLButtonElement>('button.btn-danger')!;
    expect(button().disabled).toBe(true);

    fixture.componentInstance['reasons']['1'] = 'Aula inagibile';
    fixture.detectChanges();
    expect(button().disabled).toBe(false);
  });

  it('cancels the booking with the reason', () => {
    loadAlerts();
    fixture.componentInstance['reasons']['1'] = ' Aula inagibile ';
    fixture.componentInstance['cancelBooking'](ALERTS[0]);

    const req = http.expectOne('/api/bookings/admin/bookings/SC-AAA/cancel');
    expect(req.request.body).toEqual({ reason: 'Aula inagibile' });
    req.flush({});
    fixture.detectChanges();

    expect(el.textContent).toContain('Prenotazione SC-AAA annullata');
  });

  it('shows a message when the booking was already cancelled (409)', () => {
    loadAlerts();
    fixture.componentInstance['reasons']['1'] = 'Guasto';
    fixture.componentInstance['cancelBooking'](ALERTS[0]);

    http
      .expectOne('/api/bookings/admin/bookings/SC-AAA/cancel')
      .flush({}, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(el.textContent).toContain('già annullata');
  });

  it('shows an error when the list cannot be loaded', () => {
    http
      .expectOne((r) => r.url === '/api/bookings/admin/alerts')
      .flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Impossibile caricare gli avvisi');
  });
});

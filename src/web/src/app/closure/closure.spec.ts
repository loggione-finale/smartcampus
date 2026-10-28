import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AUTH_TOKEN_KEY, ClosureApiService } from './closure-api.service';
import { PendingBookingItem } from './closure.models';
import { ClosurePage } from './closure.page';

const MOCK_BOOKING_PENDING: PendingBookingItem = {
  id: '00000000-0000-0000-0000-000000000001',
  referenceCode: 'SC-261028-TEST',
  spaceId: '11111111-1111-1111-1111-111111111111',
  spaceName: 'Aula Magna',
  spaceBuilding: 'Edificio Centrale',
  start: '2026-10-28T09:00:00Z',
  end: '2026-10-28T11:00:00Z',
  status: 'AwaitingComment',
  role: 'Organizer',
  hasPendingClosureComment: true,
};

const MOCK_BOOKING_CLOSED: PendingBookingItem = {
  id: '00000000-0000-0000-0000-000000000002',
  referenceCode: 'SC-261028-DONE',
  spaceId: '22222222-2222-2222-2222-222222222222',
  spaceName: 'Laboratorio 1',
  spaceBuilding: 'Edificio B',
  start: '2026-10-28T08:00:00Z',
  end: '2026-10-28T10:00:00Z',
  status: 'Closed',
  role: 'Organizer',
  hasPendingClosureComment: false,
};

describe('ClosureApiService', () => {
  let service: ClosureApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ClosureApiService);
    http = TestBed.inject(HttpTestingController);
    window.sessionStorage.clear();
  });

  afterEach(() => {
    http.verify();
    window.sessionStorage.clear();
  });

  it('filters only bookings that have hasPendingClosureComment = true', () => {
    let result: PendingBookingItem[] = [];
    service.getPendingBookings().subscribe((bookings) => (result = bookings));

    const req = http.expectOne('/api/bookings/bookings/mine');
    expect(req.request.method).toBe('GET');
    req.flush([MOCK_BOOKING_PENDING, MOCK_BOOKING_CLOSED]);

    expect(result.length).toBe(1);
    expect(result[0].referenceCode).toBe('SC-261028-TEST');
  });

  it('attaches Bearer token from sessionStorage when present', () => {
    window.sessionStorage.setItem(AUTH_TOKEN_KEY, 'jwt-test-token');

    service.getPendingBookings().subscribe();

    const req = http.expectOne('/api/bookings/bookings/mine');
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-test-token');
    req.flush([]);
  });

  it('submits closing comment payload with POST', () => {
    service
      .submitComment('SC-261028-TEST', {
        spaceUsed: true,
        attendeesCount: 20,
        issuesReported: false,
        comment: 'Tutto utilizzato correttamente.',
      })
      .subscribe();

    const req = http.expectOne('/api/bookings/bookings/SC-261028-TEST/closing-comment');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      spaceUsed: true,
      attendeesCount: 20,
      issuesReported: false,
      comment: 'Tutto utilizzato correttamente.',
    });
    req.flush({ bookingId: MOCK_BOOKING_PENDING.id, referenceCode: 'SC-261028-TEST', status: 'Closed' });
  });
});

describe('ClosurePage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    window.sessionStorage.clear();
  });

  afterEach(() => {
    http.verify();
    window.sessionStorage.clear();
  });

  it('displays warning banner when pending bookings exist', () => {
    const fixture = TestBed.createComponent(ClosurePage);
    fixture.detectChanges();

    const req = http.expectOne('/api/bookings/bookings/mine');
    req.flush([MOCK_BOOKING_PENDING]);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    const banner = el.querySelector('.pending-alert-banner');
    expect(banner).not.toBeNull();
    expect(banner?.textContent).toContain('Commento in sospeso');
    expect(banner?.textContent).toContain('non potrai effettuare nuove prenotazioni');
  });

  it('displays empty state when no pending bookings exist', () => {
    const fixture = TestBed.createComponent(ClosurePage);
    fixture.detectChanges();

    const req = http.expectOne('/api/bookings/bookings/mine');
    req.flush([]);
    fixture.detectChanges();

    const el: HTMLElement = fixture.nativeElement;
    expect(el.querySelector('.pending-alert-banner')).toBeNull();
    expect(el.textContent).toContain('Nessun commento in sospeso');
  });

  it('validates comment length: disables submit button when comment is too short (< 10 chars)', () => {
    const fixture = TestBed.createComponent(ClosurePage);
    fixture.detectChanges();

    const req = http.expectOne('/api/bookings/bookings/mine');
    req.flush([MOCK_BOOKING_PENDING]);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.form.patchValue({
      spaceUsed: true,
      attendeesCount: 5,
      issuesReported: false,
      comment: 'Troppo',
    });
    fixture.detectChanges();

    expect(component.form.valid).toBeFalse();
    expect(component.form.get('comment')?.hasError('minlength')).toBeTrue();

    const submitBtn: HTMLButtonElement = fixture.nativeElement.querySelector('button[type="submit"]');
    expect(submitBtn.disabled).toBeTrue();
  });

  it('submits closing comment successfully and displays unblocking message', () => {
    const fixture = TestBed.createComponent(ClosurePage);
    fixture.detectChanges();

    const req = http.expectOne('/api/bookings/bookings/mine');
    req.flush([MOCK_BOOKING_PENDING]);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.form.patchValue({
      spaceUsed: true,
      attendeesCount: 15,
      issuesReported: false,
      comment: 'Aula utilizzata regolarmente, nessun problema riscontrato.',
    });
    fixture.detectChanges();

    expect(component.form.valid).toBeTrue();

    component.onSubmit();
    fixture.detectChanges();

    const postReq = http.expectOne('/api/bookings/bookings/SC-261028-TEST/closing-comment');
    expect(postReq.request.method).toBe('POST');
    postReq.flush({
      bookingId: MOCK_BOOKING_PENDING.id,
      referenceCode: 'SC-261028-TEST',
      status: 'Closed',
    });
    fixture.detectChanges();

    expect(component.successMessage()).toContain('Commento finale inviato con successo');
    expect(component.successMessage()).toContain('Ora puoi effettuare nuove prenotazioni');
    expect(component.pendingBookings().length).toBe(0);
  });

  it('handles 409 conflict error properly', () => {
    const fixture = TestBed.createComponent(ClosurePage);
    fixture.detectChanges();

    const req = http.expectOne('/api/bookings/bookings/mine');
    req.flush([MOCK_BOOKING_PENDING]);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.form.patchValue({
      spaceUsed: true,
      attendeesCount: 10,
      issuesReported: false,
      comment: 'Commento duplicato per test conflitto.',
    });
    fixture.detectChanges();

    component.onSubmit();
    fixture.detectChanges();

    const postReq = http.expectOne('/api/bookings/bookings/SC-261028-TEST/closing-comment');
    postReq.flush(
      { title: 'Comment already exists', detail: 'La prenotazione ha già un commento.' },
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();

    expect(component.errorMessage()).toContain('La prenotazione ha già un commento');
  });
});

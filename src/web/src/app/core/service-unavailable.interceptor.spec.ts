import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import {
  SERVICE_UNAVAILABLE_TIMEOUT_MS,
  serviceUnavailableInterceptor,
} from './service-unavailable.interceptor';
import { ServiceUnavailableNotifier } from './service-unavailable.notifier';

describe('serviceUnavailableInterceptor', () => {
  let http: HttpClient;
  let httpTesting: HttpTestingController;
  let notifier: jasmine.SpyObj<ServiceUnavailableNotifier>;

  beforeEach(() => {
    notifier = jasmine.createSpyObj<ServiceUnavailableNotifier>('ServiceUnavailableNotifier', [
      'notify',
    ]);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([serviceUnavailableInterceptor])),
        provideHttpClientTesting(),
        { provide: ServiceUnavailableNotifier, useValue: notifier },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    // A timed out request stays open but cancelled in HttpTestingController.
    httpTesting.verify({ ignoreCancelled: true });
  });

  it('shows the bookings message on a 503 and keeps the error for the page', () => {
    let status: number | undefined;
    http.get('/api/bookings/bookings/mine').subscribe({
      error: (error: HttpErrorResponse) => (status = error.status),
    });

    httpTesting
      .expectOne('/api/bookings/bookings/mine')
      .flush({}, { status: 503, statusText: 'Service Unavailable' });

    expect(notifier.notify).toHaveBeenCalledOnceWith(
      'Prenotazioni momentaneamente non disponibili. Riprova tra poco.',
    );
    expect(status).toBe(503);
  });

  it('shows the spaces message when the service is unreachable', () => {
    let status: number | undefined;
    http.get('/api/spaces/spaces').subscribe({
      error: (error: HttpErrorResponse) => (status = error.status),
    });

    httpTesting.expectOne('/api/spaces/spaces').error(new ProgressEvent('error'));

    expect(notifier.notify).toHaveBeenCalledOnceWith(
      'Il catalogo degli spazi non è raggiungibile. Controlla la connessione e riprova.',
    );
    expect(status).toBe(0);
  });

  it('shows the users message on a 503 from the login call', () => {
    http.post('/api/users/auth/login', {}).subscribe({ error: () => undefined });

    httpTesting
      .expectOne('/api/users/auth/login')
      .flush({}, { status: 503, statusText: 'Service Unavailable' });

    expect(notifier.notify).toHaveBeenCalledOnceWith(
      'Accesso momentaneamente non disponibile. Riprova tra poco.',
    );
  });

  it('shows the timeout message and reports a 504 when the service does not answer', fakeAsync(() => {
    let status: number | undefined;
    http.get('/api/invitations/mine').subscribe({
      error: (error: HttpErrorResponse) => (status = error.status),
    });

    tick(SERVICE_UNAVAILABLE_TIMEOUT_MS);

    expect(notifier.notify).toHaveBeenCalledOnceWith(
      'Il servizio prenotazioni non risponde in tempo. Riprova tra poco.',
    );
    expect(status).toBe(504);
  }));

  it('shows the timeout message for a 504 returned by the proxy', () => {
    http.get('/api/other').subscribe({ error: () => undefined });

    httpTesting.expectOne('/api/other').flush({}, { status: 504, statusText: 'Gateway Timeout' });

    expect(notifier.notify).toHaveBeenCalledOnceWith(
      'Il servizio non risponde in tempo. Riprova tra poco.',
    );
  });

  it('keeps the other services usable after one fails', () => {
    let bookingsStatus: number | undefined;
    http.get('/api/bookings/bookings/mine').subscribe({
      error: (error: HttpErrorResponse) => (bookingsStatus = error.status),
    });
    httpTesting
      .expectOne('/api/bookings/bookings/mine')
      .flush({}, { status: 503, statusText: 'Service Unavailable' });

    let spaces: unknown;
    http.get('/api/spaces/spaces').subscribe((response) => (spaces = response));
    httpTesting.expectOne('/api/spaces/spaces').flush([{ id: 'space-1' }]);

    expect(bookingsStatus).toBe(503);
    expect(spaces).toEqual([{ id: 'space-1' }]);
  });

  it('leaves client and business errors to the pages', () => {
    for (const status of [400, 401, 403, 404, 409]) {
      http.get(`/api/bookings/bookings/${status}`).subscribe({ error: () => undefined });
      httpTesting
        .expectOne(`/api/bookings/bookings/${status}`)
        .flush({}, { status, statusText: 'Error' });
    }

    expect(notifier.notify).not.toHaveBeenCalled();
  });

  it('does not add a snackbar to the health checks of the home page', () => {
    http.get('/api/spaces/health').subscribe({ error: () => undefined });

    httpTesting
      .expectOne('/api/spaces/health')
      .flush({}, { status: 503, statusText: 'Service Unavailable' });

    expect(notifier.notify).not.toHaveBeenCalled();
  });
});

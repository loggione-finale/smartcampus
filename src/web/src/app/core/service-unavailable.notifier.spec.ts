import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import {
  SERVICE_UNAVAILABLE_DEDUPE_MS,
  ServiceUnavailableNotifier,
} from './service-unavailable.notifier';

describe('ServiceUnavailableNotifier', () => {
  let open: jasmine.Spy;
  let notifier: ServiceUnavailableNotifier;

  beforeEach(() => {
    open = jasmine.createSpy('open');
    TestBed.configureTestingModule({
      providers: [{ provide: MatSnackBar, useValue: { open } }],
    });
    notifier = TestBed.inject(ServiceUnavailableNotifier);
  });

  afterEach(() => {
    jasmine.clock().uninstall();
  });

  it('opens a closing snackbar styled like the site', () => {
    notifier.notify('Prenotazioni momentaneamente non disponibili. Riprova tra poco.');

    expect(open).toHaveBeenCalledOnceWith(
      'Prenotazioni momentaneamente non disponibili. Riprova tra poco.',
      'Chiudi',
      jasmine.objectContaining({
        duration: 6_000,
        horizontalPosition: 'center',
        verticalPosition: 'bottom',
        panelClass: 'sc-snackbar',
        politeness: 'assertive',
      }),
    );
  });

  it('does not repeat the same message inside the dedupe window', () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2026, 9, 28, 10, 0, 0));

    notifier.notify('Servizio non raggiungibile.');
    notifier.notify('Servizio non raggiungibile.');

    expect(open).toHaveBeenCalledTimes(1);
  });

  it('shows the same message again after the dedupe window', () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2026, 9, 28, 10, 0, 0));

    notifier.notify('Servizio non raggiungibile.');
    jasmine.clock().tick(SERVICE_UNAVAILABLE_DEDUPE_MS);
    notifier.notify('Servizio non raggiungibile.');

    expect(open).toHaveBeenCalledTimes(2);
  });

  it('shows a different message immediately', () => {
    notifier.notify('Catalogo momentaneamente non disponibile. Riprova tra poco.');
    notifier.notify('Prenotazioni momentaneamente non disponibili. Riprova tra poco.');

    expect(open).toHaveBeenCalledTimes(2);
  });
});

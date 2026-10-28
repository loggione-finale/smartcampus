import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BOOKINGS_CHANGED_EVENT, MyBookingsPage } from './my-bookings.page';
import {
  BOOKING_STATUS_LABELS,
  MyBookingDto,
  MyInvitationDto,
  PARTICIPANT_ROLE_LABELS,
  formatBookingDate,
  formatBookingTimeRange,
  formatInvitationExpiry,
  splitBookings,
} from './my-bookings.models';

const BOOKINGS_URL = '/api/bookings/mine';
const INVITATIONS_URL = '/api/invitations/mine';

const FUTURE_BOOKING: MyBookingDto = {
  id: 'booking-future',
  referenceCode: 'SC-990115-AAAA',
  spaceId: 'space-1',
  spaceName: 'Aula Magna',
  spaceBuilding: 'Edificio A',
  start: '2099-01-15T09:00:00Z',
  end: '2099-01-15T11:00:00Z',
  status: 'Confirmed',
  role: 'Participant',
  hasPendingClosureComment: false,
  hasPendingInvitation: true,
  invitationExpiresAt: '2099-01-10T17:00:00Z',
};

const FUTURE_ORGANIZER_BOOKING: MyBookingDto = {
  id: 'booking-organizer',
  referenceCode: 'SC-990116-DDDD',
  spaceId: 'space-4',
  spaceName: 'Laboratorio Grafica',
  spaceBuilding: 'Edificio C',
  start: '2099-01-16T14:00:00Z',
  end: '2099-01-16T16:00:00Z',
  status: 'Confirmed',
  role: 'Organizer',
  hasPendingClosureComment: false,
  hasPendingInvitation: false,
  invitationExpiresAt: null,
};

const PAST_AWAITING_COMMENT: MyBookingDto = {
  id: 'booking-awaiting',
  referenceCode: 'SC-200602-BBBB',
  spaceId: 'space-2',
  spaceName: 'Laboratorio 2',
  spaceBuilding: 'Edificio B',
  start: '2020-06-02T08:00:00Z',
  end: '2020-06-02T10:00:00Z',
  status: 'AwaitingComment',
  role: 'Organizer',
  hasPendingClosureComment: true,
  hasPendingInvitation: false,
  invitationExpiresAt: null,
};

const PAST_CANCELLED: MyBookingDto = {
  id: 'booking-past-cancelled',
  referenceCode: 'SC-200601-CCCC',
  spaceId: 'space-3',
  spaceName: 'Sala studio Nord',
  spaceBuilding: 'Piano terra',
  start: '2020-06-01T08:00:00Z',
  end: '2020-06-01T10:00:00Z',
  status: 'Cancelled',
  role: 'Organizer',
  hasPendingClosureComment: false,
  hasPendingInvitation: false,
  invitationExpiresAt: null,
};

const PENDING_INVITATION: MyInvitationDto = {
  id: 'invitation-1',
  bookingId: FUTURE_BOOKING.id,
  email: 'student@smartcampus.test',
  userId: 'user-1',
  role: 'Participant',
  status: 'Invited',
  expiresAt: '2099-01-10T17:00:00Z',
  respondedAt: null,
};

describe('My bookings presentation helpers', () => {
  it('formats dates, time ranges and invitation deadlines in Europe/Rome', () => {
    expect(formatBookingDate('2099-01-15T09:00:00Z')).toBe('Giovedì 15 gennaio 2099');
    expect(formatBookingTimeRange('2099-01-15T09:00:00Z', '2099-01-15T11:00:00Z')).toBe(
      '10:00 – 12:00',
    );
    expect(formatInvitationExpiry('2099-01-10T17:00:00Z')).toBe('10 gennaio 2099, 18:00');
    expect(formatInvitationExpiry(null)).toBeNull();
  });

  it('splits future and past bookings while preserving the API order', () => {
    const now = Date.parse('2026-10-28T12:00:00Z');
    const groups = splitBookings([FUTURE_BOOKING, PAST_AWAITING_COMMENT, PAST_CANCELLED], now);

    expect(groups.upcoming.map((booking) => booking.id)).toEqual([FUTURE_BOOKING.id]);
    expect(groups.past.map((booking) => booking.id)).toEqual([
      PAST_AWAITING_COMMENT.id,
      PAST_CANCELLED.id,
    ]);
  });

  it('keeps a booking with an unparsable end date in the archive', () => {
    const broken: MyBookingDto = { ...PAST_CANCELLED, id: 'broken', end: 'not-a-date' };
    const groups = splitBookings([broken], Date.parse('2026-10-28T12:00:00Z'));

    expect(groups.upcoming).toEqual([]);
    expect(groups.past.map((booking) => booking.id)).toEqual(['broken']);
  });

  it('uses Italian labels for every status and role', () => {
    expect(BOOKING_STATUS_LABELS).toEqual({
      Confirmed: 'Confermata',
      Cancelled: 'Annullata',
      Closed: 'Conclusa',
      AwaitingComment: 'Da commentare',
    });
    expect(PARTICIPANT_ROLE_LABELS).toEqual({
      Organizer: 'Organizzatore',
      Participant: 'Partecipante',
    });
  });
});

describe('MyBookingsPage', () => {
  let fixture: ComponentFixture<MyBookingsPage>;
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MyBookingsPage],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(MyBookingsPage);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  function host(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function flushInitialLoad(bookings: MyBookingDto[], invitations: MyInvitationDto[] = []): void {
    fixture.detectChanges();

    httpTesting.expectOne(BOOKINGS_URL).flush(bookings);

    if (bookings.some((booking) => booking.hasPendingInvitation)) {
      httpTesting.expectOne(INVITATIONS_URL).flush(invitations);
    }

    fixture.detectChanges();
  }

  it('shows space, date, time, role and status, keeping cancelled bookings visible', () => {
    flushInitialLoad([FUTURE_BOOKING, PAST_AWAITING_COMMENT, PAST_CANCELLED]);

    expect(host().querySelector('[data-testid="bookings-count"]')?.textContent).toContain(
      '3 prenotazioni',
    );

    const upcoming = host().querySelector('[data-testid="upcoming-list"]');
    expect(upcoming?.textContent).toContain('Aula Magna');
    expect(upcoming?.textContent).toContain('Edificio A');
    expect(upcoming?.textContent).toContain('Giovedì 15 gennaio 2099');
    expect(upcoming?.textContent).toContain('10:00 – 12:00');
    expect(upcoming?.textContent).toContain('Partecipante');
    expect(upcoming?.textContent).toContain('Confermata');

    const pageText = host().textContent ?? '';
    const upcomingTitleIndex = pageText.indexOf('Prossime prenotazioni');
    const pastTitleIndex = pageText.indexOf('Prenotazioni passate');
    expect(upcomingTitleIndex).toBeGreaterThanOrEqual(0);
    expect(pastTitleIndex).toBeGreaterThan(upcomingTitleIndex);

    const pastCards = host().querySelectorAll(
      '[data-testid="past-list"] [data-testid="booking-card"]',
    );
    expect(pastCards.length).toBe(2);
    expect(pastCards[0].textContent).toContain('Laboratorio 2');
    expect(pastCards[0].textContent).toContain('Organizzatore');
    expect(pastCards[0].textContent).toContain('Da commentare');
    expect(pastCards[1].textContent).toContain('Sala studio Nord');
    expect(pastCards[1].textContent).toContain('Annullata');
  });

  it('uses the singular for a single booking', () => {
    flushInitialLoad([PAST_CANCELLED]);

    expect(host().querySelector('[data-testid="bookings-count"]')?.textContent).toContain(
      '1 prenotazione',
    );
  });

  it('shows the invitation deadline and confirms a pending invitation', () => {
    flushInitialLoad([FUTURE_BOOKING], [PENDING_INVITATION]);

    expect(host().querySelector('[data-testid="pending-invitation"]')?.textContent).toContain(
      'Scade il 10 gennaio 2099, 18:00.',
    );

    const acceptButton = host().querySelector(
      '[data-testid="accept-invitation-btn"]',
    ) as HTMLButtonElement;
    expect(acceptButton.textContent).toContain('Conferma invito');
    acceptButton.click();

    const acceptRequest = httpTesting.expectOne(`/api/invitations/${PENDING_INVITATION.id}/accept`);
    expect(acceptRequest.request.method).toBe('POST');
    acceptRequest.flush({
      ...PENDING_INVITATION,
      status: 'Accepted',
      respondedAt: new Date().toISOString(),
    });

    httpTesting
      .expectOne(BOOKINGS_URL)
      .flush([{ ...FUTURE_BOOKING, hasPendingInvitation: false, invitationExpiresAt: null }]);
    fixture.detectChanges();

    expect(host().querySelector('[data-testid="accept-invitation-btn"]')).toBeNull();
    expect(host().querySelector('[data-testid="pending-invitation"]')).toBeNull();
    expect(host().textContent).toContain('Aula Magna');
    expect(host().querySelector('[data-testid="action-success"]')?.textContent).toContain(
      'Invito confermato',
    );
  });

  it('does not offer the confirm button once the invitation deadline has passed', () => {
    flushInitialLoad(
      [{ ...FUTURE_BOOKING, invitationExpiresAt: '2020-01-10T17:00:00Z' }],
      [{ ...PENDING_INVITATION, expiresAt: '2020-01-10T17:00:00Z' }],
    );

    expect(host().querySelector('[data-testid="pending-invitation"]')?.textContent).toContain(
      'Invito scaduto',
    );
    expect(host().querySelector('[data-testid="accept-invitation-btn"]')).toBeNull();
  });

  it('explains when a pending invitation can no longer be accepted', () => {
    flushInitialLoad([FUTURE_BOOKING], [PENDING_INVITATION]);

    (host().querySelector('[data-testid="accept-invitation-btn"]') as HTMLButtonElement).click();

    httpTesting
      .expectOne(`/api/invitations/${PENDING_INVITATION.id}/accept`)
      .flush({ title: 'Invitation no longer pending' }, { status: 409, statusText: 'Conflict' });

    // An expired or already answered invitation is gone: the page reloads.
    httpTesting
      .expectOne(BOOKINGS_URL)
      .flush([{ ...FUTURE_BOOKING, hasPendingInvitation: false, invitationExpiresAt: null }]);
    fixture.detectChanges();

    expect(host().querySelector('[data-testid="action-error"]')?.textContent).toContain(
      'non è più disponibile',
    );
    expect(host().querySelector('[data-testid="accept-invitation-btn"]')).toBeNull();
  });

  it('keeps the invitation and explains a personal overlap conflict', () => {
    flushInitialLoad([FUTURE_BOOKING], [PENDING_INVITATION]);

    (host().querySelector('[data-testid="accept-invitation-btn"]') as HTMLButtonElement).click();

    httpTesting
      .expectOne(`/api/invitations/${PENDING_INVITATION.id}/accept`)
      .flush(
        { title: 'Overlapping booking', detail: 'You already have a confirmed booking.' },
        { status: 409, statusText: 'Conflict' },
      );
    fixture.detectChanges();

    expect(host().querySelector('[data-testid="action-error"]')?.textContent).toContain(
      'si sovrappone a questa fascia oraria',
    );
    expect(host().querySelector('[data-testid="accept-invitation-btn"]')).not.toBeNull();
  });

  it('reloads the list when the invitation no longer exists', () => {
    flushInitialLoad([FUTURE_BOOKING], [PENDING_INVITATION]);

    (host().querySelector('[data-testid="accept-invitation-btn"]') as HTMLButtonElement).click();

    httpTesting
      .expectOne(`/api/invitations/${PENDING_INVITATION.id}/accept`)
      .flush({ title: 'Invitation not found' }, { status: 404, statusText: 'Not Found' });

    httpTesting
      .expectOne(BOOKINGS_URL)
      .flush([{ ...FUTURE_BOOKING, hasPendingInvitation: false, invitationExpiresAt: null }]);
    fixture.detectChanges();

    expect(host().querySelector('[data-testid="action-error"]')?.textContent).toContain(
      'non è più disponibile',
    );
  });

  it('explains when the invitation belongs to another account', () => {
    flushInitialLoad([FUTURE_BOOKING], [PENDING_INVITATION]);

    (host().querySelector('[data-testid="accept-invitation-btn"]') as HTMLButtonElement).click();

    httpTesting
      .expectOne(`/api/invitations/${PENDING_INVITATION.id}/accept`)
      .flush({ title: 'Not your invitation' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(host().querySelector('[data-testid="action-error"]')?.textContent).toContain(
      'intestato a un altro account',
    );
  });

  it('shows the closure link on a concluded booking without a comment', () => {
    flushInitialLoad([PAST_AWAITING_COMMENT, PAST_CANCELLED]);

    const notices = host().querySelectorAll(
      '[data-testid="pending-comment-notice"]',
    ) as NodeListOf<HTMLElement>;

    expect(notices.length).toBe(1);
    expect(notices[0].textContent).toContain('Prenotazione conclusa senza commento');
    const link = notices[0].querySelector('a') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe(
      `/closure?booking=${PAST_AWAITING_COMMENT.referenceCode}`,
    );
  });

  it('keeps the bookings visible when the invitations call fails', () => {
    fixture.detectChanges();

    httpTesting.expectOne(BOOKINGS_URL).flush([FUTURE_BOOKING]);
    httpTesting
      .expectOne(INVITATIONS_URL)
      .flush({ title: 'Unavailable' }, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(host().querySelector('[data-testid="invitations-unavailable"]')).not.toBeNull();
    expect(host().textContent).toContain('Aula Magna');
    expect(host().querySelector('[data-testid="error-state"]')).toBeNull();
  });

  it('shows a clear service error and retries the request', () => {
    fixture.detectChanges();

    httpTesting
      .expectOne(BOOKINGS_URL)
      .flush({ title: 'Bookings unavailable' }, { status: 503, statusText: 'Service Unavailable' });
    fixture.detectChanges();

    expect(host().querySelector('[data-testid="error-state"]')?.textContent).toContain(
      'Il servizio prenotazioni non è al momento raggiungibile',
    );

    (host().querySelector('[data-testid="error-state"] button') as HTMLButtonElement).click();

    httpTesting.expectOne(BOOKINGS_URL).flush([FUTURE_BOOKING]);
    httpTesting.expectOne(INVITATIONS_URL).flush([]);
    fixture.detectChanges();

    expect(host().querySelectorAll('[data-testid="booking-card"]').length).toBe(1);
  });

  it('explains when the session has expired', () => {
    fixture.detectChanges();

    httpTesting
      .expectOne(BOOKINGS_URL)
      .flush({ title: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    expect(host().querySelector('[data-testid="error-state"]')?.textContent).toContain(
      'La sessione è scaduta',
    );
  });

  it('shows an empty state with a link to the catalogue', () => {
    flushInitialLoad([]);

    expect(host().querySelector('[data-testid="empty-state"]')?.textContent).toContain(
      'Non hai ancora prenotazioni',
    );
    const link = host().querySelector('[data-testid="empty-state"] a') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/spaces');
  });

  it('shows cancel button only for the organizer of active confirmed bookings and not for participants or cancelled bookings', () => {
    flushInitialLoad([FUTURE_ORGANIZER_BOOKING, FUTURE_BOOKING, PAST_CANCELLED]);

    const cards = host().querySelectorAll('[data-testid="booking-card"]');
    expect(cards.length).toBe(3);

    // Organizer future confirmed: has cancel button
    expect(cards[0].querySelector('[data-testid="cancel-booking-btn"]')).toBeTruthy();

    // Participant future confirmed: no cancel button
    expect(cards[1].querySelector('[data-testid="cancel-booking-btn"]')).toBeFalsy();

    // Cancelled past: no cancel button
    expect(cards[2].querySelector('[data-testid="cancel-booking-btn"]')).toBeFalsy();
  });

  it('shows inline confirmation box when cancel button is clicked, and aborts when requested', () => {
    flushInitialLoad([FUTURE_ORGANIZER_BOOKING]);

    const cancelBtn = host().querySelector('[data-testid="cancel-booking-btn"]') as HTMLButtonElement;
    expect(cancelBtn).toBeTruthy();
    cancelBtn.click();
    fixture.detectChanges();

    const confirmBox = host().querySelector('[data-testid="cancel-confirm-box"]');
    expect(confirmBox).toBeTruthy();
    expect(confirmBox?.textContent).toContain('Sei sicuro di voler annullare questa prenotazione?');

    const abortBtn = host().querySelector('[data-testid="abort-cancel-btn"]') as HTMLButtonElement;
    expect(abortBtn).toBeTruthy();
    abortBtn.click();
    fixture.detectChanges();

    expect(host().querySelector('[data-testid="cancel-confirm-box"]')).toBeFalsy();
    expect(host().querySelector('[data-testid="cancel-booking-btn"]')).toBeTruthy();
  });

  it('cancels the booking, reloads the list, shows success and dispatches smartcampus:bookings-changed event', () => {
    let eventDispatched = false;
    const eventListener = () => {
      eventDispatched = true;
    };
    window.addEventListener(BOOKINGS_CHANGED_EVENT, eventListener);

    try {
      flushInitialLoad([FUTURE_ORGANIZER_BOOKING]);

      (host().querySelector('[data-testid="cancel-booking-btn"]') as HTMLButtonElement).click();
      fixture.detectChanges();

      const confirmBtn = host().querySelector('[data-testid="confirm-cancel-btn"]') as HTMLButtonElement;
      confirmBtn.click();
      fixture.detectChanges();

      const cancelReq = httpTesting.expectOne(`/api/bookings/${FUTURE_ORGANIZER_BOOKING.id}/cancel`);
      expect(cancelReq.request.method).toBe('POST');
      cancelReq.flush({ id: FUTURE_ORGANIZER_BOOKING.id, status: 'Cancelled' });
      fixture.detectChanges();

      expect(eventDispatched).toBeTrue();

      // The component reloads bookings
      const reloadReq = httpTesting.expectOne(BOOKINGS_URL);
      reloadReq.flush([{ ...FUTURE_ORGANIZER_BOOKING, status: 'Cancelled' }]);
      fixture.detectChanges();

      expect(host().querySelector('[data-testid="action-success"]')?.textContent).toContain(
        'Prenotazione annullata. Lo spazio è di nuovo disponibile.',
      );
    } finally {
      window.removeEventListener(BOOKINGS_CHANGED_EVENT, eventListener);
    }
  });

  it('handles 409 conflict when booking was already cancelled or ended', () => {
    flushInitialLoad([FUTURE_ORGANIZER_BOOKING]);

    (host().querySelector('[data-testid="cancel-booking-btn"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    (host().querySelector('[data-testid="confirm-cancel-btn"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    httpTesting
      .expectOne(`/api/bookings/${FUTURE_ORGANIZER_BOOKING.id}/cancel`)
      .flush({ title: 'Booking already cancelled' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    // Reloads bookings
    httpTesting.expectOne(BOOKINGS_URL).flush([{ ...FUTURE_ORGANIZER_BOOKING, status: 'Cancelled' }]);
    fixture.detectChanges();

    expect(host().querySelector('[data-testid="action-error"]')?.textContent).toContain(
      'La prenotazione risulta già annullata o non più annullabile.',
    );
  });

  it('handles 403 forbidden when user is not authorized to cancel', () => {
    flushInitialLoad([FUTURE_ORGANIZER_BOOKING]);

    (host().querySelector('[data-testid="cancel-booking-btn"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    (host().querySelector('[data-testid="confirm-cancel-btn"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    httpTesting
      .expectOne(`/api/bookings/${FUTURE_ORGANIZER_BOOKING.id}/cancel`)
      .flush({ title: 'Only organizer can cancel' }, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();

    expect(host().querySelector('[data-testid="action-error"]')?.textContent).toContain(
      'Solo l’organizzatore può annullare la prenotazione.',
    );
  });

  it('handles 503 service unavailable during cancellation', () => {
    flushInitialLoad([FUTURE_ORGANIZER_BOOKING]);

    (host().querySelector('[data-testid="cancel-booking-btn"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    (host().querySelector('[data-testid="confirm-cancel-btn"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    httpTesting
      .expectOne(`/api/bookings/${FUTURE_ORGANIZER_BOOKING.id}/cancel`)
      .flush({ title: 'Unavailable' }, { status: 503, statusText: 'Service Unavailable' });
    fixture.detectChanges();

    expect(host().querySelector('[data-testid="action-error"]')?.textContent).toContain(
      'Il servizio prenotazioni non è raggiungibile. Riprova tra poco.',
    );
  });
});

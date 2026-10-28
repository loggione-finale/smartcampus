export type BookingStatus = 'Confirmed' | 'Cancelled' | 'Closed' | 'AwaitingComment';

export type ParticipantRole = 'Organizer' | 'Participant';

export type InvitationStatus = 'Invited' | 'Accepted' | 'Expired' | 'Left' | 'Cancelled';

export interface MyBookingDto {
  id: string;
  referenceCode: string;
  spaceId: string;
  spaceName: string;
  spaceBuilding: string;
  start: string;
  end: string;
  status: BookingStatus;
  role: ParticipantRole;
  hasPendingClosureComment: boolean;
  hasPendingInvitation: boolean;
  invitationExpiresAt: string | null;
}

export interface MyInvitationDto {
  id: string;
  bookingId: string;
  email: string;
  userId: string | null;
  role: ParticipantRole;
  status: InvitationStatus;
  expiresAt: string;
  respondedAt: string | null;
}

export const BOOKING_STATUS_LABELS: Readonly<Record<BookingStatus, string>> = {
  Confirmed: 'Confermata',
  Cancelled: 'Annullata',
  Closed: 'Conclusa',
  AwaitingComment: 'Da commentare',
};

export const PARTICIPANT_ROLE_LABELS: Readonly<Record<ParticipantRole, string>> = {
  Organizer: 'Organizzatore',
  Participant: 'Partecipante',
};

const BOOKING_TIME_ZONE = 'Europe/Rome';

const dateFormatter = new Intl.DateTimeFormat('it-IT', {
  timeZone: BOOKING_TIME_ZONE,
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  year: 'numeric',
});

const timeFormatter = new Intl.DateTimeFormat('it-IT', {
  timeZone: BOOKING_TIME_ZONE,
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

const expiryDateFormatter = new Intl.DateTimeFormat('it-IT', {
  timeZone: BOOKING_TIME_ZONE,
  day: 'numeric',
  month: 'long',
  year: 'numeric',
});

export function formatBookingDate(isoDate: string): string {
  const timestamp = Date.parse(isoDate);
  if (Number.isNaN(timestamp)) {
    return 'Data non disponibile';
  }

  const label = dateFormatter.format(new Date(timestamp));
  return label.charAt(0).toUpperCase() + label.slice(1);
}

export function formatBookingTimeRange(startIso: string, endIso: string): string {
  const start = Date.parse(startIso);
  const end = Date.parse(endIso);
  if (Number.isNaN(start) || Number.isNaN(end)) {
    return 'Orario non disponibile';
  }

  return `${timeFormatter.format(new Date(start))} – ${timeFormatter.format(new Date(end))}`;
}

export function formatInvitationExpiry(isoDate: string | null): string | null {
  if (!isoDate) {
    return null;
  }

  const timestamp = Date.parse(isoDate);
  if (Number.isNaN(timestamp)) {
    return null;
  }

  return `${expiryDateFormatter.format(new Date(timestamp))}, ${timeFormatter.format(
    new Date(timestamp),
  )}`;
}

export interface BookingGroups {
  upcoming: MyBookingDto[];
  past: MyBookingDto[];
}

/**
 * The Bookings API already returns future bookings first, then past ones.
 * Splitting them lets the page show the two groups without relying on that order.
 * Unparsable dates go to the archive instead of disappearing from both groups.
 */
export function splitBookings(bookings: MyBookingDto[], nowMs: number = Date.now()): BookingGroups {
  const upcoming: MyBookingDto[] = [];
  const past: MyBookingDto[] = [];

  for (const booking of bookings) {
    const end = Date.parse(booking.end);
    if (!Number.isNaN(end) && end >= nowMs) {
      upcoming.push(booking);
    } else {
      past.push(booking);
    }
  }

  return { upcoming, past };
}

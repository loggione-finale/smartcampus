export type BookingStatus = 'Confirmed' | 'Cancelled' | 'Closed';

export interface DashboardParticipant {
  email: string;
  displayName: string | null;
  role: string;
  status: string;
}

export interface DashboardBooking {
  referenceCode: string;
  organizer: string;
  start: string;
  end: string;
  status: BookingStatus;
  occupiedSeats: number;
  participants: DashboardParticipant[];
}

// The API lists only the spaces that have bookings on the chosen day.
export interface DashboardSpace {
  spaceId: string;
  spaceName: string;
  spaceType: string;
  spaceBuilding: string;
  capacity: number;
  bookings: DashboardBooking[];
}

export interface DashboardParams {
  date: string;
  building?: string;
  type?: string;
}

export const BOOKING_STATUS_LABELS: Readonly<Record<BookingStatus, string>> = {
  Confirmed: 'Prenotata',
  Cancelled: 'Annullata',
  Closed: 'Chiusa',
};

export type AlertKind = 'NewBooking' | 'EquipmentRequest' | 'Maintenance' | 'ReportedIssue';
export type AlertStatus = 'Open' | 'Handled';

export interface AdminAlert {
  id: string;
  kind: AlertKind;
  bookingId: string | null;
  bookingReferenceCode: string | null;
  message: string;
  status: AlertStatus;
  createdAt: string;
  handledAt: string | null;
  handlingNote: string | null;
}

export const ALERT_KIND_LABELS: Readonly<Record<AlertKind, string>> = {
  NewBooking: 'Nuova prenotazione',
  EquipmentRequest: 'Richiesta attrezzatura',
  Maintenance: 'Manutenzione',
  ReportedIssue: 'Problema segnalato',
};

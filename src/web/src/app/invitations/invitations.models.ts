export type InvitationStatus = 'Invited' | 'Accepted' | 'Expired' | 'Left' | 'Cancelled';

export type ParticipantRole = 'Organizer' | 'Participant';

export type Invitation = {
  id: string;
  bookingId: string;
  email: string;
  userId: string | null;
  role: ParticipantRole;
  status: InvitationStatus;
  expiresAt: string;
  respondedAt: string | null;
  spaceName: string;
  spaceBuilding: string;
  start: string;
  end: string;
  participantCount: number;
};

export const INVITATION_STATUS_LABELS: Record<InvitationStatus, string> = {
  Invited: 'Da confermare',
  Accepted: 'Confermato',
  Expired: 'Scaduto',
  Left: 'Abbandonato',
  Cancelled: 'Annullato',
};

const INVITATION_TIME_ZONE = 'Europe/Rome';

const dateFormatter = new Intl.DateTimeFormat('it-IT', {
  timeZone: INVITATION_TIME_ZONE,
  weekday: 'long',
  day: 'numeric',
  month: 'long',
  year: 'numeric',
});

const timeFormatter = new Intl.DateTimeFormat('it-IT', {
  timeZone: INVITATION_TIME_ZONE,
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

export function formatInvitationDate(isoDate: string): string {
  const timestamp = Date.parse(isoDate);
  if (Number.isNaN(timestamp)) {
    return 'Data non disponibile';
  }

  const label = dateFormatter.format(new Date(timestamp));
  return label.charAt(0).toUpperCase() + label.slice(1);
}

export function formatInvitationTimeRange(startIso: string, endIso: string): string {
  const start = Date.parse(startIso);
  const end = Date.parse(endIso);
  if (Number.isNaN(start) || Number.isNaN(end)) {
    return 'Orario non disponibile';
  }

  return `${timeFormatter.format(new Date(start))} – ${timeFormatter.format(new Date(end))}`;
}

/** Rounded duration in hours such as "2 ore"; minutes are shown only below two hours. */
export function formatInvitationDuration(startIso: string, endIso: string): string {
  const start = Date.parse(startIso);
  const end = Date.parse(endIso);
  if (Number.isNaN(start) || Number.isNaN(end) || end <= start) {
    return 'Durata non disponibile';
  }

  const minutes = Math.round((end - start) / 60000);
  const hours = Math.floor(minutes / 60);
  const remainderMinutes = minutes % 60;
  if (hours === 0) {
    return `${minutes} minuti`;
  }
  if (remainderMinutes === 0) {
    return `${hours} ${hours === 1 ? 'ora' : 'ore'}`;
  }
  return `${hours} ${hours === 1 ? 'ora' : 'ore'} e ${remainderMinutes} minuti`;
}

/** "1 invitato oltre all'organizzatore" / "2 invitati oltre all'organizzatore". */
export function formatInvitationParticipants(participantCount: number): string {
  if (!Number.isFinite(participantCount) || participantCount < 0) {
    return 'Partecipanti non disponibili';
  }
  return `${participantCount} ${participantCount === 1 ? 'invitato' : 'invitati'} oltre all'organizzatore`;
}

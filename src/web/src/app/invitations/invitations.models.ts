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
};

export const INVITATION_STATUS_LABELS: Record<InvitationStatus, string> = {
  Invited: 'Da confermare',
  Accepted: 'Confermato',
  Expired: 'Scaduto',
  Left: 'Abbandonato',
  Cancelled: 'Annullato',
};

export interface PendingBookingItem {
  id: string;
  referenceCode: string;
  spaceId: string;
  spaceName: string;
  spaceBuilding: string;
  start: string;
  end: string;
  status: string;
  role: string;
  hasPendingClosureComment: boolean;
  hasPendingInvitation?: boolean;
}

export interface SubmitClosingCommentPayload {
  spaceUsed: boolean;
  attendeesCount: number;
  issuesReported: boolean;
  comment: string;
}

export interface ClosingCommentResponse {
  bookingId: string;
  referenceCode: string;
  status: string;
  closingCommentId?: string;
  commentedAt?: string;
}

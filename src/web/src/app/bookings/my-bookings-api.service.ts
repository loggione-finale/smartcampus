import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { MyBookingDto, MyInvitationDto } from './my-bookings.models';

// The dev proxy in main rewrites /api/bookings to /bookings, so the bookings
// route is reached directly. Invitations live on their own controller and have
// a dedicated /api/invitations entry in proxy.conf.json.
const BOOKINGS_BASE = '/api/bookings';
const INVITATIONS_BASE = '/api/invitations';

@Injectable({
  providedIn: 'root',
})
export class MyBookingsApiService {
  private readonly http = inject(HttpClient);

  // The auth interceptor adds Authorization: Bearer for every /api request.
  getMyBookings(): Observable<MyBookingDto[]> {
    return this.http.get<MyBookingDto[]>(`${BOOKINGS_BASE}/mine`);
  }

  getMyInvitations(): Observable<MyInvitationDto[]> {
    return this.http.get<MyInvitationDto[]>(`${INVITATIONS_BASE}/mine`);
  }

  acceptInvitation(invitationId: string): Observable<MyInvitationDto> {
    const encodedId = encodeURIComponent(invitationId.trim());
    return this.http.post<MyInvitationDto>(`${INVITATIONS_BASE}/${encodedId}/accept`, null);
  }
}

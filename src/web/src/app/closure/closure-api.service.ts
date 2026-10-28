import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import {
  ClosingCommentResponse,
  PendingBookingItem,
  SubmitClosingCommentPayload,
} from './closure.models';

export const AUTH_TOKEN_KEY = 'smartcampus.auth.token';

@Injectable({
  providedIn: 'root',
})
export class ClosureApiService {
  private readonly http = inject(HttpClient);
  private bookingsBaseUrl = '/api/bookings/bookings';

  private getAuthHeaders(): HttpHeaders {
    let headers = new HttpHeaders();
    if (typeof window !== 'undefined' && window.sessionStorage) {
      const token = window.sessionStorage.getItem(AUTH_TOKEN_KEY);
      if (token?.trim()) {
        headers = headers.set('Authorization', `Bearer ${token.trim()}`);
      }
    }
    return headers;
  }

  getPendingBookings(): Observable<PendingBookingItem[]> {
    const headers = this.getAuthHeaders();
    return this.http.get<PendingBookingItem[]>(`${this.bookingsBaseUrl}/mine`, { headers }).pipe(
      catchError((error) => {
        // Fallback in case proxy route is /api/bookings/mine directly
        if (error.status === 404 && this.bookingsBaseUrl === '/api/bookings/bookings') {
          this.bookingsBaseUrl = '/api/bookings';
          return this.http.get<PendingBookingItem[]>(`${this.bookingsBaseUrl}/mine`, { headers });
        }
        return throwError(() => error);
      }),
      map((bookings) => (bookings ?? []).filter((b) => b.hasPendingClosureComment)),
    );
  }

  submitComment(
    referenceCode: string,
    payload: SubmitClosingCommentPayload,
  ): Observable<ClosingCommentResponse> {
    const headers = this.getAuthHeaders();
    const encodedCode = encodeURIComponent(referenceCode.trim());

    return this.http
      .post<ClosingCommentResponse>(
        `${this.bookingsBaseUrl}/${encodedCode}/closing-comment`,
        payload,
        { headers },
      )
      .pipe(
        catchError((error) => {
          if (error.status === 404 && this.bookingsBaseUrl === '/api/bookings/bookings') {
            this.bookingsBaseUrl = '/api/bookings';
            return this.http.post<ClosingCommentResponse>(
              `${this.bookingsBaseUrl}/${encodedCode}/closing-comment`,
              payload,
              { headers },
            );
          }
          return throwError(() => error);
        }),
      );
  }
}

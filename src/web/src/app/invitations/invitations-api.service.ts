import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { Invitation } from './invitations.models';

export const AUTH_TOKEN_KEY = 'smartcampus.auth.token';

/** Esiti espliciti dell'accettazione, mappati dai ProblemDetails del backend. */
export type AcceptInvitationError = 'not-found' | 'expired' | 'conflict' | 'unknown';

@Injectable({
  providedIn: 'root',
})
export class InvitationsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/invitations';

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

  getMine(): Observable<Invitation[]> {
    return this.http.get<Invitation[]>(`${this.baseUrl}/mine`, { headers: this.getAuthHeaders() });
  }

  accept(id: string): Observable<Invitation> {
    return this.http
      .post<Invitation>(`${this.baseUrl}/${encodeURIComponent(id)}/accept`, null, {
        headers: this.getAuthHeaders(),
      })
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => mapAcceptError(error))));
  }
}

export function mapAcceptError(error: HttpErrorResponse): AcceptInvitationError {
  if (error.status === 404) {
    return 'not-found';
  }
  if (error.status === 409) {
    const title: string = error.error?.title ?? '';
    if (title === 'Overlapping booking') {
      return 'conflict';
    }
    if (title === 'Invitation no longer pending') {
      return 'expired';
    }
  }
  return 'unknown';
}

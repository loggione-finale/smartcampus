import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { AuthSessionService } from './auth-session.service';
import { LoginCredentials, LoginResponse } from './auth.models';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(AuthSessionService);

  login(credentials: LoginCredentials): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>('/api/users/auth/login', {
        username: credentials.identifier.trim(),
        password: credentials.password,
      })
      .pipe(
        tap((response) => {
          if (!this.session.store(response)) {
            throw new Error('Invalid login response.');
          }
        }),
      );
  }

  logout(): void {
    this.session.clear();
  }
}
